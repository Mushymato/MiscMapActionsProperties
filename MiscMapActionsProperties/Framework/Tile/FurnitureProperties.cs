using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MiscMapActionsProperties.Framework.Buildings;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.GameData.Buildings;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Objects;
using StardewValley.TokenizableStrings;

namespace MiscMapActionsProperties.Framework.Tile;

[Flags]
public enum FurnitureDrawMode
{
    None,
    Base,
    Layer,
}

/// <summary>
/// Allow furniture to get tile data, using the same format as building tile data
/// </summary>
internal static class FurnitureProperties
{
    internal const float LAYER_OFFSET = 1E-06f;
    internal const string Asset_FurnitureProperties = $"{ModEntry.ModId}/FurnitureProperties";
    private static Dictionary<string, BuildingData>? _fpData = null;
    private static readonly PerScreen<FurnitureDrawMode> FurnitureDraw = new(() => FurnitureDrawMode.None);
    private static readonly PerScreen<float> FurnitureLayerDepthOffset = new();
    private static readonly PerScreen<List<float>> drawFurnitureLayerDepths = new();
    private static List<float> DrawFurnitureLayerDepths => drawFurnitureLayerDepths.Value ??= [];
    private static readonly Regex IdIsRotation = new(@"^.+_Rotation.(\d+)$", RegexOptions.IgnoreCase);
    private static readonly MethodInfo? Furniture_getScaleSize = AccessTools.DeclaredMethod(
        typeof(Furniture),
        "getScaleSize"
    );

    private sealed record FurnitureDLState(
        BuildingData FpData,
        List<(BuildingDrawLayer drawLayer, DLExtInfo? drawLayerExt)> LayerInfo
    )
    {
        internal enum DrawSource
        {
            Normal,
            Menu,
            NonTile,
        }

        internal void Draw(
            Furniture furniture,
            Vector2 drawPosition,
            SpriteBatch spriteBatch,
            float alpha,
            float furnitureLayerDepth,
            float scaleSize,
            DrawSource drawSource = DrawSource.Normal
        )
        {
            foreach ((BuildingDrawLayer drawLayer, DLExtInfo? drawLayerExt) in LayerInfo)
            {
                if (
                    !string.IsNullOrEmpty(drawLayer.Id)
                    && IdIsRotation.Match(drawLayer.Id) is Match match
                    && match.Success
                    && int.Parse(match.Groups[1].Value) == furniture.currentRotation.Value
                )
                {
                    continue;
                }

                ParsedItemData dataOrErrorItem = ItemRegistry.GetDataOrErrorItem(furniture.QualifiedItemId);

                float layerDepth;
                Vector2 drawPos;
                if (drawSource == DrawSource.Menu)
                {
                    layerDepth = 0f;
                    Rectangle baseSrcRect = dataOrErrorItem.GetSourceRect();
                    drawPos =
                        drawPosition
                        + new Vector2(32, 32)
                        - new Vector2(baseSrcRect.Width / 2 * scaleSize, baseSrcRect.Height / 2 * scaleSize)
                        + drawLayer.DrawPosition * scaleSize;
                }
                else
                {
                    layerDepth =
                        (drawLayer.DrawInBackground ? 0f : furnitureLayerDepth)
                        - (drawLayer.SortTileOffset * 64f / 10000f);
                    if (drawSource == DrawSource.NonTile)
                    {
                        drawPos = drawPosition + drawLayer.DrawPosition * scaleSize;
                    }
                    else
                    {
                        drawPos = Game1.GlobalToLocal(drawPosition + drawLayer.DrawPosition * scaleSize);
                    }
                }
                layerDepth -= furniture.TileLocation.X * LAYER_OFFSET;

                Rectangle sourceRect = drawLayer.GetSourceRect(
                    (int)Game1.currentGameTime.TotalGameTime.TotalMilliseconds
                );
                sourceRect = AdjustSourceRectToSeason(FpData, furniture.Location, sourceRect);
                Texture2D texture = dataOrErrorItem.GetTexture();
                if (Game1.content.DoesAssetExist<Texture2D>(drawLayer.Texture))
                {
                    texture = Game1.content.Load<Texture2D>(drawLayer.Texture);
                }

                if (drawLayerExt != null)
                {
                    drawLayerExt.Draw(
                        spriteBatch,
                        texture,
                        drawPos,
                        sourceRect,
                        Color.White * alpha,
                        0f,
                        Vector2.Zero,
                        scaleSize,
                        SpriteEffects.None,
                        layerDepth
                    );
                }
                else
                {
                    spriteBatch.Draw(
                        texture,
                        drawPos,
                        sourceRect,
                        Color.White * alpha,
                        0f,
                        Vector2.Zero,
                        scaleSize,
                        SpriteEffects.None,
                        layerDepth
                    );
                }
            }
        }
    }

    private static readonly PerScreen<Dictionary<string, FurnitureDLState?>> dlExtInfoCacheImpl = new();
    private static Dictionary<string, FurnitureDLState?> DlExtInfoCache => dlExtInfoCacheImpl.Value ??= [];
    private static IEnumerable<FurnitureDLState> DlExtInfoCacheValues
    {
        get
        {
            if (dlExtInfoCacheImpl.Value == null)
                yield break;
            foreach (FurnitureDLState? state in dlExtInfoCacheImpl.Value.Values)
            {
                if (state != null)
                    yield return state;
            }
        }
    }

    /// <summary>Furniture tile property data (secretly building data)</summary>
    internal static Dictionary<string, BuildingData> FPData
    {
        get
        {
            _fpData ??= Game1.content.Load<Dictionary<string, BuildingData>>(Asset_FurnitureProperties);
            return _fpData;
        }
    }

    private static void OnAssetInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        if (e.NamesWithoutLocale.Any(an => an.IsEquivalentTo(Asset_FurnitureProperties)))
        {
            _fpData = null;
            DlExtInfoCache.Clear();
        }
    }

    private static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.Name.IsEquivalentTo(Asset_FurnitureProperties))
            e.LoadFrom(() => new Dictionary<string, BuildingData>(), AssetLoadPriority.Exclusive);
    }

    internal static Rectangle AdjustSourceRectToSeason(BuildingData fpData, GameLocation location, Rectangle sourceRect)
    {
        if (fpData.SeasonOffset != Point.Zero)
        {
            int seasonIndexForLocation = Game1.GetSeasonIndexForLocation(location);
            sourceRect.X += fpData.SeasonOffset.X * seasonIndexForLocation;
            sourceRect.Y += fpData.SeasonOffset.Y * seasonIndexForLocation;
        }
        return sourceRect;
    }

    internal static void Register()
    {
        ModEntry.help.Events.Content.AssetRequested += OnAssetRequested;
        ModEntry.help.Events.Content.AssetsInvalidated += OnAssetInvalidated;
        ModEntry.help.Events.GameLoop.DayStarted += OnDayStarted;
        ModEntry.help.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        ModEntry.help.Events.GameLoop.TimeChanged += OnTimeChanged;
        ModEntry.help.Events.Player.Warped += OnWarped;
        try
        {
            foreach (Type furnitureType in new Type[] { typeof(Furniture), typeof(BedFurniture) })
            {
                if (
                    AccessTools.DeclaredMethod(furnitureType, nameof(Furniture.DoesTileHaveProperty))
                    is MethodInfo origMethod1
                )
                {
                    ModEntry.harm.Patch(
                        original: origMethod1,
                        postfix: new HarmonyMethod(
                            typeof(FurnitureProperties),
                            nameof(Furniture_DoesTileHaveProperty_Postfix)
                        )
                    );
                }
                if (
                    AccessTools.DeclaredMethod(furnitureType, nameof(Furniture.GetAdditionalTilePropertyRadius))
                    is MethodInfo origMethod2
                )
                {
                    ModEntry.harm.Patch(
                        original: origMethod2,
                        postfix: new HarmonyMethod(
                            typeof(FurnitureProperties),
                            nameof(Furniture_GetAdditionalTilePropertyRadius_Postfix)
                        )
                    );
                }
            }
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.draw)),
                prefix: new HarmonyMethod(typeof(FurnitureProperties), nameof(Furniture_draw_Prefix)),
                finalizer: new HarmonyMethod(typeof(FurnitureProperties), nameof(Furniture_draw_Finalizer))
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(BedFurniture), nameof(BedFurniture.draw)),
                prefix: new HarmonyMethod(typeof(FurnitureProperties), nameof(BedFurniture_draw_Prefix)),
                finalizer: new HarmonyMethod(typeof(FurnitureProperties), nameof(BedFurniture_draw_Finalizer))
            );

            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.IntersectsForCollision)),
                postfix: new HarmonyMethod(
                    typeof(FurnitureProperties),
                    nameof(Furniture_IntersectsForCollision_Postfix)
                )
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.AllowPlacementOnThisTile)),
                postfix: new HarmonyMethod(
                    typeof(FurnitureProperties),
                    nameof(Furniture_AllowPlacementOnThisTile_Postfix)
                )
            );

            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(
                    typeof(SpriteBatch),
                    nameof(SpriteBatch.Draw),
                    [
                        typeof(Texture2D),
                        typeof(Vector2),
                        typeof(Rectangle?),
                        typeof(Color),
                        typeof(float),
                        typeof(Vector2),
                        typeof(float),
                        typeof(SpriteEffects),
                        typeof(float),
                    ]
                ),
                prefix: new HarmonyMethod(typeof(FurnitureProperties), nameof(SpriteBatch_Draw_Prefix))
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.drawInMenu)),
                finalizer: new HarmonyMethod(typeof(FurnitureProperties), nameof(Furniture_drawInMenu_Finalizer))
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.drawAtNonTileSpot)),
                finalizer: new HarmonyMethod(typeof(FurnitureProperties), nameof(Furniture_drawAtNonTileSpot_Finalizer))
            );
            // This patch targets a function earlier than spacecore (which patches at Furniture.getDescription), so spacecore description will override it.
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(Furniture), "loadDescription"),
                prefix: new HarmonyMethod(typeof(FurnitureProperties), nameof(Furniture_loadDescription_Prefix))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch FurnitureProperties:\n{err}", LogLevel.Error);
        }
    }

    private static FurnitureDLState? TryAddFurnitureToDLCache(Furniture furniture, BuildingData fpData)
    {
        if (fpData.DrawLayers == null)
        {
            DlExtInfoCache[furniture.ItemId] = null;
            return null;
        }
        if (DlExtInfoCache.TryGetValue(furniture.ItemId, out FurnitureDLState? state))
            return state;

        state = new(
            fpData,
            fpData
                .DrawLayers.Select(
                    (drawLayer) =>
                    {
                        DrawLayerExt.TryGetDRExtInfo(fpData, drawLayer, out DLExtInfo? dlExtInfo);
                        return new ValueTuple<BuildingDrawLayer, DLExtInfo?>(drawLayer, dlExtInfo);
                    }
                )
                .ToList()
        );
        DlExtInfoCache[furniture.ItemId] = state;
        return state;
    }

    private static void OnWarped(object? sender, WarpedEventArgs e)
    {
        DlExtInfoCache.Clear();
    }

    private static void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        DlExtInfoCache.Clear();
    }

    private static void OnTimeChanged(object? sender, TimeChangedEventArgs e)
    {
        foreach (FurnitureDLState? state in DlExtInfoCacheValues)
        {
            foreach ((_, DLExtInfo? dLExtInfo) in state.LayerInfo)
            {
                dLExtInfo?.TimeChanged();
            }
        }
    }

    private static void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        foreach (FurnitureDLState state in DlExtInfoCacheValues)
        {
            foreach ((_, DLExtInfo? dLExtInfo) in state.LayerInfo)
            {
                dLExtInfo?.UpdateTicked();
            }
        }
    }

    private static void BedFurniture_draw_Prefix(Furniture __instance, ref Rectangle __state)
    {
        if (Furniture.isDrawingLocationFurniture)
            Furniture_draw_Prefix(__instance, ref __state);
    }

    private static void BedFurniture_draw_Finalizer(
        Furniture __instance,
        Netcode.NetVector2 ___drawPosition,
        SpriteBatch spriteBatch,
        int x,
        int y,
        float alpha,
        ref Rectangle __state
    )
    {
        if (Furniture.isDrawingLocationFurniture)
            Furniture_draw_Finalizer(__instance, ___drawPosition, spriteBatch, x, y, alpha, ref __state);
    }

    private static void Furniture_draw_Prefix(Furniture __instance, ref Rectangle __state)
    {
        FurnitureDraw.Value = FurnitureDrawMode.None;
        FurnitureLayerDepthOffset.Value = 0;
        if (__instance.isTemporarilyInvisible || !FPData.TryGetValue(__instance.ItemId, out BuildingData? fpData))
            return;

        __state = __instance.sourceRect.Value;
        FurnitureDraw.Value = FurnitureDrawMode.Base;
        if (TryAddFurnitureToDLCache(__instance, fpData) is not null)
        {
            if (fpData.DrawShadow)
                FurnitureDraw.Value |= FurnitureDrawMode.Layer;
            else
                FurnitureDraw.Value = FurnitureDrawMode.Layer;
        }
        FurnitureLayerDepthOffset.Value =
            fpData.SortTileOffset * 64f / 10000f + __instance.TileLocation.X * LAYER_OFFSET;
        if (fpData.DrawShadow)
        {
            __instance.sourceRect.Value = AdjustSourceRectToSeason(
                fpData,
                __instance.Location,
                __instance.sourceRect.Value
            );
        }
    }

    private static bool SpriteBatch_Draw_Prefix(float layerDepth)
    {
        if (FurnitureDraw.Value == FurnitureDrawMode.None)
            return true;
        if (FurnitureDraw.Value.HasFlag(FurnitureDrawMode.Layer))
            DrawFurnitureLayerDepths.Add(layerDepth);
        return FurnitureDraw.Value.HasFlag(FurnitureDrawMode.Base);
    }

    private static void Furniture_draw_Finalizer(
        Furniture __instance,
        Netcode.NetVector2 ___drawPosition,
        SpriteBatch spriteBatch,
        int x,
        int y,
        float alpha,
        ref Rectangle __state
    )
    {
        if (
            FurnitureDraw.Value == FurnitureDrawMode.None
            || !DlExtInfoCache.TryGetValue(__instance.ItemId, out FurnitureDLState? state)
            || state == null
        )
            return;

        FurnitureDraw.Value = FurnitureDrawMode.None;
        state.Draw(
            __instance,
            Furniture.isDrawingLocationFurniture
                ? ___drawPosition.Value
                : new Vector2(
                    x * Game1.tileSize,
                    y * Game1.tileSize - (__instance.sourceRect.Height * 4f - __instance.boundingBox.Height)
                ),
            spriteBatch,
            alpha,
            DrawFurnitureLayerDepths.Max(),
            4f
        );
        DrawFurnitureLayerDepths.Clear();
        FurnitureLayerDepthOffset.Value = 0;
        __instance.sourceRect.Value = __state;
    }

    private static float TryGetScaleSize(Furniture furniture)
    {
        if (Furniture_getScaleSize?.Invoke(furniture, null) is float scaleSize)
            return scaleSize;
        return 1f;
    }

    private static void Furniture_drawInMenu_Finalizer(
        Furniture __instance,
        SpriteBatch spriteBatch,
        Vector2 location,
        float scaleSize,
        float transparency,
        float layerDepth
    )
    {
        if (!DlExtInfoCache.TryGetValue(__instance.ItemId, out FurnitureDLState? state))
        {
            if (FPData.TryGetValue(__instance.ItemId, out BuildingData? fpData))
                state = TryAddFurnitureToDLCache(__instance, fpData);
            else
                return;
        }
        state?.Draw(
            __instance,
            location,
            spriteBatch,
            transparency,
            layerDepth,
            TryGetScaleSize(__instance) * scaleSize,
            drawSource: FurnitureDLState.DrawSource.Menu
        );
    }

    private static void Furniture_drawAtNonTileSpot_Finalizer(
        Furniture __instance,
        SpriteBatch spriteBatch,
        Vector2 location,
        float layerDepth,
        float alpha
    )
    {
        if (!DlExtInfoCache.TryGetValue(__instance.ItemId, out FurnitureDLState? state))
        {
            if (FPData.TryGetValue(__instance.ItemId, out BuildingData? fpData))
                state = TryAddFurnitureToDLCache(__instance, fpData);
            else
                return;
        }
        state?.Draw(
            __instance,
            location,
            spriteBatch,
            alpha,
            layerDepth,
            4f,
            drawSource: FurnitureDLState.DrawSource.NonTile
        );
    }

    private static bool Furniture_loadDescription_Prefix(Furniture __instance, ref string __result)
    {
        if (
            FPData.TryGetValue(__instance.ItemId, out BuildingData? fpData)
            && !string.IsNullOrEmpty(fpData.Description)
            && TokenParser.ParseText(fpData.Description) is string furniDesc
        )
        {
            __result = Game1.parseText(furniDesc, Game1.smallFont, 320);
            return false;
        }
        return true;
    }

    private static void Furniture_GetAdditionalTilePropertyRadius_Postfix(Furniture __instance, ref int __result)
    {
        if (!FPData.TryGetValue(__instance.ItemId, out BuildingData? fpData))
            return;
        __result = Math.Max(0, fpData.AdditionalTilePropertyRadius);
    }

    private static void Furniture_IntersectsForCollision_Postfix(
        Furniture __instance,
        Rectangle rect,
        ref bool __result
    )
    {
        if (!__result || !FPData.TryGetValue(__instance.ItemId, out BuildingData? fpData))
            return;
        if (fpData.CollisionMap == null)
            return;

        fpData.Size = new Point(__instance.getTilesWide(), __instance.getTilesHigh());
        Rectangle bounds = CommonPatch.GetFurnitureTileDataBounds(__instance);

        for (int i = rect.Top / 64; i <= rect.Bottom / 64; i++)
        {
            for (int j = rect.Left / 64; j <= rect.Right / 64; j++)
            {
                if (
                    bounds.Contains(j, i)
                    && !fpData.IsTilePassable(
                        (int)(j - __instance.TileLocation.X),
                        (int)(i - __instance.TileLocation.Y)
                    )
                )
                {
                    return;
                }
            }
        }
        __result = false;
    }

    private static void Furniture_AllowPlacementOnThisTile_Postfix(
        Furniture __instance,
        int tile_x,
        int tile_y,
        ref bool __result
    )
    {
        if (__result || !FPData.TryGetValue(__instance.ItemId, out BuildingData? fpData))
            return;
        if (fpData.CollisionMap == null)
            return;
        __result = fpData.IsTilePassable((int)(tile_x - __instance.TileLocation.X), (int)(tile_y - __instance.TileLocation.Y));
    }

    private static void Furniture_DoesTileHaveProperty_Postfix(
        Furniture __instance,
        int tile_x,
        int tile_y,
        string property_name,
        string layer_name,
        ref string property_value,
        ref bool __result
    )
    {
        if (__result || !FPData.TryGetValue(__instance.ItemId, out BuildingData? fpData))
            return;
        __result = fpData.HasPropertyAtTile(
            (int)(tile_x - __instance.TileLocation.X),
            (int)(tile_y - __instance.TileLocation.Y),
            property_name,
            layer_name,
            ref property_value
        );
    }
}

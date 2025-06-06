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

public enum DrawFurnitureWithLayerMode
{
    None,
    BaseAndLayer,
    OnlyLayer,
}

/// <summary>
/// Allow furniture to get tile data, using the same format as building tile data
/// </summary>
internal static class FurnitureProperties
{
    internal const string Asset_FurnitureProperties = $"{ModEntry.ModId}/FurnitureProperties";
    private static Dictionary<string, BuildingData>? _fpData = null;
    private static readonly PerScreen<DrawFurnitureWithLayerMode> IsDrawingFurnitureWithLayer =
        new() { Value = DrawFurnitureWithLayerMode.None };
    private static readonly PerScreen<List<float>> DrawFurnitureLayerDepths = new() { Value = [] };
    private static readonly Regex IdIsRotation = new(@"^.+_Rotation.(\d+)$", RegexOptions.IgnoreCase);

    private sealed record FurnitureDLState(
        BuildingData FpData,
        List<(BuildingDrawLayer drawLayer, DLExtInfo? drawLayerExt)> LayerInfo
    )
    {
        internal void Draw(
            Furniture furniture,
            Vector2 drawPosition,
            SpriteBatch spriteBatch,
            float alpha,
            float furnitureLayerDepth
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
                float layerDepth =
                    (drawLayer.DrawInBackground ? 0f : furnitureLayerDepth) - (drawLayer.SortTileOffset * 64f / 10000f);
                Rectangle sourceRect = drawLayer.GetSourceRect(
                    (int)Game1.currentGameTime.TotalGameTime.TotalMilliseconds
                );
                sourceRect = AdjustSourceRectToSeason(FpData, furniture.Location, sourceRect);
                ParsedItemData dataOrErrorItem = ItemRegistry.GetDataOrErrorItem(furniture.QualifiedItemId);
                Texture2D texture = dataOrErrorItem.GetTexture();
                if (Game1.content.DoesAssetExist<Texture2D>(drawLayer.Texture))
                {
                    texture = Game1.content.Load<Texture2D>(drawLayer.Texture);
                }
                Vector2 drawPos = Game1.GlobalToLocal(drawPosition + drawLayer.DrawPosition);

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
                        4f,
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
                        4f,
                        SpriteEffects.None,
                        layerDepth
                    );
                }
            }
        }
    }

    private static readonly PerScreen<Dictionary<string, FurnitureDLState>> dlExtInfoCacheImpl = new() { Value = [] };
    private static Dictionary<string, FurnitureDLState> DlExtInfoCache => dlExtInfoCacheImpl.Value;

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
        ModEntry.help.Events.World.FurnitureListChanged += OnFurnitureListChanged;
        try
        {
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.DoesTileHaveProperty)),
                postfix: new HarmonyMethod(typeof(FurnitureProperties), nameof(Furniture_DoesTileHaveProperty_Postfix))
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(
                    typeof(Furniture),
                    nameof(Furniture.GetAdditionalTilePropertyRadius)
                ),
                postfix: new HarmonyMethod(
                    typeof(FurnitureProperties),
                    nameof(Furniture_GetAdditionalTilePropertyRadius_Postfix)
                )
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.IntersectsForCollision)),
                postfix: new HarmonyMethod(
                    typeof(FurnitureProperties),
                    nameof(Furniture_IntersectsForCollision_Postfix)
                )
            );

            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.draw)),
                prefix: new HarmonyMethod(typeof(FurnitureProperties), nameof(Furniture_draw_Prefix)),
                finalizer: new HarmonyMethod(typeof(FurnitureProperties), nameof(Furniture_draw_Finalizer))
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
                        typeof(Vector2),
                        typeof(SpriteEffects),
                        typeof(float),
                    ]
                ),
                prefix: new HarmonyMethod(typeof(FurnitureProperties), nameof(SpriteBatch_Draw_Prefix))
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

    private static void AddFurnitureToDLCache(Furniture furniture)
    {
        if (!FPData.TryGetValue(furniture.ItemId, out BuildingData? fpData))
            return;

        if (fpData.DrawLayers == null)
            return;

        DlExtInfoCache[furniture.ItemId] = new(
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
    }

    private static void OnFurnitureListChanged(object? sender, FurnitureListChangedEventArgs e)
    {
        if (!e.IsCurrentLocation)
            return;

        foreach (Furniture furniture in e.Added)
        {
            AddFurnitureToDLCache(furniture);
        }
    }

    private static void OnWarped(object? sender, WarpedEventArgs e)
    {
        if (e.NewLocation == null)
            return;

        DlExtInfoCache.Clear();
        foreach (Furniture furniture in e.NewLocation.furniture)
        {
            AddFurnitureToDLCache(furniture);
        }
    }

    private static void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        if (Game1.currentLocation == null)
            return;

        DlExtInfoCache.Clear();
        foreach (Furniture furniture in Game1.currentLocation.furniture)
        {
            AddFurnitureToDLCache(furniture);
        }
    }

    private static void OnTimeChanged(object? sender, TimeChangedEventArgs e)
    {
        foreach (FurnitureDLState state in DlExtInfoCache.Values)
        {
            foreach ((_, DLExtInfo? dLExtInfo) in state.LayerInfo)
            {
                dLExtInfo?.TimeChanged();
            }
        }
    }

    private static void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        foreach (FurnitureDLState state in DlExtInfoCache.Values)
        {
            foreach ((_, DLExtInfo? dLExtInfo) in state.LayerInfo)
            {
                dLExtInfo?.UpdateTicked();
            }
        }
    }

    private static void Furniture_draw_Prefix(Furniture __instance, ref Rectangle __state)
    {
        __state = __instance.sourceRect.Value;
        if (!__instance.isTemporarilyInvisible)
        {
            if (DlExtInfoCache.ContainsKey(__instance.ItemId))
            {
                IsDrawingFurnitureWithLayer.Value = DrawFurnitureWithLayerMode.BaseAndLayer;
            }
            if (__instance.Location != null && FPData.TryGetValue(__instance.ItemId, out BuildingData? fpData))
            {
                if (fpData.DrawShadow)
                {
                    __instance.sourceRect.Value = AdjustSourceRectToSeason(
                        fpData,
                        __instance.Location,
                        __instance.sourceRect.Value
                    );
                }
                else if (IsDrawingFurnitureWithLayer.Value != DrawFurnitureWithLayerMode.None)
                {
                    IsDrawingFurnitureWithLayer.Value = DrawFurnitureWithLayerMode.OnlyLayer;
                }
            }
        }
        else
        {
            IsDrawingFurnitureWithLayer.Value = DrawFurnitureWithLayerMode.None;
        }
    }

    private static bool SpriteBatch_Draw_Prefix(float layerDepth)
    {
        if (IsDrawingFurnitureWithLayer.Value == DrawFurnitureWithLayerMode.None)
            return true;
        DrawFurnitureLayerDepths.Value.Add(layerDepth);
        return IsDrawingFurnitureWithLayer.Value == DrawFurnitureWithLayerMode.BaseAndLayer;
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
            IsDrawingFurnitureWithLayer.Value != DrawFurnitureWithLayerMode.None
            && DlExtInfoCache.TryGetValue(__instance.ItemId, out FurnitureDLState? state)
        )
        {
            IsDrawingFurnitureWithLayer.Value = DrawFurnitureWithLayerMode.None;
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
                DrawFurnitureLayerDepths.Value.Max() + 1 / 10000f
            );
        }
        DrawFurnitureLayerDepths.Value.Clear();
        __instance.sourceRect.Value = __state;
        IsDrawingFurnitureWithLayer.Value = DrawFurnitureWithLayerMode.None;
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

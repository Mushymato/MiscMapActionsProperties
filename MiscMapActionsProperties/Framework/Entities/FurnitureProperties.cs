using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.GameData.Buildings;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Objects;
using StardewValley.TokenizableStrings;

namespace MiscMapActionsProperties.Framework.Entities;

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
    private static FurnitureDrawMode FurnitureDraw = FurnitureDrawMode.None;
    private static float FurnitureLayerDepthOffset = 0f;

    private static float DrawFurnitureLayerDepthMax = 0f;
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

        private readonly Dictionary<string, int?> parsedRotations = [];

        internal bool CheckRotation(string drawLayerId, int currentRotation)
        {
            if (!parsedRotations.TryGetValue(drawLayerId, out int? rotation))
            {
                if (
                    !string.IsNullOrEmpty(drawLayerId)
                    && IdIsRotation.Match(drawLayerId) is Match match
                    && match.Success
                )
                {
                    rotation = int.Parse(match.Groups[1].Value);
                }
                else
                {
                    rotation = null;
                }
                parsedRotations[drawLayerId] = rotation;
            }
            return rotation == null || rotation == currentRotation;
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
            bool hasShakes = DLShakeCache.TryGetValue(
                furniture,
                out ConditionalWeakTable<DLExtInfo, DLShakeState>? dlShakes
            );
            foreach ((BuildingDrawLayer drawLayer, DLExtInfo? drawLayerExt) in LayerInfo)
            {
                if (!CheckRotation(drawLayer.Id, furniture.currentRotation.Value))
                {
                    continue;
                }

                ParsedItemData? dataOrErrorItem = null;

                float layerDepth;
                Vector2 drawPos;
                if (drawSource == DrawSource.Menu)
                {
                    layerDepth = 0f;
                    dataOrErrorItem ??= ItemRegistry.GetDataOrErrorItem(furniture.QualifiedItemId);
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
                Texture2D texture;
                if (Game1.content.DoesAssetExist<Texture2D>(drawLayer.Texture))
                {
                    texture = Game1.content.Load<Texture2D>(drawLayer.Texture);
                }
                else
                {
                    dataOrErrorItem ??= ItemRegistry.GetDataOrErrorItem(furniture.QualifiedItemId);
                    texture = dataOrErrorItem.GetTexture();
                }

                if (drawLayerExt != null)
                {
                    float rotate = 0f;
                    if (hasShakes && dlShakes != null)
                    {
                        if (dlShakes.TryGetValue(drawLayerExt, out DLShakeState? shakeState))
                        {
                            rotate = shakeState.Rotate;
                        }
                    }
                    drawLayerExt.Draw(
                        spriteBatch,
                        texture,
                        drawPos,
                        sourceRect,
                        Color.White * alpha,
                        rotate,
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

    internal static IEnumerable<DLExtInfo> DLStatesIter
    {
        get
        {
            if (dlExtInfoCacheImpl.Value == null)
                yield break;
            foreach (FurnitureDLState? state in dlExtInfoCacheImpl.Value.Values)
            {
                if (state != null)
                {
                    foreach ((_, DLExtInfo? dLExtInfo) in state.LayerInfo)
                    {
                        if (dLExtInfo != null)
                            yield return dLExtInfo;
                    }
                }
            }
        }
    }

    internal static ConditionalWeakTable<DLExtInfo, DLShakeState> CreateDLShakeStates(Furniture furniture)
    {
        ConditionalWeakTable<DLExtInfo, DLShakeState> shakeStates = [];
        if (DlExtInfoCache.TryGetValue(furniture.ItemId, out FurnitureDLState? state) && state != null)
        {
            foreach ((_, DLExtInfo? dLExtInfo) in state.LayerInfo)
            {
                if (dLExtInfo is not null && dLExtInfo.ShakeRotate.Value != 0)
                {
                    shakeStates.GetValue(dLExtInfo, (dLExtInfo) => new());
                }
            }
        }
        return shakeStates;
    }

    private static readonly PerScreen<
        ConditionalWeakTable<Furniture, ConditionalWeakTable<DLExtInfo, DLShakeState>>
    > dlShakeCacheImpl = new();
    private static ConditionalWeakTable<Furniture, ConditionalWeakTable<DLExtInfo, DLShakeState>> DLShakeCache =>
        dlShakeCacheImpl.Value ??= [];

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
            DLShakeCache.Clear();
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
        ModEntry.help.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        ModEntry.help.Events.GameLoop.TimeChanged += OnTimeChanged;
        ModEntry.help.Events.Player.Warped += OnWarped;
        ModEntry.help.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
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
                // if (
                //     AccessTools.DeclaredMethod(furnitureType, nameof(Furniture.GetAdditionalTilePropertyRadius))
                //     is MethodInfo origMethod2
                // )
                // {
                //     ModEntry.harm.Patch(
                //         original: origMethod2,
                //         postfix: new HarmonyMethod(
                //             typeof(FurnitureProperties),
                //             nameof(Furniture_GetAdditionalTilePropertyRadius_Postfix)
                //         )
                //     );
                // }
            }
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
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch FurnitureProperties Props:\n{err}", LogLevel.Error);
        }

        // #region obsolete_1.6.16
        // // these should be removed in 1.6.16
        // try
        // {
        //     // This patch targets a function earlier than spacecore (which patches at Furniture.getDescription), so spacecore description will override it.
        //     ModEntry.harm.Patch(
        //         original: AccessTools.DeclaredMethod(typeof(Furniture), "loadDescription"),
        //         prefix: new HarmonyMethod(typeof(FurnitureProperties), nameof(Furniture_loadDescription_Prefix))
        //     );
        //     // custom TV is a feature to be eaten by 1.6.16 but i'll add it here for now
        //     ModEntry.harm.Patch(
        //         original: AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.GetFurnitureInstance)),
        //         postfix: new HarmonyMethod(typeof(FurnitureProperties), nameof(Furniture_GetFurnitureInstance_Postfix))
        //     );
        //     ModEntry.harm.Patch(
        //         original: AccessTools.DeclaredMethod(typeof(TV), nameof(TV.getScreenPosition)),
        //         postfix: new HarmonyMethod(typeof(FurnitureProperties), nameof(TV_getScreenPosition_Postfix))
        //     );
        //     ModEntry.harm.Patch(
        //         original: AccessTools.DeclaredMethod(typeof(TV), nameof(TV.getScreenSizeModifier)),
        //         postfix: new HarmonyMethod(typeof(FurnitureProperties), nameof(TV_getScreenSizeModifier_Postfix))
        //     );
        // }
        // catch (Exception err)
        // {
        //     ModEntry.Log($"Failed to patch FurnitureProperties Draw:\n{err}", LogLevel.Error);
        // }
        // #endregion

        // try
        // {
        //     ModEntry.harm.Patch(
        //         original: AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.draw)),
        //         prefix: new HarmonyMethod(typeof(FurnitureProperties), nameof(Furniture_draw_Prefix)),
        //         transpiler: new HarmonyMethod(typeof(FurnitureProperties), nameof(Furniture_draw_Transpiler))
        //         {
        //             priority = Priority.Last,
        //         },
        //         finalizer: new HarmonyMethod(typeof(FurnitureProperties), nameof(Furniture_draw_Finalizer))
        //     );
        //     ModEntry.harm.Patch(
        //         original: AccessTools.DeclaredMethod(typeof(BedFurniture), nameof(BedFurniture.draw)),
        //         prefix: new HarmonyMethod(typeof(FurnitureProperties), nameof(BedFurniture_draw_Prefix)),
        //         transpiler: new HarmonyMethod(typeof(FurnitureProperties), nameof(BedFurniture_draw_Transpiler))
        //         {
        //             priority = Priority.Last,
        //         },
        //         finalizer: new HarmonyMethod(typeof(FurnitureProperties), nameof(BedFurniture_draw_Finalizer))
        //     );
        //     ModEntry.harm.Patch(
        //         original: AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.drawInMenu)),
        //         transpiler: new HarmonyMethod(typeof(FurnitureProperties), nameof(Furniture_drawInMenu_Transpiler))
        //         {
        //             priority = Priority.Last,
        //         },
        //         finalizer: new HarmonyMethod(typeof(FurnitureProperties), nameof(Furniture_drawInMenu_Finalizer))
        //     );
        //     ModEntry.harm.Patch(
        //         original: AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.drawAtNonTileSpot)),
        //         transpiler: new HarmonyMethod(
        //             typeof(FurnitureProperties),
        //             nameof(Furniture_drawAtNonTileSpot_Transpiler)
        //         )
        //         {
        //             priority = Priority.Last,
        //         },
        //         finalizer: new HarmonyMethod(typeof(FurnitureProperties), nameof(Furniture_drawAtNonTileSpot_Finalizer))
        //     );
        // }
        // catch (Exception err)
        // {
        //     ModEntry.Log($"Failed to patch FurnitureProperties Draw:\n{err}", LogLevel.Error);
        // }
    }

    private static void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        DlExtInfoCache.Clear();
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
        foreach (DLExtInfo dLExtInfo in DLStatesIter)
        {
            dLExtInfo.RecheckRands();
        }
    }

    private static void OnTimeChanged(object? sender, TimeChangedEventArgs e)
    {
        foreach (DLExtInfo dLExtInfo in DLStatesIter)
        {
            dLExtInfo.TimeChanged();
        }
    }

    private static void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        foreach (DLExtInfo dLExtInfo in DLStatesIter)
        {
            dLExtInfo.UpdateTicked();
        }
        foreach ((_, ConditionalWeakTable<DLExtInfo, DLShakeState> dlShakes) in DLShakeCache)
        {
            foreach ((_, DLShakeState shake) in dlShakes)
            {
                shake.UpdateTicked();
            }
        }
    }

    private static void BedFurniture_draw_Prefix(Furniture __instance, ref (Rectangle?, FurnitureDLState?)? __state)
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
        ref (Rectangle?, FurnitureDLState?)? __state
    )
    {
        if (Furniture.isDrawingLocationFurniture)
            Furniture_draw_Finalizer(__instance, ___drawPosition, spriteBatch, x, y, alpha, ref __state);
    }

    private static void Furniture_draw_Prefix(Furniture __instance, ref (Rectangle?, FurnitureDLState?)? __state)
    {
        __state = null;
        FurnitureDraw = FurnitureDrawMode.None;
        FurnitureLayerDepthOffset = 0;
        if (__instance.isTemporarilyInvisible || !FPData.TryGetValue(__instance.ItemId, out BuildingData? fpData))
            return;

        Rectangle? oldSourceRect = null;
        FurnitureDLState? dlInfoCache;
        FurnitureDraw = FurnitureDrawMode.Base;
        if ((dlInfoCache = TryAddFurnitureToDLCache(__instance, fpData)) is not null)
        {
            if (fpData.DrawShadow)
                FurnitureDraw |= FurnitureDrawMode.Layer;
            else
                FurnitureDraw = FurnitureDrawMode.Layer;
        }
        FurnitureLayerDepthOffset = fpData.SortTileOffset * 64f / 10000f + __instance.TileLocation.X * LAYER_OFFSET;
        if (fpData.DrawShadow)
        {
            oldSourceRect = __instance.sourceRect.Value;
            __instance.sourceRect.Value = AdjustSourceRectToSeason(
                fpData,
                __instance.Location,
                __instance.sourceRect.Value
            );
        }
        __state = new(oldSourceRect, dlInfoCache);
    }

    private static void Furniture_draw_Finalizer(
        Furniture __instance,
        Netcode.NetVector2 ___drawPosition,
        SpriteBatch spriteBatch,
        int x,
        int y,
        float alpha,
        ref (Rectangle?, FurnitureDLState?)? __state
    )
    {
        if (FurnitureDraw == FurnitureDrawMode.None || __state == null)
            return;

        if (__state.Value.Item2 is FurnitureDLState state)
        {
            float layerDepth = DrawFurnitureLayerDepthMax;
            FurnitureDraw = FurnitureDrawMode.None;
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
                layerDepth,
                4f
            );
        }

        DrawFurnitureLayerDepthMax = 0;
        FurnitureLayerDepthOffset = 0;

        if (__state.Value.Item1 is Rectangle sourceRect)
            __instance.sourceRect.Value = sourceRect;
    }

    internal static void DrawReplace(
        SpriteBatch b,
        Texture2D texture,
        Vector2 position,
        Rectangle? sourceRectangle,
        Color color,
        float rotation,
        Vector2 origin,
        float scale,
        SpriteEffects effects,
        float layerDepth
    )
    {
        float overrideLayerDepth = layerDepth;
        FurnitureDrawMode mode = FurnitureDraw;
        if (mode != FurnitureDrawMode.None)
        {
            DrawFurnitureLayerDepthMax = Math.Max(layerDepth, DrawFurnitureLayerDepthMax);
            if (!mode.HasFlag(FurnitureDrawMode.Base))
                return;
            if (Furniture.isDrawingLocationFurniture)
                overrideLayerDepth += FurnitureLayerDepthOffset;
        }
        b.Draw(texture, position, sourceRectangle, color, rotation, origin, scale, effects, overrideLayerDepth);
    }

    private static IEnumerable<CodeInstruction> Furniture_draw_Transpiler_Inner(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator,
        string target
    )
    {
        try
        {
            CodeMatcher matcher = new(instructions, generator);

            int foundDraw = 0;
            MethodInfo replacedDraw = AccessTools.DeclaredMethod(typeof(FurnitureProperties), nameof(DrawReplace));
            CodeMatch[] callvirtDraw =
            [
                new(
                    OpCodes.Callvirt,
                    AccessTools.DeclaredMethod(
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
                    )
                ),
            ];
            matcher
                .MatchStartForward(callvirtDraw)
                .Repeat(match =>
                {
                    match.Opcode = OpCodes.Call;
                    match.Operand = replacedDraw;
                    match.Advance(1);
                    foundDraw++;
                    if (foundDraw >= 10)
                        match.End();
                });

            ModEntry.Log($"{target}-Transpiler: Replaced {foundDraw} SpriteBatch.Draw calls.");

            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in Building_draw_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
        }
    }

    private static IEnumerable<CodeInstruction> Furniture_draw_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        return Furniture_draw_Transpiler_Inner(instructions, generator, "Furniture.draw");
    }

    private static IEnumerable<CodeInstruction> BedFurniture_draw_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        return Furniture_draw_Transpiler_Inner(instructions, generator, "BedFurniture.draw");
    }

    private static IEnumerable<CodeInstruction> Furniture_drawInMenu_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        return Furniture_draw_Transpiler_Inner(instructions, generator, "Furniture.drawInMenu");
    }

    private static IEnumerable<CodeInstruction> Furniture_drawAtNonTileSpot_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        return Furniture_draw_Transpiler_Inner(instructions, generator, "Furniture.drawAtNonTileSpot");
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

    private static float TryGetScaleSize(Furniture furniture)
    {
        if (Furniture_getScaleSize?.Invoke(furniture, null) is float scaleSize)
            return scaleSize;
        return 1f;
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

    #region obsolete_1.6.16
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

    internal const string CustomFields_TV = "TV";

    private record TVScreenShape(float PosX, float PosY, float Scale);

    private static readonly ConditionalWeakTable<TV, TVScreenShape?> TVScreens = [];

    private static void Furniture_GetFurnitureInstance_Postfix(string itemId, ref Furniture __result)
    {
        if (__result is TV)
            return;
        if (!FPData.TryGetValue(__result.ItemId, out BuildingData? fpData))
            return;
        if (fpData.CustomFields?.ContainsKey(CustomFields_TV) ?? false)
        {
            __result = new TV(itemId, __result.TileLocation);
            return;
        }
    }

    private static TVScreenShape? GetTVScreenShape(TV tv)
    {
        if (
            FPData.TryGetValue(tv.ItemId, out BuildingData? fpData)
            && (fpData.CustomFields?.TryGetValue(CustomFields_TV, out string? tvRect) ?? false)
        )
        {
            string[] args = ArgUtility.SplitBySpace(tvRect);
            if (
                !ArgUtility.TryGetVector2(args, 0, out Vector2 pos, out string error, name: "Vector2 pos")
                || !ArgUtility.TryGetFloat(args, 2, out float scale, out error, name: "float scale")
            )
            {
                ModEntry.Log(error, LogLevel.Error);
                return null;
            }
            return new(pos.X, pos.Y, scale);
        }
        return null;
    }

    public static void TV_getScreenPosition_Postfix(TV __instance, ref Vector2 __result)
    {
        if (TVScreens.GetValue(__instance, GetTVScreenShape) is TVScreenShape shape)
        {
            __result = new(__instance.boundingBox.X + shape.PosX, __instance.boundingBox.Y + shape.PosY);
        }
    }

    public static void TV_getScreenSizeModifier_Postfix(TV __instance, ref float __result)
    {
        if (TVScreens.GetValue(__instance, GetTVScreenShape) is TVScreenShape shape)
        {
            __result = shape.Scale;
        }
    }
    #endregion

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

        if (!Game1.player.isMoving())
            return;

        Rectangle furniBounds =
            new(
                bounds.X * Game1.tileSize,
                bounds.Y * Game1.tileSize,
                bounds.Width * Game1.tileSize,
                bounds.Height * Game1.tileSize
            );
        Rectangle playerBounds = Game1.player.GetBoundingBox();
        if (
            playerBounds.Intersects(furniBounds)
            && DlExtInfoCache.TryGetValue(__instance.ItemId, out FurnitureDLState? state)
            && state != null
        )
        {
            // tries to shake draw layers // character.speed + character.addedSpeed
            float speed = Game1.player.getMovementSpeed();

            bool left = playerBounds.Center.X > furniBounds.Center.X;
            ConditionalWeakTable<DLExtInfo, DLShakeState> dlShakes = DLShakeCache.GetValue(
                __instance,
                CreateDLShakeStates
            );
            if (!dlShakes.Any())
                return;
            foreach ((DLExtInfo dlExt, DLShakeState dlShake) in dlShakes)
            {
                dlShake.StartShaking(speed, left, dlExt.ShakeRotate);
            }
        }
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
        __result = fpData.IsTilePassable(
            (int)(tile_x - __instance.TileLocation.X),
            (int)(tile_y - __instance.TileLocation.Y)
        );
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

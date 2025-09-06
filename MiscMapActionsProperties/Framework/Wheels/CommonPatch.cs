using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Extensions;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;

namespace MiscMapActionsProperties.Framework.Wheels;

public static class CommonPatch
{
    public static string Building_PreviousBounds => $"{ModEntry.ModId}/PreviousBounds";

    public static event EventHandler<GameLocation>? GameLocation_resetLocalState;

    public sealed record UpdateWhenCurrentLocationArgs(GameLocation Location, GameTime Time);

    public static event EventHandler<UpdateWhenCurrentLocationArgs>? GameLocation_UpdateWhenCurrentLocationPrefix;
    public static event EventHandler<UpdateWhenCurrentLocationArgs>? GameLocation_UpdateWhenCurrentLocationFinalizer;

    public sealed record DrawAboveAlwaysFrontLayerArgs(GameLocation Location, SpriteBatch B);

    public sealed record ApplyMapOverrideArgs(GameLocation Location, Rectangle DestRect);

    public static event EventHandler<ApplyMapOverrideArgs>? GameLocation_ApplyMapOverride;

    public static event EventHandler<GameLocation>? GameLocation_ReloadMap;

    public sealed record OnBuildingMovedArgs(GameLocation Location, Building Building, Rectangle PreviousBounds);

    public static event EventHandler<OnBuildingMovedArgs>? GameLocation_OnBuildingEndMove;

    public sealed record OnFurnitureMovedArgs(Furniture Furniture, bool IsRemove, PlacementInfo Placement);

    public static event EventHandler<OnFurnitureMovedArgs>? Furniture_OnMoved;

    public static event EventHandler<Flooring>? Flooring_OnMoved;

    public sealed record MapTilePropChangedArgs(GameLocation Location, Point DestPoint, string Layer);

    public static event EventHandler<MapTilePropChangedArgs>? GameLocation_MapTilePropChanged;

    internal static void Register()
    {
        try
        {
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(GameLocation), "resetLocalState"),
                postfix: new HarmonyMethod(typeof(CommonPatch), nameof(GameLocation_resetLocalState_Postfix))
            );
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.UpdateWhenCurrentLocation)),
                prefix: new HarmonyMethod(typeof(CommonPatch), nameof(GameLocation_UpdateWhenCurrentLocation_Prefix)),
                finalizer: new HarmonyMethod(
                    typeof(CommonPatch),
                    nameof(GameLocation_UpdateWhenCurrentLocation_Finalizer)
                )
            );
            ModEntry.harm.Patch(
                // Map override_map, string override_key, Microsoft.Xna.Framework.Rectangle? source_rect = null, Microsoft.Xna.Framework.Rectangle? dest_rect = null, Action<Point> perTileCustomAction = null
                original: AccessTools.Method(
                    typeof(GameLocation),
                    nameof(GameLocation.ApplyMapOverride),
                    [typeof(xTile.Map), typeof(string), typeof(Rectangle?), typeof(Rectangle?), typeof(Action<Point>)]
                ),
                prefix: new HarmonyMethod(typeof(CommonPatch), nameof(GameLocation_ApplyMapOverride_Prefix)),
                finalizer: new HarmonyMethod(typeof(CommonPatch), nameof(GameLocation_ApplyMapOverride_Finalizer))
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(Building), nameof(Building.OnStartMove)),
                prefix: new HarmonyMethod(typeof(CommonPatch), nameof(Building_OnStartMove_Prefix))
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.OnBuildingMoved)),
                finalizer: new HarmonyMethod(typeof(CommonPatch), nameof(GameLocation_OnBuildingMoved_Finalizer))
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.reloadMap)),
                finalizer: new HarmonyMethod(typeof(CommonPatch), nameof(GameLocation_reloadMap_Finalizer))
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.setMapTile)),
                prefix: new HarmonyMethod(typeof(CommonPatch), nameof(GameLocation_setMapTile_Prefix)),
                finalizer: new HarmonyMethod(typeof(CommonPatch), nameof(GameLocation_setMapTile_Finalizer))
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.setAnimatedMapTile)),
                finalizer: new HarmonyMethod(typeof(CommonPatch), nameof(GameLocation_setAnimatedMapTile_Finalizer))
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.removeMapTile)),
                prefix: new HarmonyMethod(typeof(CommonPatch), nameof(GameLocation_removeMapTile_Prefix)),
                finalizer: new HarmonyMethod(typeof(CommonPatch), nameof(GameLocation_removeMapTile_Finalizer))
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.setTileProperty)),
                finalizer: new HarmonyMethod(typeof(CommonPatch), nameof(GameLocation_setTileProperty_Finalizer))
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.removeTileProperty)),
                finalizer: new HarmonyMethod(typeof(CommonPatch), nameof(GameLocation_setTileProperty_Finalizer))
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(Flooring), nameof(Flooring.OnAdded)),
                finalizer: new HarmonyMethod(typeof(CommonPatch), nameof(Flooring_OnAdded_Finalizer))
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(Flooring), nameof(Flooring.OnRemoved)),
                prefix: new HarmonyMethod(typeof(CommonPatch), nameof(Flooring_OnRemoved_Prefix))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log(
                $"Failed to apply CommonPatch, this is a critical error that should be reported to the mod page:\n{err}",
                LogLevel.Error
            );
        }

        ModEntry.help.Events.World.FurnitureListChanged += OnFurnitureListChanged;
        GameLocation_resetLocalState += On_GameLocation_resetLocalState;
    }

    #region furniture_caching
    private static readonly PerScreenCache<Dictionary<Point, HashSet<Furniture>>?> psTileToFurni =
        PerScreenCache.Make<Dictionary<Point, HashSet<Furniture>>?>();

    private static Dictionary<Point, HashSet<Furniture>> CreateTileToFurniture(GameLocation location)
    {
        Dictionary<Point, HashSet<Furniture>> tileToFurni = [];
        foreach (Furniture furni in location.furniture)
        {
            Rectangle bounds = GetFurnitureTileDataBounds(furni);
            foreach (Point pnt in IterateBounds(bounds))
            {
                if (tileToFurni.TryGetValue(pnt, out HashSet<Furniture>? furniSet))
                {
                    furniSet.Add(furni);
                }
                else
                {
                    tileToFurni[pnt] = [furni];
                }
            }
        }
        return tileToFurni;
    }

    internal static bool TryGetFurnitureAtTileForLocation(
        GameLocation location,
        Point pnt,
        [NotNullWhen(true)] out HashSet<Furniture>? furniSet
    )
    {
        if (psTileToFurni.Value is not Dictionary<Point, HashSet<Furniture>> tileToFurni)
        {
            tileToFurni = CreateTileToFurniture(location);
            psTileToFurni.Value = tileToFurni;
        }
        return tileToFurni.TryGetValue(pnt, out furniSet);
    }

    public sealed record PlacementInfo(GameLocation Location, Point TileLocation);

    private static readonly ConditionalWeakTable<Furniture, PlacementInfo> FurnitureRectCache = [];

    private static PlacementInfo CreateFurniturePlacementInfo(Furniture furniture) =>
        new(furniture.Location, furniture.TileLocation.ToPoint());

    private static void On_GameLocation_resetLocalState(object? sender, GameLocation e)
    {
        if (e == Game1.currentLocation)
        {
            psTileToFurni.Value = null;
            foreach (Furniture added in e.furniture)
            {
                FurnitureRectCache.GetValue(added, CreateFurniturePlacementInfo);
            }
        }
    }

    private static void OnFurnitureListChanged(object? sender, FurnitureListChangedEventArgs e)
    {
        // update tile to furniture
        if (psTileToFurni.Value is Dictionary<Point, HashSet<Furniture>> tileToFurni)
        {
            foreach (Furniture added in e.Added)
            {
                Rectangle bounds = GetFurnitureTileDataBounds(added);
                foreach (Point pnt in IterateBounds(bounds))
                {
                    if (tileToFurni.TryGetValue(pnt, out HashSet<Furniture>? furniSet))
                    {
                        furniSet.Add(added);
                    }
                    else
                    {
                        tileToFurni[pnt] = [added];
                    }
                }
            }
            foreach (Furniture removed in e.Removed)
            {
                Rectangle bounds = GetFurnitureTileDataBounds(removed);
                foreach (Point pnt in IterateBounds(bounds))
                {
                    if (tileToFurni.TryGetValue(pnt, out HashSet<Furniture>? furniSet))
                    {
                        furniSet.Remove(removed);
                        if (furniSet.Count == 0)
                        {
                            tileToFurni.Remove(pnt);
                        }
                    }
                }
            }
        }

        // fire moved events
        foreach (Furniture added in e.Added)
        {
            Furniture_OnMoved?.Invoke(
                null,
                new(added, false, FurnitureRectCache.GetValue(added, CreateFurniturePlacementInfo))
            );
        }
        foreach (Furniture removed in e.Removed)
        {
            Furniture_OnMoved?.Invoke(
                null,
                new(removed, true, FurnitureRectCache.GetValue(removed, CreateFurniturePlacementInfo))
            );
            FurnitureRectCache.Remove(removed);
        }
    }
    #endregion

    private static void Flooring_OnRemoved_Prefix(Flooring __instance)
    {
        Flooring_OnMoved?.Invoke(null, __instance);
    }

    private static void Flooring_OnAdded_Finalizer(Flooring __instance)
    {
        Flooring_OnMoved?.Invoke(null, __instance);
    }

    internal static void GameLocation_MapTilePropChangedInvoke(GameLocation location, Point pos, string layer)
    {
        GameLocation_MapTilePropChanged?.Invoke(null, new(location, pos, layer));
    }

    private static void GameLocation_setTileProperty_Finalizer(
        GameLocation __instance,
        int tileX,
        int tileY,
        string layer,
        string key
    )
    {
        if (__instance.map?.GetLayer(layer)?.Tiles[tileX, tileY] != null)
        {
            GameLocation_MapTilePropChanged?.Invoke(null, new(__instance, new(tileX, tileY), layer));
        }
    }

    private static void GameLocation_removeMapTile_Prefix(
        GameLocation __instance,
        int tileX,
        int tileY,
        string layer,
        ref bool __state
    )
    {
        xTile.Layers.Layer layer2 = __instance.map.RequireLayer(layer);
        __state = layer2.Tiles[tileX, tileY] != null;
    }

    private static void GameLocation_removeMapTile_Finalizer(
        GameLocation __instance,
        int tileX,
        int tileY,
        string layer,
        ref bool __state
    )
    {
        if (__state)
        {
            GameLocation_MapTilePropChanged?.Invoke(null, new(__instance, new(tileX, tileY), layer));
        }
    }

    private static void GameLocation_setAnimatedMapTile_Finalizer(
        GameLocation __instance,
        int tileX,
        int tileY,
        string layer,
        bool copyProperties
    )
    {
        if (!copyProperties)
        {
            GameLocation_MapTilePropChanged?.Invoke(null, new(__instance, new(tileX, tileY), layer));
        }
    }

    private static void GameLocation_setMapTile_Prefix(
        GameLocation __instance,
        int tileX,
        int tileY,
        string layer,
        string tileSheetId,
        bool copyProperties,
        ref bool __state
    )
    {
        __state = false;
        if (!copyProperties)
        {
            xTile.Layers.Layer layer2 = __instance.map.RequireLayer(layer);
            __state = layer2.Tiles[tileX, tileY] is xTile.Tiles.StaticTile stile && stile.TileSheet.Id == tileSheetId;
        }
    }

    private static void GameLocation_setMapTile_Finalizer(
        GameLocation __instance,
        int tileX,
        int tileY,
        string layer,
        ref bool __state
    )
    {
        if (__state)
        {
            GameLocation_MapTilePropChanged?.Invoke(null, new(__instance, new(tileX, tileY), layer));
        }
    }

    private static void Building_OnStartMove_Prefix(Building __instance)
    {
        __instance.modData[Building_PreviousBounds] =
            $"{__instance.tileX.Value} {__instance.tileY.Value} {__instance.tilesWide.Value} {__instance.tilesHigh.Value}";
    }

    private static Rectangle GetBuildingPreviousBounds(Building building, bool withRadius)
    {
        if (building.modData.TryGetValue(Building_PreviousBounds, out string prevBoundsStr))
        {
            if (
                ArgUtility.TryGetRectangle(
                    ArgUtility.SplitBySpaceQuoteAware(prevBoundsStr),
                    0,
                    out Rectangle prevBounds,
                    out _,
                    "string prevBoundsStr"
                )
            )
            {
                if (!withRadius)
                    return prevBounds;
                int radius = building.GetAdditionalTilePropertyRadius();
                return new(
                    prevBounds.X - radius,
                    prevBounds.Y - radius,
                    prevBounds.Width + radius,
                    prevBounds.Height + radius
                );
            }
        }
        return Rectangle.Empty;
    }

    private static void GameLocation_OnBuildingMoved_Finalizer(GameLocation __instance, Building building)
    {
        GameLocation_OnBuildingEndMove?.Invoke(
            null,
            new(__instance, building, GetBuildingPreviousBounds(building, true))
        );
    }

    private static void GameLocation_reloadMap_Finalizer(GameLocation __instance)
    {
        GameLocation_ReloadMap?.Invoke(null, __instance);
    }

    private static void GameLocation_resetLocalState_Postfix(GameLocation __instance)
    {
        GameLocation_resetLocalState?.Invoke(null, __instance);
    }

    private static void GameLocation_UpdateWhenCurrentLocation_Prefix(GameLocation __instance, GameTime time)
    {
        GameLocation_UpdateWhenCurrentLocationPrefix?.Invoke(null, new(__instance, time));
    }

    private static void GameLocation_UpdateWhenCurrentLocation_Finalizer(GameLocation __instance, GameTime time)
    {
        GameLocation_UpdateWhenCurrentLocationFinalizer?.Invoke(null, new(__instance, time));
    }

    private static readonly ConditionalWeakTable<
        GameLocation,
        Dictionary<string, WeakReference<xTile.Map>>
    > RenovationMaps = [];

    private static Dictionary<string, WeakReference<xTile.Map>> CreatePerLocRenovations(GameLocation location) => [];

    private static void GameLocation_ApplyMapOverride_Prefix(
        HashSet<string> ____appliedMapOverrides,
        string override_key,
        ref bool __state
    )
    {
        __state = ____appliedMapOverrides.Contains(override_key);
    }

    private static void GameLocation_ApplyMapOverride_Finalizer(
        GameLocation __instance,
        HashSet<string> ____appliedMapOverrides,
        xTile.Map override_map,
        string override_key,
        ref Rectangle? dest_rect,
        bool __state
    )
    {
        if (dest_rect == null)
            return;
        if (__state == ____appliedMapOverrides.Contains(override_key))
            return;
        if (
            RenovationMaps.GetValue(__instance, CreatePerLocRenovations)
            is Dictionary<string, WeakReference<xTile.Map>> perLocRenovations
        )
        {
            if (perLocRenovations.TryGetValue(override_key, out WeakReference<xTile.Map>? mapRef))
            {
                if (mapRef.TryGetTarget(out xTile.Map? knownMap) && override_map == knownMap)
                {
                    return;
                }
                mapRef.SetTarget(override_map);
            }
            else
            {
                perLocRenovations[override_key] = new(override_map);
            }
        }
        GameLocation_ApplyMapOverride?.Invoke(null, new(__instance, (Rectangle)dest_rect));
    }

    internal static bool HasCustomFieldsOrMapProperty(GameLocation location, string propKey)
    {
        return TryGetLocationalProperty(location, propKey, out _);
    }

    internal static bool TryGetLocationalProperty(
        GameLocation location,
        string propKey,
        [NotNullWhen(true)] out string? prop
    )
    {
        prop = null;
        if (location == null)
            return false;
        if (location.GetData()?.CustomFields?.TryGetValue(propKey, out prop) ?? false)
        {
            return !string.IsNullOrWhiteSpace(prop);
        }
        if (location.Map != null && location.Map.Properties != null && location.TryGetMapProperty(propKey, out prop))
        {
            return !string.IsNullOrWhiteSpace(prop);
        }
        if (location.GetLocationContext()?.CustomFields?.TryGetValue(propKey, out prop) ?? false)
        {
            return !string.IsNullOrWhiteSpace(prop);
        }
        return false;
    }

    internal static bool TryGetLocationalPropertyInt(
        GameLocation location,
        string propKey,
        [NotNullWhen(true)] out int prop
    )
    {
        prop = 0;
        if (TryGetLocationalProperty(location, propKey, out string? propValue))
        {
            if (int.TryParse(propValue, out prop))
            {
                return true;
            }
        }
        return false;
    }

    internal static bool TryGetLocationalPropertyVector2(
        GameLocation location,
        string propKey,
        [NotNullWhen(true)] out Vector2 prop
    )
    {
        prop = Vector2.Zero;
        if (TryGetLocationalProperty(location, propKey, out string? propValue))
        {
            string[] args = ArgUtility.SplitBySpaceQuoteAware(propValue);
            if (
                ArgUtility.TryGetFloat(args, 0, out float xVal, out string error, "float X")
                && ArgUtility.TryGetFloat(args, 1, out float yVal, out error, "float Y")
            )
            {
                prop = new Vector2(xVal, yVal);
                return true;
            }
            ModEntry.Log(error, LogLevel.Warn);
        }
        return false;
    }

    internal static Rectangle GetBuildingTileDataBounds(Building building, int scale = 1)
    {
        int radius = building.GetAdditionalTilePropertyRadius();
        return new(
            (building.tileX.Value - radius) * scale,
            (building.tileY.Value - radius) * scale,
            (building.tilesWide.Value + 2 * radius) * scale,
            (building.tilesHigh.Value + 2 * radius) * scale
        );
    }

    internal static Rectangle GetFurnitureTileDataBounds(Furniture furniture)
    {
        int radius = furniture.GetAdditionalTilePropertyRadius();
        return new(
            (int)furniture.TileLocation.X - radius,
            (int)furniture.TileLocation.Y - radius,
            furniture.getTilesWide() + 2 * radius,
            furniture.getTilesHigh() + 2 * radius
        );
    }

    internal static Rectangle GetFurnitureTileDataBounds(Furniture furniture, Point TileLocation)
    {
        int radius = furniture.GetAdditionalTilePropertyRadius();
        return new(
            TileLocation.X - radius,
            TileLocation.Y - radius,
            furniture.getTilesWide() + 2 * radius,
            furniture.getTilesHigh() + 2 * radius
        );
    }

    internal static IEnumerable<Point> IterateBounds(Rectangle bounds)
    {
        for (int x = bounds.Left; x < bounds.Right; x++)
        {
            for (int y = bounds.Top; y < bounds.Bottom; y++)
            {
                yield return new Point(x, y);
            }
        }
    }

    internal static void RegisterTileAndTouch(
        string actionName,
        Func<GameLocation, string[], Farmer, Point, bool> callbackAction
    )
    {
        GameLocation.RegisterTileAction(
            actionName,
            (location, args, farmer, tile) => callbackAction(location, args, farmer, tile)
        );
        GameLocation.RegisterTouchAction(
            actionName,
            (location, args, farmer, tile) => callbackAction(location, args, farmer, tile.ToPoint())
        );
    }

    internal static IEnumerable<ValueTuple<Vector2, MapTile>> IterateMapTiles(xTile.Map map, string layerName)
    {
        xTile.Layers.Layer layer = map.RequireLayer(layerName);
        for (int x = 0; x < layer.LayerWidth; x++)
        {
            for (int y = 0; y < layer.LayerHeight; y++)
            {
                Vector2 pos = new(x, y);
                if (layer.Tiles[x, y] is not MapTile tile)
                    continue;
                yield return new(pos, tile);
            }
        }
    }

    internal static string[]? SimpleTilePropTransformer(string?[] propValues)
    {
        if (propValues.Length != 1 || propValues[0] is not string propV)
            return null;
        return ArgUtility.SplitBySpaceQuoteAware(propV);
    }

    private static bool SimpleTilePropComparer(string[]? props1, string[]? props2)
    {
        return (props1 != null) != (props2 != null);
    }

    internal static TileDataCache<string[]> GetSimpleTileDataCache(string[] propKeys, string layer)
    {
        return new(propKeys, [layer], SimpleTilePropTransformer, SimpleTilePropComparer);
    }
}

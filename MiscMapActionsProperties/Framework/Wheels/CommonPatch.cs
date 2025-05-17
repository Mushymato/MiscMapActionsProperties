using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Extensions;
using StardewValley.Objects;

namespace MiscMapActionsProperties.Framework.Wheels;

public static class CommonPatch
{
    public static string Building_PreviousBounds => $"{ModEntry.ModId}/PreviousBounds";

    public static event EventHandler<GameLocation>? GameLocation_resetLocalState;

    public record UpdateWhenCurrentLocationArgs(GameLocation Location, GameTime Time);

    public static event EventHandler<UpdateWhenCurrentLocationArgs>? GameLocation_UpdateWhenCurrentLocation;

    public record DrawAboveAlwaysFrontLayerArgs(GameLocation Location, SpriteBatch B);

    public record ApplyMapOverrideArgs(GameLocation Location, Rectangle DestRect);

    public static event EventHandler<ApplyMapOverrideArgs>? GameLocation_ApplyMapOverride;

    public static event EventHandler<GameLocation>? GameLocation_ReloadMap;

    public record OnBuildingMovedArgs(GameLocation Location, Building Building, Rectangle PreviousBounds);

    public static event EventHandler<OnBuildingMovedArgs>? GameLocation_OnBuildingEndMove;

    public static event EventHandler<Furniture>? Furniture_OnMoved;

    public record MapTilePropChangedArgs(GameLocation Location, Point DestPoint, string Layer);

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
                postfix: new HarmonyMethod(typeof(CommonPatch), nameof(GameLocation_UpdateWhenCurrentLocation_Postfix))
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
                original: AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.OnRemoved)),
                prefix: new HarmonyMethod(typeof(CommonPatch), nameof(Furniture_OnRemoved_Prefix))
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.OnAdded)),
                finalizer: new HarmonyMethod(typeof(CommonPatch), nameof(Furniture_OnAdded_Finalizer))
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
        }
        catch (Exception err)
        {
            ModEntry.Log(
                $"Failed to patch CommonPatch, this should be reported to the mod page:\n{err}",
                LogLevel.Error
            );
        }
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

    private static void Furniture_OnAdded_Finalizer(Furniture __instance)
    {
        Furniture_OnMoved?.Invoke(null, __instance);
    }

    private static void Furniture_OnRemoved_Prefix(Furniture __instance)
    {
        Furniture_OnMoved?.Invoke(null, __instance);
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
                    ArgUtility.SplitBySpace(prevBoundsStr),
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

    private static void GameLocation_UpdateWhenCurrentLocation_Postfix(GameLocation __instance, GameTime time)
    {
        GameLocation_UpdateWhenCurrentLocation?.Invoke(null, new(__instance, time));
    }

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
        string override_key,
        ref Rectangle? dest_rect,
        bool __state
    )
    {
        if (dest_rect == null)
            return;
        if (__state == ____appliedMapOverrides.Contains(override_key))
            return;
        GameLocation_ApplyMapOverride?.Invoke(null, new(__instance, (Rectangle)dest_rect));
    }

    internal static bool HasCustomFieldsOrMapProperty(GameLocation location, string propKey)
    {
        return TryGetCustomFieldsOrMapProperty(location, propKey, out _);
    }

    internal static bool TryGetCustomFieldsOrMapProperty(
        GameLocation location,
        string propKey,
        [NotNullWhen(true)] out string? prop
    )
    {
        prop = null;
        if (location == null)
            return false;
        if (
            (location.GetData()?.CustomFields?.TryGetValue(propKey, out prop) ?? false)
            || (
                location.Map != null && location.Map.Properties != null && location.TryGetMapProperty(propKey, out prop)
            )
            || false
        )
            return !string.IsNullOrEmpty(prop);
        return false;
    }

    internal static bool TryGetCustomFieldsOrMapPropertyAsInt(
        GameLocation location,
        string propKey,
        [NotNullWhen(true)] out int prop
    )
    {
        prop = 0;
        if (TryGetCustomFieldsOrMapProperty(location, propKey, out string? propValue))
        {
            if (int.TryParse(propValue, out prop))
            {
                return true;
            }
        }
        return false;
    }

    internal static bool TryGetCustomFieldsOrMapPropertyAsVector2(
        GameLocation location,
        string propKey,
        [NotNullWhen(true)] out Vector2 prop
    )
    {
        prop = Vector2.Zero;
        if (TryGetCustomFieldsOrMapProperty(location, propKey, out string? propValue))
        {
            string[] args = ArgUtility.SplitBySpace(propValue);
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

    internal static Rectangle GetBuildingTileDataBounds(Building building)
    {
        int radius = building.GetAdditionalTilePropertyRadius();
        return new(
            building.tileX.Value - radius,
            building.tileY.Value - radius,
            building.tilesWide.Value + 2 * radius,
            building.tilesHigh.Value + 2 * radius
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
        return props1 == props2;
    }

    internal static TileDataCache<string[]> GetSimpleTileDataCache(string[] propKeys, string layer)
    {
        return new(propKeys, [layer], SimpleTilePropTransformer, SimpleTilePropComparer);
    }
}

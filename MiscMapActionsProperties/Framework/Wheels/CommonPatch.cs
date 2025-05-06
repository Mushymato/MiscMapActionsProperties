using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Extensions;

namespace MiscMapActionsProperties.Framework.Wheels;

internal static class CommonPatch
{
    public static event EventHandler<GameLocation>? GameLocation_resetLocalState;

    public record UpdateWhenCurrentLocationArgs(GameLocation Location, GameTime Time);

    public static event EventHandler<UpdateWhenCurrentLocationArgs>? GameLocation_UpdateWhenCurrentLocation;

    public record DrawAboveAlwaysFrontLayerArgs(GameLocation Location, SpriteBatch B);

    public record ApplyMapOverrideArgs(GameLocation Location, Rectangle DestRect);

    public static event EventHandler<ApplyMapOverrideArgs>? GameLocation_ApplyMapOverride;

    public static event EventHandler<GameLocation>? GameLocation_ReloadMap;

    public record OnBuildingMovedArgs(GameLocation Location, Building Building);

    public static event EventHandler<OnBuildingMovedArgs>? GameLocation_OnBuildingMoved;

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
                original: AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.OnBuildingMoved)),
                finalizer: new HarmonyMethod(typeof(CommonPatch), nameof(GameLocation_OnBuildingMoved_Finalizer))
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.reloadMap)),
                finalizer: new HarmonyMethod(typeof(CommonPatch), nameof(GameLocation_reloadMap_Finalizer))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch CommonPatch, this is a severe issue:\n{err}", LogLevel.Error);
        }
    }

    private static void GameLocation_OnBuildingMoved_Finalizer(GameLocation __instance, Building building)
    {
        GameLocation_OnBuildingMoved?.Invoke(null, new(__instance, building));
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

    internal static TileDataCache<string[]> GetSimpleTileDataCache(string[] propKeys, string layers)
    {
        return new(propKeys, layers, SimpleTilePropTransformer, SimpleTilePropComparer);
    }
}

using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Extensions;

namespace MiscMapActionsProperties.Framework.Tile;

internal record LightCondAndProps(string? Cond, string[] Props);

/// <summary>
/// Add new tile property mushymato.MMAP_Light [radius] [color] [type|texture] [offsetX] [offsetY] [context]
/// Place a light source on a tile.
/// [type] is either a light id or a texture (must be loaded).
/// A GSQ can be used to control the light, by setting mushymato.MMAP_LightCond "GSQ" on the same tile.
/// </summary>
internal static class LightSpot
{
    internal const string TileProp_Light = $"{ModEntry.ModId}_Light";
    internal const string TileProp_LightCond = $"{ModEntry.ModId}_LightCond";
    internal const string MapLightPrefix = $"{ModEntry.ModId}_MapLight_";

    private static readonly TileDataCache<LightCondAndProps> lightSpotsCache =
        new([TileProp_LightCond, TileProp_Light], ["Front", "Back"], LightSpotValueGetter, LightSpotValueComparer);

    private static bool LightSpotValueComparer(LightCondAndProps? props1, LightCondAndProps? props2)
    {
        if (props1 == null)
            return props2 == null;
        if (props2 == null)
            return props1 == null;
        return props1.Cond == props2.Cond && props1.Props == props2.Props;
    }

    private static LightCondAndProps? LightSpotValueGetter(string?[] propValues)
    {
        if (propValues.Length != 2 || propValues[1] is null)
            return null;
        string[] lightProps = ArgUtility.SplitBySpaceQuoteAware(propValues[1]);
        return new(propValues[0], lightProps);
    }

    private static string FormLightId(Point pos) =>
        string.Concat(MapLightPrefix, pos.X.ToString(), ",", pos.Y.ToString());

    private static readonly PerScreen<Dictionary<string, List<LightSource>>?> conditionalLightSources = new();

    internal static void Register()
    {
        ModEntry.help.Events.GameLoop.DayStarted += OnDayStarted;
        ModEntry.help.Events.Player.Warped += OnWarped;
        ModEntry.help.Events.GameLoop.TimeChanged += OnTimeChanged;

        lightSpotsCache.TileDataCacheChanged += OnCacheChanged;
    }

    private static void OnCacheChanged(object? sender, (GameLocation, HashSet<Point>?) e)
    {
        if (e.Item1 != Game1.currentLocation)
            return;

        if (e.Item2 == null)
        {
            Game1.currentLightSources.RemoveWhere(kv => kv.Key.StartsWith(MapLightPrefix));
            SpawnLocationLights(e.Item1);
            return;
        }

        string lightId;
        foreach (Point pos in e.Item2)
        {
            lightId = FormLightId(pos);
            Game1.currentLightSources.Remove(lightId);
            if (conditionalLightSources.Value == null)
                continue;
            foreach (List<LightSource> lights in conditionalLightSources.Value.Values)
            {
                lights.RemoveWhere(light => light.Id == lightId);
            }
        }

        UpdateLocationLightsForCache(e.Item1, e.Item2, lightSpotsCache);
        UpdateConditionalLights(e.Item1);
    }

    private static void OnDayStarted(object? sender, DayStartedEventArgs e) =>
        SpawnLocationLights(Game1.currentLocation);

    private static void OnWarped(object? sender, WarpedEventArgs e) => SpawnLocationLights(e.NewLocation);

    private static void OnTimeChanged(object? sender, TimeChangedEventArgs e)
    {
        if (Game1.currentLocation != null)
            UpdateConditionalLights(Game1.currentLocation);
    }

    private static void SpawnLocationLights(GameLocation location)
    {
        conditionalLightSources.Value = [];

        if (location == null || location.ignoreLights.Value)
            return;

        SpawnLocationLightsForCache(location, lightSpotsCache);

        UpdateConditionalLights(location);
    }

    private static void SpawnLocationLightsForCache(GameLocation location, TileDataCache<LightCondAndProps> cache)
    {
        if (cache.GetTileData(location) is not Dictionary<Point, LightCondAndProps> cachedProps)
            return;

        foreach ((Point pos, LightCondAndProps condprop) in cachedProps)
        {
            CreateNewLight(location, pos, condprop);
        }
    }

    private static void UpdateLocationLightsForCache(
        GameLocation location,
        HashSet<Point> changedPos,
        TileDataCache<LightCondAndProps> cache
    )
    {
        if (cache.GetTileData(location) is not Dictionary<Point, LightCondAndProps> cachedProps)
            return;

        foreach (Point pos in changedPos)
        {
            if (cachedProps.TryGetValue(pos, out LightCondAndProps? condprop))
            {
                CreateNewLight(location, pos, condprop);
            }
        }
    }

    private static void CreateNewLight(GameLocation location, Point pos, LightCondAndProps condprop)
    {
        if (
            Light.MakeMapLightFromProps(
                condprop.Props,
                FormLightId(pos),
                new Vector2(pos.X + 0.5f, pos.Y + 0.5f) * Game1.tileSize,
                location.NameOrUniqueName
            )
            is LightSource light
        )
        {
            if (condprop.Cond == null)
            {
                Game1.currentLightSources.Add(light);
            }
            else if (conditionalLightSources.Value != null)
            {
                string condTrim = condprop.Cond.Trim();
                if (conditionalLightSources.Value.TryGetValue(condTrim, out List<LightSource>? lightSources))
                {
                    lightSources.Add(light);
                }
                else
                {
                    conditionalLightSources.Value[condTrim] = [light];
                }
            }
        }
    }

    private static void UpdateConditionalLights(GameLocation location)
    {
        if (conditionalLightSources.Value == null)
            return;
        GameStateQueryContext context = new(location, Game1.player, null, null, null);
        foreach ((string cond, List<LightSource> lights) in conditionalLightSources.Value)
        {
            if (GameStateQuery.CheckConditions(cond, context))
            {
                foreach (LightSource light in lights)
                    Game1.currentLightSources.Add(light);
            }
            else
            {
                foreach (LightSource light in lights)
                    Game1.currentLightSources.Remove(light.Id);
            }
        }
    }
}

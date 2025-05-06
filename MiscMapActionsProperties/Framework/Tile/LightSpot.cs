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
/// Add new tile property mushymato.MMAP_Light [radius] [color] [type|texture] [offsetX] [offsetY]
/// Place a light source on a tile.
/// [type] is either a light id or a texture (must be loaded).
/// A GSQ can be used to control the light, by setting mushymato.MMAP_LightCond "GSQ" on the same tile.
/// </summary>
internal static class LightSpot
{
    internal static readonly string TileProp_Light = $"{ModEntry.ModId}_Light";
    internal static readonly string TileProp_LightCond = $"{ModEntry.ModId}_LightCond";

    private static readonly TileDataCache<LightCondAndProps> lightSpotsCacheBack =
        new([TileProp_LightCond, TileProp_Light], "Back", LightSpotValueGetter, LightSpotValueComparer);

    private static readonly TileDataCache<LightCondAndProps> lightSpotsCacheFront =
        new([TileProp_LightCond, TileProp_Light], "Front", LightSpotValueGetter, LightSpotValueComparer);

    private static bool LightSpotValueComparer(LightCondAndProps? props1, LightCondAndProps? props2)
    {
        if (props1 == null)
            return props2 == null;
        if (props2 == null)
            return props1 == null;
        ModEntry.LogOnce(
            $"{string.Join(',', props1.Props)}=={string.Join(',', props2.Props)}? {props1.Props == props2.Props}"
        );
        return props1.Cond == props2.Cond && props1.Props == props2.Props;
    }

    private static LightCondAndProps? LightSpotValueGetter(string?[] propValues)
    {
        if (propValues.Length != 2 || propValues[1] is null)
            return null;
        string[] lightProps = ArgUtility.SplitBySpaceQuoteAware(propValues[1]);
        return new(propValues[0], lightProps);
    }

    private static readonly PerScreen<List<LightSource>> unconditionalLightSources = new();
    private static readonly PerScreen<Dictionary<string, List<LightSource>>> conditionalLightSources = new();

    internal static void Register()
    {
        ModEntry.help.Events.GameLoop.DayStarted += OnDayStarted;
        ModEntry.help.Events.Player.Warped += OnWarped;
        ModEntry.help.Events.GameLoop.TimeChanged += OnTimeChanged;

        lightSpotsCacheBack.TileDataCacheChanged += OnCacheChanged;
        lightSpotsCacheFront.TileDataCacheChanged += OnCacheChanged;
    }

    // TODO: refactor this later to take advantage of changed
    private static void OnCacheChanged(object? sender, (GameLocation, HashSet<Point>?) e)
    {
        if (e.Item1 != Game1.currentLocation)
            return;
        foreach (LightSource light in unconditionalLightSources.Value)
        {
            Game1.currentLightSources.Remove(light.Id);
        }
        foreach ((string cond, List<LightSource> lights) in conditionalLightSources.Value)
        {
            foreach (LightSource light in lights)
                Game1.currentLightSources.Remove(light.Id);
        }
        SpawnLocationLights(e.Item1);
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
        unconditionalLightSources.Value = [];
        conditionalLightSources.Value = [];

        if (location == null || location.ignoreLights.Value)
            return;

        foreach (
            (Point pos, LightCondAndProps condprop) in lightSpotsCacheFront
                .GetTileData(location)
                .Concat(lightSpotsCacheBack.GetTileData(location))
        )
        {
            if (
                Light.MakeMapLightFromProps(
                    condprop.Props,
                    new Vector2(pos.X + 1 / 2, pos.Y + 1 / 2) * Game1.tileSize,
                    location.NameOrUniqueName
                )
                is not LightSource light
            )
            {
                continue;
            }
            if (condprop.Cond == null)
            {
                Game1.currentLightSources.Add(light);
                unconditionalLightSources.Value.Add(light);
            }
            else
            {
                if (!conditionalLightSources.Value.ContainsKey(condprop.Cond))
                    conditionalLightSources.Value[condprop.Cond] = [];
                conditionalLightSources.Value[condprop.Cond].Add(light);
            }
        }

        UpdateConditionalLights(location);
    }

    private static void UpdateConditionalLights(GameLocation location)
    {
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

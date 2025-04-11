using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Extensions;
using StardewValley.GameData.Buildings;

namespace MiscMapActionsProperties.Framework.Tile;

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

    internal static string[] LayerNames = ["Back", "Front"];

    private static readonly TileDataCache<Tuple<string?, string[]>> lightSpotsCache =
        new(TileProp_Light, ["Back", "Front"], LightSpotValueGetter);

    private static Tuple<string?, string[]>? LightSpotValueGetter(string propKey, MapTile tile)
    {
        if (
            tile.Properties.TryGetValue(TileProp_Light, out string lightProps)
            || tile.TileIndexProperties.TryGetValue(TileProp_Light, out lightProps)
        )
        {
            if (
                tile.Properties.TryGetValue($"{propKey}Cond", out string? lightCond)
                || tile.TileIndexProperties.TryGetValue($"{propKey}Cond", out lightCond)
            )
            {
                return new(lightCond, ArgUtility.SplitBySpaceQuoteAware(lightProps));
            }
            return new(null, ArgUtility.SplitBySpaceQuoteAware(lightProps));
        }
        return null;
    }

    internal static void Register()
    {
        CommonPatch.GameLocation_resetLocalState += GameLocation_resetLocalState_Postfix;
    }

    private static IEnumerable<LightSource> GetMapTileLights(GameLocation location)
    {
        foreach ((Vector2 pos, (string? cond, string[] props)) in lightSpotsCache.GetProps(location.Map))
        {
            if (!GameStateQuery.CheckConditions(cond, location: location))
                continue;
            if (
                Light.MakeMapLightFromProps(
                    props,
                    pos * Game1.tileSize + new Vector2(Game1.tileSize / 2, Game1.tileSize / 2),
                    location.NameOrUniqueName
                )
                is LightSource light
            )
                yield return light;
        }

        // map building layer lights
        foreach (Building building in location.buildings)
        {
            if (building.GetData() is not BuildingData data)
                continue;

            HashSet<ValueTuple<int, int>> bannedTiles = [];
            foreach (string layerName in LayerNames)
            {
                foreach (BuildingTileProperty btp in data.TileProperties)
                {
                    if (btp.Name != TileProp_LightCond || btp.Layer != layerName)
                        continue;
                    if (!GameStateQuery.CheckConditions(btp.Value, location: location))
                        for (int i = 0; i < btp.TileArea.Width; i++)
                        for (int j = 0; j < btp.TileArea.Height; j++)
                            bannedTiles.Add(new(i, j));
                }

                foreach (BuildingTileProperty btp in data.TileProperties)
                {
                    if (btp.Name != TileProp_Light || btp.Layer != layerName)
                        continue;
                    if (
                        Light.MakeMapLightFromProps(
                            ArgUtility.SplitBySpaceQuoteAware(btp.Value),
                            new Vector2(building.tileX.Value, building.tileY.Value),
                            location.NameOrUniqueName
                        )
                        is not LightSource baseLight
                    )
                        continue;
                    for (int i = 0; i < btp.TileArea.Width; i++)
                    {
                        for (int j = 0; j < btp.TileArea.Height; j++)
                        {
                            if (bannedTiles.Contains(new(i, j)))
                                continue;
                            Vector2 pos =
                                new(
                                    building.tileX.Value + btp.TileArea.X + i,
                                    building.tileY.Value + btp.TileArea.Y + j
                                );
                            LightSource light = baseLight.Clone();
                            light.Id = $"{light.Id}+{btp.TileArea.X + i},{btp.TileArea.Y + j}";
                            light.position.Value =
                                pos * Game1.tileSize + new Vector2(Game1.tileSize / 2, Game1.tileSize / 2);
                            light.lightTexture = baseLight.lightTexture;
                            yield return light;
                        }
                    }
                }
            }
        }
    }

    private static void GameLocation_resetLocalState_Postfix(object? sender, CommonPatch.ResetLocalStateArgs e)
    {
        if (e.Location.ignoreLights.Value)
            return;

        foreach (LightSource light in GetMapTileLights(e.Location))
        {
            Game1.currentLightSources.Add(light);
        }
    }
}

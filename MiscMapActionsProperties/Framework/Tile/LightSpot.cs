using HarmonyLib;
using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
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

    internal static void Register()
    {
        CommonPatch.GameLocation_resetLocalState += GameLocation_resetLocalState_Postfix;
    }

    private static IEnumerable<LightSource> GetMapTileLights(GameLocation location, string layerName)
    {
        // map layer lights
        var frontLayer = location.map.RequireLayer(layerName);
        for (int x = 0; x < frontLayer.LayerWidth; x++)
        {
            for (int y = 0; y < frontLayer.LayerHeight; y++)
            {
                Vector2 pos = new(x, y);
                if (pos.Equals(Vector2.Zero))
                    continue;
                MapTile tile = frontLayer.Tiles[x, y];
                if (tile == null)
                    continue;
                if (tile.Properties.TryGetValue(TileProp_Light, out string lightProps))
                {
                    if (
                        tile.Properties.TryGetValue(TileProp_LightCond, out string lightCond)
                        && !GameStateQuery.CheckConditions(lightCond, location: location)
                    )
                        continue;
                    if (
                        Light.MakeMapLightFromProps(
                            lightProps,
                            pos * Game1.tileSize + new Vector2(Game1.tileSize / 2, Game1.tileSize / 2),
                            location.NameOrUniqueName
                        )
                        is LightSource light
                    )
                        yield return light;
                }
            }
        }

        // map building layer lights
        foreach (Building building in location.buildings)
        {
            if (building.GetData() is not BuildingData data)
                continue;

            HashSet<ValueTuple<int, int>> bannedTiles = [];
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
                string lightProps = btp.Value;
                if (
                    Light.MakeMapLightFromProps(
                        lightProps,
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
                            new(building.tileX.Value + btp.TileArea.X + i, building.tileY.Value + btp.TileArea.Y + j);
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

    private static void GameLocation_resetLocalState_Postfix(object? sender, CommonPatch.ResetLocalStateArgs e)
    {
        if (e.Location.ignoreLights.Value)
            return;

        foreach (string layerName in LayerNames)
        {
            foreach (LightSource light in GetMapTileLights(e.Location, layerName))
            {
                Game1.currentLightSources.Add(light);
            }
        }
    }
}

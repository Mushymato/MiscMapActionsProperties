using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewValley;

namespace MiscMapActionsProperties.Framework.Tile;

/// <summary>
/// Add new tile property mushymato.MMAP_AnimalSpot T
/// Control where animals go when asked for random position
/// </summary>
internal static class AnimalSpot
{
    internal static readonly string TileProp_AnimalSpot = $"{ModEntry.ModId}_AnimalSpot";
    private static readonly ConditionalWeakTable<xTile.Map, List<Vector2>> animalSpotsCache = [];

    internal static void Register()
    {
        ModEntry.help.Events.GameLoop.ReturnedToTitle += ClearAnimalSpotsCache;
        try
        {
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(FarmAnimal), nameof(FarmAnimal.setRandomPosition)),
                prefix: new HarmonyMethod(typeof(AnimalSpot), nameof(FarmAnimal_setRandomPosition_Prefix))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch AnimalSpot:\n{err}", LogLevel.Error);
        }
    }

    private static void ClearAnimalSpotsCache(object? sender, EventArgs e)
    {
        animalSpotsCache.Clear();
    }

    private static List<Vector2> GetAnimalSpots(xTile.Map map)
    {
        List<Vector2> animalSpots = [];
        foreach ((Vector2 pos, MapTile tile) in CommonPatch.IterateMapTiles(map, "Back"))
        {
            if (
                tile.Properties.ContainsKey(TileProp_AnimalSpot)
                || tile.TileIndexProperties.ContainsKey(TileProp_AnimalSpot)
            )
            {
                animalSpots.Add(pos);
            }
        }
        return animalSpots;
    }

    private static bool HasOtherAnimals(FarmAnimal __instance, GameLocation location, Vector2 tile)
    {
        Rectangle bounds = __instance.GetBoundingBox();
        foreach (FarmAnimal animal in location.animals.Values)
            if (animal != __instance && bounds.Intersects(animal.GetBoundingBox()))
                return true;
        return false;
    }

    private static bool FarmAnimal_setRandomPosition_Prefix(FarmAnimal __instance, GameLocation location)
    {
        try
        {
            __instance.StopAllActions();
            List<Vector2> animalSpots = animalSpotsCache.GetValue(location.Map, GetAnimalSpots);
            Character _base = __instance;
            foreach (Vector2 pos in animalSpots)
            {
                _base.Position = pos * 64;
                if (
                    location.Objects.ContainsKey(pos)
                    || HasOtherAnimals(__instance, location, pos)
                    || location.isCollidingPosition(
                        _base.GetBoundingBox(),
                        Game1.viewport,
                        isFarmer: false,
                        0,
                        glider: false,
                        __instance
                    )
                )
                    continue;
                __instance.SleepIfNecessary();
                return false;
            }
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in FarmAnimal_setRandomPosition_Prefix:\n{err}", LogLevel.Error);
        }
        return true;
    }
}

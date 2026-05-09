using HarmonyLib;
using StardewModdingAPI;
using StardewValley.Objects;

namespace MiscMapActionsProperties.Framework.Tile;

/// <summary>
/// Add new tile property mushymato.MMAP_CaskSpot T
/// This will enable cask for that spot.
/// </summary>
internal static class CaskSpot
{
    internal const string TileProp_CaskSpot = $"{ModEntry.ModId}_CaskSpot";

    internal static void Register()
    {
        try
        {
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(Cask), nameof(Cask.IsValidCaskLocation)),
                postfix: new HarmonyMethod(typeof(CaskSpot), nameof(Cask_IsValidCaskLocation_Postfix))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch AnimalSpot:\n{err}", LogLevel.Error);
        }
    }

    private static void Cask_IsValidCaskLocation_Postfix(Cask __instance, ref bool __result)
    {
        if (__result)
            return;
        if (
            __instance.Location?.doesTileHaveProperty(
                (int)__instance.TileLocation.X,
                (int)__instance.TileLocation.Y,
                TileProp_CaskSpot,
                "Back"
            ) == "T"
        )
        {
            __result = true;
        }
    }
}

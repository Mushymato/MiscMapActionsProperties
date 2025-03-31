using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley.Locations;

namespace MiscMapActionsProperties.Framework.Location;

/// <summary>
/// Add new map property mushymato.MMAP_CribPosition x y
/// Overrides the default crib bounds's top left position (width and height still 3x4)
/// Only works in farmhouse/cabins
/// </summary>
internal static class CribPosition
{
    internal static readonly string MapProp_CribPosition = $"{ModEntry.ModId}_CribPosition";

    internal static void Register()
    {
        try
        {
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(FarmHouse), nameof(FarmHouse.GetCribBounds)),
                postfix: new HarmonyMethod(typeof(CribPosition), nameof(FarmHouse_GetCribPosition_Postfix))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch CribPosition:\n{err}", LogLevel.Error);
        }
    }

    private static void FarmHouse_GetCribPosition_Postfix(FarmHouse __instance, ref Rectangle? __result)
    {
        if (__result != null && __instance.TryGetMapPropertyAs(MapProp_CribPosition, out Point cribPos))
        {
            __result = new Rectangle(cribPos.X, cribPos.Y, __result.Value.Width, __result.Value.Height);
        }
    }
}

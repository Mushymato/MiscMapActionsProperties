using HarmonyLib;
using StardewModdingAPI;
using StardewValley.TerrainFeatures;

namespace MiscMapActionsProperties.Framework.Tile;

/// <summary>
/// Add new back layer tile property mushymato.MMAP_Paddy [T|I]
/// Makes this spot valid for Paddies
/// </summary>
internal static class PaddySpot
{
    internal const string TileProp_Paddy = $"{ModEntry.ModId}_Paddy";

    internal static void Register()
    {
        try
        {
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(HoeDirt), nameof(HoeDirt.paddyWaterCheck)),
                prefix: new HarmonyMethod(typeof(PaddySpot), nameof(HoeDirt_paddyWaterCheck_Prefix)),
                postfix: new HarmonyMethod(typeof(PaddySpot), nameof(HoeDirt_paddyWaterCheck_Postfix))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch PaddySpot:\n{err}", LogLevel.Error);
        }
    }

    private static void HoeDirt_paddyWaterCheck_Prefix(HoeDirt __instance, bool forceUpdate, ref bool __state)
    {
        __state = (forceUpdate || __instance.nearWaterForPaddy.Value < 0) && __instance.hasPaddyCrop();
    }

    private static void HoeDirt_paddyWaterCheck_Postfix(HoeDirt __instance, ref bool __state, ref bool __result)
    {
        if (!__state)
            return;

        if (
            __instance.Location.doesTileHaveProperty(
                (int)__instance.Tile.X,
                (int)__instance.Tile.Y,
                TileProp_Paddy,
                "Back"
            )
            is string Value
        )
        {
            if (__instance.Pot == null || Value == "I")
            {
                __instance.nearWaterForPaddy.Value = 1;
                __result = true;
            }
        }
    }
}

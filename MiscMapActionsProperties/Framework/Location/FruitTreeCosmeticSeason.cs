using HarmonyLib;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace MiscMapActionsProperties.Framework.Location;

/// <summary>
/// Add new map property mushymato.MMAP_FruitTreeCosmeticSeason T
/// If set, follow the location's season instead of always using summer even when in a greenhouse.
/// </summary>
internal static class FruitTreeCosmeticSeason
{
    internal const string MapProp_FruitTreeCosmeticSeason = $"{ModEntry.ModId}_FruitTreeCosmeticSeason";

    internal static void Register()
    {
        // This mod is an unconditional and stronger patch on the same thing, disable my patch
        if (ModEntry.help.ModRegistry.IsLoaded("Esper89.FruitTreeSeasons"))
            return;
        try
        {
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(FruitTree), nameof(FruitTree.GetCosmeticSeason)),
                postfix: new HarmonyMethod(typeof(FruitTreeCosmeticSeason), nameof(FruitTree_GetCosmeticSeason_Postfix))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch FruitTreeCosmeticSeason:\n{err}", LogLevel.Error);
        }
    }

    private static void FruitTree_GetCosmeticSeason_Postfix(FruitTree __instance, ref Season __result)
    {
        if (
            __instance.IgnoresSeasonsHere()
            && CommonPatch.HasCustomFieldsOrMapProperty(__instance.Location, MapProp_FruitTreeCosmeticSeason)
        )
        {
            __result = __instance.Location.GetSeason();
        }
    }
}

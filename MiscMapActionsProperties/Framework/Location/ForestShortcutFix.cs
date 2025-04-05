using HarmonyLib;
using StardewModdingAPI;
using StardewValley.Locations;

namespace MiscMapActionsProperties.Framework.Location;

/// <summary>
/// Maybe fix weird forest shortcut thing???
/// </summary>
internal static class ForestShortcutFix
{
    internal static void Register()
    {
        try
        {
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(Forest), "showCommunityUpgradeShortcuts"),
                prefix: new HarmonyMethod(
                    typeof(ForestShortcutFix),
                    nameof(Forest_showCommunityUpgradeShortcuts_Prefix)
                )
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch ForestShortcutFix:\n{err}", LogLevel.Error);
        }
    }

    private static void Forest_showCommunityUpgradeShortcuts_Prefix(ref bool ___hasShownCCUpgrade)
    {
        ModEntry.Log($"showCommunityUpgradeShortcuts: {___hasShownCCUpgrade}, will set this to false", LogLevel.Info);
        ___hasShownCCUpgrade = false;
    }
}

using HarmonyLib;
using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.TokenizableStrings;
using StardewValley.Tools;
using StardewValley.Triggers;

namespace MiscMapActionsProperties.Framework.Location;

/// <summary>
/// Add new map property mushymato.MMAP_ProtectTree [T|message]
/// If set, all wild trees on this map is protected
/// Also add new map property mushymato.MMAP_ProtectFruitTree [T|message] which does the same thing, but for fruit trees.
/// Both of these fire a trigger of same name should the player attempt to chop tree.
/// </summary>
internal static class ProtectTree
{
    internal const string MapProp_ProtectTree = $"{ModEntry.ModId}_ProtectTree";
    internal const string MapProp_ProtectFruitTree = $"{ModEntry.ModId}_ProtectFruitTree";

    internal static void Register()
    {
        try
        {
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(Tree), nameof(Tree.performToolAction)),
                prefix: new HarmonyMethod(typeof(ProtectTree), nameof(Tree_performToolAction_Prefix))
            );
            TriggerActionManager.RegisterTrigger(MapProp_ProtectTree);
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(FruitTree), nameof(FruitTree.performToolAction)),
                prefix: new HarmonyMethod(typeof(ProtectTree), nameof(FruitTree_performToolAction_Prefix))
            );
            TriggerActionManager.RegisterTrigger(MapProp_ProtectFruitTree);
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch ProtectTree:\n{err}", LogLevel.Error);
        }
    }

    private static void ShowProtectMessage(GameLocation location, Vector2 tileLocation, string protectMessageKey)
    {
        if (protectMessageKey != "T")
        {
            string? protectMessage = null;
            if (Game1.content.IsValidTranslationKey(protectMessageKey))
            {
                protectMessage = Game1.content.LoadString(protectMessageKey);
            }
            else
            {
                protectMessage = TokenParser.ParseText(protectMessageKey);
            }
            if (protectMessage != null)
            {
                location.playSound("axchop", tileLocation, null);
                Game1.addHUDMessage(new HUDMessage(protectMessage) { noIcon = true });
            }
        }
    }

    private static bool Tree_performToolAction_Prefix(
        Tree __instance,
        Tool t,
        int explosion,
        Vector2 tileLocation,
        ref bool __result
    )
    {
        if (
            (t is not Axe && explosion == 0)
            || !CommonPatch.TryGetLocationalProperty(
                __instance.Location,
                MapProp_ProtectTree,
                out string? protectMessageKey
            )
        )
        {
            return true;
        }
        __instance.shake(tileLocation, doEvenIfStillShaking: true);
        ShowProtectMessage(__instance.Location, tileLocation, protectMessageKey);
        TriggerActionManager.Raise(MapProp_ProtectTree);
        __result = false;
        return false;
    }

    private static bool FruitTree_performToolAction_Prefix(
        FruitTree __instance,
        Tool t,
        int explosion,
        Vector2 tileLocation
    )
    {
        if (
            (t is not Axe && explosion == 0)
            || !CommonPatch.TryGetLocationalProperty(
                __instance.Location,
                MapProp_ProtectFruitTree,
                out string? protectMessageKey
            )
        )
        {
            return true;
        }
        __instance.shake(tileLocation, doEvenIfStillShaking: true);
        ShowProtectMessage(__instance.Location, tileLocation, protectMessageKey);
        TriggerActionManager.Raise(MapProp_ProtectFruitTree);
        return false;
    }
}

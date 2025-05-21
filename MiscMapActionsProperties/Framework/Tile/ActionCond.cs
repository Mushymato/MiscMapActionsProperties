using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace MiscMapActionsProperties.Framework.Tile;

/// <summary>
/// Add new tile prop mushymato.MMAP_ActionCond GSQ
/// If set on a tile with Action, this GSQ is checked before the Action is allowed to run
/// Add new tile prop mushymato.MMAP_TouchActionCond GSQ
/// Does similar thing but for touch actions
/// </summary>
internal static class ActionCond
{
    internal const string TileProp_ActionCond = $"{ModEntry.ModId}_ActionCond";
    internal const string TileProp_TouchActionCond = $"{ModEntry.ModId}_TouchActionCond";

    internal static void Register()
    {
        try
        {
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.ShouldIgnoreAction)),
                postfix: new HarmonyMethod(typeof(ActionCond), nameof(GameLocation_ShouldIgnoreAction_Postfix))
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.IgnoreTouchActions)),
                postfix: new HarmonyMethod(typeof(ActionCond), nameof(GameLocation_IgnoreTouchActions_Postfix))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch ActionCond:\n{err}", LogLevel.Error);
        }
    }

    private static void GameLocation_IgnoreTouchActions_Postfix(GameLocation __instance, ref bool __result)
    {
        if (__result)
            return;

        if (
            __instance.doesTileHaveProperty(
                (int)__instance.lastTouchActionLocation.X,
                (int)__instance.lastTouchActionLocation.Y,
                TileProp_TouchActionCond,
                "Back"
            )
            is string actionCond
        )
        {
            __result = !GameStateQuery.CheckConditions(actionCond, location: __instance);
        }
    }

    private static void GameLocation_ShouldIgnoreAction_Postfix(
        GameLocation __instance,
        Farmer who,
        xTile.Dimensions.Location tileLocation,
        ref bool __result
    )
    {
        if (__result)
            return;

        if (
            __instance.doesTileHaveProperty(tileLocation.X, tileLocation.Y, TileProp_ActionCond, "Buildings")
            is string actionCond
        )
        {
            __result = !GameStateQuery.CheckConditions(actionCond, location: __instance, player: who);
        }
    }
}

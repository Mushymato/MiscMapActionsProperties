using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Locations;
using StardewValley.Triggers;

namespace MiscMapActionsProperties.Framework.Location;

/// <summary>
/// Add various things to help relocate stuff on farmhouse upgrade.
/// Can be used for other maps ofc.
/// </summary>
internal static class MapChangeRelocate
{
    internal static readonly string MapProp_SkipMoveObjectsForHouseUpgrade =
        $"{ModEntry.ModId}_SkipMoveObjectsForHouseUpgrade";
    internal static readonly string Trigger_MoveObjectsForHouseUpgrade = $"{ModEntry.ModId}_MoveObjectsForHouseUpgrade";
    internal static readonly string Action_ShiftContents = $"{ModEntry.ModId}_ShiftContents";

    internal static void Register()
    {
        TriggerActionManager.RegisterTrigger(Trigger_MoveObjectsForHouseUpgrade);
        TriggerActionManager.RegisterAction(Action_ShiftContents, MapChangeRelocateAction);
        try
        {
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(FarmHouse), nameof(FarmHouse.moveObjectsForHouseUpgrade)),
                prefix: new HarmonyMethod(
                    typeof(MapChangeRelocate),
                    nameof(FarmHouse_moveObjectsForHouseUpgrade_Prefix)
                )
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch FarmHouse:\n{err}", LogLevel.Error);
        }
    }

    private static bool FarmHouse_moveObjectsForHouseUpgrade_Prefix(FarmHouse __instance)
    {
        TriggerActionManager.Raise(Trigger_MoveObjectsForHouseUpgrade);
        if (__instance.HasMapPropertyWithValue(MapProp_SkipMoveObjectsForHouseUpgrade))
        {
            ModEntry.Log("Skipping FarmHouse.moveObjectsForHouseUpgrade");
            __instance.overlayObjects.Clear();
            return false;
        }
        return true;
    }

    private static bool MapChangeRelocateAction(string[] args, TriggerActionContext context, out string error)
    {
        if (
            !ArgUtility.TryGetPoint(args, 1, out Point source, out error, name: "Point source")
            || !ArgUtility.TryGetPoint(args, 3, out Point target, out error, name: "Point target")
            || !ArgUtility.TryGetPoint(args, 5, out Point area, out error, name: "Point area")
        )
        {
            return false;
        }
        ModEntry.Log($"{args[0]}: {source} -> {target} ({area})");
        DoRelocate(Utility.getHomeOfFarmer(Game1.player), source, target, area);
        return true;
    }

    private static void DoRelocate(GameLocation location, Point source, Point target, Point area)
    {
        Point delta = target - source;
        Rectangle bounds = new(source.X, source.Y, area.X, area.Y);
        location.shiftContents(delta.X, delta.Y, (tile, obj) => bounds.Contains(tile));
    }
}

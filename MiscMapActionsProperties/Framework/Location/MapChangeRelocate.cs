using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.Triggers;

namespace MiscMapActionsProperties.Framework.Location;

/// <summary>
/// Add various things to help relocate stuff on farmhouse upgrade.
/// Can be used for other maps ofc.
/// </summary>
internal static class MapChangeRelocate
{
    internal const string MapProp_SkipMoveObjectsForHouseUpgrade = $"{ModEntry.ModId}_SkipMoveObjectsForHouseUpgrade";
    internal const string Trigger_MoveObjectsForHouseUpgrade = $"{ModEntry.ModId}_MoveObjectsForHouseUpgrade";
    internal const string Action_ShiftContents = $"{ModEntry.ModId}_ShiftContents";

    internal static void Register()
    {
        ModEntry.help.Events.Multiplayer.ModMessageReceived += OnModMessageReceived;
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

    private static void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
    {
        if (Context.IsMainPlayer && e.FromModID == ModEntry.ModId && e.Type == Action_ShiftContents)
        {
            string[] args = e.ReadAs<string[]>();
            if (
                Game1.GetPlayer(e.FromPlayerID) is Farmer farmhand
                && !MapChangeRelocateAction(args, out string error, farmhand)
            )
            {
                ModEntry.Log(error, LogLevel.Error);
            }
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
            !Context.IsMainPlayer
            && (
                !ArgUtility.TryGetOptional(args, 7, out string? locationName, out _, name: "string? locationName")
                || locationName != Game1.player.homeLocation.Value
            )
        )
        {
            error = null!;
            ModEntry.help.Multiplayer.SendMessage(
                args,
                Action_ShiftContents,
                [ModEntry.ModId],
                [Game1.serverHost.Value.UniqueMultiplayerID]
            );
            return true;
        }
        return MapChangeRelocateAction(args, out error, Game1.player);
    }

    private static bool MapChangeRelocateAction(string[] args, out string error, Farmer farmer)
    {
        if (
            !ArgUtility.TryGetPoint(args, 1, out Point source, out error, name: "Point source")
            || !ArgUtility.TryGetPoint(args, 3, out Point target, out error, name: "Point target")
            || !ArgUtility.TryGetPoint(args, 5, out Point area, out error, name: "Point area")
            || !ArgUtility.TryGetOptional(args, 7, out string? locationName, out error, name: "string? locationName")
        )
        {
            return false;
        }
        GameLocation? gameLocation;
        if (locationName != null)
        {
            if (locationName == "Here")
                gameLocation = Game1.currentLocation;
            else if (locationName == "Cellar")
                gameLocation = Utility.getHomeOfFarmer(farmer)?.GetCellar();
            else
                gameLocation = Utility.fuzzyLocationSearch(locationName);
        }
        else
            gameLocation = Utility.getHomeOfFarmer(farmer);
        if (gameLocation == null)
            return false;
        ModEntry.Log($"{gameLocation.NameOrUniqueName}: {source} -> {target} ({area})");
        foreach (Furniture furniture in gameLocation.furniture)
        {
            ModEntry.Log($"{furniture.QualifiedItemId}: {furniture.DisplayName}");
        }

        Point delta = target - source;
        Rectangle bounds = new(source.X, source.Y, area.X, area.Y);
        gameLocation.shiftContents(delta.X, delta.Y, (tile, obj) => bounds.Contains(tile));

        return true;
    }
}

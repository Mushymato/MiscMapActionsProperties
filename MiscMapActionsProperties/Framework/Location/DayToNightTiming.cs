using HarmonyLib;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Triggers;

namespace MiscMapActionsProperties.Framework.Location;

/// <summary>
/// Add 3 new map properties for changing when the map/location becomes dark.
/// mushymato.MMAP_NightTimeStarting <time>
/// mushymato.MMAP_NightTimeModerate <time>
/// mushymato.MMAP_NightTimeTruly <time>
/// </summary>
internal static class DayToNightTiming
{
    internal const string MapProp_NightTimeStarting = $"{ModEntry.ModId}_NightTimeStarting";
    internal const string MapProp_NightTimeModerate = $"{ModEntry.ModId}_NightTimeModerate";
    internal const string MapProp_NightTimeTruly = $"{ModEntry.ModId}_NightTimeTruly";

    internal const string GSQ_TIME_IS_DAY = $"{ModEntry.ModId}_TIME_IS_DAY";
    internal const string GSQ_TIME_IS_SUNSET = $"{ModEntry.ModId}_TIME_IS_SUNSET";
    internal const string GSQ_TIME_IS_NIGHT = $"{ModEntry.ModId}_TIME_IS_NIGHT";

    internal static void Register()
    {
        ModEntry.help.Events.GameLoop.TimeChanged += OnTimeChanged;
        TriggerActionManager.RegisterTrigger(MapProp_NightTimeStarting);
        TriggerActionManager.RegisterTrigger(MapProp_NightTimeModerate);
        TriggerActionManager.RegisterTrigger(MapProp_NightTimeTruly);
        GameStateQuery.Register(GSQ_TIME_IS_DAY, TIME_IS_DAY);
        GameStateQuery.Register(GSQ_TIME_IS_SUNSET, TIME_IS_SUNSET);
        GameStateQuery.Register(GSQ_TIME_IS_NIGHT, TIME_IS_NIGHT);
        try
        {
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(Game1), nameof(Game1.getStartingToGetDarkTime)),
                postfix: new HarmonyMethod(typeof(DayToNightTiming), nameof(Game1_getStartingToGetDarkTime_Postfix))
            );
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(Game1), nameof(Game1.getModeratelyDarkTime)),
                postfix: new HarmonyMethod(typeof(DayToNightTiming), nameof(Game1_getModeratelyDarkTime_Postfix))
            );
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(Game1), nameof(Game1.getTrulyDarkTime)),
                postfix: new HarmonyMethod(typeof(DayToNightTiming), nameof(Game1_getTrulyDarkTime_Postfix))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch DayToNightTiming:\n{err}", LogLevel.Error);
        }
    }

    private static bool TIME_IS_DAY(string[] query, GameStateQueryContext context) =>
        !Game1.isStartingToGetDarkOut(context.Location);

    private static bool TIME_IS_SUNSET(string[] query, GameStateQueryContext context) =>
        Game1.isStartingToGetDarkOut(context.Location) && !Game1.isDarkOut(context.Location);

    private static bool TIME_IS_NIGHT(string[] query, GameStateQueryContext context) =>
        Game1.isDarkOut(context.Location);

    private static void OnTimeChanged(object? sender, TimeChangedEventArgs e)
    {
        if (e.NewTime == Game1.getStartingToGetDarkTime(Game1.currentLocation))
            TriggerActionManager.Raise(MapProp_NightTimeStarting);
        else if (e.NewTime == Game1.getModeratelyDarkTime(Game1.currentLocation))
            TriggerActionManager.Raise(MapProp_NightTimeModerate);
        else if (e.NewTime == Game1.getTrulyDarkTime(Game1.currentLocation))
            TriggerActionManager.Raise(MapProp_NightTimeTruly);
    }

    private static void Game1_getStartingToGetDarkTime_Postfix(GameLocation location, ref int __result)
    {
        if (
            CommonPatch.TryGetCustomFieldsOrMapPropertyAsInt(
                location,
                MapProp_NightTimeStarting,
                out int nightTimeStarting
            )
        )
        {
            __result = nightTimeStarting;
        }
    }

    private static void Game1_getModeratelyDarkTime_Postfix(GameLocation location, ref int __result)
    {
        if (
            CommonPatch.TryGetCustomFieldsOrMapPropertyAsInt(
                location,
                MapProp_NightTimeModerate,
                out int nightTimeModerate
            )
        )
        {
            if (nightTimeModerate > Game1.getStartingToGetDarkTime(location))
                __result = nightTimeModerate;
            else
                ModEntry.Log($"Invalid {MapProp_NightTimeModerate} value {nightTimeModerate:04}", LogLevel.Warn);
        }
    }

    private static void Game1_getTrulyDarkTime_Postfix(GameLocation location, ref int __result)
    {
        if (CommonPatch.TryGetCustomFieldsOrMapPropertyAsInt(location, MapProp_NightTimeTruly, out int nightTimeTruly))
        {
            if (nightTimeTruly > Game1.getModeratelyDarkTime(location))
                __result = nightTimeTruly;
            else
                ModEntry.Log($"Invalid {MapProp_NightTimeTruly} value {nightTimeTruly:04}", LogLevel.Warn);
        }
    }
}

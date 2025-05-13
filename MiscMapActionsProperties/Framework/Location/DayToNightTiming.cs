using HarmonyLib;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewValley;

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

    internal static void Register()
    {
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

using HarmonyLib;
using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Locations;

namespace MiscMapActionsProperties.Framework.Location;

/// <summary>
/// Add new map property mushymato.MMAP_CribPosition x y
/// Overrides the default crib bounds's top left position (width and height still 3x4)
/// Only works in farmhouse/cabins
/// </summary>
internal static class CribPosition
{
    internal const string MapProp_CribPosition = $"{ModEntry.ModId}_CribPosition";
    internal static readonly Vector2 ChildOffset = new Vector2(1f, 2f) * Game1.tileSize;

    internal static void Register()
    {
        try
        {
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(FarmHouse), nameof(FarmHouse.GetCribBounds)),
                postfix: new HarmonyMethod(typeof(CribPosition), nameof(FarmHouse_GetCribPosition_Postfix))
            );
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(Child), nameof(Child.dayUpdate)),
                postfix: new HarmonyMethod(typeof(CribPosition), nameof(Child_dayUpdate_Postfix))
            );
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(Child), nameof(Child.resetForPlayerEntry)),
                postfix: new HarmonyMethod(typeof(CribPosition), nameof(Child_resetForPlayerEntry_Postfix))
            );
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(Child), nameof(Child.isInCrib)),
                postfix: new HarmonyMethod(typeof(CribPosition), nameof(Child_isInCrib_Postfix))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch CribPosition:\n{err}", LogLevel.Error);
        }
    }

    private static bool TryGetCribPosition(GameLocation location, out Vector2 cribPos)
    {
        return CommonPatch.TryGetLocationalPropertyVector2(location, MapProp_CribPosition, out cribPos);
    }

    private static void Child_isInCrib_Postfix(Child __instance, ref bool __result)
    {
        if (TryGetCribPosition(__instance.currentLocation, out Vector2 cribPos))
        {
            Point tilePoint = __instance.TilePoint;
            if (tilePoint.X >= cribPos.X && tilePoint.X <= cribPos.X + 2 && tilePoint.Y >= cribPos.Y)
            {
                __result = tilePoint.Y <= cribPos.Y + 1;
            }
        }
    }

    private static void Child_dayUpdate_Postfix(Child __instance)
    {
        if (__instance.Age == 2 && __instance.Position == new Vector2(31f, 14f) * 64f + new Vector2(0f, -24f))
        {
            if (TryGetCribPosition(__instance.currentLocation, out Vector2 cribPos))
            {
                __instance.Position = cribPos * Game1.tileSize + new Vector2(Game1.tileSize, Game1.tileSize * 2 - 24f);
            }
        }
    }

    private static void Child_resetForPlayerEntry_Postfix(Child __instance, GameLocation l)
    {
        if (__instance.Age > 2)
            return;
        if (TryGetCribPosition(l, out Vector2 cribPos))
        {
            switch (__instance.Age)
            {
                case 0:
                    __instance.Position = cribPos * Game1.tileSize + ChildOffset + new Vector2(0, -24f);
                    break;
                case 1:
                    __instance.Position = cribPos * Game1.tileSize + ChildOffset + new Vector2(0, -12f);
                    break;
                case 2:
                    if (Game1.timeOfDay >= 1800)
                    {
                        __instance.Position = cribPos * Game1.tileSize + ChildOffset + new Vector2(0, -24f);
                    }
                    break;
            }
        }
    }

    private static void FarmHouse_GetCribPosition_Postfix(FarmHouse __instance, ref Rectangle? __result)
    {
        if (__result != null && TryGetCribPosition(__instance, out Vector2 cribPos))
        {
            __result = new Rectangle((int)cribPos.X, (int)cribPos.Y, __result.Value.Width, __result.Value.Height);
        }
    }
}

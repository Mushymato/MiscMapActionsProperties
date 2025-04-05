using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace MiscMapActionsProperties.Framework.Wheels;

internal static class CommonPatch
{
    public record ResetLocalStateArgs(GameLocation Location);

    public static event EventHandler<ResetLocalStateArgs>? GameLocation_resetLocalState;

    public record UpdateWhenCurrentLocationArgs(GameLocation Location, GameTime Time);

    public static event EventHandler<UpdateWhenCurrentLocationArgs>? GameLocation_UpdateWhenCurrentLocation;

    public record DrawAboveAlwaysFrontLayerArgs(GameLocation Location, SpriteBatch B);

    public static event EventHandler<DrawAboveAlwaysFrontLayerArgs>? GameLocation_DrawAboveAlwaysFrontLayer;

    internal static void Register()
    {
        try
        {
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(GameLocation), "resetLocalState"),
                postfix: new HarmonyMethod(typeof(CommonPatch), nameof(GameLocation_resetLocalState_Postfix))
            );
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.UpdateWhenCurrentLocation)),
                postfix: new HarmonyMethod(typeof(CommonPatch), nameof(GameLocation_UpdateWhenCurrentLocation_Postfix))
            );
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.drawAboveAlwaysFrontLayer)),
                postfix: new HarmonyMethod(typeof(CommonPatch), nameof(GameLocation_drawAboveAlwaysFrontLayer_Postfix))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch CommonPatch:\n{err}", LogLevel.Error);
        }
    }

    private static void GameLocation_resetLocalState_Postfix(GameLocation __instance)
    {
        GameLocation_resetLocalState?.Invoke(null, new(__instance));
    }

    private static void GameLocation_UpdateWhenCurrentLocation_Postfix(GameLocation __instance, GameTime time)
    {
        GameLocation_UpdateWhenCurrentLocation?.Invoke(null, new(__instance, time));
    }

    private static void GameLocation_drawAboveAlwaysFrontLayer_Postfix(GameLocation __instance, SpriteBatch b)
    {
        GameLocation_DrawAboveAlwaysFrontLayer?.Invoke(null, new(__instance, b));
    }

    internal static bool HasCustomFieldsOrMapProperty(GameLocation location, string propKey)
    {
        return TryGetCustomFieldsOrMapProperty(location, propKey, out _);
    }

    internal static bool TryGetCustomFieldsOrMapProperty(
        GameLocation location,
        string propKey,
        [NotNullWhen(true)] out string? prop
    )
    {
        prop = null;
        if (
            (location.GetData()?.CustomFields?.TryGetValue(propKey, out prop) ?? false)
            || (location.map != null && location.TryGetMapProperty(propKey, out prop))
            || false
        )
            return !string.IsNullOrEmpty(prop);
        return false;
    }

    internal static bool TryGetCustomFieldsOrMapPropertyAsInt(
        GameLocation location,
        string propKey,
        [NotNullWhen(true)] out int prop
    )
    {
        prop = 0;
        if (TryGetCustomFieldsOrMapProperty(location, propKey, out string? propValue))
        {
            if (int.TryParse(propValue, out prop))
            {
                return true;
            }
        }
        return false;
    }

    internal static bool TryGetCustomFieldsOrMapPropertyAsVector2(
        GameLocation location,
        string propKey,
        [NotNullWhen(true)] out Vector2 prop
    )
    {
        prop = Vector2.Zero;
        if (TryGetCustomFieldsOrMapProperty(location, propKey, out string? propValue))
        {
            string[] args = ArgUtility.SplitBySpace(propValue);
            if (
                ArgUtility.TryGetFloat(args, 0, out float xVal, out string error, "float X")
                && ArgUtility.TryGetFloat(args, 1, out float yVal, out error, "float Y")
            )
            {
                prop = new Vector2(xVal, yVal);
                return true;
            }
            ModEntry.Log(error, LogLevel.Warn);
        }
        return false;
    }

    internal static void RegisterTileAndTouch(
        string actionName,
        Func<GameLocation, string[], Farmer, Point, bool> callbackAction
    )
    {
        GameLocation.RegisterTileAction(
            actionName,
            (location, args, farmer, tile) => callbackAction(location, args, farmer, tile)
        );
        GameLocation.RegisterTouchAction(
            actionName,
            (location, args, farmer, tile) => callbackAction(location, args, farmer, tile.ToPoint())
        );
    }
}

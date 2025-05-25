using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Triggers;

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
    internal const string Action_If = $"{ModEntry.ModId}_If";

    internal static void Register()
    {
        GameLocation.RegisterTileAction(Action_If, TileActionIf);
        GameLocation.RegisterTouchAction(Action_If, TouchActionIf);
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

    private static bool TryGetIfElse(
        string[] args,
        [NotNullWhen(true)] out string[]? gsq,
        [NotNullWhen(true)] out string[]? caseif,
        out string[]? caseelse
    )
    {
        gsq = null;
        caseif = null;
        caseelse = null;
        ReadOnlySpan<string> argsSpan = args.AsSpan();
        int idx = argsSpan.IndexOf("##");
        if (idx == -1)
        {
            ModEntry.Log(
                "invalid format: expected a string in the form 'If <game state query> ## <do if true>' or 'If <game state query> ## <do if true> ## <do if false>'",
                LogLevel.Error
            );
            return false;
        }
        gsq = argsSpan[1..idx].ToArray();
        argsSpan = argsSpan[(idx + 1)..];

        idx = argsSpan.IndexOf("##");
        if (idx == -1)
        {
            caseif = argsSpan[0..].ToArray();
        }
        else
        {
            caseif = argsSpan[0..idx].ToArray();
            if (argsSpan.Length - idx - 1 > 0)
                caseelse = argsSpan[(idx + 1)..].ToArray();
        }

        return true;
    }

    private static bool TileActionIf(GameLocation location, string[] args, Farmer farmer, Point point)
    {
        if (!TryGetIfElse(args, out string[]? gsq, out string[]? caseif, out string[]? caseelse))
            return false;

        if (GameStateQuery.CheckConditions(ArgUtility.UnsplitQuoteAware(gsq, ' ')))
        {
            if (!location.performAction(caseif, farmer, new(point.X, point.Y)))
            {
                ModEntry.Log($"Failed to perform if-true: {string.Join(' ', caseif)}");
                return false;
            }
        }
        else if (caseelse != null)
        {
            if (!location.performAction(caseelse, farmer, new(point.X, point.Y)))
            {
                ModEntry.Log($"Failed to perform if-false: {string.Join(' ', caseelse)}");
                return false;
            }
        }

        return true;
    }

    private static void TouchActionIf(GameLocation location, string[] args, Farmer farmer, Vector2 vector)
    {
        if (!TryGetIfElse(args, out string[]? gsq, out string[]? caseif, out string[]? caseelse))
            return;

        if (GameStateQuery.CheckConditions(ArgUtility.UnsplitQuoteAware(gsq, ' ')))
        {
            location.performTouchAction(caseif, vector);
        }
        else if (caseelse != null)
        {
            location.performTouchAction(caseelse, vector);
        }

        return;
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

using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace MiscMapActionsProperties.Framework.Tile;

/// <summary>
/// Add new tile/touch action mushymato.MMAP_If GSQ ## if-case ## else-case
/// Works just like Trigger action If
/// </summary>
internal static class ActionCond
{
    internal const string Action_If = $"{ModEntry.ModId}_If";

    internal static void Register()
    {
        GameLocation.RegisterTileAction(Action_If, TileActionIf);
        GameLocation.RegisterTouchAction(Action_If, TouchActionIf);
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
}

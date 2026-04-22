using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Triggers;

namespace MiscMapActionsProperties.Framework.Tile;

/// <summary>
/// When explosion happens at a particular tile, try to activate mushymato.MMAP_ExplodeAction on back layer.
/// You can use any action that is available as a TouchAction there.
/// This does not work until you do tile or trigger action mushymato.MMAP_EnableExplodeAction
/// Once any ExplodeAction activates or if player leaves the map, further explode actions are disabled until the enable action is applied again.
/// </summary>
internal static class ExplodeTileAction
{
    internal const string Action_EnableExplodeAction = $"{ModEntry.ModId}_EnableExplodeAction";
    internal const string ExplodeAction = $"{ModEntry.ModId}_ExplodeAction";

    private static readonly PerScreenCache<string?> ExplodeActionEnabled = new(new());

    internal static void Register()
    {
        CommonPatch.RegisterTileAndTouch(Action_EnableExplodeAction, TileEnableExplodeAction);
        TriggerActionManager.RegisterAction(Action_EnableExplodeAction, TriggerEnableExplodeAction);
        ModEntry.help.Events.GameLoop.DayStarted += static (sender, e) => ExplodeActionEnabled.Value = null;
        ModEntry.help.Events.Player.Warped += static (sender, e) => ExplodeActionEnabled.Value = null;
        try
        {
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.explosionAt)),
                postfix: new HarmonyMethod(typeof(ExplodeTileAction), nameof(GameLocation_explosionAt_Postfix))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch ExplodeTileAction:\n{err}", LogLevel.Error);
        }
    }

    private static bool TileEnableExplodeAction(GameLocation location, string[] args, Farmer farmer, Point point)
    {
        if (!TryEnableExplodeAction(args, out string? error))
        {
            ModEntry.Log(error, LogLevel.Error);
            return false;
        }
        return true;
    }

    private static bool TryEnableExplodeAction(string[] args, [NotNullWhen(false)] out string? error)
    {
        if (!ArgUtility.TryGet(args, 1, out string layer, out error, name: "string layer"))
        {
            return false;
        }
        if (layer != "Back" && layer != "Buildings")
        {
            error = "Layer must be 'Back' or 'Buildings'";
            return false;
        }
        ExplodeActionEnabled.Value = layer;
        ModEntry.Log($"ExplodeActionEnable: {layer}");
        return true;
    }

    private static bool TriggerEnableExplodeAction(string[] args, TriggerActionContext context, out string? error)
    {
        return TryEnableExplodeAction(args, out error);
    }

    private static void GameLocation_explosionAt_Postfix(GameLocation __instance, float x, float y)
    {
        if (
            ExplodeActionEnabled.Value is string layer
            && __instance.doesTileHaveProperty((int)x, (int)y, ExplodeAction, layer) is string actionText
        )
        {
            ExplodeActionEnabled.Value = null;
            if (layer == "Back")
            {
                ModEntry.Log("ExplodeAction: Back (TouchAction)");
                __instance.performTouchAction(actionText, new(x, y));
            }
            else if (layer == "Buildings")
            {
                ModEntry.Log("ExplodeAction: Buildings (Action)");
                __instance.performAction(actionText, Game1.player, new xTile.Dimensions.Location((int)x, (int)y));
            }
        }
    }
}

using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewValley;

namespace MiscMapActionsProperties.Framework.Tile;

/// <summary>
/// Add new tile action and touch action mushymato.MMAP_HoleWarp. Warp the player in a style similar to skull cavern holes.
/// Usage:
/// - mushymato.MMAP_HoleWarp <location> <X> <Y> [mailflag]
/// Arguments are identical to vanilla warp tile actions.
/// When used with Action, the warp requires interaction, while TouchAction just sends the player directly down the hole.
/// </summary>
internal static class HoleWrp
{
    internal const string TileAction_HoleWrp = $"{ModEntry.ModId}_HoleWrp";

    internal static void Register() => CommonPatch.RegisterTileAndTouch(TileAction_HoleWrp, DoHoleWarp);

    private static bool DoHoleWarp(GameLocation location, string[] args, Farmer farmer, Point source)
    {
        if (
            !ArgUtility.TryGet(
                args,
                1,
                out var locationToWarp,
                out string error,
                allowBlank: true,
                "string locationToWarp"
            )
            || !ArgUtility.TryGetPoint(args, 2, out var tile, out error, "Point tile")
            || !ArgUtility.TryGetOptional(
                args,
                4,
                out var mailflag,
                out error,
                null,
                allowBlank: true,
                "string mailRequired"
            )
        )
        {
            ModEntry.Log(error, LogLevel.Error);
            return false;
        }
        else if (mailflag != null && Game1.player.mailReceived.Contains(mailflag))
        {
            return false;
        }

        DelayedAction.playSoundAfterDelay("fallDown", 200, location);
        DelayedAction.playSoundAfterDelay("clubSmash", 900);
        Game1.globalFadeToBlack(
            () =>
            {
                Game1.messagePause = true;
                Game1.warpFarmer(locationToWarp, tile.X, tile.Y, flip: false);
                Game1.messagePause = false;
                Game1.fadeToBlackAlpha = 1f;
                // Game1.displayFarmer = true;
                Game1.player.CanMove = true;
                Game1.freezeControls = false;
                Game1.player.faceDirection(2);
                Game1.player.showFrame(5);
            },
            0.1f
        );
        Game1.freezeControls = true;
        // Game1.displayFarmer = false;
        Game1.player.CanMove = false;
        Game1.player.jump();
        // Game1.player.temporarilyInvincible = true;
        // Game1.player.temporaryInvincibilityTimer = 0;
        // Game1.player.flashDuringThisTemporaryInvincibility = false;
        // Game1.player.currentTemporaryInvincibilityDuration = 700;
        return true;
    }
}

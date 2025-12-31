using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Triggers;

namespace MiscMapActionsProperties.Framework.Tile;

/// <summary>
/// Add mushymato.MMAP_LaunchFireworks action
/// </summary>
internal static class LaunchFireworks
{
    internal const string TileAction_LaunchFireworks = $"{ModEntry.ModId}_LaunchFireworks";

    internal static void Register()
    {
        CommonPatch.RegisterTileAndTouch(TileAction_LaunchFireworks, TileLaunchFireworks);
        TriggerActionManager.RegisterAction(TileAction_LaunchFireworks, TriggerLaunchFireWorks);
    }

    private static bool TriggerLaunchFireWorks(string[] args, TriggerActionContext context, out string error)
    {
        if (!ArgUtility.TryGetPoint(args, 1, out Point value, out error, "Point pnt"))
        {
            return false;
        }
        return DoLaunchFireworks(Game1.currentLocation, args, Game1.player, value, 3, out error);
    }

    private static bool TileLaunchFireworks(GameLocation location, string[] args, Farmer farmer, Point point)
    {
        if (DoLaunchFireworks(location, args, farmer, point, 1, out string error))
            return true;
        ModEntry.Log(error, LogLevel.Error);
        return false;
    }

    private static bool DoLaunchFireworks(
        GameLocation location,
        string[] args,
        Farmer farmer,
        Point point,
        int firstIdx,
        out string error
    )
    {
        if (
            !ArgUtility.TryGetInt(args, firstIdx, out int fireworksType, out error, "int fireworksType")
            || !ArgUtility.TryGetOptional(
                args,
                firstIdx + 1,
                out string launchedTexture,
                out error,
                "T",
                false,
                "string launchedTexture"
            )
        )
        {
            return false;
        }
        if (fireworksType < 0 || fireworksType >= 3)
        {
            fireworksType = Game1.random.Next(3);
        }
        string tasTexture = "LooseSprites\\Cursors_1_6";
        Rectangle sourceRect;
        string[] txParts = launchedTexture.Split(':');
        launchedTexture = txParts[0];
        if (launchedTexture == "F")
        {
            sourceRect = Rectangle.Empty;
        }
        else if (launchedTexture != "T" && Game1.content.DoesAssetExist<Texture2D>(launchedTexture))
        {
            tasTexture = launchedTexture;
            if (ArgUtility.TryGetRectangle(txParts, 1, out Rectangle rect, out _))
            {
                sourceRect = rect;
            }
            else
            {
                sourceRect = new(0, 0, 16, 16);
            }
        }
        else
        {
            sourceRect = new(256 + fireworksType * 16, 397, 16, 16);
        }
        Vector2 pos = point.ToVector2() * 64f;
        int extraInfoForEndBehavior = Game1.random.Next();
        Game1.Multiplayer.broadcastSprites(
            location,
            new TemporaryAnimatedSprite(
                tasTexture,
                sourceRect,
                800f,
                1,
                0,
                pos,
                flicker: false,
                flipped: false,
                -1f,
                0f,
                Color.White,
                4f,
                0f,
                0f,
                0f
            )
            {
                fireworkType = fireworksType,
                acceleration = new Vector2(0f, -0.36f + (float)Game1.random.Next(10) / 100f),
                drawAboveAlwaysFront = true,
                delayBeforeAnimationStart = 100,
                startSound = "firework",
                shakeIntensity = 0.5f,
                shakeIntensityChange = 0.002f,
                extraInfoForEndBehavior = extraInfoForEndBehavior,
                endFunction = location.removeTemporarySpritesWithID,
                id = Game1.random.Next(20, 31),
                Parent = location,
                owner = farmer,
            }
        );
        return true;
    }
}

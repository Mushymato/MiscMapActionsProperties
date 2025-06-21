using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace MiscMapActionsProperties.Framework.Tile;

/// <summary>
///  mushymato.MMAP_PoolEntry
/// Combined ChangeIntoSwimsuit/ChangeOutOfSwimsuit/PoolEntrance action
/// </summary>
internal static class PoolEntry
{
    internal const string TileAction_PoolEntry = $"{ModEntry.ModId}_PoolEntry";

    internal static void Register()
    {
        GameLocation.RegisterTouchAction(TileAction_PoolEntry, DoPoolEntry);
    }

    private static void ConformXYPos(Farmer farmer, int direction, Vector2 tile, float added = 0f)
    {
        switch (direction)
        {
            case 0:
                farmer.position.X = tile.X * 64f;
                farmer.position.Y -= added;
                break;
            case 1:
                farmer.position.Y = tile.Y * 64f;
                farmer.position.X += added;
                break;
            case 2:
                farmer.position.X = tile.X * 64f;
                farmer.position.Y += added;
                break;
            case 3:
                farmer.position.Y = tile.Y * 64f;
                farmer.position.X -= added;
                break;
        }
    }

    private static void SetVelocity(Farmer farmer, int direction, float velocity)
    {
        switch (direction)
        {
            case 0:
                farmer.yVelocity = velocity;
                break;
            case 1:
                farmer.xVelocity = velocity;
                break;
            case 2:
                farmer.yVelocity = -velocity;
                break;
            case 3:
                farmer.xVelocity = -velocity;
                break;
        }
    }

    private static int InvertDirection(int direction)
    {
        return direction switch
        {
            0 => 2,
            1 => 3,
            2 => 0,
            3 => 1,
            _ => throw new NotImplementedException(),
        };
    }

    private static void DoPoolEntry(GameLocation location, string[] args, Farmer farmer, Vector2 tile)
    {
        if (
            !ArgUtility.TryGetOptionalInt(
                args,
                1,
                out int direction,
                out string error,
                defaultValue: -1,
                name: "int direction"
            )
            || !ArgUtility.TryGetOptionalFloat(
                args,
                2,
                out float velocity,
                out error,
                defaultValue: 8f,
                name: "float velocity"
            )
            || !ArgUtility.TryGetOptional(
                args,
                2,
                out string soundCue,
                out error,
                defaultValue: "pullItemFromWater",
                allowBlank: false,
                name: "string soundCue"
            )
        )
        {
            ModEntry.Log(error, LogLevel.Error);
            return;
        }

        if (farmer.bathingClothes.Value)
        {
            if (direction == -1)
                direction = farmer.FacingDirection;
            else if ((direction = InvertDirection(direction)) != farmer.FacingDirection)
                return;
            Game1.player.changeOutOfSwimSuit();
            Game1.player.jump();
            Game1.player.swimTimer = 800;
            ConformXYPos(farmer, direction, tile, 16);
            location.playSound(soundCue);
            SetVelocity(farmer, direction, velocity);
            Game1.player.swimming.Value = false;
        }
        else
        {
            if (direction == -1)
                direction = farmer.FacingDirection;
            else if (direction != farmer.FacingDirection)
                return;
            Game1.player.changeIntoSwimsuit();
            Game1.player.swimTimer = 800;
            Game1.player.swimming.Value = true;
            ConformXYPos(farmer, direction, tile, 16);
            SetVelocity(farmer, direction, velocity);
            location.playSound(soundCue);
            Game1.Multiplayer.broadcastSprites(
                location,
                new TemporaryAnimatedSprite(
                    27,
                    100f,
                    4,
                    0,
                    new Vector2(Game1.player.Position.X, Game1.player.StandingPixel.Y - 40),
                    flicker: false,
                    flipped: false
                )
                {
                    layerDepth = 1f,
                    motion = new Vector2(0f, 2f),
                }
            );
        }
        Game1.player.noMovementPause = 500;
    }
}

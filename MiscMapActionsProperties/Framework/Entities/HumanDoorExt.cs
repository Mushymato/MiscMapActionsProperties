using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Tile;
using MiscMapActionsProperties.Framework.Wheels;
using StardewValley;
using StardewValley.Buildings;

namespace MiscMapActionsProperties.Framework.Entities;

/// <summary>
/// Adds a number of warps for use in building and instance locations
/// mushymato.MMAP_WarpBuilding - warp building
///
/// </summary>
internal static class HumanDoorExt
{
    internal const string Action_Warp = $"{ModEntry.ModId}_WarpBuilding";
    internal const string Action_MagicWarp = $"{ModEntry.ModId}_MagicWarpBuilding";
    internal const string Action_HoleWarp = $"{HoleWarp.TileAction_HoleWarp}Building";

    internal const string Action_WarpOut = $"{ModEntry.ModId}_WarpBuildingOut";
    internal const string Action_MagicWarpOut = $"{ModEntry.ModId}_MagicWarpBuildingOut";
    internal const string Action_HoleWarpOut = $"{HoleWarp.TileAction_HoleWarp}BuildingOut";
    internal const string Action_WarpHere = $"{ModEntry.ModId}_WarpHere";

    internal static void Register()
    {
        CommonPatch.RegisterTileAndTouch(Action_Warp, DoWarpBuilding);
        CommonPatch.RegisterTileAndTouch(Action_MagicWarp, DoMagicWarpBuilding);
        CommonPatch.RegisterTileAndTouch(Action_HoleWarp, DoHoleWarpBuilding);

        CommonPatch.RegisterTileAndTouch(Action_WarpOut, DoWarpBuildingOut);
        CommonPatch.RegisterTileAndTouch(Action_MagicWarpOut, DoMagicWarpBuildingOut);
        CommonPatch.RegisterTileAndTouch(Action_HoleWarpOut, DoHoleWarpBuildingOut);

        CommonPatch.RegisterTileAndTouch(Action_WarpHere, DoWarpHere);
    }

    private static bool DoWarpBuilding(GameLocation location, string[] args, Farmer who, Point point)
    {
        if (TryGetWarpArgs("Warp", location, args, who, point, out string[]? warpArgs))
        {
            location.performTouchAction(warpArgs, who.getStandingPosition());
            return true;
        }
        return false;
    }

    private static bool DoMagicWarpBuilding(GameLocation location, string[] args, Farmer who, Point point)
    {
        if (TryGetWarpArgs("MagicWarp", location, args, who, point, out string[]? warpArgs))
        {
            location.performTouchAction(warpArgs, who.getStandingPosition());
            return true;
        }
        return false;
    }

    private static bool DoHoleWarpBuilding(GameLocation location, string[] args, Farmer who, Point point)
    {
        if (TryGetWarpArgs(HoleWarp.TileAction_HoleWarp, location, args, who, point, out string[]? warpArgs))
        {
            location.performTouchAction(warpArgs, who.getStandingPosition());
            return true;
        }
        return false;
    }

    private static bool DoWarpBuildingOut(GameLocation location, string[] args, Farmer who, Point point)
    {
        if (TryGetWarpOutArgs("Warp", location, args, out string[]? warpOutArgs))
        {
            location.performTouchAction(warpOutArgs, who.getStandingPosition());
            return true;
        }
        return false;
    }

    private static bool DoMagicWarpBuildingOut(GameLocation location, string[] args, Farmer who, Point point)
    {
        if (TryGetWarpOutArgs("MagicWarp", location, args, out string[]? warpOutArgs))
        {
            location.performTouchAction(warpOutArgs, who.getStandingPosition());
            return true;
        }
        return false;
    }

    private static bool DoHoleWarpBuildingOut(GameLocation location, string[] args, Farmer who, Point point)
    {
        if (TryGetWarpOutArgs(HoleWarp.TileAction_HoleWarp, location, args, out string[]? warpOutArgs))
        {
            location.performTouchAction(warpOutArgs, who.getStandingPosition());
            return true;
        }
        return false;
    }

    private static bool DoWarpHere(GameLocation location, string[] args, Farmer farmer, Point point)
    {
        if (
            !ArgUtility.TryGetPoint(args, 1, out Point toPoint, out string error, name: "string toPoint")
            || !ArgUtility.TryGetOptionalInt(
                args,
                3,
                out int direction,
                out error,
                defaultValue: -1,
                name: "int direction"
            )
            || !ArgUtility.TryGetOptionalBool(
                args,
                4,
                out bool fadeToBlack,
                out error,
                defaultValue: true,
                name: "string fadeToBlack"
            )
        )
        {
            ModEntry.LogOnce(error);
            return false;
        }
        if (fadeToBlack)
        {
            if (direction == -1)
                direction = farmer.FacingDirection;
            Game1.warpFarmer(location.NameOrUniqueName, toPoint.X, toPoint.Y, direction);
        }
        else
        {
            farmer.Position = new Vector2(toPoint.X * 64, toPoint.Y * 64 - (farmer.Sprite.getHeight() - 32) + 16);
            if (direction != -1)
                farmer.FacingDirection = direction;
        }
        return true;
    }

    private static bool TryGetWarpArgs(
        string touchAction,
        GameLocation location,
        string[] args,
        Farmer who,
        Point point,
        [NotNullWhen(true)] out string[]? warpArgs
    )
    {
        warpArgs = null;
        if (location.getBuildingAt(new Vector2(point.X, point.Y)) is not Building building)
        {
            return false;
        }
        if (building.daysOfConstructionLeft.Value > 0 || building.GetIndoors() is not GameLocation buildingIndoors)
        {
            return false;
        }
        if (!who.IsLocalPlayer)
        {
            return false;
        }
        if (who.mount != null)
        {
            Game1.showRedMessage(Game1.content.LoadString("Strings\\Buildings:DismountBeforeEntering"));
            return false;
        }
        if (who.team.demolishLock.IsLocked())
        {
            Game1.showRedMessage(Game1.content.LoadString("Strings\\Buildings:CantEnter"));
            return false;
        }
        if (!building.OnUseHumanDoor(who))
        {
            return false;
        }

        if (
            !ArgUtility.TryGetInt(args, 1, out int warpToX, out _, "int warpToX")
            || !ArgUtility.TryGetInt(args, 2, out int warpToY, out _, "int warpToY")
        )
        {
            if (buildingIndoors.warps.Count < 1)
            {
                ModEntry.Log($"Building has no indoor warps out.");
                return false;
            }
            warpToX = buildingIndoors.warps[0].X;
            warpToY = buildingIndoors.warps[0].Y - 1;
        }
        warpArgs = [touchAction, buildingIndoors.NameOrUniqueName, warpToX.ToString(), warpToY.ToString()];
        return true;
    }

    private static bool TryGetWarpOutArgs(
        string touchAction,
        GameLocation location,
        string[] args,
        [NotNullWhen(true)] out string[]? warpOutArgs
    )
    {
        warpOutArgs = null;
        if (location.ParentBuilding is not Building building)
        {
            ModEntry.LogOnce($"Cannot use WarpBuildingOut outside of building interiors.");
            return false;
        }

        if (
            !ArgUtility.TryGetInt(args, 1, out int warpToX, out _, "int warpToX")
            || !ArgUtility.TryGetInt(args, 2, out int warpToY, out _, "int warpToY")
        )
        {
            warpToX = building.humanDoor.X;
            warpToY = building.humanDoor.Y + 1;
        }
        warpToX += building.tileX.Value;
        warpToY += building.tileY.Value;

        warpOutArgs = [touchAction, building.parentLocationName.Value, warpToX.ToString(), warpToY.ToString()];
        return true;
    }
}

using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Tile;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Delegates;
using StardewValley.Triggers;

namespace MiscMapActionsProperties.Framework.Entities;

/// <summary>
/// Adds a number of Wrps for use in building and instanced locations
/// </summary>
internal static class HumanDoorExt
{
    internal const string Action_Wrp = $"{ModEntry.ModId}_WrpBuilding";
    internal const string Action_MagicWrp = $"{ModEntry.ModId}_MagicWrpBuilding";
    internal const string Action_HoleWrp = $"{HoleWrp.TileAction_HoleWrp}Building";

    internal const string Action_WrpOut = $"{ModEntry.ModId}_WrpBuildingOut";
    internal const string Action_MagicWrpOut = $"{ModEntry.ModId}_MagicWrpBuildingOut";
    internal const string Action_HoleWrpOut = $"{HoleWrp.TileAction_HoleWrp}BuildingOut";
    internal const string Action_WrpHere = $"{ModEntry.ModId}_WrpHere";

    internal static void Register()
    {
        CommonPatch.RegisterTileAndTouch(Action_Wrp, DoWrpBuilding);
        CommonPatch.RegisterTileAndTouch(Action_MagicWrp, DoMagicWrpBuilding);
        CommonPatch.RegisterTileAndTouch(Action_HoleWrp, DoHoleWrpBuilding);

        CommonPatch.RegisterTileAndTouch(Action_WrpOut, DoWrpBuildingOut);
        CommonPatch.RegisterTileAndTouch(Action_MagicWrpOut, DoMagicWrpBuildingOut);
        CommonPatch.RegisterTileAndTouch(Action_HoleWrpOut, DoHoleWrpBuildingOut);

        CommonPatch.RegisterTileAndTouch(Action_WrpHere, TileWrpHere);
        TriggerActionManager.RegisterAction(Action_WrpHere, TriggerWrpHere);
    }

    private static bool DoWrpBuilding(GameLocation location, string[] args, Farmer who, Point point)
    {
        if (TryGetWrpArgs("Warp", location, args, who, point, out string[]? WrpArgs))
        {
            location.performTouchAction(WrpArgs, who.getStandingPosition());
            return true;
        }
        return false;
    }

    private static bool DoMagicWrpBuilding(GameLocation location, string[] args, Farmer who, Point point)
    {
        if (TryGetWrpArgs("MagicWarp", location, args, who, point, out string[]? WrpArgs))
        {
            location.performTouchAction(WrpArgs, who.getStandingPosition());
            return true;
        }
        return false;
    }

    private static bool DoHoleWrpBuilding(GameLocation location, string[] args, Farmer who, Point point)
    {
        if (TryGetWrpArgs(HoleWrp.TileAction_HoleWrp, location, args, who, point, out string[]? WrpArgs))
        {
            location.performTouchAction(WrpArgs, who.getStandingPosition());
            return true;
        }
        return false;
    }

    private static bool DoWrpBuildingOut(GameLocation location, string[] args, Farmer who, Point point)
    {
        if (TryGetWrpOutArgs("Warp", location, args, out string[]? WrpOutArgs))
        {
            location.performTouchAction(WrpOutArgs, who.getStandingPosition());
            return true;
        }
        return false;
    }

    private static bool DoMagicWrpBuildingOut(GameLocation location, string[] args, Farmer who, Point point)
    {
        if (TryGetWrpOutArgs("MagicWarp", location, args, out string[]? WrpOutArgs))
        {
            location.performTouchAction(WrpOutArgs, who.getStandingPosition());
            return true;
        }
        return false;
    }

    private static bool DoHoleWrpBuildingOut(GameLocation location, string[] args, Farmer who, Point point)
    {
        if (TryGetWrpOutArgs(HoleWrp.TileAction_HoleWrp, location, args, out string[]? WrpOutArgs))
        {
            location.performTouchAction(WrpOutArgs, who.getStandingPosition());
            return true;
        }
        return false;
    }

    private static bool TriggerWrpHere(string[] args, TriggerActionContext context, out string error)
    {
        if (!DoWrpHere(Game1.currentLocation, args, Game1.player, Game1.player.TilePoint, out error))
        {
            ModEntry.LogOnce(error);
            return false;
        }
        return true;
    }

    private static bool TileWrpHere(GameLocation location, string[] args, Farmer farmer, Point point)
    {
        if (!DoWrpHere(location, args, farmer, point, out string error))
        {
            ModEntry.LogOnce(error);
            return false;
        }
        return true;
    }

    private static bool DoWrpHere(GameLocation location, string[] args, Farmer farmer, Point point, out string error)
    {
        if (
            !ArgUtility.TryGetPoint(args, 1, out Point toPoint, out error, name: "string toPoint")
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
                name: "bool fadeToBlack"
            )
            || !ArgUtility.TryGetOptionalBool(
                args,
                5,
                out bool relative,
                out error,
                defaultValue: true,
                name: "bool relative"
            )
        )
        {
            return false;
        }
        if (relative)
        {
            toPoint.X += point.X;
            toPoint.Y += point.Y;
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

    private static bool TryGetWrpArgs(
        string touchAction,
        GameLocation location,
        string[] args,
        Farmer who,
        Point point,
        [NotNullWhen(true)] out string[]? WrpArgs
    )
    {
        WrpArgs = null;
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
            !ArgUtility.TryGetInt(args, 1, out int WrpToX, out _, "int WrpToX")
            || !ArgUtility.TryGetInt(args, 2, out int WrpToY, out _, "int WrpToY")
        )
        {
            if (buildingIndoors.warps.Count < 1)
            {
                ModEntry.Log($"Building has no indoor warps out.");
                return false;
            }
            WrpToX = buildingIndoors.warps[0].X;
            WrpToY = buildingIndoors.warps[0].Y - 1;
        }
        WrpArgs = [touchAction, buildingIndoors.NameOrUniqueName, WrpToX.ToString(), WrpToY.ToString()];
        return true;
    }

    private static bool TryGetWrpOutArgs(
        string touchAction,
        GameLocation location,
        string[] args,
        [NotNullWhen(true)] out string[]? WrpOutArgs
    )
    {
        WrpOutArgs = null;
        if (location.ParentBuilding is not Building building)
        {
            ModEntry.LogOnce($"Cannot use WrpBuildingOut outside of building interiors.");
            return false;
        }

        if (
            !ArgUtility.TryGetInt(args, 1, out int WrpToX, out _, "int WrpToX")
            || !ArgUtility.TryGetInt(args, 2, out int WrpToY, out _, "int WrpToY")
        )
        {
            WrpToX = building.humanDoor.X;
            WrpToY = building.humanDoor.Y + 1;
        }
        WrpToX += building.tileX.Value;
        WrpToY += building.tileY.Value;

        WrpOutArgs = [touchAction, building.parentLocationName.Value, WrpToX.ToString(), WrpToY.ToString()];
        return true;
    }
}

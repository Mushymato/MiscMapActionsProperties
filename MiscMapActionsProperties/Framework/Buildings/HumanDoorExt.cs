using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewValley;
using StardewValley.Buildings;

namespace MiscMapActionsProperties.Framework.Buildings;

/// <summary>
/// Adds a new tile/touch action mushymato.MMAP_MagicWarpBuilding
/// </summary>
internal static class HumanDoorExt
{
    internal const string Action_Warp = $"{ModEntry.ModId}_WarpBuilding";
    internal const string Action_MagicWarp = $"{ModEntry.ModId}_MagicWarpBuilding";

    internal static void Register()
    {
        CommonPatch.RegisterTileAndTouch(Action_Warp, DoWarpBuilding);
        CommonPatch.RegisterTileAndTouch(Action_MagicWarp, DoMagicWarpBuilding);
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
}

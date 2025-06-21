using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Tile;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;

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

    internal static Lazy<ImmutableHashSet<string>> MMAP_Wrps =
        new(() =>
        {
            HashSet<string> WrpsSet =
            [
                Action_Wrp,
                Action_MagicWrp,
                Action_HoleWrp,
                Action_WrpOut,
                Action_MagicWrpOut,
                Action_HoleWrpOut,
                Action_WrpHere,
                HoleWrp.TileAction_HoleWrp,
            ];
            return WrpsSet.ToImmutableHashSet();
        });

    internal static void Register()
    {
        CommonPatch.RegisterTileAndTouch(Action_Wrp, DoWrpBuilding);
        CommonPatch.RegisterTileAndTouch(Action_MagicWrp, DoMagicWrpBuilding);
        CommonPatch.RegisterTileAndTouch(Action_HoleWrp, DoHoleWrpBuilding);

        CommonPatch.RegisterTileAndTouch(Action_WrpOut, DoWrpBuildingOut);
        CommonPatch.RegisterTileAndTouch(Action_MagicWrpOut, DoMagicWrpBuildingOut);
        CommonPatch.RegisterTileAndTouch(Action_HoleWrpOut, DoHoleWrpBuildingOut);

        CommonPatch.RegisterTileAndTouch(Action_WrpHere, DoWrpHere);
    }

    private static bool IsMMAPWrp(string WrpStr) => MMAP_Wrps.Value.Contains(WrpStr);

    private static IEnumerable<CodeInstruction> GameLocation_updateDoors_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        try
        {
            // IL_016d: ldloc.s 8
            // IL_016f: ldstr "WrpWomensLocker"
            // IL_0174: call bool [System.Runtime]System.String::op_Equality(string, string)
            // IL_0179: brtrue.s IL_01e3
            // IL_017b: br IL_0237

            CodeMatcher matcher = new(instructions, generator);
            matcher.MatchStartForward(
                [
                    new(inst => inst.IsLdloc()),
                    new(OpCodes.Ldstr, "Wrp_Sunroom_Door"),
                    new(OpCodes.Call),
                    new(OpCodes.Brtrue_S),
                    new(OpCodes.Br),
                ]
            );
            CodeInstruction WrpLoc = new(matcher.Opcode, matcher.Operand);
            matcher.Advance(3);
            CodeInstruction brtrueS = new(matcher.Opcode, matcher.Operand);
            matcher.Advance(1);
            CodeInstruction br = new(matcher.Opcode, matcher.Operand);
            matcher.Advance(5);
            matcher.Insert(
                [
                    WrpLoc,
                    new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(HumanDoorExt), nameof(IsMMAPWrp))),
                    brtrueS,
                    br,
                ]
            );
            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log(
                $"Failed to patch GameLocation_updateDoors_Transpiler for HumanDoorExt, this patch is optional:\n{err}",
                LogLevel.Warn
            );
            return instructions;
        }
    }

    private static bool DoWrpBuilding(GameLocation location, string[] args, Farmer who, Point point)
    {
        if (TryGetWrpArgs("Wrp", location, args, who, point, out string[]? WrpArgs))
        {
            location.performTouchAction(WrpArgs, who.getStandingPosition());
            return true;
        }
        return false;
    }

    private static bool DoMagicWrpBuilding(GameLocation location, string[] args, Farmer who, Point point)
    {
        if (TryGetWrpArgs("MagicWrp", location, args, who, point, out string[]? WrpArgs))
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
        if (TryGetWrpOutArgs("Wrp", location, args, out string[]? WrpOutArgs))
        {
            location.performTouchAction(WrpOutArgs, who.getStandingPosition());
            return true;
        }
        return false;
    }

    private static bool DoMagicWrpBuildingOut(GameLocation location, string[] args, Farmer who, Point point)
    {
        if (TryGetWrpOutArgs("MagicWrp", location, args, out string[]? WrpOutArgs))
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

    private static bool DoWrpHere(GameLocation location, string[] args, Farmer farmer, Point point)
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

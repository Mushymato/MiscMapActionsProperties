using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;

namespace MiscMapActionsProperties.Framework.Wheels;

internal static class Optimization
{
    internal static void Register()
    {
        try
        {
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.doesTileHaveProperty)),
                transpiler: new HarmonyMethod(
                    typeof(Optimization),
                    nameof(GameLocation_doesTileHaveProperty_Transpiler)
                )
            );
        }
        catch (Exception err)
        {
            ModEntry.Log(
                $"Failed to apply Optimization on GameLocation.doesTileHaveProperty, MMAP will still work just slower in some cases:\n{err}",
                LogLevel.Warn
            );
            return;
        }

        CommonPatch.GameLocation_resetLocalState += GameLocation_resetLocalState;
        CommonPatch.Furniture_OnMoved += Furniture_OnMoved;
    }

    internal static ConditionalWeakTable<GameLocation, Dictionary<Point, HashSet<Furniture>>> TileToFurniture = [];

    internal static Dictionary<Point, HashSet<Furniture>> CreateTileToFurniture(GameLocation location)
    {
        Dictionary<Point, HashSet<Furniture>> tileToFurniture = [];
        foreach (Furniture furni in location.furniture)
        {
            Rectangle bounds = CommonPatch.GetFurnitureTileDataBounds(furni);
            for (int x = bounds.Left; x < bounds.Right; x++)
            {
                for (int y = bounds.Top; y < bounds.Bottom; y++)
                {
                    Point pnt = new(x, y);
                    if (tileToFurniture.TryGetValue(pnt, out HashSet<Furniture>? furniSet))
                    {
                        furniSet.Add(furni);
                    }
                    else
                    {
                        tileToFurniture[pnt] = [furni];
                    }
                }
            }
        }
        return tileToFurniture;
    }

    public static string? CheckFurnitureTileProperties(
        GameLocation location,
        int xTile,
        int yTile,
        string propertyName,
        string layerName
    )
    {
        string? propertyValue = null;

        Dictionary<Point, HashSet<Furniture>>? tileToFurniture = TileToFurniture.GetValue(
            location,
            CreateTileToFurniture
        );

        if (tileToFurniture.TryGetValue(new(xTile, yTile), out HashSet<Furniture>? furnitureSet))
        {
            foreach (Furniture furni in furnitureSet)
            {
                if (furni.DoesTileHaveProperty(xTile, yTile, propertyName, layerName, ref propertyValue))
                {
                    break;
                }
            }
        }

        return propertyValue;
    }

    private static IEnumerable<CodeInstruction> GameLocation_doesTileHaveProperty_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        CodeMatcher matcher = new(instructions, generator);

        LocalBuilder propValueLoc = generator.DeclareLocal(typeof(string));

        matcher
            .Start()
            // IL_013e: ldloc.1
            // IL_013f: brtrue.s IL_019b
            // IL_0141: ldarg.0
            // IL_0142: ldfld class [xTile]xTile.Map StardewValley.GameLocation::map
            // IL_0147: brfalse.s IL_019b
            .MatchStartForward(
                [
                    new(inst => inst.IsLdloc()),
                    new(OpCodes.Brtrue_S),
                    new(OpCodes.Ldarg_0),
                    new(OpCodes.Ldfld, AccessTools.DeclaredField(typeof(GameLocation), nameof(GameLocation.map))),
                    new(OpCodes.Brfalse_S),
                ]
            )
            .ThrowIfNotMatch("Failed to match 'if (!flag && map != null)'")
            .CreateLabel(out Label lbl)
            // IL_0078: ldarg.0
            // IL_0079: ldfld class Netcode.NetCollection`1<class StardewValley.Objects.Furniture> StardewValley.GameLocation::furniture
            // IL_007e: callvirt instance valuetype [System.Collections]System.Collections.Generic.List`1/Enumerator<!0> class Netcode.NetCollection`1<class StardewValley.Objects.Furniture>::GetEnumerator()
            .MatchStartBackwards(
                [
                    new(OpCodes.Ldarg_0),
                    new(OpCodes.Ldfld, AccessTools.DeclaredField(typeof(GameLocation), nameof(GameLocation.furniture))),
                    new(OpCodes.Callvirt),
                ]
            )
            .ThrowIfNotMatch("Failed to match 'foreach (Furniture item in furniture)'")
            .Advance(1)
            .InsertAndAdvance(
                [
                    new(OpCodes.Ldarg_1),
                    new(OpCodes.Ldarg_2),
                    new(OpCodes.Ldarg_3),
                    new(OpCodes.Ldarg_S, (sbyte)4),
                    new(
                        OpCodes.Call,
                        AccessTools.DeclaredMethod(typeof(Optimization), nameof(CheckFurnitureTileProperties))
                    ),
                    new(OpCodes.Stloc, propValueLoc.LocalIndex),
                    new(OpCodes.Ldloc, propValueLoc.LocalIndex),
                    new(OpCodes.Brfalse_S, lbl),
                    new(OpCodes.Ldloc, propValueLoc.LocalIndex),
                    new(OpCodes.Ret),
                    new(OpCodes.Ldarg_0),
                ]
            );

        return matcher.Instructions();
    }

    private static void Furniture_OnMoved(object? sender, CommonPatch.OnFurnitureMovedArgs e)
    {
        TileToFurniture.Remove(e.Placement.Location);
    }

    private static void GameLocation_resetLocalState(object? sender, GameLocation e)
    {
        TileToFurniture.Remove(e);
    }
}

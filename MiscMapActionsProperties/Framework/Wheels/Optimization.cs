using System.Reflection.Emit;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;

namespace MiscMapActionsProperties.Framework.Wheels;

internal static class Optimization
{
    internal static void Setup()
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

        if (CommonPatch.TryGetFurnitureAtTileForLocation(location, new(xTile, yTile), out HashSet<Furniture>? furniSet))
        {
            foreach (Furniture furni in furniSet)
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

        matcher.Start()
#if SDV17
        .MatchEndForward([new(OpCodes.Ldarg_S, (byte)6), new(OpCodes.Brtrue)]);

        Label lbl = (Label)matcher.Operand;
        matcher
            .MatchStartForward(
#else
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
#endif
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
}

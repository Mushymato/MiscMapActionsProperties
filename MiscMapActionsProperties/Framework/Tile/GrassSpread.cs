using System.Reflection.Emit;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace MiscMapActionsProperties.Framework.Tile;

/// <summary>
/// Add new tile property mushymato.MMAP_GrassSpread T
/// If set, allow this tile to spread grass (without using Diggable)
/// </summary>
internal static class GrassSpread
{
    internal const string TileProp_GrassSpread = $"{ModEntry.ModId}_GrassSpread";

    internal static void Register()
    {
        try
        {
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.growWeedGrass)),
                transpiler: new HarmonyMethod(typeof(GrassSpread), nameof(GameLocation_growWeedGrass_Transpiler))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch FruitTreeCosmeticSeason:\n{err}", LogLevel.Error);
        }
    }

    private static IEnumerable<CodeInstruction> GameLocation_growWeedGrass_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        // IL_011f: ldstr "Diggable"
        // IL_0124: ldstr "Back"
        // IL_0129: ldc.i4.0
        // IL_012a: callvirt instance string StardewValley.GameLocation::doesTileHaveProperty(int32, int32, string, string, bool)
        // IL_012f: brfalse.s IL_0180
        try
        {
            CodeMatcher matcher = new(instructions, generator);

            matcher.MatchEndForward(
                [
                    new(OpCodes.Ldstr, "Diggable"),
                    new(OpCodes.Ldstr, "Back"),
                    new(OpCodes.Ldc_I4_0),
                    new(
                        OpCodes.Callvirt,
                        AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.doesTileHaveProperty))
                    ),
                ]
            );
            matcher.Opcode = OpCodes.Call;
            matcher.Operand = AccessTools.DeclaredMethod(typeof(GrassSpread), nameof(GrassSpreadTilePropCheck));

            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in Building_drawInMenu_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
        }
    }

    private static string GrassSpreadTilePropCheck(
        GameLocation location,
        int xTile,
        int yTile,
        string propertyName,
        string layerName,
        bool ignoreTileSheetProperties = false
    )
    {
        return location.doesTileHaveProperty(
                xTile,
                yTile,
                TileProp_GrassSpread,
                layerName,
                ignoreTileSheetProperties: ignoreTileSheetProperties
            )
            ?? location.doesTileHaveProperty(
                xTile,
                yTile,
                propertyName,
                layerName,
                ignoreTileSheetProperties: ignoreTileSheetProperties
            );
    }
}

using System.Reflection.Emit;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace MiscMapActionsProperties.Framework.Buildings;

/// <summary>
/// Fix Building.SeasonOverride not respecting locational season
/// </summary>
internal static class SeasonOverrideFix
{
    internal static void Patch(Harmony harmony)
    {
        try
        {
            harmony.Patch(
                original: AccessTools.Method(
                    typeof(FarmAnimal),
                    nameof(FarmAnimal.setRandomPosition)
                ),
                transpiler: new HarmonyMethod(
                    typeof(SeasonOverrideFix),
                    nameof(Building_ApplySourceRectOffsets_Transpiler)
                )
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch SeasonOverrideFix:\n{err}", LogLevel.Error);
        }
    }

    internal static IEnumerable<CodeInstruction> Building_ApplySourceRectOffsets_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        try
        {
            CodeMatcher matcher = new(instructions, generator);
            matcher.MatchStartForward(
                [
                    new(
                        OpCodes.Call,
                        AccessTools.PropertyGetter(typeof(Game1), nameof(Game1.seasonIndex))
                    ),
                ]
            );
            if (matcher.Pos < matcher.Length)
                matcher.Operand = AccessTools.DeclaredMethod(
                    typeof(Game1),
                    nameof(Game1.GetSeasonIndexForLocation)
                );
            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log(
                $"Error in Building_ApplySourceRectOffsets_Transpiler:\n{err}",
                LogLevel.Error
            );
            return instructions;
        }
    }
}

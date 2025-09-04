using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace MiscMapActionsProperties.Framework.Location;

/// <summary>
/// Add new map property mushymato.MMAP_WaterColor T|color [T|color T|color T|color]
/// Overrides the watercolor
/// Can provide 4 colors for seasonal
/// </summary>
internal static class WaterColor
{
    internal const string Asset_Water = $"{ModEntry.ModId}/Water";
    internal const string MapProp_WaterColor = $"{ModEntry.ModId}_WaterColor";
    internal const string MapProp_WaterTexture = $"{ModEntry.ModId}_WaterTexture";

    // abusing the fact that content patcher always does a copy to not actually invalidate these :)
    private static Texture2D? T_WaterTx = null;
    private static readonly PerScreen<Texture2D?> PerScreenWaterTx = new();
    private static Texture2D? WaterTx = null;

    internal static void Register()
    {
        CommonPatch.GameLocation_resetLocalState += GameLocation_resetLocalState_Postfix;
        ModEntry.help.Events.Content.AssetRequested += OnAssetRequested;
        try
        {
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(
                    typeof(GameLocation),
                    nameof(GameLocation.drawWaterTile),
                    [typeof(SpriteBatch), typeof(int), typeof(int), typeof(Color)]
                ),
                transpiler: new HarmonyMethod(typeof(WaterColor), nameof(GameLocation_drawWaterTile_Transpiler))
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.drawWater)),
                prefix: new HarmonyMethod(typeof(WaterColor), nameof(GameLocation_drawWater_Prefix)),
                postfix: new HarmonyMethod(typeof(WaterColor), nameof(GameLocation_drawWater_Postfix))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch WaterDraw:\n{err}", LogLevel.Error);
        }
    }

    private static void GameLocation_drawWater_Prefix()
    {
        WaterTx = PerScreenWaterTx.Value;
    }

    private static Texture2D ModifyWaterTexture(Texture2D currentWaterTx)
    {
        return WaterTx ?? currentWaterTx;
    }

    private static int ModifyWaterYOffset(int yOffset)
    {
        return WaterTx != null ? 0 : yOffset;
    }

    private static IEnumerable<CodeInstruction> GameLocation_drawWaterTile_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        try
        {
            CodeMatcher matcher = new(instructions, generator);

            for (int i = 0; i < 2; i++)
            {
                matcher
                    .MatchEndForward(
                        [
                            new(OpCodes.Ldarg_1),
                            new(OpCodes.Ldsfld, AccessTools.DeclaredField(typeof(Game1), nameof(Game1.mouseCursors))),
                        ]
                    )
                    .ThrowIfNotMatch("Failed to find 'Game1.mouseCursors'")
                    .Advance(1)
                    .InsertAndAdvance(
                        [new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(WaterColor), nameof(ModifyWaterTexture)))]
                    )
                    .MatchEndForward([new(OpCodes.Mul), new(OpCodes.Ldc_I4, 2064)])
                    .ThrowIfNotMatch("Failed to find '* 2064")
                    .Advance(1)
                    .InsertAndAdvance(
                        [new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(WaterColor), nameof(ModifyWaterYOffset)))]
                    );
            }
            foreach (CodeInstruction inst in matcher.Instructions())
            {
                Console.WriteLine(inst);
            }
            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch WaterDraw:\n{err}", LogLevel.Error);
            return instructions;
        }
    }

    private static void GameLocation_drawWater_Postfix()
    {
        WaterTx = null;
    }

    private static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(Asset_Water))
        {
            e.LoadFromModFile<Texture2D>("assets/base_water.png", AssetLoadPriority.Exclusive);
        }
    }

    private static void GameLocation_resetLocalState_Postfix(object? sender, GameLocation location)
    {
        // water color
        if (CommonPatch.TryGetLocationalProperty(location, MapProp_WaterColor, out string? waterColors))
        {
            string[] args = ArgUtility.SplitBySpace(waterColors);
            Season season = location.GetSeason();
            Color? waterColorOverride = null;

            if (
                ArgUtility.TryGet(
                    args,
                    (int)season,
                    out string seasonColor,
                    out _,
                    allowBlank: false,
                    name: "string seasonWaterColor"
                )
            )
            {
                if (seasonColor != "T")
                    waterColorOverride = Utility.StringToColor(seasonColor);
            }
            else if (
                ArgUtility.TryGet(
                    args,
                    0,
                    out string springColor,
                    out _,
                    allowBlank: false,
                    name: "string seasonWaterColor"
                )
            )
            {
                if (springColor != "T")
                    waterColorOverride = Utility.StringToColor(springColor);
            }

            if (waterColorOverride.HasValue)
            {
                location.waterColor.Value = waterColorOverride.Value;
            }
        }

        PerScreenWaterTx.Value = null;
        if (CommonPatch.TryGetLocationalProperty(location, MapProp_WaterTexture, out string? waterTexture))
        {
            if (waterTexture == "T")
            {
                T_WaterTx ??= Game1.content.Load<Texture2D>(Asset_Water);
                PerScreenWaterTx.Value = T_WaterTx;
            }
            else if (Game1.content.DoesAssetExist<Texture2D>(waterTexture))
            {
                PerScreenWaterTx.Value = Game1.content.Load<Texture2D>(waterTexture);
            }
        }
    }
}

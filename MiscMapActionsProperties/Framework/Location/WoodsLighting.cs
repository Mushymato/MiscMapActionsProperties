using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Locations;

namespace MiscMapActionsProperties.Framework.Location;

/// <summary>
/// Add new map property mushymato.MMAP_WoodsLighting T|Color
/// If set to T, uses the default ambiant lighting (equiv to setting #6987cd, and thus has actual appearance of  #967832)
/// Otherwise, pass in an ambiant light color, which is inverted
/// </summary>
internal static class WoodsLighting
{
    internal sealed record WoodsLightingCtx(Color Color);

    internal static readonly string MapProp_WoodsLighting = $"{ModEntry.ModId}_WoodsLighting";
    private static readonly PerScreen<WoodsLightingCtx?> woodsLightingCtx = new();
    private static Color _ambientLightColor = Color.White;

    internal static void Register()
    {
        try
        {
            Harmony.ReversePatch(
                AccessTools.DeclaredMethod(typeof(Woods), "_updateWoodsLighting"),
                new(typeof(WoodsLighting), nameof(Woods_updateWoodsLighting_RevesePatch)),
                AccessTools.DeclaredMethod(
                    typeof(WoodsLighting),
                    nameof(Woods_updateWoodsLighting_RevesePatchTranspiler)
                )
            );
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(GameLocation), "UpdateWhenCurrentLocation"),
                postfix: new HarmonyMethod(
                    typeof(WoodsLighting),
                    nameof(GameLocation_UpdateWhenCurrentLocation_Postfix)
                )
            );
            ModEntry.GameLocation_resetLocalState += GameLocation_resetLocalState_Postfix;
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch WoodsLighting:\n{err}", LogLevel.Error);
        }
    }

    private static void Woods_updateWoodsLighting_RevesePatch(GameLocation location) => Console.WriteLine("No U");

    private static IEnumerable<CodeInstruction> Woods_updateWoodsLighting_RevesePatchTranspiler(
        IEnumerable<CodeInstruction> instructions
    )
    {
        CodeInstruction ldfld_ambientLightColor =
            new(OpCodes.Ldflda, AccessTools.Field(typeof(WoodsLighting), nameof(_ambientLightColor)));
        FieldInfo amb = AccessTools.Field(typeof(Woods), "_ambientLightColor");
        foreach (CodeInstruction inst in instructions)
        {
            if (inst.opcode == OpCodes.Ldflda && (FieldInfo)inst.operand == amb)
                yield return ldfld_ambientLightColor;
            else
                yield return inst;
        }
    }

    private static void GameLocation_resetLocalState_Postfix(object? sender, GameLocation __instance)
    {
        if (
            __instance.TryGetMapProperty(MapProp_WoodsLighting, out string? colorValue)
            && !string.IsNullOrWhiteSpace(colorValue)
        )
        {
            if (colorValue == "T")
            {
                _ambientLightColor = new Color(150, 120, 50);
            }
            else if (Utility.StringToColor(colorValue) is Color color)
            {
                _ambientLightColor = new Color(color.PackedValue ^ 0x00FFFFFF);
            }
            else
            {
                woodsLightingCtx.Value = null;
                return;
            }
            woodsLightingCtx.Value = new(_ambientLightColor);
            Woods_updateWoodsLighting_RevesePatch(__instance);
            return;
        }
        woodsLightingCtx.Value = null;
    }

    private static void GameLocation_UpdateWhenCurrentLocation_Postfix(GameLocation __instance)
    {
        ModEntry.LogOnce($"woodsLightingCtx {woodsLightingCtx}");
        if (woodsLightingCtx.Value != null)
        {
            _ambientLightColor = woodsLightingCtx.Value.Color;
            ModEntry.LogOnce($"_ambientLightColor {_ambientLightColor}");
            Woods_updateWoodsLighting_RevesePatch(__instance);
        }
    }
}

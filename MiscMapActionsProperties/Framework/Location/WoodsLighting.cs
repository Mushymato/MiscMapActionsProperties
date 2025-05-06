using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
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
                new(typeof(WoodsLighting), nameof(Woods_updateWoodsLighting_ReversePatch))
                {
                    reversePatchType = HarmonyReversePatchType.Original,
                },
                AccessTools.DeclaredMethod(
                    typeof(WoodsLighting),
                    nameof(Woods_updateWoodsLighting_RevesePatchTranspiler)
                )
            );
            CommonPatch.GameLocation_UpdateWhenCurrentLocation += GameLocation_UpdateWhenCurrentLocation_Postfix;
            CommonPatch.GameLocation_resetLocalState += GameLocation_resetLocalState_Postfix;
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch WoodsLighting:\n{err}", LogLevel.Error);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Woods_updateWoodsLighting_ReversePatch(GameLocation location)
    {
        ModEntry.Log(
            $"Woods_updateWoodsLighting_ReversePatch failed, deactivated {MapProp_WoodsLighting}",
            LogLevel.Error
        );
        CommonPatch.GameLocation_UpdateWhenCurrentLocation -= GameLocation_UpdateWhenCurrentLocation_Postfix;
        CommonPatch.GameLocation_resetLocalState -= GameLocation_resetLocalState_Postfix;
        woodsLightingCtx.Value = null;
    }

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

    private static void GameLocation_resetLocalState_Postfix(object? sender, GameLocation location)
    {
        if (
            CommonPatch.TryGetCustomFieldsOrMapProperty(location, MapProp_WoodsLighting, out string? colorValue)
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
            Woods_updateWoodsLighting_ReversePatch(location);
            return;
        }
        woodsLightingCtx.Value = null;
    }

    private static void GameLocation_UpdateWhenCurrentLocation_Postfix(
        object? sender,
        CommonPatch.UpdateWhenCurrentLocationArgs e
    )
    {
        if (woodsLightingCtx.Value != null)
        {
            _ambientLightColor = woodsLightingCtx.Value.Color;
            Woods_updateWoodsLighting_ReversePatch(e.Location);
        }
    }
}

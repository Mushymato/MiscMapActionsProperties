using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;

namespace MiscMapActionsProperties.Framework.Location;

/// <summary>
///
/// </summary>
internal static class LightRays
{
    internal static readonly string MapProp_LightRays = $"{ModEntry.ModId}_LightRays";
    private static bool _enableLightRays = false;
    private static int _raySeed = 0;
    private static Texture2D _rayTexture = null!;

    internal static void Register()
    {
        try
        {
            Harmony.ReversePatch(
                AccessTools.DeclaredMethod(typeof(IslandForestLocation), nameof(IslandForestLocation.DrawRays)),
                new(typeof(LightRays), nameof(IslandForestLocation_DrawRays_RevesePatch)),
                AccessTools.DeclaredMethod(
                    typeof(LightRays),
                    nameof(IslandForestLocation_DrawRays_RevesePatchTranspiler)
                )
            );
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(GameLocation), "resetLocalState"),
                postfix: new HarmonyMethod(typeof(LightRays), nameof(GameLocation_resetLocalState_Postfix))
            );
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.drawAboveAlwaysFrontLayer)),
                postfix: new HarmonyMethod(typeof(LightRays), nameof(GameLocation_drawAboveAlwaysFrontLayer_Postfix))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch LightRays:\n{err}", LogLevel.Error);
        }
    }

    private static void IslandForestLocation_DrawRays_RevesePatch(GameLocation location, SpriteBatch b) =>
        Console.WriteLine("No U");

    private static IEnumerable<CodeInstruction> IslandForestLocation_DrawRays_RevesePatchTranspiler(
        IEnumerable<CodeInstruction> instructions
    )
    {
        CodeInstruction ldfld_rayTexture =
            new(OpCodes.Ldfld, AccessTools.Field(typeof(LightRays), nameof(_rayTexture)));
        FieldInfo rayTx = AccessTools.Field(typeof(IslandForestLocation), "_rayTexture");

        CodeInstruction ldfld_raySeed = new(OpCodes.Ldfld, AccessTools.Field(typeof(LightRays), nameof(_raySeed)));
        FieldInfo raySeed = AccessTools.Field(typeof(IslandForestLocation), "_raySeed");

        foreach (CodeInstruction inst in instructions)
        {
            if (inst.opcode == OpCodes.Ldfld)
            {
                if ((FieldInfo)inst.operand == rayTx)
                {
                    yield return ldfld_rayTexture;
                    continue;
                }
                else if ((FieldInfo)inst.operand == raySeed)
                {
                    yield return ldfld_raySeed;
                    continue;
                }
            }
            yield return inst;
        }
    }

    private static void GameLocation_resetLocalState_Postfix(GameLocation __instance)
    {
        if (
            __instance.TryGetMapProperty(MapProp_LightRays, out string? rayTexture)
            && !string.IsNullOrWhiteSpace(rayTexture)
        )
        {
            if (rayTexture == "T")
            {
                _rayTexture = Game1.temporaryContent.Load<Texture2D>("LooseSprites\\LightRays");
            }
            else if (Game1.temporaryContent.DoesAssetExist<Texture2D>(rayTexture))
            {
                _rayTexture = Game1.temporaryContent.Load<Texture2D>(rayTexture);
            }
            else
            {
                _enableLightRays = false;
                _rayTexture = null!;
                _raySeed = 0;
                return;
            }
            _raySeed = (int)Game1.currentGameTime.TotalGameTime.TotalMilliseconds;
            _enableLightRays = true;
        }
        else
        {
            _enableLightRays = false;
            _rayTexture = null!;
            _raySeed = 0;
        }
    }

    private static void GameLocation_drawAboveAlwaysFrontLayer_Postfix(GameLocation __instance, SpriteBatch b)
    {
        if (_enableLightRays)
        {
            IslandForestLocation_DrawRays_RevesePatch(__instance, b);
        }
    }
}

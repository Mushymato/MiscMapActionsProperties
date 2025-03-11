using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Locations;

namespace MiscMapActionsProperties.Framework.Location;

/// <summary>
/// Add new map property mushymato.MMAP_LightRays T|TextureName
/// If set to T, light rays use LooseSprites\\LightRays
/// Otherwise uses the TextureName if given
/// </summary>
internal static class LightRays
{
    internal sealed record LightRaysCtx(int Seed, Texture2D Texture);

    internal static readonly string MapProp_LightRays = $"{ModEntry.ModId}_LightRays";
    private static readonly PerScreen<LightRaysCtx?> lightRaysCtx = new();
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
                original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.drawAboveAlwaysFrontLayer)),
                postfix: new HarmonyMethod(typeof(LightRays), nameof(GameLocation_drawAboveAlwaysFrontLayer_Postfix))
            );
            ModEntry.GameLocation_resetLocalState += GameLocation_resetLocalState_Postfix;
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
        CodeInstruction call_rayTexture = new(OpCodes.Ldfld, AccessTools.Field(typeof(LightRays), nameof(_rayTexture)));
        FieldInfo rayTx = AccessTools.Field(typeof(IslandForestLocation), "_rayTexture");

        CodeInstruction call_raySeed = new(OpCodes.Ldfld, AccessTools.Field(typeof(LightRays), nameof(_raySeed)));
        FieldInfo raySeed = AccessTools.Field(typeof(IslandForestLocation), "_raySeed");

        foreach (CodeInstruction inst in instructions)
        {
            if (inst.opcode == OpCodes.Ldfld)
            {
                if ((FieldInfo)inst.operand == rayTx)
                {
                    yield return call_rayTexture;
                    continue;
                }
                else if ((FieldInfo)inst.operand == raySeed)
                {
                    yield return call_raySeed;
                    continue;
                }
            }
            yield return inst;
        }
    }

    private static void GameLocation_resetLocalState_Postfix(object? sender, GameLocation __instance)
    {
        if (
            __instance.TryGetMapProperty(MapProp_LightRays, out string? rayTexture)
            && !string.IsNullOrWhiteSpace(rayTexture)
        )
        {
            int raySeed = (int)Game1.currentGameTime.TotalGameTime.TotalMilliseconds;
            if (rayTexture == "T")
            {
                lightRaysCtx.Value = new(raySeed, Game1.temporaryContent.Load<Texture2D>("LooseSprites\\LightRays"));
                return;
            }
            else if (Game1.temporaryContent.DoesAssetExist<Texture2D>(rayTexture))
            {
                lightRaysCtx.Value = new(raySeed, Game1.temporaryContent.Load<Texture2D>(rayTexture));
                return;
            }
        }
        lightRaysCtx.Value = null;
    }

    private static void GameLocation_drawAboveAlwaysFrontLayer_Postfix(GameLocation __instance, SpriteBatch b)
    {
        if (lightRaysCtx.Value != null)
        {
            _raySeed = lightRaysCtx.Value.Seed;
            _rayTexture = lightRaysCtx.Value.Texture;
            IslandForestLocation_DrawRays_RevesePatch(__instance, b);
        }
    }
}

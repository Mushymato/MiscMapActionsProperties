using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewModdingAPI.Events;
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

    internal const string MapProp_LightRays = $"{ModEntry.ModId}_LightRays";
    private static readonly PerScreen<LightRaysCtx?> lightRaysCtx = new();
    private static int _raySeed = 0;
    private static Texture2D _rayTexture = null!;

    internal static void Register()
    {
        try
        {
            Harmony.ReversePatch(
                AccessTools.DeclaredMethod(typeof(IslandForestLocation), nameof(IslandForestLocation.DrawRays)),
                new(typeof(LightRays), nameof(IslandForestLocation_DrawRays_ReversePatch))
                {
                    reversePatchType = HarmonyReversePatchType.Original,
                },
                AccessTools.DeclaredMethod(
                    typeof(LightRays),
                    nameof(IslandForestLocation_DrawRays_RevesePatchTranspiler)
                )
            );
            ModEntry.help.Events.Display.RenderedStep += OnRenderedStep;
            CommonPatch.GameLocation_resetLocalState += GameLocation_resetLocalState_Postfix;
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch LightRays:\n{err}", LogLevel.Error);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void IslandForestLocation_DrawRays_ReversePatch(GameLocation location, SpriteBatch b)
    {
        ModEntry.Log(
            $"IslandForestLocation_DrawRays_ReversePatch failed, deactivated {MapProp_LightRays}",
            LogLevel.Error
        );
        ModEntry.help.Events.Display.RenderedStep -= OnRenderedStep;
        CommonPatch.GameLocation_resetLocalState -= GameLocation_resetLocalState_Postfix;
        lightRaysCtx.Value = null;
    }

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

    private static void GameLocation_resetLocalState_Postfix(object? sender, GameLocation location)
    {
        if (
            CommonPatch.TryGetCustomFieldsOrMapProperty(location, MapProp_LightRays, out string? rayTexture)
            && !string.IsNullOrWhiteSpace(rayTexture)
        )
        {
            int raySeed = (int)Game1.currentGameTime.TotalGameTime.TotalMilliseconds;
            if (rayTexture == "T")
            {
                lightRaysCtx.Value = new(raySeed, Game1.content.Load<Texture2D>("LooseSprites\\LightRays"));
                return;
            }
            else if (Game1.content.DoesAssetExist<Texture2D>(rayTexture))
            {
                lightRaysCtx.Value = new(raySeed, Game1.content.Load<Texture2D>(rayTexture));
                return;
            }
        }
        lightRaysCtx.Value = null;
    }

    private static void OnRenderedStep(object? sender, RenderedStepEventArgs e)
    {
        if (e.Step == StardewValley.Mods.RenderSteps.World_AlwaysFront && lightRaysCtx.Value != null)
        {
            if (Game1.game1.takingMapScreenshot && Game1.viewport.Y != 0)
                return;
            _raySeed = lightRaysCtx.Value.Seed;
            _rayTexture = lightRaysCtx.Value.Texture;
            IslandForestLocation_DrawRays_ReversePatch(Game1.currentLocation, e.SpriteBatch);
            _raySeed = 0;
            _rayTexture = null!;
        }
    }
}

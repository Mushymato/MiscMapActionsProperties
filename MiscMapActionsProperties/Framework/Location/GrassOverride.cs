using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace MiscMapActionsProperties.Framework.Location;

/// <summary>
/// Allow mods to change the texture of the hoe dirt for a location via CustomFields/MapProperty
/// {ModEntry.ModId}_Grass
/// </summary>
internal static class GrassOverride
{
    internal static readonly string MapProp_GrassTexture = $"{ModEntry.ModId}_Grass";
    private static readonly PerScreen<List<Texture2D>?> grassTextureList = new();

    internal static void Register()
    {
        ModEntry.help.Events.GameLoop.DayStarted += OnDayStarted;
        ModEntry.help.Events.Player.Warped += OnWarped;
        ModEntry.help.Events.GameLoop.GameLaunched += OnGameLaunched;
        grassTextureList.Value = null;
    }

    /// <summary>
    /// Horrorterror compatibility patch with MoreGrass where I transpiler their skipping prefixes to make them not skip sometimes.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private static void OnGameLaunched(object? sender, EventArgs e)
    {
        if (ModEntry.help.ModRegistry.Get("EpicBellyFlop45.MoreGrass") is not IModInfo modInfo)
            return;
        try
        {
            if (modInfo?.GetType().GetProperty("Mod")?.GetValue(modInfo) is IMod mod)
            {
                var assembly = mod.GetType().Assembly;
                if (assembly.GetType("MoreGrass.Patches.GrassPatch") is Type MoreGrass_Patches_GrassPatch)
                {
                    ModEntry.harm.Patch(
                        original: AccessTools.DeclaredMethod(MoreGrass_Patches_GrassPatch, "LoadSpritePrefix"),
                        transpiler: new HarmonyMethod(
                            typeof(GrassOverride),
                            nameof(MoreGrass_Patches_GrassPatch_LoadSpritePrefix__Transpiler)
                        )
                    );
                    ModEntry.harm.Patch(
                        original: AccessTools.DeclaredMethod(MoreGrass_Patches_GrassPatch, "DrawPrefix"),
                        transpiler: new HarmonyMethod(
                            typeof(GrassOverride),
                            nameof(MoreGrass_Patches_GrassPatch_DrawPrefix__Transpiler)
                        )
                    );

                    ModEntry.Log(
                        "Patching MoreGrass to conditionally revert Grass.loadSprite and Grass.draw to vanilla."
                    );
                }
            }
        }
        catch (Exception ex)
        {
            ModEntry.Log(
                $"Failed to patch EpicBellyFlop45.MoreGrass::MoreGrass.Patches.GrassPatch, disabling {MapProp_GrassTexture}.\n{ex}"
            );
            ModEntry.help.Events.GameLoop.DayStarted -= OnDayStarted;
            ModEntry.help.Events.Player.Warped -= OnWarped;
            return;
        }
    }

    private static bool ShouldSkipMoreGrass(Grass grass)
    {
        if (
            !Context.IsWorldReady
            || grass == null
            || grass.Location == null
            || !CommonPatch.TryGetCustomFieldsOrMapProperty(grass.Location, MapProp_GrassTexture, out string? _)
        )
            return false;
        return true;
    }

    private static IEnumerable<CodeInstruction> MoreGrass_Patches_GrassPatch_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator,
        OpCode grassLdarg
    )
    {
        try
        {
            CodeMatcher matcher = new(instructions, generator);
            matcher
                .Advance(1)
                .CreateLabel(out Label lbl)
                .Insert(
                    [
                        new(grassLdarg),
                        new(
                            OpCodes.Call,
                            AccessTools.DeclaredMethod(typeof(GrassOverride), nameof(ShouldSkipMoreGrass))
                        ),
                        new(OpCodes.Brfalse_S, lbl),
                        new(OpCodes.Ldc_I4_1),
                        new(OpCodes.Ret),
                    ]
                );
            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in MoreGrass_Patches_GrassPatch_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
        }
    }

    private static IEnumerable<CodeInstruction> MoreGrass_Patches_GrassPatch_LoadSpritePrefix__Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        return MoreGrass_Patches_GrassPatch_Transpiler(instructions, generator, OpCodes.Ldarg_0);
    }

    private static IEnumerable<CodeInstruction> MoreGrass_Patches_GrassPatch_DrawPrefix__Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        return MoreGrass_Patches_GrassPatch_Transpiler(instructions, generator, OpCodes.Ldarg_1);
    }

    private static void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        if (TryGetGrassOverride(Game1.currentLocation))
        {
            ModifyGrassTextureForLocation(Game1.currentLocation);
            Game1.currentLocation.terrainFeatures.OnValueAdded += ModifyGrassTexture;
        }
    }

    private static void OnWarped(object? sender, WarpedEventArgs e)
    {
        e.OldLocation.terrainFeatures.OnValueAdded -= ModifyGrassTexture;
        if (TryGetGrassOverride(e.NewLocation))
        {
            ModifyGrassTextureForLocation(e.NewLocation);
            e.NewLocation.terrainFeatures.OnValueAdded += ModifyGrassTexture;
        }
    }

    private static bool TryGetGrassOverride(GameLocation location)
    {
        if (CommonPatch.TryGetCustomFieldsOrMapProperty(location, MapProp_GrassTexture, out string? grassTxList))
        {
            string[] grassesAsset = ArgUtility.SplitQuoteAware(
                grassTxList,
                ' ',
                splitOptions: StringSplitOptions.TrimEntries & StringSplitOptions.RemoveEmptyEntries
            );
            List<Texture2D> grassesTx = [];
            foreach (string grassAss in grassesAsset)
            {
                if (Game1.content.DoesAssetExist<Texture2D>(grassAss))
                {
                    grassesTx.Add(Game1.content.Load<Texture2D>(grassAss));
                }
            }
            if (grassesTx.Count > 0)
            {
                grassTextureList.Value = grassesTx;
                return true;
            }
        }
        grassTextureList.Value = null;
        return false;
    }

    private static Texture2D GetRandomGrass()
    {
        if (grassTextureList.Value != null)
        {
            return grassTextureList.Value[Random.Shared.Next(grassTextureList.Value.Count)];
        }
        ModEntry.LogOnce(
            $"Failed to get grass override, Game1.currentLocation: {Game1.currentLocation?.NameOrUniqueName ?? "NULL"}",
            LogLevel.Error
        );
        return Game1.content.Load<Texture2D>("TerrainFeatures\\grass");
    }

    private static void ModifyGrassTextureForLocation(GameLocation location)
    {
        foreach (var kv in location.terrainFeatures.Pairs)
        {
            ModifyGrassTexture(kv.Key, kv.Value);
        }
    }

    private static void ModifyGrassTexture(Vector2 tile, TerrainFeature feature)
    {
        if (grassTextureList.Value != null && feature is Grass grass)
        {
            grass.texture = new Lazy<Texture2D>(GetRandomGrass);
        }
    }
}

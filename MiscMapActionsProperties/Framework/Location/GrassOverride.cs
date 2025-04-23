using System.Runtime.CompilerServices;
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
    internal static bool Active = true;

    internal static void Register()
    {
        ModEntry.help.Events.GameLoop.DayStarted += OnDayStarted;
        ModEntry.help.Events.Player.Warped += OnWarped;
        ModEntry.help.Events.GameLoop.SaveLoaded += OnGameLauched;
        grassTextureList.Value = null;
        Active = true;
    }

    private static void Teardown()
    {
        Game1.currentLocation.terrainFeatures.OnValueAdded -= ModifyGrassTexture;
        ModEntry.help.Events.Player.Warped -= OnWarped;
        grassTextureList.Value = null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Grass_loadSprite_ReversePatch(Grass grass)
    {
        ModEntry.Log($"Grass_loadSprite_ReversePatch failed, deactivated {MapProp_GrassTexture}", LogLevel.Error);
        Teardown();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Grass_draw_ReversePatch(Grass grass, SpriteBatch spriteBatch)
    {
        ModEntry.Log($"Grass_draw_ReversePatch failed, deactivated {MapProp_GrassTexture}", LogLevel.Error);
        Teardown();
    }

    private static void OnGameLauched(object? sender, EventArgs e)
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
                    Harmony.ReversePatch(
                        AccessTools.DeclaredMethod(typeof(Grass), nameof(Grass.loadSprite)),
                        new(typeof(GrassOverride), nameof(Grass_loadSprite_ReversePatch))
                        {
                            reversePatchType = HarmonyReversePatchType.Original,
                        }
                    );
                    Harmony.ReversePatch(
                        AccessTools.DeclaredMethod(typeof(Grass), nameof(Grass.draw)),
                        new(typeof(GrassOverride), nameof(Grass_draw_ReversePatch))
                        {
                            reversePatchType = HarmonyReversePatchType.Original,
                        }
                    );

                    ModEntry.harm.Patch(
                        original: AccessTools.DeclaredMethod(MoreGrass_Patches_GrassPatch, "LoadSpritePrefix"),
                        prefix: new HarmonyMethod(
                            typeof(GrassOverride),
                            nameof(MoreGrass_Patches_GrassPatch_LoadSpritePrefix__Prefix)
                        )
                    );
                    ModEntry.harm.Patch(
                        original: AccessTools.DeclaredMethod(MoreGrass_Patches_GrassPatch, "DrawPrefix"),
                        prefix: new HarmonyMethod(
                            typeof(GrassOverride),
                            nameof(MoreGrass_Patches_GrassPatch_DrawPrefix__Prefix)
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
            ModEntry.Log($"Failed to patch EpicBellyFlop45.MoreGrass::MoreGrass.Patches.GrassPatch.\n{ex}");
            return;
        }
    }

    private static bool DoNotSkipMoreGrass(Grass grass)
    {
        if (
            !Active
            || grass == null
            || grass.Location == null
            || !CommonPatch.TryGetCustomFieldsOrMapProperty(grass.Location, MapProp_GrassTexture, out string? _)
        )
            return true;
        return false;
    }

    private static bool MoreGrass_Patches_GrassPatch_LoadSpritePrefix__Prefix(object[] __args)
    {
        try
        {
            Grass grass = (Grass)__args[0];
            if (DoNotSkipMoreGrass(grass))
                return true;
            Grass_loadSprite_ReversePatch(grass);
            return false;
        }
        catch
        {
            Teardown();
            return true;
        }
    }

    private static bool MoreGrass_Patches_GrassPatch_DrawPrefix__Prefix(object[] __args)
    {
        try
        {
            Grass grass = (Grass)__args[1];
            if (DoNotSkipMoreGrass(grass))
                return true;
            SpriteBatch spriteBatch = (SpriteBatch)__args[0];
            Grass_draw_ReversePatch(grass, spriteBatch);
            return false;
        }
        catch
        {
            Teardown();
            return true;
        }
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

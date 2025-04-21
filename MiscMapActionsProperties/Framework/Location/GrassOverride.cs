using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MiscMapActionsProperties.Framework.Wheels;
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
        grassTextureList.Value = null;
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
        if (TryGetGrassOverride(e.NewLocation))
        {
            ModifyGrassTextureForLocation(e.NewLocation);
            e.NewLocation.terrainFeatures.OnValueAdded += ModifyGrassTexture;
        }
        e.OldLocation.terrainFeatures.OnValueAdded -= ModifyGrassTexture;
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

    private static Texture2D GetRandomGrass() =>
        grassTextureList.Value![Random.Shared.Next(grassTextureList.Value.Count)];

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

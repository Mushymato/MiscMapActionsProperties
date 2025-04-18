using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Locations;
using StardewValley.TerrainFeatures;

namespace MiscMapActionsProperties.Framework.Location;

/// <summary>
/// Allow mods to change the texture of the hoe dirt for a location via CustomFields
/// {ModEntry.ModId}/HoeDirt.texture
/// </summary>
internal static class HoeDirtOverride
{
    internal static readonly string MapProp_HoeDirtTexture = $"{ModEntry.ModId}/HoeDirt.texture";
    private static readonly FieldInfo hoeDirtTexture = typeof(HoeDirt).GetField(
        "texture",
        BindingFlags.NonPublic | BindingFlags.Instance
    )!;

    internal static void Register()
    {
        ModEntry.help.Events.GameLoop.DayStarted += OnDayStarted;
        ModEntry.help.Events.Player.Warped += OnWarped;
    }

    private static void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        if (HasHoeDirtOverride(Game1.currentLocation))
        {
            ModifyHoeDirtTextureForLocation(Game1.currentLocation);
            Game1.currentLocation.terrainFeatures.OnValueAdded += ModifyHoeDirtTexture;
        }
    }

    private static void OnWarped(object? sender, WarpedEventArgs e)
    {
        if (HasHoeDirtOverride(e.NewLocation))
        {
            ModifyHoeDirtTextureForLocation(e.NewLocation);
            e.NewLocation.terrainFeatures.OnValueAdded += ModifyHoeDirtTexture;
        }
        e.OldLocation.terrainFeatures.OnValueAdded -= ModifyHoeDirtTexture;
    }

    private static bool HasHoeDirtOverride(GameLocation location)
    {
        return (
            location != null
            && CommonPatch.TryGetCustomFieldsOrMapProperty(location, MapProp_HoeDirtTexture, out string? hoeDirtTexture)
            && Game1.content.DoesAssetExist<Texture2D>(hoeDirtTexture)
        );
    }

    private static bool ModifyHoeDirtTextureForLocation(GameLocation location)
    {
        foreach (var kv in location.terrainFeatures.Pairs)
        {
            ModifyHoeDirtTexture(kv.Key, kv.Value);
        }
        return true;
    }

    private static void ModifyHoeDirtTexture(Vector2 tile, TerrainFeature feature)
    {
        if (
            feature is HoeDirt hoeDirt
            && CommonPatch.TryGetCustomFieldsOrMapProperty(
                hoeDirt.Location,
                MapProp_HoeDirtTexture,
                out string? hoeDirtTx2D
            )
        )
        {
            Texture2D hoeDirtOverride = Game1.content.Load<Texture2D>(hoeDirtTx2D);
            hoeDirtTexture.SetValue(hoeDirt, hoeDirtOverride);
        }
    }
}

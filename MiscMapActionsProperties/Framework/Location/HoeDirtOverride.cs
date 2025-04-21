using System.Reflection;
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
/// {ModEntry.ModId}_HoeDirt
/// </summary>
internal static class HoeDirtOverride
{
    internal static readonly string MapProp_HoeDirtTexture = $"{ModEntry.ModId}_HoeDirt";
    private static readonly FieldInfo hoeDirtTextureField = typeof(HoeDirt).GetField(
        "texture",
        BindingFlags.NonPublic | BindingFlags.Instance
    )!;
    private static readonly PerScreen<Texture2D?> hoeDirtTexture = new();

    internal static void Register()
    {
        ModEntry.help.Events.GameLoop.DayStarted += OnDayStarted;
        ModEntry.help.Events.Player.Warped += OnWarped;
        hoeDirtTexture.Value = null;
    }

    private static void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        if (TryGetHoeDirtOverride(Game1.currentLocation))
        {
            ModifyHoeDirtTextureForLocation(Game1.currentLocation);
            Game1.currentLocation.terrainFeatures.OnValueAdded += ModifyHoeDirtTexture;
        }
    }

    private static void OnWarped(object? sender, WarpedEventArgs e)
    {
        if (TryGetHoeDirtOverride(e.NewLocation))
        {
            ModifyHoeDirtTextureForLocation(e.NewLocation);
            e.NewLocation.terrainFeatures.OnValueAdded += ModifyHoeDirtTexture;
        }
        e.OldLocation.terrainFeatures.OnValueAdded -= ModifyHoeDirtTexture;
    }

    private static bool TryGetHoeDirtOverride(GameLocation location)
    {
        // return (
        //     location != null
        //     && CommonPatch.TryGetCustomFieldsOrMapProperty(location, MapProp_HoeDirtTexture, out string? hoeDirtTexture)
        //     && Game1.content.DoesAssetExist<Texture2D>(hoeDirtTexture)
        // );
        if (
            CommonPatch.TryGetCustomFieldsOrMapProperty(location, MapProp_HoeDirtTexture, out string? hoeDirtTx2D)
            && Game1.content.DoesAssetExist<Texture2D>(hoeDirtTx2D)
        )
        {
            Texture2D hoeDirtOverride = Game1.content.Load<Texture2D>(hoeDirtTx2D);
            hoeDirtTexture.Value = hoeDirtOverride;
            return true;
        }
        else
        {
            hoeDirtTexture.Value = null;
            return false;
        }
    }

    private static void ModifyHoeDirtTextureForLocation(GameLocation location)
    {
        foreach (var kv in location.terrainFeatures.Pairs)
        {
            ModifyHoeDirtTexture(kv.Key, kv.Value);
        }
    }

    private static void ModifyHoeDirtTexture(Vector2 tile, TerrainFeature feature)
    {
        if (hoeDirtTexture.Value != null && feature is HoeDirt hoeDirt)
        {
            hoeDirtTextureField.SetValue(hoeDirt, hoeDirtTexture.Value);
        }
    }
}

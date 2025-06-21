using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.TerrainFeatures;
using FloorPathPropDict = System.Collections.Generic.Dictionary<
    string,
    System.Collections.Generic.Dictionary<string, string>
>;

namespace MiscMapActionsProperties.Framework.Entities;

/// <summary>
/// Allow furniture to get tile data, using the same format as building tile data
/// </summary>
internal static class FloorPathProperties
{
    internal const string Asset_FloorPathProperties = $"{ModEntry.ModId}/FloorPathProperties";
    private static Dictionary<string, FloorPathPropDict>? _fppData = null;

    /// <summary>Furniture tile property data (secretly building data)</summary>
    internal static Dictionary<string, FloorPathPropDict> FPPData
    {
        get
        {
            _fppData ??= Game1.content.Load<Dictionary<string, FloorPathPropDict>>(Asset_FloorPathProperties);
            return _fppData;
        }
    }

    private static void OnAssetInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        if (e.NamesWithoutLocale.Any(an => an.IsEquivalentTo(Asset_FloorPathProperties)))
            _fppData = null;
    }

    private static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.Name.IsEquivalentTo(Asset_FloorPathProperties))
            e.LoadFrom(() => new Dictionary<string, FloorPathPropDict>(), AssetLoadPriority.Exclusive);
    }

    internal static void Register()
    {
        ModEntry.help.Events.Content.AssetRequested += OnAssetRequested;
        ModEntry.help.Events.Content.AssetsInvalidated += OnAssetInvalidated;
        try
        {
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.doesTileHaveProperty)),
                postfix: new HarmonyMethod(
                    typeof(FloorPathProperties),
                    nameof(GameLocation_doesTileHaveProperty_Postfix)
                )
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch FloorPathProperties:\n{err}", LogLevel.Error);
        }
    }

    private static void GameLocation_doesTileHaveProperty_Postfix(
        GameLocation __instance,
        int xTile,
        int yTile,
        string propertyName,
        string layerName,
        ref string __result
    )
    {
        if (__result != null)
            return;
        Vector2 key = new(xTile, yTile);
        if (!__instance.terrainFeatures.TryGetValue(key, out TerrainFeature value) || value is not Flooring flooring)
            return;
        if (!FPPData.TryGetValue(flooring.whichFloor.Value, out FloorPathPropDict? properties))
            return;
        if (
            !properties.TryGetValue(layerName, out Dictionary<string, string>? layerProps)
            || !layerProps.TryGetValue(propertyName, out string? propertyValue)
        )
            return;
        __result = propertyValue;
    }
}

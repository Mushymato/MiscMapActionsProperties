using HarmonyLib;
using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Buildings;
using StardewValley.Objects;

namespace MiscMapActionsProperties.Framework.Tile;

/// <summary>
/// Allow furniture to get tile data, using the same format as building tile data
/// </summary>
internal static class FurnitureProperty
{
    internal static readonly IAssetName Asset_FurnitureProperty = ModEntry.help.GameContent.ParseAssetName(
        $"{ModEntry.ModId}/FurnitureProperty"
    );
    private static Dictionary<string, BuildingData>? _ftpData = null;

    /// <summary>Furniture tile property data (secretly building data)</summary>
    internal static Dictionary<string, BuildingData> FTPData
    {
        get
        {
            _ftpData ??= ModEntry.help.GameContent.Load<Dictionary<string, BuildingData>>(Asset_FurnitureProperty);
            return _ftpData;
        }
    }

    internal static void Register()
    {
        ModEntry.help.Events.Content.AssetRequested += OnAssetRequested;
        ModEntry.help.Events.Content.AssetsInvalidated += OnAssetInvalidated;
        try
        {
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(Furniture), nameof(Furniture.DoesTileHaveProperty)),
                postfix: new HarmonyMethod(typeof(FurnitureProperty), nameof(Furniture_DoesTileHaveProperty_Postfix))
            );
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(Furniture), nameof(Furniture.GetAdditionalTilePropertyRadius)),
                postfix: new HarmonyMethod(
                    typeof(FurnitureProperty),
                    nameof(Furniture_GetAdditionalTilePropertyRadius_Postfix)
                )
            );
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(Furniture), nameof(Furniture.IntersectsForCollision)),
                postfix: new HarmonyMethod(typeof(FurnitureProperty), nameof(Furniture_IntersectsForCollision_Postfix))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch FurnitureTileData:\n{err}", LogLevel.Error);
        }
    }

    private static void Furniture_GetAdditionalTilePropertyRadius_Postfix(Furniture __instance, ref int __result)
    {
        if (!FTPData.TryGetValue(__instance.ItemId, out BuildingData? ftpData))
            return;
        __result = ftpData.AdditionalTilePropertyRadius;
    }

    private static void Furniture_IntersectsForCollision_Postfix(
        Furniture __instance,
        Rectangle rect,
        ref bool __result
    )
    {
        if (!__result || !FTPData.TryGetValue(__instance.ItemId, out BuildingData? ftpData))
            return;

        ftpData.Size = new Point(__instance.getTilesWide(), __instance.getTilesHigh());

        for (int i = rect.Top / 64; i <= rect.Bottom / 64; i++)
        {
            for (int j = rect.Left / 64; j <= rect.Right / 64; j++)
            {
                if (!ftpData.IsTilePassable((int)(j - __instance.TileLocation.X), (int)(i - __instance.TileLocation.Y)))
                {
                    return;
                }
            }
        }
        __result = false;
    }

    private static void OnAssetInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        if (e.NamesWithoutLocale.Any(an => an.IsEquivalentTo(Asset_FurnitureProperty)))
            _ftpData = null;
    }

    private static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.Name.IsEquivalentTo(Asset_FurnitureProperty))
            e.LoadFrom(() => new Dictionary<string, BuildingData>(), AssetLoadPriority.Exclusive);
    }

    private static void Furniture_DoesTileHaveProperty_Postfix(
        Furniture __instance,
        int tile_x,
        int tile_y,
        string property_name,
        string layer_name,
        ref string property_value,
        ref bool __result
    )
    {
        if (__result || !FTPData.TryGetValue(__instance.ItemId, out BuildingData? ftpData))
            return;
        __result = ftpData.HasPropertyAtTile(
            (int)(tile_x - __instance.TileLocation.X),
            (int)(tile_y - __instance.TileLocation.Y),
            property_name,
            layer_name,
            ref property_value
        );
    }
}

using System.Security.AccessControl;
using HarmonyLib;
using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley.GameData.Buildings;
using StardewValley.Objects;

namespace MiscMapActionsProperties.Framework.Tile;

/// <summary>
/// Allow furniture to get tile data, using the same format as building tile data
/// </summary>
internal static class FurnitureProperties
{
    internal static readonly IAssetName Asset_FurnitureProperties = ModEntry.help.GameContent.ParseAssetName(
        $"{ModEntry.ModId}/FurnitureProperties"
    );
    private static Dictionary<string, BuildingData>? _fpData = null;

    /// <summary>Furniture tile property data (secretly building data)</summary>
    internal static Dictionary<string, BuildingData> FPData
    {
        get
        {
            _fpData ??= ModEntry.help.GameContent.Load<Dictionary<string, BuildingData>>(Asset_FurnitureProperties);
            return _fpData;
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
                postfix: new HarmonyMethod(typeof(FurnitureProperties), nameof(Furniture_DoesTileHaveProperty_Postfix))
            );
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(Furniture), nameof(Furniture.GetAdditionalTilePropertyRadius)),
                postfix: new HarmonyMethod(
                    typeof(FurnitureProperties),
                    nameof(Furniture_GetAdditionalTilePropertyRadius_Postfix)
                )
            );
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(Furniture), nameof(Furniture.IntersectsForCollision)),
                postfix: new HarmonyMethod(
                    typeof(FurnitureProperties),
                    nameof(Furniture_IntersectsForCollision_Postfix)
                )
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch FurnitureTileData:\n{err}", LogLevel.Error);
        }
    }

    private static void Furniture_GetAdditionalTilePropertyRadius_Postfix(Furniture __instance, ref int __result)
    {
        if (!FPData.TryGetValue(__instance.ItemId, out BuildingData? ftpData))
            return;
        __result = Math.Max(0, ftpData.AdditionalTilePropertyRadius);
    }

    private static void Furniture_IntersectsForCollision_Postfix(
        Furniture __instance,
        Rectangle rect,
        ref bool __result
    )
    {
        if (!__result || !FPData.TryGetValue(__instance.ItemId, out BuildingData? ftpData))
            return;

        ftpData.Size = new Point(__instance.getTilesWide(), __instance.getTilesHigh());
        Rectangle bounds = CommonPatch.GetFurnitureTileDataBounds(__instance);

        for (int i = rect.Top / 64; i <= rect.Bottom / 64; i++)
        {
            for (int j = rect.Left / 64; j <= rect.Right / 64; j++)
            {
                if (
                    bounds.Contains(j, i)
                    && !ftpData.IsTilePassable(
                        (int)(j - __instance.TileLocation.X),
                        (int)(i - __instance.TileLocation.Y)
                    )
                )
                {
                    return;
                }
            }
        }
        __result = false;
    }

    private static void OnAssetInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        if (e.NamesWithoutLocale.Any(an => an.IsEquivalentTo(Asset_FurnitureProperties)))
            _fpData = null;
    }

    private static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.Name.IsEquivalentTo(Asset_FurnitureProperties))
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
        if (__result || !FPData.TryGetValue(__instance.ItemId, out BuildingData? ftpData))
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

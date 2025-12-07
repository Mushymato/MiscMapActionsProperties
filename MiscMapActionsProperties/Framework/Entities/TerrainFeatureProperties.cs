using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.TerrainFeatures;
using TerrainPropDict = System.Collections.Generic.Dictionary<
    string,
    System.Collections.Generic.Dictionary<string, string?>
>;

namespace MiscMapActionsProperties.Framework.Entities;

/// <summary>
/// Allow flooring and path to get tile data
/// </summary>
internal static class TerrainFeatureProperties
{
    internal const string Asset_FloorPathProperties = $"{ModEntry.ModId}/FloorPathProperties";
    private static Dictionary<string, TerrainPropDict>? _fppData = null;

    /// <summary>Flooring prop data (actually just a Dictionary<string, Dictionary<string, string?>?>)</summary>
    internal static Dictionary<string, TerrainPropDict> FPPData
    {
        get
        {
            _fppData ??= Game1.content.Load<Dictionary<string, TerrainPropDict>>(Asset_FloorPathProperties);
            return _fppData;
        }
    }

    internal const string Asset_WildTreeProperties = $"{ModEntry.ModId}/WildTreeProperties";
    private const char TreePropSep = '@';
    private static Dictionary<string, TerrainPropDict>? _wtpData = null;

    /// <summary>Flooring prop data (actually just a Dictionary<string, Dictionary<string, string?>?>)</summary>
    internal static Dictionary<string, TerrainPropDict> WTPData
    {
        get
        {
            if (_wtpData != null)
                return _wtpData;
            List<(string[], TerrainPropDict)> sourceData = [];
            foreach (
                (string srcKey, TerrainPropDict value) in Game1.content.Load<Dictionary<string, TerrainPropDict>>(
                    Asset_WildTreeProperties
                )
            )
            {
                sourceData.Add(new(ArgUtility.SplitBySpaceQuoteAware(srcKey), value));
            }
            _wtpData = [];
            foreach ((string[] parts, TerrainPropDict value) in sourceData.OrderBy(kv => kv.Item1.Length))
            {
                string normalizedKey = string.Join(TreePropSep, parts);
                if (parts.Length == 3)
                {
                    if (parts[2] != "T" && parts[2] != "F")
                    {
                        ModEntry.Log(
                            $"Invalid value for flip at position 2 '{string.Join(' ', parts)}'",
                            LogLevel.Warn
                        );
                        continue;
                    }
                    if (!char.IsDigit(parts[1][0]) || !char.IsAscii(parts[1][0]))
                    {
                        ModEntry.Log(
                            $"Invalid value for growth stage at position 1 '{string.Join(' ', parts)}'",
                            LogLevel.Warn
                        );
                        continue;
                    }
                    _wtpData[normalizedKey] = value;
                    continue;
                }
                List<string> allKeys = [];
                if (parts.Length == 2)
                {
                    if (!char.IsDigit(parts[1][0]) || !char.IsAscii(parts[1][0]))
                    {
                        ModEntry.Log(
                            $"Invalid value for growth stage at position 1 '{string.Join(' ', parts)}'",
                            LogLevel.Warn
                        );
                        continue;
                    }
                    allKeys.Add(string.Concat(normalizedKey, TreePropSep, 'T'));
                    allKeys.Add(string.Concat(normalizedKey, TreePropSep, 'F'));
                }
                else if (parts.Length == 1)
                {
                    for (int i = -1; i <= 5; i++)
                    {
                        allKeys.Add(string.Concat(normalizedKey, TreePropSep, i.ToString(), TreePropSep, 'T'));
                        allKeys.Add(string.Concat(normalizedKey, TreePropSep, i.ToString(), TreePropSep, 'F'));
                    }
                }
                foreach (string aKey in allKeys)
                {
                    ModEntry.Log(aKey);
                    _wtpData[aKey] = value;
                }
            }
            return _wtpData;
        }
    }

    private static void OnAssetInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        if (e.NamesWithoutLocale.Any(an => an.IsEquivalentTo(Asset_FloorPathProperties)))
            _fppData = null;
        if (e.NamesWithoutLocale.Any(an => an.IsEquivalentTo(Asset_WildTreeProperties)))
            _wtpData = null;
    }

    private static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.Name.IsEquivalentTo(Asset_FloorPathProperties))
            e.LoadFrom(() => new Dictionary<string, TerrainPropDict>(), AssetLoadPriority.Exclusive);
        if (e.Name.IsEquivalentTo(Asset_WildTreeProperties))
            e.LoadFrom(() => new Dictionary<string, TerrainPropDict>(), AssetLoadPriority.Exclusive);
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
                    typeof(TerrainFeatureProperties),
                    nameof(GameLocation_doesTileHaveProperty_Postfix)
                )
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch TerrainFeatureProperties:\n{err}", LogLevel.Error);
        }
    }

    internal static void GameLocation_doesTileHaveProperty_Postfix(
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
        if (!__instance.terrainFeatures.TryGetValue(key, out TerrainFeature terrain))
            return;
        switch (terrain)
        {
            case Flooring flooring:
                FlooringHaveProperty(propertyName, layerName, flooring, ref __result!);
                break;
            case Tree wildTree:
                WildTreeHaveProperty(propertyName, layerName, wildTree, ref __result!);
                break;
        }
    }

    private static void FlooringHaveProperty(
        string propertyName,
        string layerName,
        Flooring flooring,
        ref string __result
    )
    {
        if (!FPPData.TryGetValue(flooring.whichFloor.Value, out TerrainPropDict? properties))
            return;
        if (
            !properties.TryGetValue(layerName, out Dictionary<string, string?>? layerProps)
            || !layerProps.TryGetValue(propertyName, out string? propertyValue)
        )
            return;
        __result = propertyValue!;
    }

    private static void WildTreeHaveProperty(string propertyName, string layerName, Tree wildTree, ref string __result)
    {
        string key = string.Concat(
            wildTree.treeType.Value,
            TreePropSep,
            wildTree.stump.Value ? -1 : Math.Min(wildTree.growthStage.Value, 5),
            TreePropSep,
            wildTree.flipped.Value ? 'F' : 'T'
        );
        if (!WTPData.TryGetValue(key, out TerrainPropDict? properties))
            return;
        if (
            !properties.TryGetValue(layerName, out Dictionary<string, string?>? layerProps)
            || !layerProps.TryGetValue(propertyName, out string? propertyValue)
        )
            return;
        __result = propertyValue!;
    }
}

using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Entities;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Extensions;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;

namespace MiscMapActionsProperties.Framework.Wheels;

public sealed record TileDataCacheChangedArgs(GameLocation Location, HashSet<Point>? Points);

/// <summary>
/// CWT cache for tile data props
/// </summary>
internal sealed class TileDataCache<TProps>
{
    private readonly string[] propKeys;
    private readonly Func<string?[], TProps?> propsValueTransformer;
    private readonly Func<TProps?, TProps?, bool> propsValueComparer;

    private readonly string[] layers;

    internal event EventHandler<TileDataCacheChangedArgs>? TileDataCacheChanged;

    private readonly PerScreen<Dictionary<string, Dictionary<Point, TProps>>> _cachePerScreen = new();
    private Dictionary<string, Dictionary<Point, TProps>> Cache => _cachePerScreen.Value ??= [];
    internal PerScreen<HashSet<Point>?> nextTickChangedPoints = new();

    private readonly PerScreen<HashSet<string>> buildingPropertyJustInvalidated = new(() => []);
    private readonly PerScreen<HashSet<string>> furniturePropertyJustInvalidated = new(() => []);
    private readonly PerScreen<HashSet<string>> floorPathPropertyJustInvalidated = new(() => []);

    internal void PushChangedPoints(GameLocation location, HashSet<Point>? newPoints)
    {
        if (location != Game1.currentLocation)
            return;

        if (newPoints == null)
        {
            nextTickChangedPoints.Value = [];
            return;
        }
        if (nextTickChangedPoints.Value != null)
        {
            nextTickChangedPoints.Value.AddRange(newPoints);
        }
        else
        {
            nextTickChangedPoints.Value = newPoints;
        }
    }

    internal TileDataCache(
        string[] propKeys,
        string[] layers,
        Func<string?[], TProps?> propsValueTransformer,
        Func<TProps?, TProps?, bool> propsValueComparer
    )
    {
        this.propKeys = propKeys;
        this.layers = layers;
        this.propsValueTransformer = propsValueTransformer;
        this.propsValueComparer = propsValueComparer;
        ModEntry.help.Events.GameLoop.ReturnedToTitle += ClearCache;
        ModEntry.help.Events.GameLoop.UpdateTicked += OnUpdateTicked;

        ModEntry.help.Events.World.BuildingListChanged += OnBuildingListChanged;
        CommonPatch.Furniture_OnMoved += OnFurnitureMoved;
        CommonPatch.Flooring_OnMoved += OnFlooringMoved;
        CommonPatch.GameLocation_ApplyMapOverride += OnApplyMapOverride;
        CommonPatch.GameLocation_ReloadMap += OnReloadMap;
        CommonPatch.GameLocation_OnBuildingEndMove += OnBuildingEndMove;
        CommonPatch.GameLocation_MapTilePropChanged += OnMapTilePropChanged;

        ModEntry.help.Events.Content.AssetReady += OnAssetReady;
    }

    private void OnAssetReady(object? sender, AssetReadyEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        if (e.NameWithoutLocale.IsEquivalentTo("Data/Buildings"))
        {
            buildingPropertyJustInvalidated.Value = Cache.Keys.ToHashSet();
        }

        if (
            e.NameWithoutLocale.IsEquivalentTo(FurnitureProperties.Asset_FurnitureProperties)
            || e.NameWithoutLocale.IsEquivalentTo("spacechase0.SpaceCore/FurnitureExtensionData")
        )
        {
            furniturePropertyJustInvalidated.Value = Cache.Keys.ToHashSet();
        }

        if (e.NameWithoutLocale.IsEquivalentTo(FloorPathProperties.Asset_FloorPathProperties))
        {
            floorPathPropertyJustInvalidated.Value = Cache.Keys.ToHashSet();
        }
    }

    private void ClearCache(object? sender, EventArgs e) => Cache.Clear();

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        if (Game1.activeClickableMenu != null)
            return;

        // only do invalidation handling for current location
        if (Game1.currentLocation is not GameLocation curLoc || curLoc.NameOrUniqueName is not string uniqueName)
            return;

        if (buildingPropertyJustInvalidated.Value.Contains(uniqueName))
        {
            HashSet<Point> changedPoints = [];
            foreach (Building building in curLoc.buildings)
            {
                UpdateLocationTileData(curLoc, CommonPatch.GetBuildingTileDataBounds(building), ref changedPoints);
                if (changedPoints.Any())
                    PushChangedPoints(curLoc, changedPoints);
            }
            buildingPropertyJustInvalidated.Value.Remove(uniqueName);
        }
        if (furniturePropertyJustInvalidated.Value.Contains(uniqueName))
        {
            foreach (Furniture furniture in curLoc.furniture)
            {
                OnFurnitureMoved(
                    null,
                    new(furniture, new(furniture.Location, CommonPatch.GetFurnitureTileDataBounds(furniture)))
                );
            }
            furniturePropertyJustInvalidated.Value.Remove(uniqueName);
        }
        if (floorPathPropertyJustInvalidated.Value.Contains(uniqueName))
        {
            foreach (TerrainFeature feature in curLoc.terrainFeatures.Values)
            {
                if (feature is Flooring flooring)
                {
                    OnFlooringMoved(null, flooring);
                }
            }
            floorPathPropertyJustInvalidated.Value.Remove(uniqueName);
        }

        if (nextTickChangedPoints.Value != null)
        {
            TileDataCacheChanged?.Invoke(
                this,
                new(curLoc, nextTickChangedPoints.Value.Any() ? nextTickChangedPoints.Value : null)
            );
            nextTickChangedPoints.Value = null;
        }
    }

    private void OnMapTilePropChanged(object? sender, CommonPatch.MapTilePropChangedArgs e)
    {
        if (!layers.Contains(e.Layer))
            return;
        HashSet<Point> changedPoints = [];
        UpdateLocationTileData(e.Location, new(e.DestPoint, new(1, 1)), ref changedPoints);
        if (changedPoints.Any())
        {
            PushChangedPoints(e.Location, changedPoints);
        }
    }

    private void OnFlooringMoved(object? sender, Flooring flooring)
    {
        HashSet<Point> changedPoints = [];
        UpdateLocationTileData(flooring.Location, new(flooring.Tile.ToPoint(), new(1, 1)), ref changedPoints);
        if (changedPoints.Any())
        {
            PushChangedPoints(flooring.Location, changedPoints);
        }
    }

    private void OnFurnitureMoved(object? sender, CommonPatch.OnFurnitureMovedArgs e)
    {
        HashSet<Point> changedPoints = [];
        UpdateLocationTileData(e.Placement.Location, e.Placement.Bounds, ref changedPoints);
        if (changedPoints.Any())
        {
            PushChangedPoints(e.Placement.Location, changedPoints);
        }
    }

    private void OnBuildingListChanged(object? sender, BuildingListChangedEventArgs e)
    {
        HashSet<Point> changedPoints = [];
        foreach (Building building in e.Removed.Concat(e.Added))
        {
            UpdateLocationTileData(e.Location, CommonPatch.GetBuildingTileDataBounds(building), ref changedPoints);
        }
        if (changedPoints.Any())
            PushChangedPoints(e.Location, changedPoints);
    }

    private void OnBuildingEndMove(object? sender, CommonPatch.OnBuildingMovedArgs e)
    {
        HashSet<Point> changedPoints = [];
        UpdateLocationTileData(e.Location, e.PreviousBounds, ref changedPoints);
        UpdateLocationTileData(e.Location, CommonPatch.GetBuildingTileDataBounds(e.Building), ref changedPoints);
        if (changedPoints.Any())
            PushChangedPoints(e.Location, changedPoints);
    }

    private void OnApplyMapOverride(object? sender, CommonPatch.ApplyMapOverrideArgs e)
    {
        HashSet<Point> changedPoints = [];
        UpdateLocationTileData(e.Location, e.DestRect, ref changedPoints);
        if (changedPoints.Any())
            PushChangedPoints(e.Location, changedPoints);
    }

    private void OnReloadMap(object? sender, GameLocation location)
    {
        if (HasTileData(location))
        {
            Cache.Remove(location.NameOrUniqueName);
            PushChangedPoints(location, null);
        }
    }

    private Dictionary<Point, TProps> CreateLocationTileData(GameLocation location)
    {
        if (location.Map == null)
            return [];
        Dictionary<Point, TProps> cacheEntry = [];
        foreach (string layer in layers)
        {
            for (int x = 0; x < location.Map.DisplayWidth / 64; x++)
            {
                for (int y = 0; y < location.Map.DisplayHeight / 64; y++)
                {
                    TProps? result = propsValueTransformer(
                        propKeys.Select(propKey => location.doesTileHaveProperty(x, y, propKey, layer)).ToArray()
                    );
                    if (result != null)
                        cacheEntry[new(x, y)] = result;
                }
            }
        }
        return cacheEntry;
    }

    private void UpdateLocationTileData(GameLocation location, Rectangle bounds, ref HashSet<Point> changedPoints)
    {
        // guarding all this stuff extra hard i guess
        if (!Context.IsWorldReady || location == null || location.Map == null)
            return;

        if (GetTileData(location, onWarp: false) is not Dictionary<Point, TProps> cacheEntry)
            return;

        DoUpdateLocationTileData(location, bounds, ref changedPoints, ref cacheEntry);
    }

    private void DoUpdateLocationTileData(
        GameLocation location,
        Rectangle bounds,
        ref HashSet<Point> changedPoints,
        ref Dictionary<Point, TProps> cacheEntry
    )
    {
        for (int x = Math.Max(bounds.X, 0); x < Math.Min(bounds.X + bounds.Width, location.Map.DisplayWidth / 64); x++)
        {
            for (
                int y = Math.Max(bounds.Y, 0);
                y < Math.Min(bounds.Y + bounds.Height, location.Map.DisplayHeight / 64);
                y++
            )
            {
                Point pos = new(x, y);
                bool hasPrevious = cacheEntry.TryGetValue(pos, out TProps? previous);
                bool found = false;
                foreach (string layer in layers)
                {
                    if (
                        propsValueTransformer(
                            propKeys.Select(propKey => location.doesTileHaveProperty(x, y, propKey, layer)).ToArray()
                        )
                        is TProps result
                    )
                    {
                        if (!propsValueComparer(result, previous))
                        {
                            changedPoints.Add(pos);
                        }
                        cacheEntry[pos] = result;
                        found = true;
                        break;
                    }
                }
                if (!found && hasPrevious)
                {
                    changedPoints.Add(pos);
                    cacheEntry.Remove(pos);
                }
            }
        }
    }

    internal bool HasTileData(GameLocation location)
    {
        return location != null && location.NameOrUniqueName != null && Cache.ContainsKey(location.NameOrUniqueName);
    }

    internal Dictionary<Point, TProps>? GetTileData(GameLocation location, bool onWarp = true)
    {
        if (!Context.IsWorldReady || location == null || location.NameOrUniqueName is not string uniqueName)
            return null;

        Dictionary<Point, TProps> cacheEntry;
        if (Cache.ContainsKey(uniqueName))
        {
            cacheEntry = Cache[uniqueName];
            if (onWarp)
            {
                // handle invalidated situations
                HashSet<Point> changedPoints = [];
                if (buildingPropertyJustInvalidated.Value.Contains(uniqueName))
                {
                    foreach (Building building in location.buildings)
                    {
                        DoUpdateLocationTileData(
                            location,
                            CommonPatch.GetBuildingTileDataBounds(building),
                            ref changedPoints,
                            ref cacheEntry
                        );
                    }
                    buildingPropertyJustInvalidated.Value.Remove(uniqueName);
                }
                if (furniturePropertyJustInvalidated.Value.Contains(uniqueName))
                {
                    foreach (Furniture furniture in location.furniture)
                    {
                        DoUpdateLocationTileData(
                            furniture.Location,
                            CommonPatch.GetFurnitureTileDataBounds(furniture),
                            ref changedPoints,
                            ref cacheEntry
                        );
                    }
                    furniturePropertyJustInvalidated.Value.Remove(uniqueName);
                }
                if (floorPathPropertyJustInvalidated.Value.Contains(uniqueName))
                {
                    foreach (TerrainFeature feature in location.terrainFeatures.Values)
                    {
                        if (feature is Flooring flooring)
                        {
                            DoUpdateLocationTileData(
                                flooring.Location,
                                new(flooring.Tile.ToPoint(), new(1, 1)),
                                ref changedPoints,
                                ref cacheEntry
                            );
                        }
                    }
                    floorPathPropertyJustInvalidated.Value.Remove(uniqueName);
                }
            }
        }
        else
        {
            cacheEntry = CreateLocationTileData(location);
            Cache[uniqueName] = cacheEntry;
        }
        return cacheEntry;
    }
}

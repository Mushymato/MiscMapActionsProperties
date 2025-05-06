using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Objects;

namespace MiscMapActionsProperties.Framework.Wheels;

/// <summary>
/// CWT cache for tile data props
/// </summary>
internal sealed class TileDataCache<TProps>
{
    private readonly ConditionalWeakTable<GameLocation, Dictionary<Point, TProps>> _cache = [];
    private readonly string[] propKeys;
    private readonly Func<string?[], TProps?> propsValueTransformer;
    private readonly Func<TProps?, TProps?, bool> propsValueComparer;

    private readonly string layer;
    internal event EventHandler<(GameLocation, HashSet<Point>?)>? TileDataCacheChanged;

    internal TileDataCache(
        string[] propKeys,
        string layer,
        Func<string?[], TProps?> propsValueTransformer,
        Func<TProps?, TProps?, bool> propsValueComparer
    )
    {
        this.propKeys = propKeys;
        this.layer = layer;
        this.propsValueTransformer = propsValueTransformer;
        this.propsValueComparer = propsValueComparer;
        ModEntry.help.Events.GameLoop.ReturnedToTitle += ClearCache;
        ModEntry.help.Events.World.BuildingListChanged += OnBuildingListChanged;
        ModEntry.help.Events.World.FurnitureListChanged += OnFurnitureListChanged;
        CommonPatch.GameLocation_ApplyMapOverride += OnApplyMapOverride;
        CommonPatch.GameLocation_ReloadMap += OnReloadMap;
        CommonPatch.GameLocation_OnBuildingMoved += OnBuildingMoved;
    }

    private void ClearCache(object? sender, EventArgs e) => _cache.Clear();

    private static Rectangle GetFurnitureTileDataBounds(Furniture furniture)
    {
        int radius = furniture.GetAdditionalTilePropertyRadius();
        return new(
            (int)furniture.TileLocation.X - radius,
            (int)furniture.TileLocation.Y - radius,
            furniture.getTilesWide() + radius,
            furniture.getTilesHigh() + radius
        );
    }

    private void OnFurnitureListChanged(object? sender, FurnitureListChangedEventArgs e)
    {
        if (!_cache.TryGetValue(e.Location, out _))
            return;
        HashSet<Point> changedPoints = [];
        foreach (Furniture furniture in e.Removed.Concat(e.Added))
        {
            UpdateLocationTileData(e.Location, GetFurnitureTileDataBounds(furniture), ref changedPoints);
        }
        if (changedPoints.Any())
            TileDataCacheChanged?.Invoke(this, new(e.Location, changedPoints));
    }

    private static Rectangle GetBuildingTileDataBounds(Building building)
    {
        int radius = building.GetAdditionalTilePropertyRadius();
        return new(
            building.tileX.Value - radius,
            building.tileY.Value - radius,
            building.tilesWide.Value + radius,
            building.tilesHigh.Value + radius
        );
    }

    private void OnBuildingListChanged(object? sender, BuildingListChangedEventArgs e)
    {
        if (!_cache.TryGetValue(e.Location, out _))
            return;
        HashSet<Point> changedPoints = [];
        foreach (Building building in e.Removed.Concat(e.Added))
        {
            UpdateLocationTileData(e.Location, GetBuildingTileDataBounds(building), ref changedPoints);
        }
        if (changedPoints.Any())
            TileDataCacheChanged?.Invoke(this, new(e.Location, changedPoints));
    }

    private void OnBuildingMoved(object? sender, CommonPatch.OnBuildingMovedArgs e)
    {
        if (!_cache.TryGetValue(e.Location, out _))
            return;
        // hard to determine where building moved from, just gotta bonk the whole cache i guess :(
        if (_cache.TryGetValue(e.Location, out _))
        {
            _cache.Remove(e.Location);
            TileDataCacheChanged?.Invoke(this, new(e.Location, null));
        }
    }

    private void OnApplyMapOverride(object? sender, CommonPatch.ApplyMapOverrideArgs e)
    {
        HashSet<Point> changedPoints = [];
        UpdateLocationTileData(e.Location, e.DestRect, ref changedPoints);
        if (changedPoints.Any())
            TileDataCacheChanged?.Invoke(this, new(e.Location, changedPoints));
    }

    private void OnReloadMap(object? sender, GameLocation location)
    {
        if (_cache.TryGetValue(location, out _))
        {
            _cache.Remove(location);
            TileDataCacheChanged?.Invoke(this, new(location, null));
        }
    }

    private Dictionary<Point, TProps> CreateLocationTileData(GameLocation location)
    {
        if (location.Map == null)
            return [];
        Dictionary<Point, TProps> cacheEntry = [];
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
        return cacheEntry;
    }

    private void UpdateLocationTileData(GameLocation location, Rectangle bounds, ref HashSet<Point> changedPoints)
    {
        if (location.Map == null)
            return;
        Dictionary<Point, TProps> cacheEntry = _cache.GetValue(location, CreateLocationTileData);
        ModEntry.Log($"UpdateLocationTileData START");
        for (int x = Math.Max(bounds.X, 0); x < Math.Min(bounds.X + bounds.Width, location.Map.DisplayWidth / 64); x++)
        {
            for (
                int y = Math.Max(bounds.Y, 0);
                y < Math.Min(bounds.Y + bounds.Height, location.Map.DisplayHeight / 64);
                y++
            )
            {
                Point pos = new(x, y);
                ModEntry.Log($"UpdateLocationTileData: {pos}");
                bool hasPrevious = cacheEntry.TryGetValue(pos, out TProps? previous);
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
                }
                else if (hasPrevious)
                {
                    changedPoints.Add(pos);
                    cacheEntry.Remove(pos);
                }
            }
        }
    }

    internal Dictionary<Point, TProps> GetTileData(GameLocation location)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        var result = _cache.GetValue(location, CreateLocationTileData);
        ModEntry.Log($"TileDataCache.GetTileData({location.NameOrUniqueName}): {stopwatch.Elapsed}");
        return result;
    }
}

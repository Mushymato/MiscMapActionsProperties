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
        ModEntry.help.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        ModEntry.help.Events.Player.Warped += OnWarped;
        ModEntry.help.Events.World.BuildingListChanged += OnBuildingListChanged;
        CommonPatch.GameLocation_ApplyMapOverride += OnApplyMapOverride;
        CommonPatch.GameLocation_ReloadMap += OnReloadMap;
        CommonPatch.GameLocation_OnBuildingEndMove += OnBuildingEndMove;
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        SetFurnitureChanged(Game1.currentLocation);
    }

    private void OnWarped(object? sender, WarpedEventArgs e)
    {
        ClearFurnitureChanged(e.OldLocation);
        SetFurnitureChanged(e.NewLocation);
    }

    private void SetFurnitureChanged(GameLocation location)
    {
        if (location == null)
            return;
        location.furniture.OnValueAdded += OnFurnitureChanged;
        location.furniture.OnValueRemoved += OnFurnitureChanged;
    }

    private void ClearFurnitureChanged(GameLocation location)
    {
        if (location == null)
            return;
        location.furniture.OnValueAdded -= OnFurnitureChanged;
        location.furniture.OnValueRemoved -= OnFurnitureChanged;
    }

    private void ClearCache(object? sender, EventArgs e) => _cache.Clear();

    private void OnFurnitureChanged(Furniture furniture)
    {
        HashSet<Point> changedPoints = [];
        UpdateLocationTileData(
            furniture.Location,
            CommonPatch.GetFurnitureTileDataBounds(furniture),
            ref changedPoints
        );
        if (changedPoints.Any())
            TileDataCacheChanged?.Invoke(this, new(furniture.Location, changedPoints));
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
        {
            DelayedAction delayedAction = DelayedAction.functionAfterDelay(
                () => TileDataCacheChanged?.Invoke(this, new(e.Location, changedPoints)),
                1
            );
            delayedAction.waitUntilMenusGone = true;
        }
    }

    private void OnBuildingEndMove(object? sender, CommonPatch.OnBuildingMovedArgs e)
    {
        if (!_cache.TryGetValue(e.Location, out _))
            return;
        HashSet<Point> changedPoints = [];
        UpdateLocationTileData(e.Location, e.PreviousBounds, ref changedPoints);
        UpdateLocationTileData(e.Location, GetBuildingTileDataBounds(e.Building), ref changedPoints);
        if (changedPoints.Any())
        {
            DelayedAction delayedAction = DelayedAction.functionAfterDelay(
                () => TileDataCacheChanged?.Invoke(this, new(e.Location, changedPoints)),
                1
            );
            delayedAction.waitUntilMenusGone = true;
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

    internal Dictionary<Point, TProps> GetTileData(GameLocation location) =>
        _cache.GetValue(location, CreateLocationTileData);
}

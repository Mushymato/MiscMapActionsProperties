using Microsoft.Xna.Framework;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Extensions;
using StardewValley.Objects;

namespace MiscMapActionsProperties.Framework.Wheels;

/// <summary>
/// CWT cache for tile data props
/// </summary>
internal sealed class TileDataCache<TProps>
{
    private readonly string[] propKeys;
    private readonly Func<string?[], TProps?> propsValueTransformer;
    private readonly Func<TProps?, TProps?, bool> propsValueComparer;

    private readonly string[] layers;
    internal event EventHandler<(GameLocation, HashSet<Point>?)>? TileDataCacheChanged;

    private readonly Dictionary<string, Dictionary<Point, TProps>> _cache = [];
    internal Dictionary<GameLocation, HashSet<Point>?> nextTickChangedPoints = [];

    internal void PushChangedPoints(GameLocation location, HashSet<Point>? newPoints)
    {
        if (newPoints == null)
        {
            nextTickChangedPoints[location] = null;
            return;
        }
        if (nextTickChangedPoints.TryGetValue(location, out HashSet<Point>? existingPoints))
        {
            existingPoints?.AddRange(newPoints);
        }
        else
        {
            nextTickChangedPoints[location] = newPoints;
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
        CommonPatch.GameLocation_ApplyMapOverride += OnApplyMapOverride;
        CommonPatch.GameLocation_ReloadMap += OnReloadMap;
        CommonPatch.GameLocation_OnBuildingEndMove += OnBuildingEndMove;
        CommonPatch.GameLocation_MapTilePropChanged += OnMapTilePropChanged;
    }

    private void ClearCache(object? sender, EventArgs e) => _cache.Clear();

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (Game1.activeClickableMenu != null || !nextTickChangedPoints.Any())
            return;

        foreach ((GameLocation location, HashSet<Point>? changed) in nextTickChangedPoints)
        {
            TileDataCacheChanged?.Invoke(this, new(location, changed));
        }
        nextTickChangedPoints.Clear();
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

    private void OnFurnitureMoved(object? sender, Furniture furniture)
    {
        HashSet<Point> changedPoints = [];
        UpdateLocationTileData(
            furniture.Location,
            CommonPatch.GetFurnitureTileDataBounds(furniture),
            ref changedPoints
        );
        if (changedPoints.Any())
        {
            PushChangedPoints(furniture.Location, changedPoints);
        }
    }

    private void OnBuildingListChanged(object? sender, BuildingListChangedEventArgs e)
    {
        if (!_cache.TryGetValue(e.Location.NameOrUniqueName, out _))
            return;
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
        if (!_cache.TryGetValue(e.Location.NameOrUniqueName, out _))
            return;
        HashSet<Point> changedPoints = [];
        UpdateLocationTileData(e.Location, e.PreviousBounds, ref changedPoints);
        UpdateLocationTileData(e.Location, CommonPatch.GetBuildingTileDataBounds(e.Building), ref changedPoints);
        if (changedPoints.Any())
        {
            PushChangedPoints(e.Location, changedPoints);
        }
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
        if (_cache.TryGetValue(location.NameOrUniqueName, out _))
        {
            _cache.Remove(location.NameOrUniqueName);
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
        if (location.Map == null)
            return;

        Dictionary<Point, TProps> cacheEntry = GetTileData(location);

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

    internal Dictionary<Point, TProps> GetTileData(GameLocation location)
    {
        Dictionary<Point, TProps> cacheEntry;
        if (_cache.ContainsKey(location.NameOrUniqueName))
        {
            cacheEntry = _cache[location.NameOrUniqueName];
        }
        else
        {
            cacheEntry = CreateLocationTileData(location);
            _cache[location.NameOrUniqueName] = cacheEntry;
        }
        return cacheEntry;
    }
}

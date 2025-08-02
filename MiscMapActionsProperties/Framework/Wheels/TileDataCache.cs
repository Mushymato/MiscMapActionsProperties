using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Entities;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Buildings;
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
    internal PerScreen<ConditionalWeakTable<GameLocation, HashSet<Point>>> pointsToUpdate = new(() => []);

    private readonly PerScreen<HashSet<string>> buildingPropertyJustInvalidated = new(() => []);
    private readonly PerScreen<HashSet<string>> furniturePropertyJustInvalidated = new(() => []);
    private readonly PerScreen<HashSet<string>> floorPathPropertyJustInvalidated = new(() => []);

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

    private static HashSet<Point> MakePointSet(GameLocation loc) => [];

    private void ClearNextPointsToUpdate(GameLocation location)
    {
        if (pointsToUpdate.Value.TryGetValue(location, out HashSet<Point>? pointsSet))
        {
            pointsSet.Clear();
        }
    }

    private void PushNextPointsToUpdate(
        GameLocation location,
        Point point
#if DEBUG
        ,
        [CallerMemberName] string? callerMemberName = null
#endif
    )
    {
        if (pointsToUpdate.Value.GetValue(location, MakePointSet) is HashSet<Point> pointsSet)
        {
#if DEBUG
            ModEntry.Log($"{callerMemberName} add point {point} ({string.Join('+', propKeys)}, {string.Join('+', layers)})");
#endif
            pointsSet.Add(point);
        }
    }

    private void PushNextPointsToUpdate(
        GameLocation location,
        Rectangle bounds
#if DEBUG
        ,
        [CallerMemberName] string? callerMemberName = null
#endif
    )
    {
        if (pointsToUpdate.Value.GetValue(location, MakePointSet) is HashSet<Point> pointsSet)
        {
#if DEBUG
            ModEntry.Log($"{callerMemberName} add rect {bounds} ({string.Join('+', propKeys)}, {string.Join('+', layers)})");
#endif

            for (int x = bounds.Left; x < bounds.Right; x++)
            {
                for (int y = bounds.Top; y < bounds.Bottom; y++)
                {
                    pointsSet.Add(new(x, y));
                }
            }
        }
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        if (Game1.activeClickableMenu != null)
            return;

        // try to do all the updates
        UpdateQueuedPoints();
    }

    private void UpdateQueuedPoints(bool signalChanged = true)
    {

#if DEBUG
        Stopwatch stopwatch = Stopwatch.StartNew();
        List<string> hasDoneUpdate = [];
#endif

        foreach ((GameLocation loc, HashSet<Point> points) in pointsToUpdate.Value)
        {
            if (
                !points.Any()
                || loc.NameOrUniqueName == null
                || !Cache.TryGetValue(loc.NameOrUniqueName, out Dictionary<Point, TProps>? cacheEntry)
            )
            {
                points.Clear();
                continue;
            }

#if DEBUG
            hasDoneUpdate.Add(loc.NameOrUniqueName);
#endif

            HashSet<Point> changedPoints = [];
            foreach (Point pnt in points)
            {
                bool hasPrevious = cacheEntry.TryGetValue(pnt, out TProps? previous);
                bool found = false;
                foreach (string layer in layers)
                {
                    if (
                        propsValueTransformer(
                            propKeys.Select(propKey => loc.doesTileHaveProperty(pnt.X, pnt.Y, propKey, layer)).ToArray()
                        )
                        is TProps result
                    )
                    {
                        if (!propsValueComparer(result, previous))
                        {
                            changedPoints.Add(pnt);
                        }
                        cacheEntry[pnt] = result;
                        found = true;
                        break;
                    }
                }
                if (!found && hasPrevious)
                {
                    changedPoints.Add(pnt);
                    cacheEntry.Remove(pnt);
                }
            }

            if (signalChanged && changedPoints.Any())
                TileDataCacheChanged?.Invoke(this, new(loc, changedPoints));

            points.Clear();
        }

#if DEBUG
        if (hasDoneUpdate.Any())
            ModEntry.Log(
                $"{stopwatch.Elapsed}: UpdateQueuedPoints (locations {string.Join(',', hasDoneUpdate)}, {string.Join('+', propKeys)}, {string.Join('+', layers)})"
            );
#endif
    }

    private void OnMapTilePropChanged(object? sender, CommonPatch.MapTilePropChangedArgs e)
    {
        if (!layers.Contains(e.Layer))
            return;

        PushNextPointsToUpdate(e.Location, e.DestPoint);
    }

    private void OnFlooringMoved(object? sender, Flooring flooring)
    {
        PushNextPointsToUpdate(flooring.Location, flooring.Tile.ToPoint());
    }

    private void OnFurnitureMoved(object? sender, CommonPatch.OnFurnitureMovedArgs e)
    {
        PushNextPointsToUpdate(e.Placement.Location, e.Placement.Bounds);
    }

    private void OnBuildingListChanged(object? sender, BuildingListChangedEventArgs e)
    {
        foreach (Building building in e.Removed.Concat(e.Added))
        {
            PushNextPointsToUpdate(e.Location, CommonPatch.GetBuildingTileDataBounds(building));
        }
    }

    private void OnBuildingEndMove(object? sender, CommonPatch.OnBuildingMovedArgs e)
    {
        PushNextPointsToUpdate(e.Location, e.PreviousBounds);
        PushNextPointsToUpdate(e.Location, CommonPatch.GetBuildingTileDataBounds(e.Building));
    }

    private void OnApplyMapOverride(object? sender, CommonPatch.ApplyMapOverrideArgs e)
    {
        PushNextPointsToUpdate(e.Location, e.DestRect);
    }

    private void OnReloadMap(object? sender, GameLocation location)
    {
        if (HasTileData(location))
        {
            Cache.Remove(location.NameOrUniqueName);
            ClearNextPointsToUpdate(location);
        }
    }

    private Dictionary<Point, TProps> CreateLocationTileData(GameLocation location)
    {
        if (location.Map == null)
            return [];
        Dictionary<Point, TProps> cacheEntry = [];
        foreach (string layer in layers)
        {
            for (int x = 0; x < location.Map.DisplayWidth / Game1.tileSize; x++)
            {
                for (int y = 0; y < location.Map.DisplayHeight / Game1.tileSize; y++)
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

    internal bool HasTileData(GameLocation location)
    {
        return location != null && location.NameOrUniqueName != null && Cache.ContainsKey(location.NameOrUniqueName);
    }

    internal Dictionary<Point, TProps>? GetTileData(
        GameLocation location,
        bool onWarp = true,
#if DEBUG
        [CallerMemberName] string? callerMemberName = null
#endif
    )
    {
        if (!Context.IsWorldReady || location == null || location.NameOrUniqueName is not string uniqueName)
            return null;

#if DEBUG
        Stopwatch stopwatch = Stopwatch.StartNew();
#endif

        Dictionary<Point, TProps> cacheEntry;
        if (Cache.ContainsKey(uniqueName))
        {
            cacheEntry = Cache[uniqueName];
            if (onWarp)
            {
                // handle invalidated situations
                if (buildingPropertyJustInvalidated.Value.Contains(uniqueName))
                {
                    foreach (Building building in location.buildings)
                    {
                        PushNextPointsToUpdate(location, CommonPatch.GetBuildingTileDataBounds(building));
                    }
                    buildingPropertyJustInvalidated.Value.Remove(uniqueName);
                }
                if (furniturePropertyJustInvalidated.Value.Contains(uniqueName))
                {
                    foreach (Furniture furniture in location.furniture)
                    {
                        PushNextPointsToUpdate(location, CommonPatch.GetFurnitureTileDataBounds(furniture));
                    }
                    furniturePropertyJustInvalidated.Value.Remove(uniqueName);
                }
                if (floorPathPropertyJustInvalidated.Value.Contains(uniqueName))
                {
                    foreach (TerrainFeature feature in location.terrainFeatures.Values)
                    {
                        if (feature is Flooring flooring)
                        {
                            PushNextPointsToUpdate(location, flooring.Tile.ToPoint());
                        }
                    }
                    floorPathPropertyJustInvalidated.Value.Remove(uniqueName);
                }

                UpdateQueuedPoints(signalChanged: false);
            }
        }
        else
        {
            cacheEntry = CreateLocationTileData(location);
            Cache[uniqueName] = cacheEntry;
        }

#if DEBUG
        ModEntry.Log(
            $"{stopwatch.Elapsed}: GetTileData (from {callerMemberName}, {string.Join('+', propKeys)}, {string.Join('+', layers)})"
        );
#endif

        return cacheEntry;
    }
}

using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Entities;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Extensions;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
#if DEBUG_VERBOSE
using System.Diagnostics;
#endif

namespace MiscMapActionsProperties.Framework.Wheels;

public sealed record TileDataCacheChangedArgs(GameLocation Location, HashSet<Point>? Points);

/// <summary>
/// CWT cache for tile data props
/// Should be used PerScreen
/// </summary>
internal sealed class TileDataCache<TProps>
{
    private readonly string[] propKeys;
    private readonly Func<string?[], TProps?> propsValueTransformer;

    private readonly string[] layers;

    internal event EventHandler<TileDataCacheChangedArgs>? TileDataCacheChanged;

    // has to be a dict of NameOrUniqueName -> Dictionary because of split screen
    // DO NOT TRY ConditionalWeakTable AGAIN YOU DUMMY
    private readonly Dictionary<string, Dictionary<Point, TProps>> Cache = [];
    internal ConditionalWeakTable<GameLocation, HashSet<Point>> pointsToUpdate = [];

    private readonly HashSet<string> buildingPropertyJustInvalidated = [];
    private readonly HashSet<string> furniturePropertyJustInvalidated = [];
    private readonly HashSet<string> floorPathPropertyJustInvalidated = [];

    internal TileDataCache(string[] propKeys, string[] layers, Func<string?[], TProps?> propsValueTransformer)
    {
        this.propKeys = propKeys;
        this.layers = layers;
        this.propsValueTransformer = propsValueTransformer;
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

        HashSet<string> cachedLocations = Cache.Keys.ToHashSet();

        if (e.NameWithoutLocale.IsEquivalentTo("Data/Buildings"))
        {
            buildingPropertyJustInvalidated.AddRange(cachedLocations);
        }

        if (
            e.NameWithoutLocale.IsEquivalentTo(FurnitureProperties.Asset_FurnitureProperties)
            || e.NameWithoutLocale.IsEquivalentTo("spacechase0.SpaceCore/FurnitureExtensionData")
        )
        {
            furniturePropertyJustInvalidated.AddRange(cachedLocations);
        }

        if (e.NameWithoutLocale.IsEquivalentTo(TerrainFeatureProperties.Asset_FloorPathProperties))
        {
            floorPathPropertyJustInvalidated.AddRange(cachedLocations);
        }
    }

    private void ClearCache(object? sender, EventArgs e) => Cache.Clear();

    private static HashSet<Point> MakePointSet(GameLocation loc) => [];

    private void ClearNextPointsToUpdate(GameLocation location)
    {
        if (pointsToUpdate.TryGetValue(location, out HashSet<Point>? pointsSet))
        {
            pointsSet.Clear();
        }
    }

    private void PushNextPointsToUpdate(
        GameLocation location,
        Point point
#if DEBUG_VERBOSE
        ,
        [CallerMemberName] string? callerMemberName = null
#endif
    )
    {
        if (pointsToUpdate.GetValue(location, MakePointSet) is HashSet<Point> pointsSet)
        {
#if DEBUG_VERBOSE
            ModEntry.Log(
                $"{Context.ScreenId}: {callerMemberName} add point {point} ({string.Join('+', propKeys)}, {string.Join('+', layers)})"
            );
#endif
            pointsSet.Add(point);
        }
    }

    private void PushNextPointsToUpdate(
        GameLocation location,
        Rectangle bounds
#if DEBUG_VERBOSE
        ,
        [CallerMemberName] string? callerMemberName = null
#endif
    )
    {
        if (pointsToUpdate.GetValue(location, MakePointSet) is HashSet<Point> pointsSet)
        {
#if DEBUG_VERBOSE
            ModEntry.Log(
                $"{Context.ScreenId}: {callerMemberName} add rect {bounds} ({string.Join('+', propKeys)}, {string.Join('+', layers)})"
            );
#endif

            pointsSet.AddRange(CommonPatch.IterateBounds(bounds));
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
#if DEBUG_VERBOSE
        Stopwatch stopwatch = Stopwatch.StartNew();
        List<string> hasDoneUpdate = [];
#endif

        foreach ((GameLocation loc, HashSet<Point> points) in pointsToUpdate)
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

#if DEBUG_VERBOSE
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
                        changedPoints.Add(pnt);
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

#if DEBUG_VERBOSE
        if (hasDoneUpdate.Any())
            ModEntry.Log(
                $"{Context.ScreenId}: {stopwatch.Elapsed}: UpdateQueuedPoints (locations {string.Join(',', hasDoneUpdate)}, {string.Join('+', propKeys)}, {string.Join('+', layers)})"
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
        PushNextPointsToUpdate(
            e.Placement.Location,
            CommonPatch.GetFurnitureTileDataBounds(e.Furniture, e.Placement.TileLocation)
        );
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
        if (location?.NameOrUniqueName is string uniqueName)
        {
            Cache.Remove(uniqueName);
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

    internal Dictionary<Point, TProps>? GetTileData(
        GameLocation location,
        bool onWarp = true
#if DEBUG_VERBOSE
        ,
        [CallerMemberName] string? callerMemberName = null
#endif
    )
    {
        if (!Context.IsWorldReady || location == null || location.NameOrUniqueName is not string uniqueName)
            return null;

#if DEBUG_VERBOSE
        Stopwatch stopwatch = Stopwatch.StartNew();
#endif

        if (Cache.TryGetValue(uniqueName, out Dictionary<Point, TProps>? cacheEntry))
        {
            if (onWarp)
            {
                // handle invalidated situations
                if (buildingPropertyJustInvalidated.Contains(uniqueName))
                {
                    foreach (Building building in location.buildings)
                    {
                        PushNextPointsToUpdate(location, CommonPatch.GetBuildingTileDataBounds(building));
                    }
                    buildingPropertyJustInvalidated.Remove(uniqueName);
                }
                if (furniturePropertyJustInvalidated.Contains(uniqueName))
                {
                    foreach (Furniture furniture in location.furniture)
                    {
                        PushNextPointsToUpdate(location, CommonPatch.GetFurnitureTileDataBounds(furniture));
                    }
                    furniturePropertyJustInvalidated.Remove(uniqueName);
                }
                if (floorPathPropertyJustInvalidated.Contains(uniqueName))
                {
                    foreach (TerrainFeature feature in location.terrainFeatures.Values)
                    {
                        if (feature is Flooring flooring)
                        {
                            PushNextPointsToUpdate(location, flooring.Tile.ToPoint());
                        }
                    }
                    floorPathPropertyJustInvalidated.Remove(uniqueName);
                }

                UpdateQueuedPoints(signalChanged: false);
            }
        }
        else
        {
            cacheEntry = CreateLocationTileData(location);
            Cache[uniqueName] = cacheEntry;
        }

#if DEBUG_VERBOSE
        ModEntry.Log(
            $"{stopwatch.Elapsed}: GetTileData (from {callerMemberName}, {string.Join('+', propKeys)}, {string.Join('+', layers)})"
        );
#endif

        return cacheEntry;
    }
}

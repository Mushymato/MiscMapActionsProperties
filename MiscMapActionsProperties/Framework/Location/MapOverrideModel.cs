using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Extensions;
using StardewValley.Locations;
using StardewValley.TerrainFeatures;
using StardewValley.TokenizableStrings;
using xTile;
using xTile.Layers;
using xTile.Tiles;

namespace MiscMapActionsProperties.Framework.Location;

public enum MapOverrideSpawnKind
{
    None = 0,
    Grass = 1,
    Object = 2,
    Forage = 3,
}

public sealed class MapOverrideRemoval
{
    public string Id
    {
        get => field ??= $"{Layer}:{TileArea}";
        set => field = value;
    }
    public string? Layer { get; set; } = "Back";
    public Rectangle TileArea { get; set; } = Rectangle.Empty;
}

public sealed class MapOverrideRenonvationData
{
    // def
    public string? TargetLocationCondition { get; set; } = null;
    public int Price { get; set; } = 0;
    public string? AddCondition { get; set; } = null;
    public string? RemoveCondition { get; set; } = null;
    public List<Rectangle>? TargetRectGroup { get; set; } = null;
    public bool CheckForObstructions { get; set; } = true;

    // text
    public string? AddDisplayName { get; set; } = null;
    public string? AddDescription { get; set; } = null;
    public string? AddPlacementText { get; set; } = null;
    public string? RemoveDisplayName { get; set; } = null;
    public string? RemoveDescription { get; set; } = null;
    public string? RemovePlacementText { get; set; } = null;

    public bool CanHaveRenovation(GameLocation location)
    {
        return TargetLocationCondition != null
            && GameStateQuery.CheckConditions(TargetLocationCondition, location: location);
    }
}

public sealed class MapOverrideModel
{
    public string Id { get; set; } = null!;
    public string? RemovedById { get; set; } = null;
    public string SourceMap { get; set; } = "Maps/SkullCaveAltar";
    public Rectangle? SourceRect { get; set; } = null;
    public Rectangle? TargetRect { get; set; } = null;
    public List<MapOverrideRemoval>? TileRemoveRects { get; set; } = null;
    public bool TargetRectIsRelative { get; set; } = false;
    public int Precedence { get; set; } = 0;
    public bool ClearTargetRectOnApply { get; set; } = false;
    public bool? LoadWaterTilesOnApply { get; set; } = null;
    public bool LoadPathObjectsOnApply { get; set; } = false;
    public bool MapOverrideSpawnOnApply { get; set; } = false;
    public bool ResizeMapIfNeeded { get; set; } = false;
    public bool ForceTilesheetMatch { get; set; } = false;
    public MapOverrideRenonvationData? Renovation { get; set; } = null;

    private string? mapOverrideKey = null;
    internal string MapOverrideKey => mapOverrideKey ??= $"{ModEntry.ModId}+MapOverride/{Id}";

    internal string StoredId { get; private set; } = string.Empty;

    private Rectangle? RelTargetRect = null;

    internal void UpdateRelTargetRect(Point? relPoint)
    {
        if (relPoint is Point relPointV && TargetRectIsRelative && TargetRect is Rectangle targetRect)
        {
            StoredId = string.Concat(
                Id,
                MapOverride.Ctrl_SEP_RelCoord,
                relPointV.X,
                MapOverride.Ctrl_SEP_RelCoordXY,
                relPointV.Y
            );
            RelTargetRect = new(
                targetRect.X + relPointV.X,
                targetRect.Y + relPointV.Y,
                targetRect.Width,
                targetRect.Height
            );
            ModEntry.Log($"RelTargetRect {Id}: {relPoint} {RelTargetRect}");
        }
        else
        {
            StoredId = Id;
            RelTargetRect = null;
        }
    }

    private static void GetMapSize(Map map, out int width, out int height)
    {
        width = 0;
        height = 0;
        foreach (Layer layer in map.Layers)
        {
            width = Math.Max(width, layer.LayerWidth);
            height = Math.Max(height, layer.LayerHeight);
        }
    }

    internal FieldInfo? Layer_skipMap_Field = AccessTools.DeclaredField(typeof(Layer), "_skipMap");

    internal bool ApplyMapOverride(GameLocation location, HashSet<string> appliedMapOverrides)
    {
        try
        {
            if (appliedMapOverrides.Contains(MapOverrideKey))
            {
                return true;
            }
            Map overrideMap = Game1.game1.xTileContent.Load<Map>(SourceMap);

            if (ForceTilesheetMatch)
            {
                foreach (TileSheet sheet in overrideMap.TileSheets)
                {
                    if (location.Map.GetTileSheet(sheet.Id) is TileSheet existingSheet)
                    {
                        sheet.ImageSource = existingSheet.ImageSource;
                    }
                }
            }

            Rectangle? refRect = RelTargetRect ?? TargetRect;
            if (refRect == null)
            {
                GetMapSize(overrideMap, out int oWidth, out int oHeight);
                refRect = new(0, 0, oWidth, oHeight);
            }
            if (ResizeMapIfNeeded && Layer_skipMap_Field != null)
            {
                GetMapSize(location.Map, out int mWidth, out int mHeight);
                int newWidth = Math.Max(mWidth, refRect.Value.X + refRect.Value.Width);
                int newHeight = Math.Max(mHeight, refRect.Value.Y + refRect.Value.Height);
                xTile.Dimensions.Size size = new(newWidth, newHeight);
                foreach (Layer layer in location.Map.Layers)
                {
                    layer.LayerSize = new(newWidth, newHeight);
                    Layer_skipMap_Field.SetValue(layer, null);
                }
            }

            if (TileRemoveRects != null)
            {
                foreach (MapOverrideRemoval removal in TileRemoveRects)
                {
                    if (string.IsNullOrEmpty(removal.Layer))
                    {
                        continue;
                    }

                    Regex layerRE = new(removal.Layer);
                    foreach (Layer layer in location.Map.Layers)
                    {
                        if (!(layerRE.Match(layer.Id)?.Success ?? false))
                        {
                            continue;
                        }
                        Rectangle tileArea = removal.TileArea;
                        if (RelTargetRect != null)
                        {
                            tileArea = new(
                                RelTargetRect.Value.X + tileArea.X,
                                RelTargetRect.Value.Y + tileArea.Y,
                                tileArea.Width,
                                tileArea.Height
                            );
                        }
                        foreach (Point pnt in CommonPatch.IterateBounds(tileArea))
                        {
                            location.removeMapTile(pnt.X, pnt.Y, layer.Id);
                        }
                    }
                }
            }

            location.ApplyMapOverride(
                overrideMap,
                MapOverrideKey,
                SourceRect,
                refRect,
                perTileCustomAction: ClearTargetRectOnApply ? location.cleanUpTileForMapOverride : null
            );

            if (refRect is Rectangle refRectV)
            {
                // water tiles recheck
                if (
                    LoadWaterTilesOnApply
                    ?? (
                        (
                            location.IsOutdoors
                            || location.HasMapPropertyWithValue("indoorWater")
                            || location is Sewer
                            || location is Submarine
                        ) && location is not Desert
                    )
                )
                {
                    DelayedAction.functionAfterDelay(() => RecheckWaterTiles(location, refRectV), 0);
                }

                if (LoadPathObjectsOnApply && location.Map.GetLayer("Paths") is not null)
                {
                    location.loadPathsLayerObjectsInArea(refRectV.X, refRectV.Y, refRectV.Width, refRectV.Height);
                }
                if (MapOverrideSpawnOnApply)
                {
                    DoMapOverrideSpawn(location, refRectV);
                }
            }

            // path objects recheck
            return true;
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to apply map override '{Id}':\n{err}", LogLevel.Error);
            return false;
        }
    }

    private enum GrassNameToIndex
    {
        springGrass = 1,
        caveGrass = 2,
        frostGrass = 3,
        lavaGrass = 4,
        caveGrass2 = 5,
        cobweb = 6,
        blueGrass = 7,
    }

    private static void DoMapOverrideSpawn(GameLocation location, Rectangle refRect)
    {
        GameStateQueryContext gsqContext = new(location, null, null, null, Utility.CreateDaySaveRandom());
        Layer backLayer = location.Map.RequireLayer("Back");
        for (int i = refRect.X; i < Math.Min(refRect.X + refRect.Width, backLayer.LayerWidth); i++)
        {
            for (int j = refRect.Y; j < Math.Min(refRect.Y + refRect.Height, backLayer.LayerHeight); j++)
            {
                if (
                    location.doesTileHaveProperty(i, j, $"{ModEntry.ModId}_MapOverrideSpawn", "Back")
                    is string mapOverrideSpawn
                )
                {
                    string[] args = ArgUtility.SplitBySpaceQuoteAware(mapOverrideSpawn);
                    if (!ArgUtility.TryGet(args, 0, out string spawnSettings, out string error))
                    {
                        ModEntry.Log(error, LogLevel.Error);
                        return;
                    }
                    string[] settings = spawnSettings.Split(":", 2);
                    if (
                        !ArgUtility.TryGetEnum(settings, 0, out MapOverrideSpawnKind spawnKind, out error)
                        || !ArgUtility.TryGetOptional(settings, 1, out string spawnGSQ, out error, defaultValue: null)
                    )
                    {
                        ModEntry.Log(error, LogLevel.Error);
                        return;
                    }
                    if (!GameStateQuery.CheckConditions(spawnGSQ, gsqContext))
                    {
                        return;
                    }
                    Vector2 targetTile = new(i, j);
                    switch (spawnKind)
                    {
                        case MapOverrideSpawnKind.Grass:
                            DoSpawnGrass(location, targetTile, args);
                            break;
                        case MapOverrideSpawnKind.Object:
                        case MapOverrideSpawnKind.Forage:
                            DoSpawnObject(location, targetTile, args, spawnKind == MapOverrideSpawnKind.Forage);
                            break;
                    }
                }
            }
        }

        static void DoSpawnGrass(GameLocation location, Vector2 targetTile, string[] args)
        {
            if (!ArgUtility.TryGetEnum(args, 1, out GrassNameToIndex grassId, out string error))
            {
                ModEntry.Log(error, LogLevel.Error);
                return;
            }
            foreach (ResourceClump resourceClump in location.resourceClumps)
            {
                if (resourceClump.getBoundingBox().Contains(targetTile))
                {
                    return;
                }
            }
            if (!location.terrainFeatures.ContainsKey(targetTile))
                location.terrainFeatures.Add(targetTile, new Grass((int)grassId, 3));
        }

        static void DoSpawnObject(GameLocation location, Vector2 targetTile, string[] args, bool isForage)
        {
            if (!ArgUtility.TryGet(args, 1, out string itemId, out string error))
            {
                ModEntry.Log(error, LogLevel.Error);
                return;
            }
            if (ItemRegistry.Create(itemId
#if !SDV17
                    , allowNull: false
#endif
                ) is not SObject obj)
            {
                ModEntry.Log($"{itemId} is not a valid object", LogLevel.Error);
                return;
            }
            if (isForage)
            {
                obj.IsSpawnedObject = true;
                location.dropObject(obj, targetTile * 64f, Game1.viewport, initialPlacement: true);
            }
            else
            {
                location.tryPlaceObject(targetTile, obj);
            }
        }
    }

    private static void RecheckWaterTiles(GameLocation location, Rectangle refRect)
    {
        Layer backLayer = location.Map.RequireLayer("Back");
        location.waterTiles ??= new WaterTiles(backLayer.LayerWidth, backLayer.LayerHeight);
        for (int i = refRect.X; i < Math.Min(refRect.X + refRect.Width, backLayer.LayerWidth); i++)
        {
            for (int j = refRect.Y; j < Math.Min(refRect.Y + refRect.Height, backLayer.LayerHeight); j++)
            {
                if (location.doesTileHaveProperty(i, j, "Water", "Back") is string waterProp)
                {
                    if (waterProp == "I")
                        location.waterTiles.waterTiles[i, j] = new WaterTiles.WaterTileData(
                            is_water: true,
                            is_visible: false
                        );
                    else
                        location.waterTiles[i, j] = true;
                }
                else
                {
                    location.waterTiles[i, j] = false;
                }
            }
        }
    }

    private static readonly FieldInfo HouseRenovation_name = AccessTools.DeclaredField(
        typeof(HouseRenovation),
        "_name"
    );
    private static readonly FieldInfo HouseRenovation_displayName = AccessTools.DeclaredField(
        typeof(HouseRenovation),
        "_displayName"
    );
    private static readonly FieldInfo HouseRenovation_description = AccessTools.DeclaredField(
        typeof(HouseRenovation),
        "_description"
    );

    public bool TryGetHouseRenovationEntry(GameLocation location, [NotNullWhen(true)] out HouseRenovation? houseReno)
    {
        houseReno = null;
        if (Renovation == null || !Renovation.CanHaveRenovation(location))
            return false;
        if (!Game1.game1.xTileContent.DoesAssetExist<Map>(SourceMap))
            return false;

        bool isRemove =
            MapOverride.TryGetModMapOverrides(location, out Dictionary<string, Point?>? mapOverrides)
            && mapOverrides.ContainsKey(Id);
        if (isRemove)
        {
            if (!GameStateQuery.CheckConditions(Renovation.RemoveCondition, location: location))
                return false;
            houseReno = new()
            {
                placementText = TokenParser.ParseText(Renovation.RemovePlacementText) ?? "?",
                animationType = HouseRenovation.AnimationType.Destroy,
            };
            HouseRenovation_displayName.SetValue(houseReno, TokenParser.ParseText(Renovation.RemoveDisplayName) ?? "?");
            HouseRenovation_description.SetValue(houseReno, TokenParser.ParseText(Renovation.RemoveDescription) ?? "?");
        }
        else
        {
            if (!GameStateQuery.CheckConditions(Renovation.AddCondition, location: location))
                return false;
            houseReno = new()
            {
                placementText = TokenParser.ParseText(Renovation.AddPlacementText) ?? "?",
                animationType = HouseRenovation.AnimationType.Build,
            };
            HouseRenovation_displayName.SetValue(houseReno, TokenParser.ParseText(Renovation.AddDisplayName) ?? "?");
            HouseRenovation_description.SetValue(houseReno, TokenParser.ParseText(Renovation.AddDescription) ?? "?");
        }
        HouseRenovation_name.SetValue(houseReno, Id);
        houseReno.location = location;
        houseReno.Price = Renovation.Price;
        houseReno.RoomId = Id;
        if (Renovation.TargetRectGroup != null && Renovation.TargetRectGroup.Any())
        {
            houseReno.AddRenovationBound(Renovation.TargetRectGroup);
        }
        else
        {
            Rectangle boundRect;
            if (TargetRect != null)
            {
                boundRect = TargetRect.Value;
            }
            else
            {
                Map overrideMap = Game1.game1.xTileContent.Load<Map>(SourceMap);
                boundRect = new(0, 0, (int)(overrideMap.DisplayWidth / 64f), (int)(overrideMap.DisplayHeight / 64f));
            }
            houseReno.AddRenovationBound(boundRect);
        }
        if (Renovation.CheckForObstructions)
            houseReno.validate = HouseRenovation.EnsureNoObstructions;
        houseReno.onRenovation = (reno, _) =>
        {
            if (
                !MapOverride.DoUpdateMapOverride(
                    reno.location,
                    [MapOverride.Action_UpdateMapOverride, "Here", isRemove ? "-" : "+", reno.RoomId],
                    Point.Zero,
                    out string? error
                )
            )
            {
                ModEntry.Log(error, LogLevel.Error);
            }
        };
        return true;
    }
}

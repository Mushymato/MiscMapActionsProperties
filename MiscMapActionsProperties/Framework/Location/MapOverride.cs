using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Extensions;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.TokenizableStrings;
using StardewValley.Triggers;
using xTile;
using xTile.Layers;
using xTile.Tiles;

namespace MiscMapActionsProperties.Framework.Location;

public sealed record UpdateMapOverrideRequest(string LocationName, Point Pnt, string[] Args);

public sealed record ApplyMapOverrideBroadcast(
    string LocationName,
    string MapAsset,
    string MapOverrides,
    bool ForceReload
);

public sealed class MapOverrideRenonvationData
{
    // def
    public string? TargetLocation { get; set; } = null;
    public int Price { get; set; } = 0;
    public string? AddCondition { get; set; } = null;
    public string? RemoveCondition { get; set; } = null;
    public List<Rectangle>? DisplayRects { get; set; } = null;

    // text
    public string? AddDisplayName { get; set; } = null;
    public string? AddDescription { get; set; } = null;
    public string? AddPlacementText { get; set; } = null;
    public string? RemoveDisplayName { get; set; } = null;
    public string? RemoveDescription { get; set; } = null;
    public string? RemovePlacementText { get; set; } = null;
}

public sealed class MapOverrideModel
{
    public string Id { get; set; } = null!;
    public string? RemovedById { get; set; } = null;
    public string SourceMap { get; set; } = "Maps/SkullCaveAltar";
    public Rectangle? SourceRect { get; set; } = null;
    public Rectangle? TargetRect { get; set; } = null;
    public bool TargetRectIsRelative { get; set; } = false;
    public int Precedence { get; set; } = 0;
    public bool ClearTargetRectOnApply { get; set; } = false;
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

            location.ApplyMapOverride(
                overrideMap,
                MapOverrideKey,
                SourceRect,
                refRect,
                perTileCustomAction: ClearTargetRectOnApply ? location.cleanUpTileForMapOverride : null
            );

            // water tiles recheck
            if (
                refRect != null
                && (
                    location.IsOutdoors
                    || location.HasMapPropertyWithValue("indoorWater")
                    || (
                        overrideMap.Properties.TryGetValue("indoorWater", out string mapOverrideIndoorWater)
                        && !string.IsNullOrEmpty(mapOverrideIndoorWater)
                    )
                    || location is Sewer
                    || location is Submarine
                )
                && location is not Desert
            )
            {
                DelayedAction.functionAfterDelay(() => RecheckWaterTiles(location, refRect.Value), 0);
            }
            return true;
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to apply map override '{Id}':\n{err}", LogLevel.Error);
            return false;
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
        if (Renovation == null || Renovation.TargetLocation != location.Name)
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
        if (Renovation.DisplayRects != null)
        {
            houseReno.AddRenovationBound(Renovation.DisplayRects);
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
        houseReno.validate = HouseRenovation.EnsureNoObstructions;
        houseReno.onRenovation = (reno, _) =>
        {
            ModEntry.Log("houseReno.onRenovation");
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

internal static class MapOverride
{
    private const string Asset_MapOverride = $"{ModEntry.ModId}/MapOverrides";
    private const string ModData_MapOverrides = $"{ModEntry.ModId}/MapOverrides";
    private const char Ctrl_SEP = ',';
    private const char Ctrl_ADD = '+';
    private const char Ctrl_RMV = '-';
    internal const char Ctrl_SEP_RelCoord = '@';
    internal const char Ctrl_SEP_RelCoordXY = '.';
    private const string Ctrl_RemoveAll = "RemoveAll";
    internal static char[] ILLEGAL_CHARS = [Ctrl_SEP, Ctrl_SEP_RelCoord, Ctrl_ADD, Ctrl_RMV];
    internal const string Action_UpdateMapOverride = $"{ModEntry.ModId}_UpdateMapOverride";
    internal const string Action_ShowRenovations = $"{ModEntry.ModId}_ShowRenovations";
    private const string GSQ_HAS_MAP_OVERRIDE = $"{ModEntry.ModId}_HAS_MAP_OVERRIDE";
    private const string MP_UpdateMapOverride_Request = "UpdateMapOverride_Request";
    private const string MP_UpdateMapOverride_Broadcast = "UpdateMapOverride_Broadcast";

    private static readonly FieldInfo? _appliedMapOverridesField = AccessTools.DeclaredField(
        typeof(GameLocation),
        "_appliedMapOverrides"
    );

    internal static void Register()
    {
        if (_appliedMapOverridesField == null)
        {
            ModEntry.Log($"Failed to reflect '_appliedMapOverrides', '{Asset_MapOverride}' feature disabled.");
            return;
        }
        try
        {
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.MakeMapModifications)),
                prefix: new HarmonyMethod(typeof(MapOverride), nameof(GameLocation_MakeMapModifications_Prefix))
                {
                    priority = Priority.First,
                },
                postfix: new HarmonyMethod(typeof(MapOverride), nameof(GameLocation_MakeMapModifications_Postfix))
                {
                    priority = Priority.Last,
                }
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch MapOverride:\n{err}", LogLevel.Error);
        }

        ModEntry.help.Events.Content.AssetRequested += OnAssetRequested;
        ModEntry.help.Events.Content.AssetsInvalidated += OnAssetInvalidated;
        ModEntry.help.Events.Multiplayer.ModMessageReceived += OnModMessageReceived;
        ModEntry.help.Events.Player.Warped += OnWarped;

        TriggerActionManager.RegisterAction(Action_UpdateMapOverride, TriggerUpdateMapOverride);
        CommonPatch.RegisterTileAndTouch(Action_UpdateMapOverride, TileUpdateMapOverride);
        GameStateQuery.Register(GSQ_HAS_MAP_OVERRIDE, HAS_MAP_OVERRIDE);

        TriggerActionManager.RegisterAction(Action_ShowRenovations, TriggerShowRenovations);
        CommonPatch.RegisterTileAndTouch(Action_ShowRenovations, TileShowRenovations);
    }

    private static bool TileShowRenovations(GameLocation location, string[] args, Farmer farmer, Point point)
    {
        if (!ShowRenovations(args, location, out string? error))
        {
            ModEntry.Log(error, LogLevel.Error);
            return false;
        }
        return true;
    }

    private static bool TriggerShowRenovations(string[] args, TriggerActionContext context, out string? error)
    {
        return ShowRenovations(args, Game1.currentLocation, out error);
    }

    private static bool ShowRenovations(string[] args, GameLocation location, [NotNullWhen(false)] out string? error)
    {
        if (!ArgUtility.TryGet(args, 1, out string? locationName, out error))
        {
            return false;
        }
        if (locationName != "Here")
            location = Game1.getLocationFromName(locationName);
        if (location == null)
        {
            error = $"'{locationName}' is not a valid location";
            return false;
        }

        List<ISalable> renovations = [];
        foreach (MapOverrideModel model in MapOverrideData.Values)
        {
            if (model.TryGetHouseRenovationEntry(location, out HouseRenovation? houseReno))
            {
                renovations.Add(houseReno);
            }
        }

        if (!renovations.Any())
        {
            error = $"No renovations for '{location.Name}' ({location.NameOrUniqueName})";
            return false;
        }

        Game1.activeClickableMenu = new ShopMenu(
            Action_ShowRenovations,
            renovations,
            0,
            null,
            HouseRenovation.OnPurchaseRenovation
        )
        {
            purchaseSound = null,
        };
        error = null;
        return true;
    }

    private static bool HAS_MAP_OVERRIDE(string[] query, GameStateQueryContext context)
    {
        if (
            !ArgUtility.TryGet(query, 1, out string? locationName, out string? error, name: "string locationName")
            || !ArgUtility.TryGet(query, 2, out string? mapOverrideId, out error, name: "string mapOverrideId")
        )
        {
            ModEntry.Log(error, LogLevel.Error);
            return false;
        }
        GameLocation location = GameStateQuery.Helpers.GetLocation(locationName, Game1.currentLocation);
        if (!TryGetModMapOverrides(location, out Dictionary<string, Point?>? mapOverrides))
        {
            return false;
        }
        return mapOverrides.ContainsKey(mapOverrideId);
    }

    internal static bool TryGetModMapOverrides(
        GameLocation location,
        [NotNullWhen(true)] out Dictionary<string, Point?>? mapOverrides
    )
    {
        mapOverrides = null;
        if (!location.modData.TryGetValue(ModData_MapOverrides, out string mapOverridesStr))
        {
            return false;
        }
        mapOverrides = [];
        foreach (string part in mapOverridesStr.Split(Ctrl_SEP))
        {
            string[] subparts = part.Split(Ctrl_SEP_RelCoord);
            if (
                subparts.Length > 1
                && ArgUtility.TryGetPoint(subparts[1].Split(Ctrl_SEP_RelCoordXY), 0, out Point relPoint, out _)
            )
            {
                mapOverrides[subparts[0]] = relPoint;
            }
            else
            {
                mapOverrides[subparts[0]] = null;
            }
        }
        return mapOverrides.Any();
    }

    private static string UpdateModMapOverrides(
        GameLocation location,
        IEnumerable<string> mapOverrides,
        [CallerMemberName] string? caller = null
    )
    {
        if (mapOverrides.Any())
        {
            string joined = string.Join(Ctrl_SEP, mapOverrides);
            ModEntry.Log($"{caller}.UpdateModMapOverrides({location.NameOrUniqueName}): '{joined}'");
            location.modData[ModData_MapOverrides] = joined;
            return joined;
        }
        else
        {
            ModEntry.Log($"{caller}.UpdateModMapOverrides({location.NameOrUniqueName}): <empty>");
            location.modData.Remove(ModData_MapOverrides);
            return "";
        }
    }

    private static void GameLocation_MakeMapModifications_Prefix(
        GameLocation __instance,
        bool force,
        ref HashSet<string> ____appliedMapOverrides,
        ref List<MapOverrideModel>? __state
    )
    {
        __state = null;
        if (!TryGetModMapOverrides(__instance, out Dictionary<string, Point?>? mapOverrides))
        {
            return;
        }
        List<MapOverrideModel>? validOverrideModels = [];
        HashSet<string> validOverrideIds = [];
        foreach ((string mapOverrideId, Point? relPoint) in mapOverrides)
        {
            if (!MapOverrideData.TryGetValue(mapOverrideId, out MapOverrideModel? model))
            {
                continue;
            }
            if (!Game1.game1.xTileContent.DoesAssetExist<Map>(model.SourceMap))
            {
                continue;
            }
            model.UpdateRelTargetRect(relPoint);
            if (validOverrideIds.Add(model.Id))
            {
                validOverrideModels.Add(model);
            }
        }
        if (validOverrideModels.Count > 0)
        {
            validOverrideModels.Sort(
                (modelA, modelB) =>
                {
                    if (modelA.Precedence != modelB.Precedence)
                        return modelA.Precedence.CompareTo(modelB.Precedence);
                    return 0;
                }
            );
            if (force)
            {
                ____appliedMapOverrides.Clear();
            }
            foreach (MapOverrideModel model in validOverrideModels)
            {
                if (model.Precedence < 0)
                {
                    model.ApplyMapOverride(__instance, ____appliedMapOverrides);
                }
            }
            __state = validOverrideModels;
        }
        UpdateModMapOverrides(__instance, validOverrideModels.Select(model => model.StoredId));
    }

    private static void GameLocation_MakeMapModifications_Postfix(
        GameLocation __instance,
        bool force,
        ref HashSet<string> ____appliedMapOverrides,
        ref List<MapOverrideModel>? __state
    )
    {
        if (__state?.Count > 0)
        {
            foreach (MapOverrideModel model in __state)
            {
                if (model.Precedence < 0)
                {
                    // must re-add the keys here because vanilla would have cleared them
                    if (force)
                        ____appliedMapOverrides.Add(model.MapOverrideKey);
                }
                else
                {
                    model.ApplyMapOverride(__instance, ____appliedMapOverrides);
                }
            }
        }
    }

    private static Dictionary<string, MapOverrideModel>? _mapOverrideData = null;

    internal static Dictionary<string, MapOverrideModel> MapOverrideData
    {
        get
        {
            _mapOverrideData ??= Game1.content.Load<Dictionary<string, MapOverrideModel>>(Asset_MapOverride);
            HashSet<string> invalid = [];
            // pass 1 check illegal chars
            foreach ((string id, MapOverrideModel model) in _mapOverrideData)
            {
                foreach (char illegal in ILLEGAL_CHARS)
                {
                    if (id.Contains(illegal))
                    {
                        ModEntry.Log($"Cannot use '{illegal}' in '{Asset_MapOverride}' key", LogLevel.Error);
                        invalid.Add(id);
                        continue;
                    }
                }
                model.Id = id;
                if (model.RemovedById != null)
                {
                    if (
                        !invalid.Contains(model.RemovedById)
                        && _mapOverrideData.TryGetValue(model.RemovedById, out MapOverrideModel? removeModel)
                    )
                    {
                        removeModel.RemovedById = model.Id;
                        removeModel.Precedence = model.Precedence;
                    }
                    else
                    {
                        ModEntry.Log(
                            $"Override with RemovedById '{model.RemovedById}' which does not refer to valid model",
                            LogLevel.Error
                        );
                        invalid.Add(id);
                    }
                }
                if (model.SourceRect == null && model.TargetRect != null)
                {
                    model.SourceRect = new(0, 0, model.TargetRect.Value.Width, model.TargetRect.Value.Height);
                }
            }
            _mapOverrideData.RemoveWhere(kv => invalid.Contains(kv.Key));
            return _mapOverrideData;
        }
    }

    private static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.Name.IsEquivalentTo(Asset_MapOverride))
            e.LoadFrom(() => new Dictionary<string, MapOverrideModel>(), AssetLoadPriority.Exclusive);
    }

    private static void OnAssetInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        if (e.NamesWithoutLocale.Any(an => an.IsEquivalentTo(Asset_MapOverride)))
            _mapOverrideData = null;
    }

    private static void OnWarped(object? sender, WarpedEventArgs e)
    {
        if (e.NewLocation.modData.TryGetValue(ModData_MapOverrides, out string mapOverrides))
        {
            ModEntry.Log($"{e.NewLocation.NameOrUniqueName}[{ModData_MapOverrides}]: {mapOverrides}");
        }
    }

    private static void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
    {
        if (e.FromModID != ModEntry.ModId || e.FromPlayerID == Game1.player.UniqueMultiplayerID)
        {
            return;
        }

        if (e.Type == MP_UpdateMapOverride_Request && Context.IsMainPlayer)
        {
            ModEntry.Log($"Perform {Action_UpdateMapOverride} for {e.FromPlayerID}");
            UpdateMapOverrideRequest request = e.ReadAs<UpdateMapOverrideRequest>();
            GameLocation targetLocation = Game1.getLocationFromName(request.LocationName);
            if (!DoUpdateMapOverride(targetLocation, request.Args, request.Pnt, out string? error))
            {
                ModEntry.Log(error, LogLevel.Error);
            }
            return;
        }

        if (e.Type == MP_UpdateMapOverride_Broadcast)
        {
            ApplyMapOverrideBroadcast broadcast = e.ReadAs<ApplyMapOverrideBroadcast>();
            if (Game1.currentLocation is GameLocation location && location.NameOrUniqueName == broadcast.LocationName)
            {
                location.InvalidateCachedMultiplayerMap(Game1.Multiplayer.cachedMultiplayerMaps);
                if (broadcast.ForceReload)
                {
                    ModEntry.Log($"Require force reload for reorder");
                    ModEntry.help.GameContent.InvalidateCache(location.mapPath.Value);
                    location.loadMap(location.mapPath.Value, true);
                }
                location.modData[ModData_MapOverrides] = broadcast.MapOverrides;
                location.MakeMapModifications(broadcast.ForceReload);
                location.StoreCachedMultiplayerMap(Game1.Multiplayer.cachedMultiplayerMaps);
            }
            else if (broadcast.ForceReload)
            {
                ModEntry.Log($"Invalidate map {broadcast.MapAsset} for {broadcast.LocationName}");
                ModEntry.help.GameContent.InvalidateCache(broadcast.MapAsset);
                Game1.Multiplayer.cachedMultiplayerMaps.Remove(broadcast.LocationName);
            }
        }
    }

    private static bool TileUpdateMapOverride(GameLocation location, string[] args, Farmer who, Point point)
    {
        if (DoUpdateMapOverride(Game1.currentLocation, args, point, out string? error))
            return true;
        ModEntry.Log(error, LogLevel.Error);
        return false;
    }

    private static bool TriggerUpdateMapOverride(string[] args, TriggerActionContext context, out string? error)
    {
        return DoUpdateMapOverride(Game1.currentLocation, args, Game1.player.TilePoint, out error);
    }

    internal static bool DoUpdateMapOverride(
        GameLocation location,
        string[] args,
        Point point,
        [NotNullWhen(false)] out string? error
    )
    {
        if (!Context.IsMainPlayer)
        {
            error = null;
            ModEntry.help.Multiplayer.SendMessage<UpdateMapOverrideRequest>(
                new(location.NameOrUniqueName, point, args),
                MP_UpdateMapOverride_Request,
                [ModEntry.ModId],
                [Game1.serverHost.Value.UniqueMultiplayerID]
            );
            return true;
        }

        if (ArgUtility.TryGet(args, 1, out string? locationName, out error, name: "string locationName"))
        {
            location = GameStateQuery.Helpers.GetLocation(locationName, location);
        }

        if (location == null || location.Map == null)
        {
            error = "Location map is null";
            return false;
        }

        Dictionary<string, (MapOverrideModel, Point?)> mapOverrides = [];
        int maxPrecedence = 0;
        bool needForcedReload = false;
        bool hasChanged = false;

        if (TryGetModMapOverrides(location, out Dictionary<string, Point?>? mapOverridesArray))
        {
            mapOverrides = [];
            foreach ((string mapOverrideId, Point? relPoint) in mapOverridesArray)
            {
                if (!MapOverrideData.TryGetValue(mapOverrideId, out MapOverrideModel? model))
                {
                    hasChanged = true;
                    continue;
                }
                maxPrecedence = Math.Max(maxPrecedence, model.Precedence);
                mapOverrides[mapOverrideId] = (model, relPoint);
            }
        }

        List<(char, string)> ArgList = [];
        bool isRemoveAll = false;
        if (
            ArgUtility.TryGetOptional(args, 2, out string removeAll, out error, name: "string removeAll")
            && removeAll.EqualsIgnoreCase(Ctrl_RemoveAll)
        )
        {
            isRemoveAll = true;
            ModEntry.Log("Will remove all map overrides");
            foreach (string key in mapOverrides.Keys)
            {
                ArgList.Add((Ctrl_RMV, key));
            }
        }
        else
        {
            for (int i = 2; i < args.Length - 1; i += 2)
            {
                char mode = args[i][0];
                string mapOverrideId = args[i + 1];
                ArgList.Add((mode, mapOverrideId));
            }
        }

        HashSet<string> _appliedMapOverrides = (HashSet<string>)_appliedMapOverridesField!.GetValue(location)!;
        foreach ((char mode, string mapOverrideId) in ArgList)
        {
            if (!MapOverrideData.TryGetValue(mapOverrideId, out MapOverrideModel? model))
            {
                error = $"Map override id '{mapOverrideId}' not found";
                return false;
            }
            if (!Game1.game1.xTileContent.DoesAssetExist<Map>(model.SourceMap))
            {
                error = $"Map override asset '{model.SourceMap}' from '{model.Id}' not found";
                return false;
            }

            switch (mode)
            {
                case Ctrl_ADD:
                    if (!mapOverrides.ContainsKey(model.Id))
                    {
                        hasChanged = true;
                        needForcedReload = needForcedReload || maxPrecedence > model.Precedence;
                        model.UpdateRelTargetRect(point);
                        if (model.RemovedById != null)
                        {
                            // RemovedById: need to clear the the "remove" map override
                            // Does not actually attempt to remove it though, trusts that the map does what it says
                            if (mapOverrides.TryGetValue(model.RemovedById, out (MapOverrideModel, Point?) prevRmvData))
                            {
                                mapOverrides.Remove(model.RemovedById);
                                _appliedMapOverrides.Remove(prevRmvData.Item1.MapOverrideKey);
                                prevRmvData.Item1.UpdateRelTargetRect(null);
                            }
                        }
                        mapOverrides[model.Id] = (model, point);
                    }
                    break;
                case Ctrl_RMV:
                    if (mapOverrides.TryGetValue(model.Id, out (MapOverrideModel, Point?) prevData))
                    {
                        hasChanged = true;
                        if (
                            !isRemoveAll
                            && model.RemovedById != null
                            && !mapOverrides.ContainsKey(model.RemovedById)
                            && MapOverrideData.TryGetValue(model.RemovedById, out MapOverrideModel? removeModel)
                        )
                        {
                            // RemovedById: remove by applying a different model
                            needForcedReload = needForcedReload || maxPrecedence > removeModel.Precedence;
                            removeModel.UpdateRelTargetRect(prevData.Item2);
                            mapOverrides[model.RemovedById] = (removeModel, prevData.Item2);
                        }
                        else
                        {
                            needForcedReload = true;
                        }
                        model.UpdateRelTargetRect(null);
                        mapOverrides.Remove(model.Id);
                        _appliedMapOverrides.Remove(model.MapOverrideKey);
                    }
                    break;
            }
        }

        if (!hasChanged)
            return true;

        string updatedOverrides = UpdateModMapOverrides(
            location,
            mapOverrides.Values.Select(modelData => modelData.Item1.StoredId)
        );

        location.InvalidateCachedMultiplayerMap(Game1.Multiplayer.cachedMultiplayerMaps);
        if (needForcedReload)
        {
            ModEntry.Log($"Require force reload for reorder");
            location.loadMap(location.mapPath.Value, true);
        }
        location.MakeMapModifications(needForcedReload);
        location.StoreCachedMultiplayerMap(Game1.Multiplayer.cachedMultiplayerMaps);

        long[] otherPlayers = Game1
            .getOnlineFarmers()
            .Where(farmer => farmer.UniqueMultiplayerID != Game1.player.UniqueMultiplayerID)
            .Select(farmer => farmer.UniqueMultiplayerID)
            .ToArray();
        if (otherPlayers.Length > 0)
        {
            ModEntry.Log($"otherPlayers: {string.Join(',', otherPlayers)}");
            ModEntry.help.Multiplayer.SendMessage(
                new ApplyMapOverrideBroadcast(
                    location.NameOrUniqueName,
                    location.mapPath.Value,
                    updatedOverrides,
                    needForcedReload
                ),
                MP_UpdateMapOverride_Broadcast,
                [ModEntry.ModId],
                otherPlayers
            );
        }
        return true;
    }
}

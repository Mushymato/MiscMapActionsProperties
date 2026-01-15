using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Extensions;
using StardewValley.Triggers;
using xTile;

namespace MiscMapActionsProperties.Framework.Location;

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

    private string? mapOverrideKey = null;
    internal string MapOverrideKey => mapOverrideKey ??= $"{ModEntry.ModId}+MapOverride/{Id}";

    internal string StoredId { get; private set; } = string.Empty;

    private Rectangle? RelTargetRect = null;

    internal void UpdateRelTargetRect(Point? relPoint)
    {
        if (relPoint is Point relPointV && TargetRectIsRelative && TargetRect is Rectangle targetRect)
        {
            StoredId = string.Concat(Id, MapOverride.Ctrl_SEP_RelCoord, relPointV.X, ' ', relPointV.Y);
            RelTargetRect = new(
                targetRect.X + relPointV.X,
                targetRect.Y + relPointV.Y,
                targetRect.Width,
                targetRect.Height
            );
            ModEntry.Log($"RelTargetRect {RelTargetRect}");
        }
        else
        {
            StoredId = Id;
            RelTargetRect = null;
        }
    }

    internal bool ApplyMapOverride(GameLocation location, HashSet<string> appliedMapOverrides)
    {
        try
        {
            if (!appliedMapOverrides.Contains(MapOverrideKey))
            {
                location.ApplyMapOverride(
                    Game1.game1.xTileContent.Load<Map>(SourceMap),
                    MapOverrideKey,
                    SourceRect,
                    RelTargetRect ?? TargetRect,
                    perTileCustomAction: ClearTargetRectOnApply ? location.cleanUpTileForMapOverride : null
                );
            }
            return true;
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to apply map override '{Id}':\n{err}", LogLevel.Error);
            return false;
        }
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
    private const string Ctrl_RemoveAll = "RemoveAll";
    internal static char[] ILLEGAL_CHARS = [Ctrl_SEP, Ctrl_SEP_RelCoord, Ctrl_ADD, Ctrl_RMV];
    private const string Action_UpdateMapOverride = $"{ModEntry.ModId}_UpdateMapOverride";
    private const string GSQ_M = $"{ModEntry.ModId}_UpdateMapOverride";
    private const string MP_UpdateMapOverride = "UpdateMapOverride";
    private const string MP_UpdateMapOverride_ReloadMap = "UpdateMapOverride_ReloadMap";

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

        ModEntry.help.Events.Content.AssetRequested += OnAssetRequested;
        ModEntry.help.Events.Content.AssetsInvalidated += OnAssetInvalidated;
        ModEntry.help.Events.Multiplayer.ModMessageReceived += OnModMessageReceived;

        TriggerActionManager.RegisterAction(Action_UpdateMapOverride, TriggerUpdateMapOverride);
        CommonPatch.RegisterTileAndTouch(Action_UpdateMapOverride, TileUpdateMapOverride);
    }

    private static bool TryGetModMapOverrides(
        GameLocation location,
        [NotNullWhen(true)] out List<(string, Point?)>? mapOverrides
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
            if (subparts.Length > 1 && ArgUtility.TryGetPoint(subparts[1].Split(' '), 0, out Point relPoint, out _))
            {
                mapOverrides.Add(new(subparts[0], relPoint));
            }
            else
            {
                mapOverrides.Add(new(subparts[0], null));
            }
        }
        return mapOverrides.Any();
    }

    private static string UpdateModMapOverrides(GameLocation location, IEnumerable<string> mapOverrides)
    {
        if (mapOverrides.Any())
        {
            string joined = string.Join(Ctrl_SEP, mapOverrides);
            ModEntry.Log($"UpdateModMapOverrides({location.NameOrUniqueName}): '{joined}'");
            location.modData[ModData_MapOverrides] = joined;
            return joined;
        }
        else
        {
            ModEntry.Log($"UpdateModMapOverrides({location.NameOrUniqueName}): ''");
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
        if (!TryGetModMapOverrides(__instance, out List<(string, Point?)>? mapOverrides))
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
                    return modelA.Id.CompareTo(modelB.Id);
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

    private static void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
    {
        if (e.FromModID != ModEntry.ModId || e.FromPlayerID == Game1.player.UniqueMultiplayerID)
        {
            return;
        }
        if (
            Game1.currentLocation == null
            || Game1.currentLocation.mapPath.Value == null
            || Game1.currentLocation.Map == null
        )
        {
            return;
        }

        if (e.Type == MP_UpdateMapOverride_ReloadMap)
        {
            Game1.currentLocation.loadMap(Game1.currentLocation.mapPath.Value, true);
        }
        else if (e.Type == MP_UpdateMapOverride)
        {
            Game1.currentLocation.InvalidateCachedMultiplayerMap(Game1.Multiplayer.cachedMultiplayerMaps);
        }
        else
        {
            return;
        }
        // needed because modData updates too slow
        Game1.currentLocation.modData[ModData_MapOverrides] = e.ReadAs<string>();
        Game1.currentLocation.MakeMapModifications();
    }

    private static bool TileUpdateMapOverride(GameLocation location, string[] args, Farmer who, Point point)
    {
        if (DoUpdateMapOverride(Game1.currentLocation, args, point, out string error))
            return true;
        ModEntry.Log(error, LogLevel.Error);
        return false;
    }

    private static bool TriggerUpdateMapOverride(string[] args, TriggerActionContext context, out string error)
    {
        return DoUpdateMapOverride(Game1.currentLocation, args, Game1.player.TilePoint, out error);
    }

    private static bool DoUpdateMapOverride(GameLocation location, string[] args, Point point, out string error)
    {
        ModEntry.Log($"DoUpdateMapOverride {point}");
        if (location == null || location.Map == null)
        {
            error = "Location map is null";
            return false;
        }

        if (ArgUtility.TryGet(args, 1, out string locationName, out error, name: "string locationName"))
        {
            location = GameStateQuery.Helpers.GetLocation(locationName, location);
        }

        Dictionary<string, (MapOverrideModel, Point?)> mapOverrides = [];
        int maxPrecedence = 0;
        string maxId = string.Empty;
        bool needForcedReload = false;
        bool hasChanged = false;

        if (TryGetModMapOverrides(location, out List<(string, Point?)>? mapOverridesArray))
        {
            mapOverrides = [];
            foreach ((string mapOverrideId, Point? relPoint) in mapOverridesArray)
            {
                if (!MapOverrideData.TryGetValue(mapOverrideId, out MapOverrideModel? model))
                {
                    hasChanged = true;
                    needForcedReload = true;
                    continue;
                }
                maxPrecedence = Math.Max(maxPrecedence, model.Precedence);
                maxId = maxId.CompareTo(model.Id) > 0 ? model.Id : maxId;
                mapOverrides[mapOverrideId] = (model, relPoint);
            }
        }

        List<(char, string)> ArgList = [];
        if (
            ArgUtility.TryGetOptional(args, 2, out string removeAll, out error, name: "string removeAll")
            && removeAll.EqualsIgnoreCase(Ctrl_RemoveAll)
        )
        {
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
        for (int i = 2; i < args.Length - 1; i += 2)
        {
            char mode = args[i][0];
            string mapOverrideId = args[i + 1];

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
                        needForcedReload =
                            needForcedReload
                            || maxPrecedence > model.Precedence
                            || maxPrecedence == model.Precedence && maxId.CompareTo(model.Id) > 0;
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
                    }
                    break;
                case Ctrl_RMV:
                    if (mapOverrides.TryGetValue(model.Id, out (MapOverrideModel, Point?) prevData))
                    {
                        hasChanged = true;
                        if (
                            model.RemovedById != null
                            && !mapOverrides.ContainsKey(model.RemovedById)
                            && MapOverrideData.TryGetValue(model.RemovedById, out MapOverrideModel? removeModel)
                        )
                        {
                            // RemovedById: remove by applying a different model
                            needForcedReload =
                                needForcedReload
                                || maxPrecedence > removeModel.Precedence
                                || maxPrecedence == removeModel.Precedence && maxId.CompareTo(removeModel.Id) > 0;
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
        if (Game1.currentLocation == location)
        {
            if (needForcedReload)
                location.loadMap(location.mapPath.Value, true);
            location.MakeMapModifications(needForcedReload);
        }

        long[] playersInTargetLocation = Game1
            .getOnlineFarmers()
            .Where(farmer =>
                farmer.currentLocation != null && farmer.currentLocation.NameOrUniqueName == location.NameOrUniqueName
            )
            .Select(farmer => farmer.UniqueMultiplayerID)
            .ToArray();
        ModEntry.Log($"playersInTargetLocation: {string.Join(',', playersInTargetLocation)}");
        if (playersInTargetLocation.Length > 0)
        {
            ModEntry.help.Multiplayer.SendMessage(
                updatedOverrides,
                needForcedReload ? MP_UpdateMapOverride_ReloadMap : MP_UpdateMapOverride,
                [ModEntry.ModId],
                playersInTargetLocation
            );
        }
        return true;
    }
}

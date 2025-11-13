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
    public string MapOverrideAsset { get; set; } = "SkullCaveAltar";
    public Rectangle? SourceRect { get; set; } = null;
    public Rectangle? TargetRect { get; set; } = null;
    public int Precedence { get; set; } = 0;

    private string? mapOverrideKey = null;
    internal string MapOverrideKey => mapOverrideKey ??= $"{ModEntry.ModId}+MapOverride/{Id}";
}

internal static class MapOverride
{
    private const string Asset_MapOverride = $"{ModEntry.ModId}/MapOverrides";
    private const string ModData_MapOverrides = $"{ModEntry.ModId}/MapOverrides";
    private const char Ctrl_SEP = ',';
    private const char Ctrl_ADD = '+';
    private const char Ctrl_RMV = '-';
    internal static char[] ILLEGAL_CHARS = [Ctrl_SEP, Ctrl_ADD, Ctrl_RMV];
    private const string Action_UpdateMapOverride = $"{ModEntry.ModId}_UpdateMapOverride";
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

    private static bool TryGetModMapOverrides(GameLocation location, [NotNullWhen(true)] out string[]? mapOverrides)
    {
        mapOverrides = null;
        if (!location.modData.TryGetValue(ModData_MapOverrides, out string mapOverridesStr))
        {
            return false;
        }
        mapOverrides = mapOverridesStr.Split(Ctrl_SEP);
        return mapOverrides.Any();
    }

    private static string UpdateModMapOverrides(GameLocation location, HashSet<string> mapOverrides)
    {
        if (mapOverrides.Count > 0)
        {
            string joined = string.Join(Ctrl_SEP, mapOverrides);
            location.modData[ModData_MapOverrides] = joined;
            return joined;
        }
        else
        {
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
        if (!TryGetModMapOverrides(__instance, out string[]? mapOverrides))
        {
            return;
        }
        List<MapOverrideModel>? validOverrideModels = [];
        HashSet<string> validOverrideIds = [];
        foreach (string mapOverrideId in mapOverrides)
        {
            if (!MapOverrideData.TryGetValue(mapOverrideId, out MapOverrideModel? model))
            {
                continue;
            }
            if (!Game1.game1.xTileContent.DoesAssetExist<Map>("Maps\\" + model.MapOverrideAsset))
            {
                continue;
            }
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
                    __instance.ApplyMapOverride(
                        model.MapOverrideAsset,
                        model.MapOverrideKey,
                        model.SourceRect,
                        model.TargetRect
                    );
                }
            }
            __state = validOverrideModels;
        }
        UpdateModMapOverrides(__instance, validOverrideIds);
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
                    if (force)
                        ____appliedMapOverrides.Add(model.MapOverrideKey);
                }
                else
                {
                    __instance.ApplyMapOverride(
                        model.MapOverrideAsset,
                        model.MapOverrideKey,
                        model.SourceRect,
                        model.TargetRect
                    );
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
            || !Game1.currentLocation.farmers.Any(farmer => farmer.UniqueMultiplayerID == e.FromPlayerID)
        )
        {
            return;
        }
        if (e.Type == MP_UpdateMapOverride_ReloadMap)
        {
            Game1.currentLocation.loadMap(Game1.currentLocation.mapPath.Value, true);
        }
        else if (e.Type != MP_UpdateMapOverride)
        {
            return;
        }
        // needed because modData updates too slow
        Game1.currentLocation.modData[ModData_MapOverrides] = e.ReadAs<string>();
        Game1.currentLocation.MakeMapModifications();
    }

    private static bool TileUpdateMapOverride(GameLocation location, string[] args, Farmer who, Point point)
    {
        if (DoUpdateMapOverride(Game1.currentLocation, args, who, out string error))
            return true;
        ModEntry.Log(error, LogLevel.Error);
        return false;
    }

    private static bool TriggerUpdateMapOverride(string[] args, TriggerActionContext context, out string error)
    {
        return DoUpdateMapOverride(Game1.currentLocation, args, Game1.player, out error);
    }

    private static bool DoUpdateMapOverride(GameLocation location, string[] args, Farmer who, out string error)
    {
        if (location == null || location.Map == null)
        {
            error = "Location map is null";
            return false;
        }

        if (ArgUtility.TryGet(args, 1, out string locationName, out error, name: "string locationName"))
        {
            location = GameStateQuery.Helpers.GetLocation(locationName, location);
        }

        HashSet<string> mapOverrides;
        int maxPrecedence = 0;
        string maxId = string.Empty;
        if (TryGetModMapOverrides(location, out string[]? mapOverridesArray))
        {
            mapOverrides = mapOverridesArray.ToHashSet();
            maxPrecedence = mapOverrides
                .Select(mapOverrideId =>
                {
                    if (!MapOverrideData.TryGetValue(mapOverrideId, out MapOverrideModel? model))
                    {
                        return 0;
                    }
                    return model.Precedence;
                })
                .Max();
            maxId =
                mapOverrides
                    .Select(mapOverrideId =>
                    {
                        if (!MapOverrideData.TryGetValue(mapOverrideId, out MapOverrideModel? model))
                        {
                            return null;
                        }
                        return model.Id;
                    })
                    .Max() ?? "";
        }
        else
        {
            mapOverrides = [];
        }

        bool needForcedReload = false;
        bool hasChanged = false;
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
            if (!Game1.game1.xTileContent.DoesAssetExist<Map>("Maps\\" + model.MapOverrideAsset))
            {
                error = $"Map override asset 'Maps/{model.MapOverrideAsset}' from '{model.Id}' not found";
                return false;
            }

            switch (mode)
            {
                case Ctrl_ADD:
                    if (mapOverrides.Add(model.Id))
                    {
                        hasChanged = true;
                        needForcedReload =
                            needForcedReload
                            || maxPrecedence > model.Precedence
                            || maxPrecedence == model.Precedence && maxId.CompareTo(model.Id) > 0;
                    }
                    break;
                case Ctrl_RMV:
                    if (mapOverrides.Remove(model.Id))
                    {
                        hasChanged = true;
                        needForcedReload = true;
                        _appliedMapOverrides.Remove(model.MapOverrideKey);
                    }
                    break;
            }
        }

        if (!hasChanged)
            return true;

        string updated = UpdateModMapOverrides(location, mapOverrides);

        if (location != Game1.currentLocation)
            return true;

        if (needForcedReload)
            location.loadMap(location.mapPath.Value, true);
        location.MakeMapModifications(needForcedReload);

        long[] playersInSameLocation = location
            .farmers.Where(farmer => farmer.UniqueMultiplayerID != who.UniqueMultiplayerID)
            .Select(farmer => farmer.UniqueMultiplayerID)
            .ToArray();
        if (playersInSameLocation.Length > 0)
        {
            ModEntry.help.Multiplayer.SendMessage(
                needForcedReload ? MP_UpdateMapOverride_ReloadMap : MP_UpdateMapOverride,
                args[0],
                [ModEntry.ModId],
                playersInSameLocation
            );
        }
        return true;
    }
}

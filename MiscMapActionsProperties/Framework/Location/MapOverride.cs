using System.Diagnostics.CodeAnalysis;
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

    private string? mapOverrideKey = null;
    internal string MapOverrideKey => mapOverrideKey ??= $"{MapOverride.ModMapOverridePrefix}{Id}";
}

internal static class MapOverride
{
    internal const string Asset_MapOverride = $"{ModEntry.ModId}/MapOverrides";
    internal const string ModMapOverridePrefix = $"{ModEntry.ModId}+MapOverride/";
    internal const string ModData_MapOverrides = $"{ModEntry.ModId}/MapOverrides";
    internal const char ModData_MapOverrides_SEP = ',';
    internal static readonly int ModMapOverridePrefixLength = ModMapOverridePrefix.AsSpan().Length;
    internal const string Action_ApplyMapOverride = $"{ModEntry.ModId}_ApplyMapOverride";
    internal const string Action_RemoveMapOverride = $"{ModEntry.ModId}_RemoveMapOverride";

    internal static void Register()
    {
        ModEntry.harm.Patch(
            original: AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.MakeMapModifications)),
            postfix: new HarmonyMethod(typeof(MapOverride), nameof(GameLocation_MakeMapModifications_Postfix))
            {
                priority = Priority.Last,
            }
        );

        ModEntry.help.Events.Content.AssetRequested += OnAssetRequested;
        ModEntry.help.Events.Content.AssetsInvalidated += OnAssetInvalidated;
        ModEntry.help.Events.Multiplayer.ModMessageReceived += OnModMessageReceived;

        TriggerActionManager.RegisterAction(Action_ApplyMapOverride, TriggerApplyMapOverride);
        TriggerActionManager.RegisterAction(Action_RemoveMapOverride, TriggerRemoveMapOverride);
        CommonPatch.RegisterTileAndTouch(Action_ApplyMapOverride, TileApplyMapOverride);
        CommonPatch.RegisterTileAndTouch(Action_RemoveMapOverride, TileRemoveMapOverride);
    }

    private static bool TryGetModMapOverrides(GameLocation location, [NotNullWhen(true)] out string[]? mapOverrides)
    {
        mapOverrides = null;
        if (!location.modData.TryGetValue(ModData_MapOverrides, out string mapOverridesStr))
        {
            return false;
        }
        mapOverrides = mapOverridesStr.Split(ModData_MapOverrides_SEP);
        return true;
    }

    private static string UpdateModMapOverrides(GameLocation location, HashSet<string> mapOverrides)
    {
        if (mapOverrides.Count > 0)
        {
            string joined = string.Join(ModData_MapOverrides_SEP, mapOverrides);
            location.modData[ModData_MapOverrides] = joined;
            return joined;
        }
        else
        {
            location.modData.Remove(ModData_MapOverrides);
            return "";
        }
    }

    private static void GameLocation_MakeMapModifications_Postfix(GameLocation __instance)
    {
        if (!TryGetModMapOverrides(__instance, out string[]? mapOverrides))
        {
            return;
        }
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
            validOverrideIds.Add(model.Id);
            __instance.ApplyMapOverride(
                model.MapOverrideAsset,
                model.MapOverrideKey,
                model.SourceRect,
                model.TargetRect
            );
        }
        UpdateModMapOverrides(__instance, validOverrideIds);
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
                if (id.Contains(ModData_MapOverrides_SEP))
                {
                    ModEntry.Log(
                        $"Cannot use '{ModData_MapOverrides_SEP}' in '{Asset_MapOverride}' key",
                        LogLevel.Error
                    );
                    invalid.Add(id);
                    continue;
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
        if (e.Type == Action_RemoveMapOverride)
        {
            Game1.currentLocation.loadMap(Game1.currentLocation.mapPath.Value, true);
        }
        else if (e.Type != Action_ApplyMapOverride)
        {
            return;
        }
        // needed because modData updates too slow
        Game1.currentLocation.modData[ModData_MapOverrides] = e.ReadAs<string>();
        Game1.currentLocation.MakeMapModifications();
    }

    private static bool TriggerApplyMapOverride(string[] args, TriggerActionContext context, out string error)
    {
        error = WrapMpHandling(Game1.currentLocation, args, Game1.player, DoApplyMapOverride)!;
        return error == null;
    }

    private static bool TriggerRemoveMapOverride(string[] args, TriggerActionContext context, out string error)
    {
        error = WrapMpHandling(Game1.currentLocation, args, Game1.player, DoRemoveMapOverride)!;
        return error == null;
    }

    private static bool TileApplyMapOverride(GameLocation location, string[] args, Farmer farmer, Point point)
    {
        string? error = WrapMpHandling(location, args, Game1.player, DoApplyMapOverride)!;
        if (error != null)
            ModEntry.Log(error, LogLevel.Error);
        return error != null;
    }

    private static bool TileRemoveMapOverride(GameLocation location, string[] args, Farmer farmer, Point point)
    {
        string? error = WrapMpHandling(location, args, Game1.player, DoRemoveMapOverride)!;
        if (error != null)
            ModEntry.Log(error, LogLevel.Error);
        return error != null;
    }

    private static string? WrapMpHandling(
        GameLocation location,
        string[] args,
        Farmer who,
        Func<GameLocation, IEnumerable<string>, (string?, string?)?> mapOverrideCb
    )
    {
        int skip = 2;
        if (ArgUtility.TryGet(args, 1, out string locationName, out string? error, name: "string locationName"))
        {
            location = GameStateQuery.Helpers.GetLocation(locationName, location);
        }

        (string?, string?)? result = mapOverrideCb(location, args.Skip(skip))!;
        string? updatedMapOverride = result?.Item1;
        error = result?.Item2;

        if (error != null)
            return error;
        if (updatedMapOverride != null)
        {
            long[] playersInSameLocation = location
                .farmers.Where(farmer => farmer.UniqueMultiplayerID != who.UniqueMultiplayerID)
                .Select(farmer => farmer.UniqueMultiplayerID)
                .ToArray();
            if (playersInSameLocation.Length > 0)
            {
                ModEntry.help.Multiplayer.SendMessage(
                    updatedMapOverride,
                    args[0],
                    [ModEntry.ModId],
                    playersInSameLocation
                );
            }
        }
        return null;
    }

    private static (string?, string?)? DoApplyMapOverride(GameLocation location, IEnumerable<string> args)
    {
        if (location == null || location.Map == null)
        {
            return (null, "Location map is null");
        }
        HashSet<string> mapOverrides;
        if (TryGetModMapOverrides(location, out string[]? mapOverridesArray))
        {
            mapOverrides = mapOverridesArray.ToHashSet();
        }
        else
        {
            mapOverrides = [];
        }
        bool hasAdded = false;
        foreach (string mapOverrideId in args)
        {
            if (!MapOverrideData.TryGetValue(mapOverrideId, out MapOverrideModel? model))
            {
                return (null, $"Map override id '{mapOverrideId}' not found");
            }
            if (!Game1.game1.xTileContent.DoesAssetExist<Map>("Maps\\" + model.MapOverrideAsset))
            {
                return (null, $"Map override asset 'Maps/{model.MapOverrideAsset}' from '{model.Id}' not found");
            }
            try
            {
                if (location == Game1.currentLocation)
                {
                    location.ApplyMapOverride(
                        model.MapOverrideAsset,
                        model.MapOverrideKey,
                        model.SourceRect,
                        model.TargetRect
                    );
                }
                mapOverrides.Add(model.Id);
                hasAdded = true;
            }
            catch (Exception err)
            {
                return (null, err.ToString());
            }
        }
        if (hasAdded)
        {
            return new(UpdateModMapOverrides(location, mapOverrides), null);
        }
        return null;
    }

    private static (string?, string?)? DoRemoveMapOverride(GameLocation location, IEnumerable<string> args)
    {
        if (location == null || location.Map == null)
        {
            return (null, "Location map is null");
        }
        if (!TryGetModMapOverrides(location, out string[]? mapOverridesArray))
        {
            return null;
        }
        bool hasRemoved = false;
        HashSet<string> mapOverrides = mapOverridesArray.ToHashSet();
        foreach (string mapOverrideId in args)
        {
            try
            {
                hasRemoved = mapOverrides.Remove(mapOverrideId) || hasRemoved;
            }
            catch (Exception err)
            {
                return (null, err.ToString());
            }
        }
        if (hasRemoved)
        {
            string updated = UpdateModMapOverrides(location, mapOverrides);
            if (location == Game1.currentLocation)
            {
                location.loadMap(location.mapPath.Value, true);
                location.MakeMapModifications();
                return (updated, null);
            }
            else
            {
                location.InvalidateCachedMultiplayerMap(Game1.Multiplayer.cachedMultiplayerMaps);
            }
        }
        return null;
    }
}

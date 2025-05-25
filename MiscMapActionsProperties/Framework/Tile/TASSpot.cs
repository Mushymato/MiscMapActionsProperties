using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using Mushymato.ExtendedTAS;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Triggers;

namespace MiscMapActionsProperties.Framework.Tile;

/// <summary>
/// Add new back layer tile property mushymato.MMAP_TAS
/// Show a TAS on the tile
/// </summary>
internal static class TASSpot
{
    internal const string TileProp_TAS = $"{ModEntry.ModId}_TAS";
    internal const string TileProp_ToggleTAS = $"{ModEntry.ModId}_ToggleTAS";

    private static readonly TileDataCache<string[]> tasSpotsCache = CommonPatch.GetSimpleTileDataCache(
        [TileProp_TAS],
        "Back"
    );

    private record LocationTASDefs(
        Dictionary<Point, List<TASContext>> Onetime,
        Dictionary<Point, List<TASContext>> Respawning
    );

    private static readonly PerScreen<LocationTASDefs?> locationTASDefs = new();
    private static readonly PerScreen<List<TASContext>?> respawningTASCache = new();
    private static readonly PerScreen<Dictionary<string, (List<TASContext>, List<TASContext>)>?> toggleTASDefs = new();

    internal static void Register()
    {
        ModEntry.help.Events.GameLoop.DayStarted += OnDayStarted;
        ModEntry.help.Events.Player.Warped += OnWarped;
        ModEntry.harm.Patch(
            original: AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.UpdateWhenCurrentLocation)),
            prefix: new HarmonyMethod(typeof(TASSpot), nameof(GameLocation_UpdateWhenCurrentLocation_Prefix))
        );
        CommonPatch.RegisterTileAndTouch(TileProp_TAS, TileAndTouchTAS);
        TriggerActionManager.RegisterAction(TileProp_TAS, TriggerActionTAS);

        GameLocation.RegisterTileAction(TileProp_ToggleTAS, ToggleTileTAS);

        tasSpotsCache.TileDataCacheChanged += OnCacheChanged;
    }

    private static void OnCacheChanged(object? sender, (GameLocation, HashSet<Point>?) e)
    {
        if (e.Item1 != Game1.currentLocation)
            return;

        if (e.Item2 == null || locationTASDefs.Value == null)
        {
            if (locationTASDefs.Value != null)
            {
                foreach (TASContext ctx in locationTASDefs.Value.Onetime.Values.SelectMany(ctx => ctx))
                {
                    ctx.RemoveAllSpawned(Game1.currentLocation.TemporarySprites.Remove);
                }
                foreach (TASContext ctx in locationTASDefs.Value.Respawning.Values.SelectMany(ctx => ctx))
                {
                    ctx.RemoveAllSpawned(Game1.currentLocation.TemporarySprites.Remove);
                }
            }
            EnterLocationTAS(Game1.currentLocation);
            return;
        }

        Dictionary<Point, string[]> tasSpotData = tasSpotsCache.GetTileData(Game1.currentLocation);
        Dictionary<Point, List<TASContext>> target;
        List<TASContext> newCtxList = [];
        foreach (Point pos in e.Item2)
        {
            if (locationTASDefs.Value.Onetime.TryGetValue(pos, out List<TASContext>? ctxList1))
            {
                foreach (TASContext ctx in ctxList1)
                {
                    ctx.RemoveAllSpawned(Game1.currentLocation.TemporarySprites.Remove);
                }
                locationTASDefs.Value.Onetime.Remove(pos);
            }
            if (locationTASDefs.Value.Respawning.TryGetValue(pos, out List<TASContext>? ctxList2))
            {
                foreach (TASContext ctx in ctxList2)
                {
                    ctx.RemoveAllSpawned(Game1.currentLocation.TemporarySprites.Remove);
                }
                locationTASDefs.Value.Respawning.Remove(pos);
            }

            if (tasSpotData.TryGetValue(pos, out string[]? tasKeyList))
            {
                foreach (var tasKey in tasKeyList)
                {
                    if (ModEntry.TAS.TryGetTASExt(tasKey, out TASExt? def))
                    {
                        TASContext newCtx = new(def) { Pos = pos.ToVector2() * Game1.tileSize };
                        target =
                            def.SpawnInterval <= 0 ? locationTASDefs.Value.Onetime : locationTASDefs.Value.Respawning;
                        if (target.TryGetValue(pos, out List<TASContext>? ctxList3))
                            ctxList3.Add(newCtx);
                        else
                            target[pos] = [newCtx];
                        if (def.SpawnInterval <= 0)
                            newCtxList.Add(newCtx);
                    }
                }
            }
        }
        AddLocationTAS(Game1.currentLocation, newCtxList);
    }

    private static void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        EnterLocationTAS(Game1.currentLocation);
    }

    private static void OnWarped(object? sender, WarpedEventArgs e)
    {
        EnterLocationTAS(e.NewLocation);
    }

    private static void EnterLocationTAS(GameLocation location)
    {
        locationTASDefs.Value = null;
        toggleTASDefs.Value = null;

        if (location == null)
            return;

        locationTASDefs.Value = CreateTASDefs(location);
        if (locationTASDefs.Value != null)
        {
            AddLocationTAS(location, locationTASDefs.Value.Onetime.Values.SelectMany(ctx => ctx));
        }
    }

    private static void GameLocation_UpdateWhenCurrentLocation_Prefix(GameLocation __instance, GameTime time)
    {
        if (__instance.wasUpdated)
            return;
        GameStateQueryContext context = new(__instance, null, null, null, Game1.random);
        if (respawningTASCache.Value != null)
        {
            foreach (TASContext tileTAS in respawningTASCache.Value)
                tileTAS.TryCreateRespawning(time, context, __instance.TemporarySprites.Add);
        }
        if (locationTASDefs.Value != null)
        {
            foreach (TASContext tileTAS in locationTASDefs.Value.Respawning.Values.SelectMany(ctx => ctx))
                tileTAS.TryCreateRespawning(time, context, __instance.TemporarySprites.Add);
        }
    }

    private static bool TriggerActionTAS(string[] args, TriggerActionContext context, out string error)
    {
        return SpawnTAS(Game1.currentLocation, args, out error);
    }

    private static bool TileAndTouchTAS(GameLocation location, string[] args, Farmer farmer, Point source)
    {
        return SpawnTAS(location, args, out _);
    }

    private static bool ToggleTileTAS(GameLocation location, string[] args, Farmer farmer, Point point)
    {
        string error = "Not enough arguments.";
        if (
            args.Length < 5
            || !ArgUtility.TryGet(args, 1, out string spawnKey, out error, allowBlank: false, name: "string spawnKey")
            || !ArgUtility.TryGetPoint(args, 2, out Point pos, out error, "Point spawnPos")
        )
        {
            ModEntry.Log(error);
            return false;
        }

        if (toggleTASDefs.Value?.TryGetValue(spawnKey, out (List<TASContext>, List<TASContext>) current) ?? false)
        {
            foreach (TASContext ctx in current.Item1)
            {
                ctx.RemoveAllSpawned(Game1.currentLocation.TemporarySprites.Remove);
            }
            foreach (TASContext ctx in current.Item2)
            {
                ctx.RemoveAllSpawned(Game1.currentLocation.TemporarySprites.Remove);
            }
            respawningTASCache.Value?.RemoveAll(current.Item2.Contains);
            toggleTASDefs.Value.Remove(spawnKey);
            return true;
        }

        if (!CreateTASDefsFromArgs(args, 4, pos, out List<TASContext>? onetime, out List<TASContext>? respawning))
        {
            return false;
        }

        toggleTASDefs.Value ??= [];
        toggleTASDefs.Value[spawnKey] = (onetime, respawning);

        AddLocationTAS(location, onetime);
        respawningTASCache.Value ??= [];
        respawningTASCache.Value.AddRange(respawning);

        return true;
    }

    private static bool SpawnTAS(GameLocation location, string[] args, out string error)
    {
        error = "Not enough arguments.";
        if (args.Length < 4 || !ArgUtility.TryGetPoint(args, 1, out Point pos, out error, "Point spawnPos"))
        {
            ModEntry.Log(error);
            return false;
        }

        if (!CreateTASDefsFromArgs(args, 3, pos, out List<TASContext>? onetime, out List<TASContext>? respawning))
        {
            return false;
        }

        AddLocationTAS(location, onetime);
        respawningTASCache.Value ??= [];
        respawningTASCache.Value.AddRange(respawning);
        return true;
    }

    private static void AddLocationTAS(GameLocation location, IEnumerable<TASContext> tileTASList)
    {
        GameStateQueryContext context = new(location, null, null, null, Game1.random);
        foreach (TASContext tileTAS in tileTASList)
        {
            if (tileTAS.TryCreateDelayed(context, location.TemporarySprites.Add))
                continue;
            tileTAS.TryCreate(context, location.TemporarySprites.Add);
        }
    }

    private static LocationTASDefs? CreateTASDefs(GameLocation location)
    {
        Dictionary<Point, List<TASContext>> onetime = [];
        Dictionary<Point, List<TASContext>> respawning = [];
        Dictionary<Point, List<TASContext>> target;
        foreach ((Point pos, string[] tasKeyList) in tasSpotsCache.GetTileData(location))
        {
            foreach (var tasKey in tasKeyList)
            {
                if (ModEntry.TAS.TryGetTASExt(tasKey, out TASExt? def))
                {
                    target = def.SpawnInterval <= 0 ? onetime : respawning;
                    if (target.TryGetValue(pos, out List<TASContext>? ctxList))
                        ctxList.Add(new(def) { Pos = pos.ToVector2() * Game1.tileSize });
                    else
                        target[pos] = [new(def) { Pos = pos.ToVector2() * Game1.tileSize }];
                }
            }
        }
        if (!onetime.Any() && !respawning.Any())
            return null;
        return new(onetime, respawning);
    }

    private static bool CreateTASDefsFromArgs(
        string[] args,
        int startIdx,
        Point pos,
        [NotNullWhen(true)] out List<TASContext>? onetime,
        [NotNullWhen(true)] out List<TASContext>? respawning
    )
    {
        onetime = [];
        respawning = [];

        Vector2 pixelPos = new(pos.X * Game1.tileSize, pos.Y * Game1.tileSize);
        foreach (var tasKey in args.Skip(startIdx))
        {
            if (ModEntry.TAS.TryGetTASExt(tasKey, out TASExt? def))
            {
                if (def.SpawnInterval <= 0)
                    onetime.Add(new(def) { Pos = pixelPos });
                else
                    respawning.Add(new(def) { Pos = pixelPos });
            }
        }

        if (!onetime.Any() && !respawning.Any())
        {
            onetime = null;
            respawning = null;
            return false;
        }
        return true;
    }
}

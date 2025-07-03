using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using Mushymato.ExtendedTAS;
using StardewModdingAPI;
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
    internal const string Action_ToggleTAS = $"{ModEntry.ModId}_ToggleTAS";
    internal const string Action_ContactTAS = $"{ModEntry.ModId}_ContactTAS";

    private static readonly TileDataCache<string[]> tasSpotsCache = CommonPatch.GetSimpleTileDataCache(
        [TileProp_TAS],
        "Back"
    );

    private record LocationTASDefs(
        Dictionary<Point, List<TASContext>> Onetime,
        Dictionary<Point, List<TASContext>> Respawning
    );

    private record ContactTASDefs(Point Pos, List<TASContext> Onetime, List<TASContext> Respawning);

    private static readonly PerScreen<LocationTASDefs?> locationTASDefs = new();
    private static readonly PerScreen<List<TASContext>?> respawningTASCache = new();
    private static readonly PerScreen<Dictionary<string, (List<TASContext>, List<TASContext>)>?> toggleTASDefs = new();
    private static readonly PerScreen<ContactTASDefs?> contactTASDefs = new();

    internal static void Register()
    {
        ModEntry.help.Events.GameLoop.DayStarted += OnDayStarted;
        ModEntry.help.Events.Player.Warped += OnWarped;
        CommonPatch.GameLocation_UpdateWhenCurrentLocationPrefix += GameLocation_UpdateWhenCurrentLocation_Prefix;

        CommonPatch.RegisterTileAndTouch(TileProp_TAS, TileAndTouchTAS);
        TriggerActionManager.RegisterAction(TileProp_TAS, TriggerActionTAS);

        GameLocation.RegisterTileAction(Action_ToggleTAS, ToggleTileTAS);
        TriggerActionManager.RegisterAction(Action_ToggleTAS, TriggerToggleTileTAS);

        GameLocation.RegisterTouchAction(Action_ContactTAS, ContactTouchTAS);

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

        if (tasSpotsCache.GetTileData(Game1.currentLocation) is not Dictionary<Point, string[]> tasSpotData)
            return;

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

        GameStateQueryContext context = new(Game1.currentLocation, null, null, null, null);
        AddLocationTAS(Game1.currentLocation, context, newCtxList);
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
        respawningTASCache.Value = null;
        contactTASDefs.Value = null;

        if (location == null)
            return;

        locationTASDefs.Value = CreateTASDefs(location);
        if (locationTASDefs.Value != null)
        {
            GameStateQueryContext context = new(Game1.currentLocation, null, null, null, null);
            AddLocationTAS(location, context, locationTASDefs.Value.Onetime.Values.SelectMany(ctx => ctx));
        }
    }

    private static void GameLocation_UpdateWhenCurrentLocation_Prefix(
        object? sender,
        CommonPatch.UpdateWhenCurrentLocationArgs e
    )
    {
        if (e.Location.wasUpdated)
            return;

        GameStateQueryContext context = new(e.Location, null, null, null, null);
        if (respawningTASCache.Value != null)
        {
            AddRespawnTAS(e.Location, e.Time, context, respawningTASCache.Value);
        }
        if (locationTASDefs.Value != null)
        {
            AddRespawnTAS(e.Location, e.Time, context, locationTASDefs.Value.Respawning.Values.SelectMany(ctx => ctx));
        }
        if (contactTASDefs.Value != null)
        {
            if (contactTASDefs.Value.Pos == Game1.player.TilePoint)
            {
                AddRespawnTAS(e.Location, e.Time, context, contactTASDefs.Value.Respawning);
            }
            else
            {
                foreach (TASContext ctx in contactTASDefs.Value.Onetime)
                {
                    ctx.RemoveAllSpawned(e.Location.TemporarySprites.Remove);
                }
                contactTASDefs.Value = null;
            }
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

    private static bool TriggerToggleTileTAS(string[] args, TriggerActionContext context, out string error)
    {
        error = null!;
        return ToggleTileTAS(Game1.currentLocation, args, Game1.player, Point.Zero);
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
            ModEntry.Log(error, LogLevel.Error);
            return false;
        }

        if (toggleTASDefs.Value?.TryGetValue(spawnKey, out (List<TASContext>, List<TASContext>) current) ?? false)
        {
            foreach (TASContext ctx in current.Item1)
            {
                ctx.RemoveAllSpawned(location.TemporarySprites.Remove);
            }
            foreach (TASContext ctx in current.Item2)
            {
                ctx.RemoveAllSpawned(location.TemporarySprites.Remove);
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

        GameStateQueryContext context = new(location, null, null, null, null);
        AddLocationTAS(location, context, onetime);
        AddRespawnTAS(location, Game1.currentGameTime, context, respawning);
        respawningTASCache.Value ??= [];
        respawningTASCache.Value.AddRange(respawning);

        return true;
    }

    private static void ContactTouchTAS(GameLocation location, string[] args, Farmer farmer, Vector2 point)
    {
        Point pos = point.ToPoint();
        if (!CreateTASDefsFromArgs(args, 1, pos, out List<TASContext>? onetime, out List<TASContext>? respawning))
        {
            return;
        }

        contactTASDefs.Value = new(pos, onetime, respawning);

        GameStateQueryContext context = new(location, null, null, null, null);
        AddLocationTAS(location, context, onetime);
        AddRespawnTAS(location, Game1.currentGameTime, context, respawning);
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

        GameStateQueryContext context = new(location, null, null, null, null);
        AddLocationTAS(location, context, onetime);
        AddRespawnTAS(location, Game1.currentGameTime, context, respawning);
        respawningTASCache.Value ??= [];
        respawningTASCache.Value.AddRange(respawning);

        return true;
    }

    private static void AddLocationTAS(
        GameLocation location,
        GameStateQueryContext context,
        IEnumerable<TASContext> tileTASList
    )
    {
        foreach (TASContext tileTAS in tileTASList)
        {
            if (tileTAS.TryCreateDelayed(context, location.TemporarySprites.Add))
                continue;
            tileTAS.TryCreate(context, location.TemporarySprites.Add);
        }
    }

    private static void AddRespawnTAS(
        GameLocation location,
        GameTime time,
        GameStateQueryContext context,
        IEnumerable<TASContext> tileTASList
    )
    {
        foreach (TASContext tileTAS in tileTASList)
        {
            tileTAS.TryCreateRespawning(time, context, location.TemporarySprites.Add);
        }
    }

    private static LocationTASDefs? CreateTASDefs(GameLocation location)
    {
        Dictionary<Point, List<TASContext>> onetime = [];
        Dictionary<Point, List<TASContext>> respawning = [];
        Dictionary<Point, List<TASContext>> target;

        if (tasSpotsCache.GetTileData(Game1.currentLocation) is not Dictionary<Point, string[]> tasSpotData)
            return null;

        foreach ((Point pos, string[] tasKeyList) in tasSpotData)
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
            ModEntry.Log($"No TAS found from '{string.Join(' ', args)}'", LogLevel.Error);
            onetime = null;
            respawning = null;
            return false;
        }
        return true;
    }
}

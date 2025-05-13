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
    private static readonly TileDataCache<string[]> tasSpotsCache = CommonPatch.GetSimpleTileDataCache(
        [TileProp_TAS],
        "Back"
    );

    private record LocationTASDefs(List<TASContext> Onetime, List<TASContext> Respawning);

    private static readonly PerScreen<LocationTASDefs?> locationTASDefs = new();
    private static readonly PerScreen<List<TASContext>?> respawningTASCache = new();

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

        tasSpotsCache.TileDataCacheChanged += OnCacheChanged;
    }

    private static void OnCacheChanged(object? sender, (GameLocation, HashSet<Point>?) e)
    {
        if (e.Item1 != Game1.currentLocation || locationTASDefs.Value == null)
            return;

        foreach (TASContext ctx in locationTASDefs.Value.Onetime.Concat(locationTASDefs.Value.Respawning))
        {
            ctx.RemoveAllSpawned(Game1.currentLocation.TemporarySprites.Remove);
        }
        EnterLocationTAS(Game1.currentLocation);
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
        locationTASDefs.Value = CreateTASDefs(location);
        AddLocationTAS(location, locationTASDefs.Value.Onetime);
        respawningTASCache.Value = [.. locationTASDefs.Value.Respawning];
        AddLocationTASRespawning(location, respawningTASCache.Value, Game1.currentGameTime);
    }

    private static void GameLocation_UpdateWhenCurrentLocation_Prefix(GameLocation __instance, GameTime time)
    {
        if (respawningTASCache.Value == null)
            return;
        AddLocationTASRespawning(__instance, respawningTASCache.Value, time);
    }

    private static bool TriggerActionTAS(string[] args, TriggerActionContext context, out string error)
    {
        return SpawnTAS(Game1.currentLocation, args, out error);
    }

    private static bool TileAndTouchTAS(GameLocation location, string[] args, Farmer farmer, Point source)
    {
        return SpawnTAS(location, args, out _);
    }

    private static bool SpawnTAS(GameLocation location, string[] args, out string error)
    {
        error = "Not enough arguments.";
        if (args.Length < 4 || !ArgUtility.TryGetVector2(args, 1, out Vector2 pos, out error, true, "Vector2 spawnPos"))
        {
            ModEntry.Log(error);
            return false;
        }
        List<TASContext> onetime = [];
        List<TASContext> respawning = [];

        pos *= Game1.tileSize;
        foreach (var tasKey in args.Skip(3))
            if (ModEntry.TAS.TryGetTASExt(tasKey, out TASExt? def))
            {
                if (def.SpawnInterval <= 0)
                    onetime.Add(new(def) { Pos = pos });
                else
                    respawning.Add(new(def) { Pos = pos });
            }

        AddLocationTAS(location, onetime);
        if (respawningTASCache.Value != null)
            respawningTASCache.Value.AddRange(respawning);
        else
            respawningTASCache.Value = respawning;
        return true;
    }

    private static void AddLocationTAS(GameLocation location, IEnumerable<TASContext> tileTASList)
    {
        GameStateQueryContext context = new(location, null, null, null, Game1.random);
        foreach (TASContext tileTAS in tileTASList)
        {
            if (tileTAS.TryCreateDelayed(context, location.TemporarySprites.Add))
                continue;
            if (tileTAS.TryCreate(context, out TemporaryAnimatedSprite? tas))
                location.TemporarySprites.Add(tas);
        }
    }

    private static void AddLocationTASRespawning(
        GameLocation location,
        IEnumerable<TASContext> tileTASList,
        GameTime time
    )
    {
        if (location.wasUpdated)
            return;
        GameStateQueryContext context = new(location, null, null, null, Game1.random);
        foreach (TASContext tileTAS in tileTASList)
        {
            tileTAS.TryCreateRespawning(time, context, location.TemporarySprites.Add);
        }
    }

    private static LocationTASDefs CreateTASDefs(GameLocation location)
    {
        List<TASContext> onetime = [];
        List<TASContext> respawning = [];
        foreach ((Point pos, string[] tasKeyList) in tasSpotsCache.GetTileData(location))
        {
            foreach (var tasKey in tasKeyList)
            {
                if (ModEntry.TAS.TryGetTASExt(tasKey, out TASExt? def))
                {
                    if (def.SpawnInterval <= 0)
                        onetime.Add(new(def) { Pos = pos.ToVector2() * Game1.tileSize });
                    else
                        respawning.Add(new(def) { Pos = pos.ToVector2() * Game1.tileSize });
                }
            }
        }
        return new(onetime, respawning);
    }
}

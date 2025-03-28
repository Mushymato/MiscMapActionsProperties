using HarmonyLib;
using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Extensions;
using StardewValley.Triggers;

namespace MiscMapActionsProperties.Framework.Tile;

/// <summary>
/// Add new back layer tile property mushymato.MMAP_TAS
/// Show a TAS on the tile
/// </summary>
internal static class TASSpot
{
    internal static readonly string TileProp_TAS = $"{ModEntry.ModId}_TAS";
    private static readonly PerScreen<List<TileTAS>?> respawningTASCache = new();

    internal static void Register()
    {
        ModEntry.help.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        ModEntry.help.Events.Player.Warped += OnWarped;
        ModEntry.harm.Patch(
            original: AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.UpdateWhenCurrentLocation)),
            prefix: new HarmonyMethod(typeof(TASSpot), nameof(GameLocation_UpdateWhenCurrentLocation_Prefix))
        );
        CommonPatch.RegisterTileAndTouch(TileProp_TAS, TileAndTouchTAS);
        TriggerActionManager.RegisterAction(TileProp_TAS, TriggerActionTAS);
    }

    private static void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        EnterLocationTAS(Game1.currentLocation);
    }

    private static void OnWarped(object? sender, WarpedEventArgs e)
    {
        EnterLocationTAS(e.NewLocation);
    }

    private static void EnterLocationTAS(GameLocation location)
    {
        var tileTASs = CreateMapDefs(location.map);
        AddLocationTAS(location, tileTASs.Item1);
        respawningTASCache.Value = tileTASs.Item2;
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
        List<TileTAS> onetime = [];
        List<TileTAS> respawning = [];

        pos *= Game1.tileSize;
        foreach (var tasKey in args.Skip(3))
            if (TASAssetManager.TASData.TryGetValue(tasKey, out TASExt? def))
            {
                if (def.SpawnInterval <= 0)
                    onetime.Add(new(def, pos));
                else
                    respawning.Add(new(def, pos));
            }

        AddLocationTAS(location, onetime);
        if (respawningTASCache.Value != null)
            respawningTASCache.Value.AddRange(respawning);
        else
            respawningTASCache.Value = respawning;
        return true;
    }

    private static void AddLocationTAS(GameLocation location, IEnumerable<TileTAS> tileTASList)
    {
        GameStateQueryContext context = new(location, null, null, null, Game1.random);
        foreach (TileTAS tileTAS in tileTASList)
        {
            if (tileTAS.TryCreateDelayed(context, location.TemporarySprites.Add))
                continue;
            if (tileTAS.TryCreate(context, out TemporaryAnimatedSprite? tas))
                location.TemporarySprites.Add(tas);
        }
    }

    private static void AddLocationTASRespawning(GameLocation location, IEnumerable<TileTAS> tileTASList, GameTime time)
    {
        if (location.wasUpdated)
            return;
        GameStateQueryContext context = new(location, null, null, null, Game1.random);
        foreach (TileTAS tileTAS in tileTASList)
        {
            tileTAS.TryCreateRespawning(time, context, location.TemporarySprites.Add);
        }
    }

    private static ValueTuple<List<TileTAS>, List<TileTAS>> CreateMapDefs(xTile.Map map)
    {
        List<TileTAS> onetime = [];
        List<TileTAS> respawning = [];
        var backLayer = map.RequireLayer("Back");
        for (int x = 0; x < backLayer.LayerWidth; x++)
        {
            for (int y = 0; y < backLayer.LayerHeight; y++)
            {
                Vector2 pos = new(x, y);
                if (pos.Equals(Vector2.Zero))
                    continue;
                MapTile tile = backLayer.Tiles[x, y];
                if (tile == null)
                    continue;
                if (tile.Properties.TryGetValue(TileProp_TAS, out string tasKeyList))
                {
                    foreach (var tasKey in ArgUtility.SplitBySpaceQuoteAware(tasKeyList))
                        if (TASAssetManager.TASData.TryGetValue(tasKey, out TASExt? def))
                        {
                            if (def.SpawnInterval <= 0)
                                onetime.Add(new(def, pos * Game1.tileSize));
                            else
                                respawning.Add(new(def, pos * Game1.tileSize));
                        }
                }
            }
        }
        return new(onetime, respawning);
    }
}

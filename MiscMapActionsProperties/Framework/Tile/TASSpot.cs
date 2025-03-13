using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.GameData;

namespace MiscMapActionsProperties.Framework.Tile;

public class TASExtSub : TemporaryAnimatedSpriteDefinition
{
    public Vector2 Motion { get; set; } = Vector2.Zero;
    public Vector2 Acceleration { get; set; } = Vector2.Zero;
    public Vector2 AccelerationChange { get; set; } = Vector2.Zero;
    public float? LayerDepth { get; set; } = null;
    public float Alpha { get; set; } = 0f;
}

public class TASExtRand
{
    public float SortOffset { get; set; } = 0f;
    public float Alpha { get; set; } = 0f;
    public float AlphaFade { get; set; } = 0f;
    public float Scale { get; set; } = 0f;
    public float ScaleChange { get; set; } = 0f;
    public float Rotation { get; set; } = 0f;
    public float RotationChange { get; set; } = 0f;
    public Vector2 Motion { get; set; } = Vector2.Zero;
    public Vector2 Acceleration { get; set; } = Vector2.Zero;
    public Vector2 AccelerationChange { get; set; } = Vector2.Zero;
    public Vector2 PositionOffset { get; set; } = Vector2.Zero;
}

public class TASExt : TASExtSub
{
    internal bool HasRand => RandMin != null && RandMax != null;
    public TASExtRand? RandMin { get; set; } = null;
    public TASExtRand? RandMax { get; set; } = null;
    public bool PingPong { get; set; }
    public double SpawnInterval { get; set; } = -1;
}

internal sealed record TileTAS(TASExt Def, Vector2 Tile)
{
    private TimeSpan spawnTimeout = TimeSpan.Zero;

    internal TemporaryAnimatedSprite Create()
    {
        // csharpier-ignore
        return new(
            Def.Texture,
            Def.SourceRect,
            Def.Interval,
            Def.Frames,
            Def.Loops,
            Tile * Game1.tileSize + (Def.PositionOffset + (Def.HasRand ? Random.Shared.NextVector2(Def.RandMin!.PositionOffset, Def.RandMax!.PositionOffset) : Vector2.Zero)) * 4f,
            Def.Flicker,
            Def.Flip,
            Def.SortOffset + (Def.HasRand ? Random.Shared.NextSingle(Def.RandMin!.SortOffset, Def.RandMax!.SortOffset) : 0),
            Def.AlphaFade + (Def.HasRand ? Random.Shared.NextSingle(Def.RandMin!.AlphaFade, Def.RandMax!.AlphaFade) : 0),
            Utility.StringToColor(Def.Color) ?? Color.White,
            (Def.Scale + (Def.HasRand ? Random.Shared.NextSingle(Def.RandMin!.Scale, Def.RandMax!.Scale) : 0)) * 4f,
            Def.ScaleChange + (Def.HasRand ? Random.Shared.NextSingle(Def.RandMin!.ScaleChange, Def.RandMax!.ScaleChange) : 0),
            Def.Rotation + (Def.HasRand ? Random.Shared.NextSingle(Def.RandMin!.Rotation, Def.RandMax!.Rotation) : 0),
            Def.RotationChange + (Def.HasRand ? Random.Shared.NextSingle(Def.RandMin!.RotationChange, Def.RandMax!.RotationChange) : 0)
        )
        {
            pingPong = Def.PingPong,
            alpha = Def.Alpha + (Def.HasRand ? Random.Shared.NextSingle(Def.RandMin!.Alpha, Def.RandMax!.Alpha) : 0),
            layerDepth = Def.LayerDepth ?? (Tile.Y + 0.66f) * Game1.tileSize / 10000f + Tile.X * 1E-05f,
            motion = Def.Motion + (Def.HasRand ? Random.Shared.NextVector2(Def.RandMin!.Motion, Def.RandMax!.Motion) : Vector2.Zero),
            acceleration = Def.Acceleration + (Def.HasRand ? Random.Shared.NextVector2(Def.RandMin!.Acceleration, Def.RandMax!.Acceleration) : Vector2.Zero),
            accelerationChange = Def.AccelerationChange + (Def.HasRand ? Random.Shared.NextVector2(Def.RandMin!.AccelerationChange, Def.RandMax!.AccelerationChange) : Vector2.Zero),
        };
    }

    internal TemporaryAnimatedSprite? TryCreateRespawning(GameTime time)
    {
        if (spawnTimeout <= TimeSpan.Zero)
        {
            spawnTimeout = TimeSpan.FromMilliseconds(Def.SpawnInterval);
            return Create();
        }
        spawnTimeout -= time.ElapsedGameTime;
        return null;
    }
}

internal record TileTASLists(List<TileTAS> Onetime, List<TileTAS> Respawning);

/// <summary>
/// Add new back layer tile property mushymato.MMAP_TAS
/// Show a TAS on the tile
/// </summary>
internal static class TASSpot
{
    internal static readonly string TileProp_TAS = $"{ModEntry.ModId}_TAS";
    internal static readonly string Asset_TAS = $"{ModEntry.ModId}/TAS";
    private static readonly PerScreen<TileTASLists?> currentTASCache = new();

    internal static void Register()
    {
        ModEntry.help.Events.Content.AssetRequested += OnAssetRequested;
        ModEntry.help.Events.Content.AssetsInvalidated += OnAssetInvalidated;
        ModEntry.help.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        ModEntry.help.Events.Player.Warped += OnWarped;
        ModEntry.harm.Patch(
            original: AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.UpdateWhenCurrentLocation)),
            prefix: new HarmonyMethod(typeof(TASSpot), nameof(GameLocation_UpdateWhenCurrentLocation_Prefix))
        );
    }

    private static void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        EnterLocationTAS(Game1.currentLocation);
    }

    private static Dictionary<string, TASExt>? _tasData = null;

    /// <summary>Question dialogue data</summary>
    internal static Dictionary<string, TASExt> TASData
    {
        get
        {
            _tasData ??= Game1.content.Load<Dictionary<string, TASExt>>(Asset_TAS);
            return _tasData;
        }
    }

    private static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.Name.IsEquivalentTo(Asset_TAS))
            e.LoadFrom(() => new Dictionary<string, TASExt>(), AssetLoadPriority.Low);
    }

    private static void OnAssetInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        if (e.NamesWithoutLocale.Any(an => an.IsEquivalentTo(Asset_TAS)))
            _tasData = null;
    }

    private static void OnWarped(object? sender, WarpedEventArgs e)
    {
        EnterLocationTAS(e.NewLocation);
    }

    private static void EnterLocationTAS(GameLocation location)
    {
        currentTASCache.Value = CreateMapDefs(location.map);
        AddLocationTAS(location, currentTASCache.Value.Onetime);
        AddLocationTASRespawning(location, currentTASCache.Value.Respawning, Game1.currentGameTime);
    }

    private static void GameLocation_UpdateWhenCurrentLocation_Prefix(GameLocation __instance, GameTime time)
    {
        if (currentTASCache.Value == null)
            return;
        AddLocationTASRespawning(__instance, currentTASCache.Value.Respawning, time);
    }

    private static void AddLocationTAS(GameLocation location, IEnumerable<TileTAS> tileTASList)
    {
        foreach (TileTAS tileTAS in tileTASList)
        {
            if (!GameStateQuery.CheckConditions(tileTAS.Def.Condition, location))
                return;
            TemporaryAnimatedSprite tas = tileTAS.Create();
            location.TemporarySprites.Add(tas);
        }
    }

    private static void AddLocationTASRespawning(GameLocation location, IEnumerable<TileTAS> tileTASList, GameTime time)
    {
        if (location.wasUpdated)
            return;
        foreach (TileTAS tileTAS in tileTASList)
        {
            if (!GameStateQuery.CheckConditions(tileTAS.Def.Condition, location))
                return;
            if (tileTAS.TryCreateRespawning(time) is TemporaryAnimatedSprite tas)
                location.TemporarySprites.Add(tas);
        }
    }

    private static TileTASLists CreateMapDefs(xTile.Map map)
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
                    foreach (var tasKey in ArgUtility.SplitBySpace(tasKeyList))
                        if (TASData.TryGetValue(tasKey, out TASExt? def))
                        {
                            ModEntry.Log($"{tasKey}: {def.Id} ({def.SpawnInterval})");
                            if (def.SpawnInterval <= 0)
                                onetime.Add(new(def, pos));
                            else
                                respawning.Add(new(def, pos));
                        }
                }
            }
        }
        return new(onetime, respawning);
    }
}

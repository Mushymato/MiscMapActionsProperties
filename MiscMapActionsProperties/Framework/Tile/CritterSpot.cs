using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Delegates;
using StardewValley.Extensions;
using StardewValley.Triggers;

namespace MiscMapActionsProperties.Framework.Tile;

internal enum SupportedCritter
{
    Firefly,
    Seagull,
    Crab,
    Birdie,
    Butterfly,
}

/// <summary>
/// Add new back layer tile property mushymato.MMAP_Critter <critter type> [type dependent args]
/// Add critter at tile, supports
/// - Firefly: [color] [count]
/// - Seagull: [texture|T] [count]
/// - Crab: [texture|T] [count]
/// - Birdies: [texture|<number>|T] [count]
/// - Butterfly: [texture|<number>|T] [count]
/// </summary>
internal static class CritterSpot
{
    internal static readonly string TileProp_Critter = $"{ModEntry.ModId}_Critter";
    private static readonly TileDataCache<string[]> critterSpotsCache = CommonPatch.GetSimpleTileDataCache(
        [TileProp_Critter],
        "Back"
    );
    private static readonly FieldInfo fireflyLight = AccessTools.DeclaredField(typeof(Firefly), "light");
    private static readonly FieldInfo crabSourceRectangle = AccessTools.DeclaredField(
        typeof(CrabCritter),
        "_baseSourceRectangle"
    );

    internal static PerScreen<Dictionary<Point, List<Critter>>> TileDataSpawnedCritters = new();

    internal static void Register()
    {
        ModEntry.help.Events.GameLoop.DayStarted += OnDayStarted;
        ModEntry.help.Events.Player.Warped += OnWarped;
        CommonPatch.RegisterTileAndTouch(TileProp_Critter, TileAndTouchCritter);
        TriggerActionManager.RegisterAction(TileProp_Critter, TriggerActionCritter);

        critterSpotsCache.TileDataCacheChanged += OnCacheChanged;
    }

    private static void OnCacheChanged(object? sender, (GameLocation, HashSet<Point>?) e)
    {
        GameLocation location = e.Item1;
        if (location != Game1.currentLocation)
            return;
        if (location.critters == null)
        {
            SpawnLocationCritters(location);
            return;
        }
        Dictionary<Point, List<Critter>> spawnedCritters = TileDataSpawnedCritters.Value;
        if (e.Item2 == null)
        {
            foreach (List<Critter> critters in spawnedCritters.Values)
                location.critters.RemoveAll(critters.Contains);
            SpawnLocationCritters(location);
            return;
        }

        Dictionary<Point, string[]> cacheEntry = critterSpotsCache.GetTileData(location);
        foreach (Point pos in e.Item2)
        {
            if (spawnedCritters.TryGetValue(pos, out List<Critter>? critters))
            {
                location.critters.RemoveAll(critters.Contains);
                spawnedCritters.Remove(pos);
            }
            if (cacheEntry.TryGetValue(pos, out string[]? props))
            {
                var spawned = SpawnCritter(location, pos, props, 0, out string _);
                if (spawned.Count > 0)
                {
                    spawnedCritters[pos] = spawned;
                }
            }
        }
    }

    private static void OnDayStarted(object? sender, DayStartedEventArgs e) =>
        SpawnLocationCritters(Game1.currentLocation);

    private static void OnWarped(object? sender, WarpedEventArgs e) => SpawnLocationCritters(e.NewLocation);

    private static void SpawnLocationCritters(GameLocation location)
    {
        if (location == null)
            return;
        TileDataSpawnedCritters.Value = [];
        foreach ((Point pos, string[] props) in critterSpotsCache.GetTileData(location))
        {
            var spawned = SpawnCritter(location, pos, props, 0, out string _);
            if (spawned.Count > 0)
            {
                TileDataSpawnedCritters.Value[pos] = spawned;
            }
        }
    }

    private static bool TriggerActionCritter(string[] args, TriggerActionContext context, out string error)
    {
        if (!ArgUtility.TryGetPoint(args, 1, out Point position, out error, "Point position"))
            return false;
        return SpawnCritter(Game1.currentLocation, position, args, 3, out error).Count > 0;
    }

    private static bool TileAndTouchCritter(GameLocation location, string[] args, Farmer farmer, Point source)
    {
        return SpawnCritter(location, source, args, 1, out _).Count > 0;
    }

    private static List<Critter> SpawnCritter(
        GameLocation location,
        Point position,
        string[] args,
        int firstIdx,
        out string error
    )
    {
        error = "";
        List<Critter> spawned = [];
        location.instantiateCrittersList();
        for (int i = firstIdx; i <= args.Length - 3; i += 3)
        {
            if (
                !ArgUtility.TryGet(
                    args,
                    i,
                    out string critterKindStr,
                    out error,
                    allowBlank: false,
                    name: "string critterKind"
                ) || !Enum.TryParse(critterKindStr, true, out SupportedCritter critterKind)
            )
            {
                break;
            }
            if (
                !ArgUtility.TryGetOptional(args, firstIdx + 1, out string? arg1, out error, name: "string arg1")
                || !ArgUtility.TryGetOptionalInt(
                    args,
                    firstIdx + 2,
                    out int count,
                    out error,
                    defaultValue: 1,
                    name: "int count"
                )
            )
            {
                ModEntry.Log(error, LogLevel.Error);
                break;
            }
            // csharpier-ignore
            var spawnedThisTime = critterKind switch
            {
                SupportedCritter.Firefly => SpawnCritterFirefly(location, position, arg1, count),
                SupportedCritter.Seagull => SpawnCritterSeagull(location, position, arg1, count),
                SupportedCritter.Crab => SpawnCritterCrab(location, position, arg1, count),
                SupportedCritter.Birdie => SpawnCritterBirdie(location, position, arg1, count),
                SupportedCritter.Butterfly => SpawnCritterButterfly(location, position, arg1, count),
                _ => null,
            };
            if (spawnedThisTime != null)
                spawned.AddRange(spawnedThisTime);
        }
        location.critters.AddRange(spawned);
        return spawned;
    }

    private static IEnumerable<Critter> SpawnCritterFirefly(
        GameLocation location,
        Point position,
        string? color,
        int count
    )
    {
        Color? c = null;
        if (color != null && color != "T" && (c = Utility.StringToColor(color)) != null)
        {
            c = new Color(((Color)c).PackedValue ^ 0x00FFFFFF);
        }
        for (int i = 0; i < count; i++)
        {
            Firefly firefly = new(position.ToVector2());
            firefly.position.X += Random.Shared.Next(Game1.tileSize);
            firefly.position.Y += Random.Shared.Next(Game1.tileSize);
            firefly.startingPosition = firefly.position;
            if (c != null && fireflyLight.GetValue(firefly) is LightSource light)
                light.color.Value = (Color)c;
            yield return firefly;
        }
    }

    private static IEnumerable<Critter> SpawnCritterSeagull(
        GameLocation location,
        Point position,
        string? texture,
        int count
    )
    {
        if (texture == "T" || !Game1.content.DoesAssetExist<Texture2D>(texture))
            texture = null;
        int startingState = 3;
        if (
            location.isWaterTile((int)position.X, (int)position.Y)
            && location.doesTileHaveProperty((int)position.X, (int)position.Y, "Passable", "Buildings") == null
        )
            startingState = 2;
        for (int i = 0; i < count; i++)
        {
            Seagull seagull =
                new(
                    position.ToVector2() * Game1.tileSize
                        + new Vector2(Random.Shared.Next(Game1.tileSize), Random.Shared.Next(Game1.tileSize)),
                    startingState
                );
            if (texture != null)
                seagull.sprite.textureName.Value = texture;
            yield return seagull;
        }
    }

    private static IEnumerable<Critter> SpawnCritterCrab(
        GameLocation location,
        Point position,
        string? texture,
        int count
    )
    {
        if (texture == "T" || !Game1.content.DoesAssetExist<Texture2D>(texture))
            texture = null;
        for (int i = 0; i < count; i++)
        {
            CrabCritter crab =
                new(
                    position.ToVector2() * Game1.tileSize
                        + new Vector2(Random.Shared.Next(Game1.tileSize), Random.Shared.Next(Game1.tileSize))
                );
            if (texture != null)
            {
                crab.sprite.textureName.Value = texture;
                crabSourceRectangle.SetValue(crab, new Rectangle(0, 0, 18, 18));
            }
            yield return crab;
        }
    }

    private static IEnumerable<Critter> SpawnCritterBirdie(
        GameLocation location,
        Point position,
        string? texture,
        int count
    )
    {
        int startingIndex = -1;
        if (texture != null)
        {
            if (int.TryParse(texture, out startingIndex))
            {
                texture = null;
            }
            else if (texture != "T" && Game1.content.DoesAssetExist<Texture2D>(texture))
            {
                startingIndex = 0;
            }
            else
            {
                texture = null;
                startingIndex = -1;
            }
        }
        Season season = location.GetSeason();
        for (int i = 0; i < count; i++)
        {
            int startIdx = startingIndex;
            if (startIdx == -1)
            {
                // GameLocation.addBirdies
                if (Random.Shared.NextBool() && Game1.MasterPlayer.mailReceived.Contains("Farm_Eternal"))
                {
                    startIdx = (season == Season.Fall) ? 135 : 125;
                }
                else
                {
                    if (season == Season.Fall)
                    {
                        startIdx = 45;
                    }
                    else if (Game1.random.NextDouble() < 0.05)
                    {
                        startIdx = 165;
                    }
                    else
                    {
                        startIdx = 25;
                    }
                }
            }
            Birdie birdie = new((int)position.X, (int)position.Y, startIdx);
            birdie.position += new Vector2(
                Random.Shared.Next(-Game1.tileSize / 2, Game1.tileSize / 2),
                Random.Shared.Next(-Game1.tileSize / 2, Game1.tileSize / 2)
            );
            if (texture != null)
            {
                birdie.sprite.textureName.Value = texture;
            }
            yield return birdie;
        }
    }

    private static IEnumerable<Critter> SpawnCritterButterfly(
        GameLocation location,
        Point position,
        string? texture,
        int count
    )
    {
        int startingIndex = -1;
        if (texture != null)
        {
            if (int.TryParse(texture, out startingIndex))
            {
                texture = null;
            }
            else if (texture != "T" && Game1.content.DoesAssetExist<Texture2D>(texture))
            {
                startingIndex = 0;
            }
            else
            {
                texture = null;
                startingIndex = -1;
            }
        }
        for (int i = 0; i < count; i++)
        {
            Butterfly butterfly =
                new(
                    location,
                    position.ToVector2(),
                    forceSummerButterfly: startingIndex != -1,
                    baseFrameOverride: startingIndex
                );
            butterfly.position += new Vector2(
                Random.Shared.Next(0, Game1.tileSize),
                Random.Shared.Next(0, Game1.tileSize)
            );
            if (texture != null)
            {
                butterfly.sprite.textureName.Value = texture;
            }
            yield return butterfly;
        }
    }
}

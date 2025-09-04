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
    Frog,
    LeaperFrog,
    Rabbit,
    Squirrel,
    Opossum,
}

/// <summary>
/// Add new back layer tile property mushymato.MMAP_Critter <critter type> [type dependent args]
/// Add critter at tile, supports
/// - Firefly: [color] [count]
/// - Seagull: [texture|T] [count]
/// - Crab: [texture|T] [count]
/// - Birdie: [texture|<number>|T](:height)? [count]
/// - Butterfly: [texture|<number>|T] [count]
/// - Frog: [T|F] [count]
/// - LeaperFrog: [T|F] [count]
/// - Rabbit: [texture|<number>](:T|F)? [count]
/// </summary>
internal static class CritterSpot
{
    internal const string TileProp_Critter = $"{ModEntry.ModId}_Critter";
    internal const string Action_CritterRandom = $"{ModEntry.ModId}_CritterRandom";
    private static readonly TileDataCache<string[]> critterSpotsCache = CommonPatch.GetSimpleTileDataCache(
        [TileProp_Critter],
        "Back"
    );
    private static readonly FieldInfo fireflyLight = AccessTools.DeclaredField(typeof(Firefly), "light");
    private static readonly FieldInfo crabSourceRectangle = AccessTools.DeclaredField(
        typeof(CrabCritter),
        "_baseSourceRectangle"
    );
    private static readonly FieldInfo butterflyLightId = AccessTools.DeclaredField(typeof(Butterfly), "lightId");

    internal static PerScreen<Dictionary<Point, List<Critter>>> tileDataSpawnedCritters = new();
    internal static Dictionary<Point, List<Critter>> TileDataSpawnedCritters => tileDataSpawnedCritters.Value ??= [];

    internal static void Register()
    {
        ModEntry.help.Events.GameLoop.DayStarted += OnDayStarted;
        ModEntry.help.Events.Player.Warped += OnWarped;
        CommonPatch.RegisterTileAndTouch(TileProp_Critter, TileAndTouchCritter);
        TriggerActionManager.RegisterAction(TileProp_Critter, TriggerActionCritter);
        TriggerActionManager.RegisterAction(Action_CritterRandom, TriggerActionCritterRandom);

        critterSpotsCache.TileDataCacheChanged += OnCacheChanged;
    }

    private static void RemoveTheseCritters(GameLocation location, List<Critter> critters)
    {
        location.critters.RemoveAll(critters.Contains);
        foreach (Critter critter in critters)
        {
            if (critter is Firefly firefly && fireflyLight.GetValue(firefly) is LightSource light)
            {
                Game1.currentLightSources.Remove(light.Id);
            }
            else if (
                critter is Butterfly butterfly
                && butterfly.isLit
                && butterflyLightId.GetValue(butterfly) is string lightId
            )
            {
                Game1.currentLightSources.Remove(lightId);
            }
        }
    }

    private static void OnCacheChanged(object? sender, TileDataCacheChangedArgs e)
    {
        if (e.Location != Game1.currentLocation)
            return;
        if (e.Location.critters == null)
        {
            SpawnLocationCritters(e.Location);
            return;
        }
        Dictionary<Point, List<Critter>> spawnedCritters = TileDataSpawnedCritters;
        if (e.Points == null)
        {
            foreach (List<Critter> critters in spawnedCritters.Values)
                RemoveTheseCritters(e.Location, critters);
            SpawnLocationCritters(e.Location);
            return;
        }

        if (critterSpotsCache.GetTileData(e.Location) is not Dictionary<Point, string[]> cacheEntry)
            return;

        foreach (Point pos in e.Points)
        {
            if (spawnedCritters.TryGetValue(pos, out List<Critter>? critters))
            {
                RemoveTheseCritters(e.Location, critters);
                spawnedCritters.Remove(pos);
            }
            if (cacheEntry.TryGetValue(pos, out string[]? props))
            {
                List<Critter> spawned = SpawnCritter(e.Location, pos, props, 0, out string _);
                if (spawned.Count > 0)
                {
                    spawnedCritters[pos] = spawned;
                }
            }
        }
    }

    private static void OnDayStarted(object? sender, DayStartedEventArgs e) =>
        SpawnLocationCritters(Game1.currentLocation);

    private static void OnWarped(object? sender, WarpedEventArgs e)
    {
        SpawnLocationCritters(e.NewLocation);
    }

    private static void SpawnLocationCritters(GameLocation location)
    {
        if (location == null)
            return;

        Dictionary<Point, List<Critter>> spawnedCritters = TileDataSpawnedCritters;
        if (
            location.critters != null
            && spawnedCritters.Values.SelectMany(critter => critter).ToList() is List<Critter> critters
        )
        {
            RemoveTheseCritters(location, critters);
        }
        spawnedCritters.Clear();

        if (critterSpotsCache.GetTileData(location) is not Dictionary<Point, string[]> cacheEntry)
            return;

        foreach ((Point pos, string[] props) in cacheEntry)
        {
            List<Critter> spawned = SpawnCritter(location, pos, props, 0, out string _);
            if (spawned.Count > 0)
            {
                spawnedCritters[pos] = spawned;
            }
        }
    }

    private static bool TriggerActionCritter(string[] args, TriggerActionContext context, out string error)
    {
        if (!ArgUtility.TryGetPoint(args, 1, out Point position, out error, "Point position"))
            return false;
        return SpawnCritter(Game1.currentLocation, position, args, 3, out error).Count > 0;
    }

    private static bool TriggerActionCritterRandom(string[] args, TriggerActionContext context, out string error)
    {
        if (!ArgUtility.TryGetFloat(args, 1, out float chance, out error, "float name"))
            return false;
        if (Game1.currentLocation?.Map == null)
        {
            error = "Current location/map is null";
            return false;
        }

        bool res = false;
        foreach ((Vector2 pos, _) in CommonPatch.IterateMapTiles(Game1.currentLocation.Map, "Back"))
        {
            if (Random.Shared.NextSingle() <= chance)
                res = SpawnCritter(Game1.currentLocation, pos.ToPoint(), args, 2, out error).Count > 0 || res;
        }
        return res;
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
        Point pntOffset = Point.Zero;
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
                )
            )
            {
                break;
            }
            if (!Enum.TryParse(critterKindStr, true, out SupportedCritter critterKind))
            {
                string[] critterKindArgs = critterKindStr.Split(":");
                if (
                    !ArgUtility.TryGetEnum(critterKindArgs, 0, out critterKind, out error, name: "enum critterKind")
                    || !ArgUtility.TryGetOptional(
                        critterKindArgs,
                        1,
                        out string gsq,
                        out error,
                        name: "string critterGSQ"
                    )
                )
                {
                    break;
                }
                if (!GameStateQuery.CheckConditions(gsq))
                {
                    continue;
                }
                ArgUtility.TryGetPoint(critterKindArgs, 2, out pntOffset, out _, name: "Point pntOffset");
            }
            if (
                !ArgUtility.TryGetOptional(args, i + 1, out string? arg1, out error, name: "string arg1")
                || !ArgUtility.TryGetOptionalInt(
                    args,
                    i + 2,
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
            Point pnt = position;
            // csharpier-ignore
            IEnumerable<Critter>? spawnedThisTime = critterKind switch
            {
                SupportedCritter.Firefly => SpawnCritterFirefly(location, pnt, pntOffset, arg1, count),
                SupportedCritter.Seagull => SpawnCritterSeagull(location, pnt, pntOffset, arg1, count),
                SupportedCritter.Crab => SpawnCritterCrab(location, pnt, pntOffset, arg1, count),
                SupportedCritter.Birdie => SpawnCritterBirdie(location, pnt, pntOffset, arg1, count),
                SupportedCritter.Butterfly => SpawnCritterButterfly(location, pnt, pntOffset, arg1, count),
                SupportedCritter.Frog => SpawnCritterFrog(location, pnt, pntOffset, arg1, count),
                SupportedCritter.LeaperFrog => SpawnCritterLeaperFrog(location, pnt, pntOffset, arg1, count),
                SupportedCritter.Rabbit => SpawnCritterRabbit(location, pnt, pntOffset, arg1, count),
                SupportedCritter.Squirrel => SpawnCritterSquirrel(location, pnt, pntOffset, arg1, count),
                SupportedCritter.Opossum => SpawnCritterOpossum(location, pnt, pntOffset, arg1, count),
                _ => null,
            };
            if (spawnedThisTime != null)
                spawned.AddRange(spawnedThisTime);
        }
        location.critters.AddRange(spawned);
        return spawned;
    }

    private static Vector2 GetPosOffset(Point posOffset)
    {
        if (posOffset == Point.Zero)
        {
            return new(Random.Shared.Next(Game1.tileSize), Random.Shared.Next(Game1.tileSize));
        }
        return posOffset.ToVector2();
    }

    private static IEnumerable<Critter> SpawnCritterFirefly(
        GameLocation location,
        Point position,
        Point posOffset,
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
            firefly.position += GetPosOffset(posOffset);
            firefly.startingPosition = firefly.position;
            if (c != null && fireflyLight.GetValue(firefly) is LightSource light)
                light.color.Value = (Color)c;
            yield return firefly;
        }
    }

    private static IEnumerable<Critter> SpawnCritterSeagull(
        GameLocation location,
        Point position,
        Point posOffset,
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
            Seagull seagull = new(position.ToVector2() * Game1.tileSize + GetPosOffset(posOffset), startingState);
            if (texture != null)
                seagull.sprite.textureName.Value = texture;
            yield return seagull;
        }
    }

    private static IEnumerable<Critter> SpawnCritterCrab(
        GameLocation location,
        Point position,
        Point posOffset,
        string? texture,
        int count
    )
    {
        if (texture == "T" || !Game1.content.DoesAssetExist<Texture2D>(texture))
            texture = null;
        for (int i = 0; i < count; i++)
        {
            CrabCritter crab = new(position.ToVector2() * Game1.tileSize + GetPosOffset(posOffset));
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
        Point posOffset,
        string? texture,
        int count
    )
    {
        int startingIndex = -1;
        int yOffset = 0;
        if (texture != null)
        {
            string[] parts = texture.Split(":");
            if (parts.Length >= 2)
            {
                texture = parts[0];
                if (!int.TryParse(parts[1], out yOffset))
                {
                    yOffset = 0;
                }
            }
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
            Birdie birdie = new(position.X, position.Y, startIdx);
            birdie.position += new Vector2(-Game1.tileSize / 2, -Game1.tileSize / 2) + GetPosOffset(posOffset);
            birdie.yOffset = yOffset;
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
        Point posOffset,
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
            butterfly.position += GetPosOffset(posOffset);
            if (texture != null)
            {
                butterfly.sprite.textureName.Value = texture;
            }
            yield return butterfly;
        }
    }

    private static IEnumerable<Critter> SpawnCritterFrog(
        GameLocation location,
        Point position,
        Point posOffset,
        string? arg1,
        int count
    )
    {
        for (int i = 0; i < count; i++)
        {
            Frog frog = new(position.ToVector2(), waterLeaper: false);
            frog.position += GetPosOffset(posOffset);
            frog.flip = arg1 == "F";
            yield return frog;
        }
    }

    private static IEnumerable<Critter> SpawnCritterLeaperFrog(
        GameLocation location,
        Point position,
        Point posOffset,
        string? arg1,
        int count
    )
    {
        for (int i = 0; i < count; i++)
        {
            Frog frog = new(position.ToVector2(), waterLeaper: true);
            frog.position += GetPosOffset(posOffset);
            frog.flip = arg1 == "F";
            yield return frog;
        }
    }

    private static IEnumerable<Critter> SpawnCritterRabbit(
        GameLocation location,
        Point position,
        Point posOffset,
        string? texture,
        int count
    )
    {
        bool flipped = Random.Shared.NextBool();
        int baseFrame = -1;
        if (texture != null)
        {
            string[] parts = texture.Split(":");
            if (parts.Length >= 2)
            {
                flipped = parts[1] == "F";
                texture = parts[0];
            }

            if (
                texture == "T"
                || !(
                    (int.TryParse(texture, out baseFrame) && (baseFrame == 74 || baseFrame == 54))
                    || Game1.content.DoesAssetExist<Texture2D>(texture)
                )
            )
            {
                texture = null;
            }
        }
        for (int i = 0; i < count; i++)
        {
            Rabbit rabbit = new(location, position.ToVector2(), flipped);
            rabbit.position += GetPosOffset(posOffset);
            if (baseFrame >= 0)
            {
                rabbit.baseFrame = baseFrame;
                // 74 = winter
                // 54 = not winter
                rabbit.sprite.CurrentFrame = baseFrame == 74 ? 69 : 68;
            }
            else if (texture != null)
            {
                rabbit.sprite.textureName.Value = texture;
                rabbit.baseFrame = 1;
                rabbit.sprite.CurrentFrame = 0;
            }
            yield return rabbit;
        }
    }

    private static IEnumerable<Critter> SpawnCritterSquirrel(
        GameLocation location,
        Point position,
        Point posOffset,
        string? texture,
        int count
    )
    {
        bool flipped = Random.Shared.NextBool();
        if (texture != null)
        {
            string[] parts = texture.Split(":");
            if (parts.Length >= 2)
            {
                flipped = parts[1] == "F";
                texture = parts[0];
            }

            if (texture == "T" || !Game1.content.DoesAssetExist<Texture2D>(texture))
            {
                texture = null;
            }
        }
        for (int i = 0; i < count; i++)
        {
            Squirrel squirrel = new(position.ToVector2(), flipped);
            squirrel.position += GetPosOffset(posOffset);
            if (texture != null)
            {
                squirrel.sprite.textureName.Value = texture;
                squirrel.baseFrame = 0;
                squirrel.sprite.CurrentFrame = 0;
            }
            yield return squirrel;
        }
    }

    private static IEnumerable<Critter> SpawnCritterOpossum(
        GameLocation location,
        Point position,
        Point posOffset,
        string? texture,
        int count
    )
    {
        bool flipped = Random.Shared.NextBool();
        if (texture != null)
        {
            string[] parts = texture.Split(":");
            if (parts.Length >= 2)
            {
                flipped = parts[1] == "F";
                texture = parts[0];
            }

            if (texture == "T" || !Game1.content.DoesAssetExist<Texture2D>(texture))
            {
                texture = null;
            }
        }
        for (int i = 0; i < count; i++)
        {
            Opossum opossum = new(location, position.ToVector2(), flipped);
            opossum.position += GetPosOffset(posOffset);
            if (texture != null)
            {
                opossum.sprite.textureName.Value = texture;
                opossum.baseFrame = 0;
                opossum.sprite.CurrentFrame = 0;
            }
            yield return opossum;
        }
    }
}

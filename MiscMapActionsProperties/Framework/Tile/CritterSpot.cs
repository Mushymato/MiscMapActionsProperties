using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI.Events;
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
        TileProp_Critter,
        ["Back"]
    );
    private static readonly FieldInfo fireflyLight = AccessTools.DeclaredField(typeof(Firefly), "light");
    private static readonly FieldInfo crabSourceRectangle = AccessTools.DeclaredField(
        typeof(CrabCritter),
        "_baseSourceRectangle"
    );

    internal static void Register()
    {
        ModEntry.help.Events.GameLoop.DayStarted += OnDayStarted;
        ModEntry.help.Events.Player.Warped += OnWarped;
        CommonPatch.RegisterTileAndTouch(TileProp_Critter, TileAndTouchCritter);
        TriggerActionManager.RegisterAction(TileProp_Critter, TriggerActionCritter);
    }

    private static void OnDayStarted(object? sender, DayStartedEventArgs e) =>
        SpawnLocationCritters(Game1.currentLocation);

    private static void OnWarped(object? sender, WarpedEventArgs e) => SpawnLocationCritters(e.NewLocation);

    private static void SpawnLocationCritters(GameLocation location)
    {
        if (location == null)
            return;
        foreach ((Vector2 pos, string[] props) in critterSpotsCache.GetProps(location.Map))
            SpawnCritter(location, pos, props, 0, out string _);
    }

    private static bool TriggerActionCritter(string[] args, TriggerActionContext context, out string error)
    {
        if (!ArgUtility.TryGetVector2(args, 1, out Vector2 position, out error, integerOnly: true, "Vector2 position"))
            return false;
        return SpawnCritter(Game1.currentLocation, position, args, 3, out error);
    }

    private static bool TileAndTouchCritter(GameLocation location, string[] args, Farmer farmer, Point source)
    {
        return SpawnCritter(location, source.ToVector2(), args, 1, out _);
    }

    private static bool SpawnCritter(
        GameLocation location,
        Vector2 position,
        string[] args,
        int firstIdx,
        out string error
    )
    {
        error = "";
        bool spawned = false;
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
            // csharpier-ignore
            spawned = critterKind switch
            {
                SupportedCritter.Firefly => SpawnCritterFirefly(location, position, args, i + 1, out error),
                SupportedCritter.Seagull => SpawnCritterSeagull(location, position, args, i + 1, out error),
                SupportedCritter.Crab => SpawnCritterCrab(location, position, args, i + 1, out error),
                SupportedCritter.Birdie => SpawnCritterBirdie(location, position, args, i + 1, out error),
                SupportedCritter.Butterfly => SpawnCritterButterfly(location, position, args, i + 1, out error),
                _ => false,
            } || spawned;
        }
        return spawned;
    }

    private static bool SpawnCritterFirefly(
        GameLocation location,
        Vector2 position,
        string[] args,
        int firstIdx,
        out string error
    )
    {
        if (
            !ArgUtility.TryGetOptional(args, firstIdx, out string? color, out error, name: "string color")
            || !ArgUtility.TryGetOptionalInt(
                args,
                firstIdx + 1,
                out int count,
                out error,
                defaultValue: 1,
                name: "int count"
            )
        )
        {
            return false;
        }
        Color? c = null;
        if (color != null && color != "T" && (c = Utility.StringToColor(color)) != null)
        {
            c = new Color(((Color)c).PackedValue ^ 0x00FFFFFF);
        }
        for (int i = 0; i < count; i++)
        {
            Firefly firefly = new(position);
            firefly.position.X += Random.Shared.Next(Game1.tileSize);
            firefly.position.Y += Random.Shared.Next(Game1.tileSize);
            firefly.startingPosition = firefly.position;
            if (c != null && fireflyLight.GetValue(firefly) is LightSource light)
                light.color.Value = (Color)c;
            location.addCritter(firefly);
        }
        return true;
    }

    private static bool SpawnCritterSeagull(
        GameLocation location,
        Vector2 position,
        string[] args,
        int firstIdx,
        out string error
    )
    {
        if (
            !ArgUtility.TryGetOptional(args, firstIdx, out string? texture, out error, name: "string texture")
            || !ArgUtility.TryGetOptionalInt(
                args,
                firstIdx + 1,
                out int count,
                out error,
                defaultValue: 1,
                name: "int count"
            )
        )
        {
            return false;
        }
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
                    position * Game1.tileSize
                        + new Vector2(Random.Shared.Next(Game1.tileSize), Random.Shared.Next(Game1.tileSize)),
                    startingState
                );
            if (texture != null)
                seagull.sprite.textureName.Value = texture;
            location.addCritter(seagull);
        }
        return false;
    }

    private static bool SpawnCritterCrab(
        GameLocation location,
        Vector2 position,
        string[] args,
        int firstIdx,
        out string error
    )
    {
        if (
            !ArgUtility.TryGetOptional(args, firstIdx, out string? texture, out error, name: "string texture")
            || !ArgUtility.TryGetOptionalInt(
                args,
                firstIdx + 1,
                out int count,
                out error,
                defaultValue: 1,
                name: "int count"
            )
        )
        {
            return false;
        }
        if (texture == "T" || !Game1.content.DoesAssetExist<Texture2D>(texture))
            texture = null;
        for (int i = 0; i < count; i++)
        {
            CrabCritter crab =
                new(
                    position * Game1.tileSize
                        + new Vector2(Random.Shared.Next(Game1.tileSize), Random.Shared.Next(Game1.tileSize))
                );
            if (texture != null)
            {
                crab.sprite.textureName.Value = texture;
                crabSourceRectangle.SetValue(crab, new Rectangle(0, 0, 18, 18));
            }
            location.addCritter(crab);
        }
        return false;
    }

    private static bool SpawnCritterBirdie(
        GameLocation location,
        Vector2 position,
        string[] args,
        int firstIdx,
        out string error
    )
    {
        if (
            !ArgUtility.TryGetOptional(args, firstIdx, out string? texture, out error, name: "string texture")
            || !ArgUtility.TryGetOptionalInt(
                args,
                firstIdx + 1,
                out int count,
                out error,
                defaultValue: 1,
                name: "int count"
            )
        )
        {
            return false;
        }
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
            location.addCritter(birdie);
        }
        return false;
    }

    private static bool SpawnCritterButterfly(
        GameLocation location,
        Vector2 position,
        string[] args,
        int firstIdx,
        out string error
    )
    {
        if (
            !ArgUtility.TryGetOptional(args, firstIdx, out string? texture, out error, name: "string texture")
            || !ArgUtility.TryGetOptionalInt(
                args,
                firstIdx + 1,
                out int count,
                out error,
                defaultValue: 1,
                name: "int count"
            )
        )
        {
            return false;
        }
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
            Butterfly butterfly = new(location, position, forceSummerButterfly: startingIndex != -1, baseFrameOverride: startingIndex);
            butterfly.position += new Vector2(
                Random.Shared.Next(0, Game1.tileSize),
                Random.Shared.Next(0, Game1.tileSize)
            );
            if (texture != null)
            {
                butterfly.sprite.textureName.Value = texture;
            }
            location.addCritter(butterfly);
        }
        return false;
    }
}

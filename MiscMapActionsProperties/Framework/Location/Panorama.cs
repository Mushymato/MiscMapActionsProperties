using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Delegates;

namespace MiscMapActionsProperties.Framework.Location;

internal sealed class PanoramaBackground : Background
{
    private readonly List<TileTAS>? respawningTAS;

    internal PanoramaBackground(
        GameLocation location,
        Texture2D? bgImage,
        int seedValue,
        int chunksWide,
        int chunksHigh,
        int chunkWidth,
        int chunkHeight,
        float zoom,
        int defaultChunkIndex,
        int numChunksInSheet,
        double chanceForDeviation,
        Color c,
        List<TileTAS>? respawningTAS
    )
        : base(
            location,
            bgImage,
            seedValue,
            chunksWide,
            chunksHigh,
            chunkWidth,
            chunkHeight,
            zoom,
            defaultChunkIndex,
            numChunksInSheet,
            chanceForDeviation,
            c
        )
    {
        tempSprites = [];
        this.respawningTAS = respawningTAS;
    }

    internal static PanoramaBackground? FromProp(GameLocation location, string[] args, string[]? tas)
    {
        if (
            !ArgUtility.TryGet(args, 0, out string bgImageStr, out string error, allowBlank: false, "string bgImage")
            || !ArgUtility.TryGetOptional(
                args,
                1,
                out string colorStr,
                out error,
                defaultValue: "White",
                name: "string color"
            )
        )
        {
            ModEntry.Log(error, LogLevel.Error);
            return null;
        }
        Texture2D? bgImage = null;
        float zoom = 4f;
        int chunksWide = 1;
        int chunksHigh = 1;
        int chunkWidth = 1;
        int chunkHeight = 1;
        int numChunksInSheet = 1;
        float chanceForDeviation = 1f;
        if (Game1.content.DoesAssetExist<Texture2D>(bgImageStr))
        {
            bgImage = Game1.content.Load<Texture2D>(bgImageStr);
            if (
                !ArgUtility.TryGetOptionalFloat(args, 2, out zoom, out error, defaultValue: zoom, name: "float zoom")
                || !ArgUtility.TryGetOptionalInt(
                    args,
                    3,
                    out chunksWide,
                    out error,
                    defaultValue: chunksWide,
                    name: "int chunksWide"
                )
                || !ArgUtility.TryGetOptionalInt(
                    args,
                    4,
                    out chunksHigh,
                    out error,
                    defaultValue: chunksHigh,
                    name: "int chunksHigh"
                )
                || !ArgUtility.TryGetOptionalInt(
                    args,
                    5,
                    out chunkWidth,
                    out error,
                    defaultValue: bgImage.Width,
                    name: "int chunkWidth"
                )
                || !ArgUtility.TryGetOptionalInt(
                    args,
                    6,
                    out chunkHeight,
                    out error,
                    defaultValue: bgImage.Height,
                    name: "int chunkHeight"
                )
                || !ArgUtility.TryGetOptionalInt(
                    args,
                    7,
                    out numChunksInSheet,
                    out error,
                    defaultValue: 1,
                    name: "int numChunksInSheet"
                )
                || !ArgUtility.TryGetOptionalFloat(
                    args,
                    8,
                    out chanceForDeviation,
                    out error,
                    defaultValue: 1f,
                    name: "float chanceForDeviation"
                )
            )
            {
                ModEntry.Log(error, LogLevel.Error);
                return null;
            }
        }
        if (Utility.StringToColor(colorStr) is not Color c)
        {
            c = Color.White;
        }

        List<TileTAS>? respawning = null;
        if (tas != null)
        {
            respawning = [];
            foreach (var tasKey in tas)
            {
                if (TASAssetManager.TASData.TryGetValue(tasKey, out TASExt? def))
                {
                    if (def.SpawnInterval <= 0)
                    {
                        ModEntry.Log(
                            $"Cannot use '{tasKey}' in background temporary animated sprites, must have a interval"
                        );
                    }
                    TileTAS tileTAS = new(def, Vector2.Zero);
                    tileTAS.Def.RandMin ??= new();
                    tileTAS.Def.RandMax ??= new();
                    respawning.Add(tileTAS);
                }
            }
            if (respawning.Count == 0)
            {
                respawning = null;
            }
        }

        return new(
            location,
            bgImage,
            (int)Game1.stats.DaysPlayed,
            chunksWide,
            chunksHigh,
            chunkWidth,
            chunkHeight,
            zoom,
            0,
            numChunksInSheet,
            chanceForDeviation,
            c,
            respawning
        );
    }

    public override void update(xTile.Dimensions.Rectangle viewport)
    {
        base.update(viewport);
        for (int i = tempSprites.Count - 1; i >= 0; i--)
        {
            if (tempSprites[i].update(Game1.currentGameTime))
            {
                tempSprites.RemoveAt(i);
            }
        }
        if (respawningTAS != null)
        {
            GameStateQueryContext context = new(location, null, null, null, Game1.random);
            foreach (TileTAS tileTAS in respawningTAS)
            {
                tileTAS.Def.RandMin!.PositionOffset =
                    new Vector2(tileTAS.Def.SourceRect.Width, tileTAS.Def.SourceRect.Height) * tileTAS.Def.Scale * -4;
                tileTAS.Def.RandMax!.PositionOffset = new(viewport.Width, viewport.Height);
                if (tileTAS.TryCreateRespawning(Game1.currentGameTime, context, out TemporaryAnimatedSprite? tas))
                    tempSprites.Insert(0, tas);
            }
        }
    }

    public override void draw(SpriteBatch b)
    {
        if (backgroundImage == null)
        {
            Rectangle destinationRectangle = new(0, 0, Game1.viewport.Width, Game1.viewport.Height);
            b.Draw(
                Game1.staminaRect,
                destinationRectangle,
                Game1.staminaRect.Bounds,
                c,
                0f,
                Vector2.Zero,
                SpriteEffects.None,
                0f
            );
        }
        else
        {
            Vector2 zero = Vector2.Zero;
            Rectangle value = new(0, 0, chunkWidth, chunkHeight);
            for (int j = 0; j < chunks.Length; j++)
            {
                zero.X = position.X + (float)(j * chunkWidth % (chunksWide * chunkWidth)) * zoom;
                zero.Y = position.Y + (float)(j * chunkWidth / (chunksWide * chunkWidth) * chunkHeight) * zoom;
                value.X = chunks[j] * chunkWidth % backgroundImage.Width;
                value.Y = chunks[j] * chunkWidth / backgroundImage.Width * chunkHeight;
                b.Draw(backgroundImage, zero, value, c, 0f, Vector2.Zero, zoom, SpriteEffects.None, 0f);
            }
        }
        for (int i = tempSprites.Count - 1; i >= 0; i--)
        {
            tempSprites[i].draw(b);
        }
    }
}

/// <summary>
/// Add new map property mushymato.MMAP_LightRays T|TextureName
/// If set to T, light rays use LooseSprites\\LightRays
/// Otherwise uses the TextureName if given
/// </summary>
internal static class Panorama
{
    internal static readonly string MapProp_Background = $"{ModEntry.ModId}_Background";
    internal static readonly string MapProp_BackgroundTAS = $"{ModEntry.ModId}_Background/TAS";

    internal static void Register()
    {
        ModEntry.help.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        ModEntry.GameLocation_resetLocalState += ApplyPanoramaBackground;
    }

    private static bool TryGetCustomFieldsOrMapProperty(
        GameLocation location,
        string propKey,
        [NotNullWhen(true)] out string? prop
    )
    {
        prop = null;
        if (
            (location.GetData()?.CustomFields?.TryGetValue(propKey, out prop) ?? false)
            || location.TryGetMapProperty(propKey, out prop)
            || false
        )
            return true;
        return false;
    }

    private static void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        if (Game1.currentLocation != null)
            ApplyPanoramaBackground(sender, Game1.currentLocation);
    }

    private static void ApplyPanoramaBackground(object? sender, GameLocation location)
    {
        if (TryGetCustomFieldsOrMapProperty(location, MapProp_Background, out string? backgroundProp))
        {
            string[] args = ArgUtility.SplitBySpaceQuoteAware(backgroundProp);
            string[]? tas = null;
            if (TryGetCustomFieldsOrMapProperty(location, MapProp_BackgroundTAS, out string? backgroundTASProp))
            {
                tas = ArgUtility.SplitBySpaceQuoteAware(backgroundTASProp);
            }
            Game1.background = PanoramaBackground.FromProp(location, args, tas);
            return;
        }
        Game1.background = null;
    }
}

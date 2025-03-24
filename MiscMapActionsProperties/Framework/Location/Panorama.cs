using System.Diagnostics.CodeAnalysis;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Extensions;
using StardewValley.Locations;
using xTile.Layers;

namespace MiscMapActionsProperties.Framework.Location;

internal enum PanoramaTASSpawnMode
{
    Everywhere,
    Below,
    Right,
    Left,
    Above,
}

internal sealed record PanoramaTASSpawnModeDef(
    PanoramaTASSpawnMode Mode,
    float XStart,
    float XEnd,
    float YStart,
    float YEnd
)
{
    static readonly Regex spawnModePattern =
        new(@"^(Everywhere|Below|Right|Left|Above)(?:@(-?[01]\.?[\d]*)x?(-?[01]\.?[\d]*)?)$");

    internal static PanoramaTASSpawnModeDef? FromString(string spawnModeStr)
    {
        if (spawnModePattern.Match(spawnModeStr.Trim()) is Match match && match.Success)
        {
            if (!Enum.TryParse(match.Groups[1].Value, ignoreCase: true, out PanoramaTASSpawnMode mode))
                return null;
            float xZone = 1f;
            float yZone = 1f;
            float tmp;
            if (match.Groups.Count > 2)
            {
                if (mode == PanoramaTASSpawnMode.Everywhere)
                {
                    if (float.TryParse(match.Groups[2].Value, out tmp))
                        xZone = tmp;
                    if (float.TryParse(match.Groups[3].Value, out tmp))
                        yZone = tmp;
                }
                else if (mode == PanoramaTASSpawnMode.Left || mode == PanoramaTASSpawnMode.Right)
                {
                    if (float.TryParse(match.Groups[2].Value, out tmp))
                        yZone = tmp;
                }
                else if (mode == PanoramaTASSpawnMode.Above || mode == PanoramaTASSpawnMode.Below)
                {
                    if (float.TryParse(match.Groups[2].Value, out tmp))
                        xZone = tmp;
                }
            }
            float xStart = 0f;
            float xEnd = 1f;
            if (xZone < 0)
            {
                xStart = 1f - xZone;
                xEnd = 1f;
            }
            else if (xZone > 0)
            {
                xStart = 0;
                xEnd = xZone;
            }
            float yStart = 0f;
            float yEnd = 1f;
            if (yZone < 0)
            {
                yStart = 1f - yZone;
                yEnd = 1f;
            }
            else if (xZone > 0)
            {
                yStart = 0;
                yEnd = yZone;
            }
            return new PanoramaTASSpawnModeDef(mode, xStart, xEnd, yStart, yEnd);
        }
        return null;
    }
}

internal sealed record PanoramaBgStaticDef(Color Clr, Texture2D? BgImage, Rectangle? SourceRect)
{
    internal static PanoramaBgStaticDef FromProp(string[] args)
    {
        if (
            !ArgUtility.TryGet(args, 0, out string colorStr, out string error, name: "string color")
            || !ArgUtility.TryGetOptional(args, 1, out string bgImageStr, out error, name: "string bgImage")
        )
        {
            ModEntry.Log(error, LogLevel.Error);
            return new(Color.Black, null, null);
        }
        Color? c = Utility.StringToColor(colorStr);
        if (!string.IsNullOrEmpty(bgImageStr) && Game1.temporaryContent.DoesAssetExist<Texture2D>(bgImageStr))
        {
            Texture2D bgImage = Game1.temporaryContent.Load<Texture2D>(bgImageStr);
            if (!ArgUtility.TryGetRectangle(args, 2, out Rectangle sourceRect, out error, "Rectangle sourceRect"))
            {
                sourceRect = bgImage.Bounds;
            }
            return new(c ?? Color.White, bgImage, sourceRect);
        }
        return new(c ?? Color.Black, null, null);
    }
}

internal sealed record PanoramaBgParallaxDef(
    Texture2D BgImage,
    int XSource,
    int YSource,
    int ChunksWide,
    int ChunksHigh,
    int ChunkWidth,
    int ChunkHeight,
    float Scale,
    int DefaultChunkIndex,
    int NumChunksInSheet,
    double ChanceForDeviation,
    Color Clr
)
{
    private int[]? chunks = null;
    internal int[] Chunks => chunks ??= GetChunks();

    private int[] GetChunks()
    {
        Random random = Utility.CreateRandom((int)Game1.stats.DaysPlayed);
        int[] newChunks = new int[ChunksWide * ChunksHigh];
        for (int i = 0; i < ChunksWide * ChunksHigh; i++)
        {
            if (random.NextDouble() < ChanceForDeviation)
            {
                newChunks[i] = random.Next(NumChunksInSheet);
            }
            else
            {
                newChunks[i] = DefaultChunkIndex;
            }
        }
        return newChunks;
    }

    private Vector2 Position = Vector2.Zero;

    internal void UpdatePosition(xTile.Dimensions.Rectangle viewport, Layer layer)
    {
        Position.X =
            0f
            - (viewport.X + viewport.Width / 2)
                / (layer.LayerWidth * 64f)
                * (ChunksWide * ChunkWidth * Scale - viewport.Width);
        Position.Y =
            0f
            - (viewport.Y + viewport.Height / 2)
                / (layer.LayerHeight * 64f)
                * (ChunksHigh * ChunkHeight * Scale - viewport.Height);
    }

    internal static PanoramaBgParallaxDef? FromProp(string[] args)
    {
        if (!ArgUtility.TryGet(args, 0, out string bgImageStr, out string error, allowBlank: false, "string bgImage"))
        {
            ModEntry.Log(error, LogLevel.Error);
            return null;
        }
        if (!Game1.temporaryContent.DoesAssetExist<Texture2D>(bgImageStr))
        {
            return null;
        }
        Texture2D bgImage = Game1.temporaryContent.Load<Texture2D>(bgImageStr);
        if (
            !ArgUtility.TryGetOptionalFloat(args, 1, out float zoom, out error, defaultValue: 4f, name: "float zoom")
            || !ArgUtility.TryGetOptional(
                args,
                2,
                out string colorStr,
                out error,
                defaultValue: "White",
                name: "string color"
            )
            || !ArgUtility.TryGetOptionalInt(args, 3, out int xSource, out error, defaultValue: 0, name: "int xSource")
            || !ArgUtility.TryGetOptionalInt(args, 4, out int ySource, out error, defaultValue: 0, name: "int ySource")
            || !ArgUtility.TryGetOptionalInt(
                args,
                5,
                out int chunkWidth,
                out error,
                defaultValue: bgImage.Width,
                name: "int chunkWidth"
            )
            || !ArgUtility.TryGetOptionalInt(
                args,
                6,
                out int chunkHeight,
                out error,
                defaultValue: bgImage.Height,
                name: "int chunkHeight"
            )
            || !ArgUtility.TryGetOptionalInt(
                args,
                7,
                out int chunksWide,
                out error,
                defaultValue: 1,
                name: "int chunksWide"
            )
            || !ArgUtility.TryGetOptionalInt(
                args,
                8,
                out int chunksHigh,
                out error,
                defaultValue: 1,
                name: "int chunksHigh"
            )
            || !ArgUtility.TryGetOptionalInt(
                args,
                9,
                out int defaultChunkIndex,
                out error,
                defaultValue: bgImage.Height,
                name: "int defaultChunkIndex"
            )
            || !ArgUtility.TryGetOptionalInt(
                args,
                10,
                out int numChunksInSheet,
                out error,
                defaultValue: 1,
                name: "int numChunksInSheet"
            )
            || !ArgUtility.TryGetOptionalFloat(
                args,
                11,
                out float chanceForDeviation,
                out error,
                defaultValue: 1f,
                name: "float chanceForDeviation"
            )
        )
        {
            ModEntry.Log(error, LogLevel.Error);
            return null;
        }
        if (Utility.StringToColor(colorStr) is not Color c)
        {
            c = Color.White;
        }
        return new(
            bgImage,
            xSource,
            ySource,
            chunksWide,
            chunksHigh,
            chunkWidth,
            chunkHeight,
            zoom,
            defaultChunkIndex,
            numChunksInSheet,
            chanceForDeviation,
            c
        );
    }

    internal void Draw(SpriteBatch b)
    {
        Vector2 zero = Vector2.Zero;
        Rectangle sourceRect = new(0, 0, ChunkWidth, ChunkHeight);
        int[] theChunks = Chunks;
        int imgWidth = BgImage.Width - XSource;
        for (int j = 0; j < theChunks.Length; j++)
        {
            zero.X = Position.X + j * ChunkWidth % (ChunksWide * ChunkWidth) * Scale;
            zero.Y = Position.Y + j * ChunkWidth / (ChunksWide * ChunkWidth) * ChunkHeight * Scale;
            sourceRect.X = XSource + theChunks[j] * ChunkWidth % imgWidth;
            sourceRect.Y = YSource + theChunks[j] * ChunkWidth / imgWidth * ChunkHeight;
            b.Draw(BgImage, zero, sourceRect, Clr, 0f, Vector2.Zero, Scale, SpriteEffects.None, 0f);
        }
    }
}

internal sealed class PanoramaBackground(
    GameLocation location,
    PanoramaBgStaticDef bgStatic,
    List<PanoramaBgParallaxDef> bgParallaxList,
    PanoramaTASSpawnModeDef? respawnTASMode,
    List<TileTAS>? respawningTAS
) : Background(location, bgStatic.Clr, false)
{
    internal readonly List<Vector2> BgPos = [Vector2.Zero];

    public override void update(xTile.Dimensions.Rectangle viewport)
    {
        // Update Parallax
        Layer layer = Game1.currentLocation.map.RequireLayer("Back");
        foreach (var bgDef in bgParallaxList)
        {
            bgDef.UpdatePosition(viewport, layer);
        }

        // TAS
        for (int i = tempSprites.Count - 1; i >= 0; i--)
        {
            if (tempSprites[i].update(Game1.currentGameTime))
            {
                tempSprites.RemoveAt(i);
            }
        }
        if (respawnTASMode != null && respawningTAS != null)
        {
            SpawnTAS(respawnTASMode, respawningTAS, layer.LayerWidth * 64f, layer.LayerHeight * 64f);
        }
    }

    internal void SpawnTAS(PanoramaTASSpawnModeDef spawnDef, List<TileTAS> tasList, float viewWidth, float viewHeight)
    {
        GameStateQueryContext context = new(location, null, null, null, Game1.random);
        Vector2 minOffset;
        Vector2 maxOffset;
        foreach (TileTAS tileTAS in tasList)
        {
            float tasWidth = tileTAS.Def.SourceRect.Width * tileTAS.Def.Scale * 4;
            float tasHeight = tileTAS.Def.SourceRect.Height * tileTAS.Def.Scale * 4;
            switch (spawnDef.Mode)
            {
                case PanoramaTASSpawnMode.Below:
                    minOffset = new(viewWidth * spawnDef.XStart - tasWidth, viewHeight);
                    maxOffset = new(viewWidth * spawnDef.XEnd, viewHeight);
                    break;
                case PanoramaTASSpawnMode.Right:
                    minOffset = new(viewWidth, viewHeight * spawnDef.YStart - tasHeight);
                    maxOffset = new(viewWidth, viewHeight * spawnDef.YEnd);
                    break;
                case PanoramaTASSpawnMode.Above:
                    minOffset = new(viewWidth * spawnDef.XStart - tasWidth, -tasHeight);
                    maxOffset = new(viewWidth * spawnDef.XEnd, 0);
                    break;
                case PanoramaTASSpawnMode.Left:
                    minOffset = new(-tasWidth, viewHeight * spawnDef.YStart - tasHeight);
                    maxOffset = new(0, viewHeight * spawnDef.YEnd);
                    break;
                default:
                    minOffset = new(viewWidth * spawnDef.XStart - tasWidth, viewHeight * spawnDef.YStart - tasHeight);
                    maxOffset = new(viewWidth * spawnDef.XEnd, viewHeight * spawnDef.YEnd);
                    break;
            }
            // ModEntry.Log(
            //     $"{viewWidth}x{viewHeight}={spawnDef.Mode}({spawnDef.XStart}-{spawnDef.XEnd},{spawnDef.YStart}-{spawnDef.YEnd}): {minOffset} - {maxOffset}"
            // );
            tileTAS.Def.RandMin!.PositionOffset = minOffset;
            tileTAS.Def.RandMax!.PositionOffset = maxOffset;
            if (tileTAS.TryCreateRespawning(Game1.currentGameTime, context, out TemporaryAnimatedSprite? tas))
                tempSprites.Insert(0, tas);
        }
    }

    public override void draw(SpriteBatch b)
    {
        // Static color
        if (bgStatic.BgImage != null || bgStatic.Clr != Color.Black)
        {
            b.Draw(
                bgStatic.BgImage ?? Game1.staminaRect,
                new(0, 0, Game1.viewport.Width, Game1.viewport.Height),
                bgStatic.SourceRect ?? Game1.staminaRect.Bounds,
                bgStatic.Clr,
                0f,
                Vector2.Zero,
                SpriteEffects.None,
                0f
            );
        }

        // paralax
        foreach (var bgDef in bgParallaxList)
        {
            bgDef.Draw(b);
        }

        // TAS
        for (int i = tempSprites.Count - 1; i >= 0; i--)
        {
            tempSprites[i].draw(b);
        }
    }
}

/// <summary>
/// Add several new map properties for displaying a BG in the current area.
/// mushymato.MMAP_BgStatic <color> [bgImage]
///     changes colors in the back, optionally draw a sprite
/// mushymato.MMAP_Background.{n} <bgImage> [scale] [color] [xSource] [ySource] [chunkWidth] [chunkHeight] [chunksWide] [chunksHigh] [defaultChunkIndex] [numChunksInSheet] [chanceForDeviation]
///     draws optionally repeating sprite that spawns the screen
/// mushymato.MMAP_BgTAS [SpawnMode] <tasId>+
///     temporary animated sprite go brrrrr
/// </summary>
internal static class Panorama
{
    internal static readonly string MapProp_BgStatic = $"{ModEntry.ModId}_BgStatic";
    internal static readonly string MapProp_BgParallaxPrefix = $"{ModEntry.ModId}_BgParallax.";
    internal static readonly string MapProp_BgTASOnce = $"{ModEntry.ModId}_BgTAS/Once";
    internal static readonly string MapProp_BgTASRespawning = $"{ModEntry.ModId}_BgTAS/Respawning";

    internal static void Register()
    {
        ModEntry.help.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        CommonPatch.GameLocation_resetLocalState += GameLocation_resetLocalState_Postfix;

        ModEntry.harm.Patch(
            AccessTools.DeclaredMethod(typeof(Game1), nameof(Game1.updateWeather)),
            transpiler: new HarmonyMethod(typeof(Panorama), nameof(Game1_updateWeather_Transpiler))
        );
    }

    private static IEnumerable<CodeInstruction> Game1_updateWeather_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        try
        {
            CodeMatcher matcher = new(instructions, generator);

            // IL_0141: call class StardewValley.GameLocation StardewValley.Game1::get_currentLocation()
            // IL_0146: isinst StardewValley.Locations.IslandNorth
            // IL_014b: brtrue.s IL_015c
            matcher.MatchEndForward(
                [
                    new(OpCodes.Call, AccessTools.PropertyGetter(typeof(Game1), nameof(Game1.currentLocation))),
                    new(OpCodes.Isinst, typeof(IslandNorth)),
                    new(OpCodes.Brtrue_S),
                ]
            );
            Label lbl = (Label)matcher.Operand;
            matcher
                .Advance(1)
                .InsertAndAdvance(
                    [
                        new(OpCodes.Ldsfld, AccessTools.Field(typeof(Game1), nameof(Game1.background))),
                        new(OpCodes.Brtrue_S, lbl),
                    ]
                );

            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in Game1_updateWeather_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
        }
    }

    private static void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        if (Game1.currentLocation != null)
            ApplyPanoramaBackground(Game1.currentLocation);
    }

    private static void GameLocation_resetLocalState_Postfix(object? sender, CommonPatch.ResetLocalStateArgs e)
    {
        ApplyPanoramaBackground(e.Location);
    }

    private static bool TryGetTASList(
        GameLocation location,
        string propKey,
        [NotNullWhen(true)] out PanoramaTASSpawnModeDef? spawnMode,
        [NotNullWhen(true)] out List<TileTAS>? tasList
    )
    {
        spawnMode = null;
        tasList = null;
        if (
            CommonPatch.TryGetCustomFieldsOrMapProperty(location, propKey, out string? backgroundTASProp)
            && ArgUtility.SplitBySpaceQuoteAware(backgroundTASProp) is string[] tas
        )
        {
            if (tas.Length >= 3 && (spawnMode = PanoramaTASSpawnModeDef.FromString(tas[0])) != null)
            {
                tasList = [];
                for (int i = 2; i < tas.Length; i++)
                {
                    if (!int.TryParse(tas[i - 1], out int tasCount))
                        continue;
                    for (int j = 0; j < tasCount; j++)
                    {
                        string tasKey = tas[i];
                        if (TASAssetManager.TASData.TryGetValue(tasKey, out TASExt? def))
                        {
                            TileTAS tileTAS = new(def, Vector2.Zero);
                            tileTAS.Def.RandMin ??= new();
                            tileTAS.Def.RandMax ??= new();
                            tasList.Add(tileTAS);
                        }
                        else
                        {
                            ModEntry.LogOnce($"No mushymato.MMAP/TAS '{tasKey}' defined", LogLevel.Warn);
                            break;
                        }
                    }
                }
                if (tasList.Count == 0)
                {
                    tasList = null;
                }
            }
        }
        return tasList != null;
    }

    private static void ApplyPanoramaBackground(GameLocation location)
    {
        // GameLocation location, PanoramaBgStaticDef bgStatic, List<TileTAS>? respawningTAS
        PanoramaBgStaticDef? bgStatic = null;
        if (CommonPatch.TryGetCustomFieldsOrMapProperty(location, MapProp_BgStatic, out string? bgStaticProp))
        {
            string[] args = ArgUtility.SplitBySpaceQuoteAware(bgStaticProp);
            bgStatic = PanoramaBgStaticDef.FromProp(args);
        }

        List<PanoramaBgParallaxDef> bgParallaxList = [];
        int i = 0;
        while (
            CommonPatch.TryGetCustomFieldsOrMapProperty(
                location,
                string.Concat(MapProp_BgParallaxPrefix, i.ToString()),
                out string? bgParallaxProp
            )
        )
        {
            string[] args = ArgUtility.SplitBySpaceQuoteAware(bgParallaxProp);
            if (PanoramaBgParallaxDef.FromProp(args) is PanoramaBgParallaxDef bgDef)
                bgParallaxList.Add(bgDef);
            i++;
        }

        TryGetTASList(
            location,
            MapProp_BgTASRespawning,
            out PanoramaTASSpawnModeDef? spawnDef,
            out List<TileTAS>? respawning
        );

        if (bgStatic != null || bgParallaxList.Any() || (spawnDef != null && respawning != null))
        {
            PanoramaBackground panoramaBg =
                new(location, bgStatic ?? new(Color.Black, null, null), bgParallaxList, spawnDef, respawning);
            if (
                TryGetTASList(
                    location,
                    MapProp_BgTASOnce,
                    out PanoramaTASSpawnModeDef? onceSpawnDef,
                    out List<TileTAS>? onceTas
                )
            )
            {
                Layer layer = location.map.RequireLayer("Back");
                panoramaBg.SpawnTAS(onceSpawnDef, onceTas, layer.LayerWidth * 64f, layer.LayerHeight * 64f);
            }
            Game1.background = panoramaBg;
        }
        else
        {
            Game1.background = null;
        }
    }
}

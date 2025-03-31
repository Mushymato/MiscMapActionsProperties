using System.Reflection.Emit;
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

namespace MiscMapActionsProperties.Framework.Location;

public sealed class ParallaxLayerData
{
    private string? IdImpl = null;
    public string Id
    {
        get => IdImpl ??= (Texture ?? Color ?? "None");
        set => IdImpl = value;
    }
    public string? Texture = null;
    public Rectangle SourceRect = Rectangle.Empty;
    public string? Color = null;
    public float Scale = 4f;
    public Vector2 DrawOffset = Vector2.Zero;
    public Vector2 ParallaxRate = Vector2.One;
    public bool RepeatX = false;
    public bool RepeatY = false;
    public Vector2 Velocity = Vector2.Zero;
    public int ChunksInSheet = 1;
    public double ChunksDeviationChance = 1f;
}

public sealed class PanoramaData
{
    public string? Condition = null;
    public string? BackingTexture = null;
    public Rectangle BackingSourceRect = Rectangle.Empty;
    public string? BackingColor = null;
    public string? BackingColorNighttime = null;
    public List<ParallaxLayerData>? ParallaxLayers = null;
    public List<MapWideTAS>? OnetimeTAS = null;
    public List<MapWideTAS>? RespawnTAS = null;
}

internal sealed record PanoramaParallaxContext(ParallaxLayerData Data, Texture2D Texture, Rectangle SourceRect)
{
    private Vector2 Position = Vector2.Zero;
    private Vector2 ScrollOffset = Vector2.Zero;
    private readonly Color TxColor = Utility.StringToColor(Data.Color) ?? Color.White;
    private readonly float ScaledWidth = SourceRect.Width * Data.Scale;
    private readonly float ScaledHeight = SourceRect.Height * Data.Scale;

    internal static PanoramaParallaxContext? FromData(ParallaxLayerData data)
    {
        if (!Game1.temporaryContent.DoesAssetExist<Texture2D>(data.Texture))
            return null;
        Texture2D texture = Game1.temporaryContent.Load<Texture2D>(data.Texture);
        return new(data, texture, data.SourceRect.IsEmpty ? texture.Bounds : data.SourceRect);
    }

    internal void UpdatePosition(xTile.Dimensions.Rectangle viewport, xTile.Layers.Layer layer)
    {
        // csharpier-ignore
        Position.X = 0f - (viewport.X + viewport.Width / 2) / (layer.LayerWidth * 64f) * (Data.ParallaxRate.X * ScaledWidth - viewport.Width);
        // csharpier-ignore
        Position.Y = 0f - (viewport.Y + viewport.Height / 2) / (layer.LayerHeight * 64f) * (Data.ParallaxRate.Y * ScaledHeight - viewport.Height);

        GameTime time = Game1.currentGameTime;
        ScrollOffset.X = (ScrollOffset.X + time.ElapsedGameTime.Milliseconds * Data.Velocity.X) % ScaledWidth;
        ScrollOffset.Y = (ScrollOffset.Y + time.ElapsedGameTime.Milliseconds * Data.Velocity.Y) % ScaledHeight;
    }

    private IEnumerable<Vector2> DrawPositions()
    {
        var viewport = Game1.viewport;
        float i;
        float j;
        float posX = Position.X + Data.DrawOffset.X + ScrollOffset.X;
        float posY = Position.Y + Data.DrawOffset.Y + ScrollOffset.Y;
        // repeat both, i.e. tile to fill screen
        if (Data.RepeatX && Data.RepeatY)
        {
            for (i = posX - ScaledWidth; i > -ScaledWidth; i -= ScaledWidth)
            {
                for (j = posY - ScaledHeight; j > -ScaledHeight; j -= ScaledHeight)
                {
                    yield return new(i, j);
                }
                for (j = posY; j < viewport.Height; j += ScaledHeight)
                {
                    yield return new(i, j);
                }
            }
            for (i = posX; i < viewport.Width; i += ScaledWidth)
            {
                for (j = posY - ScaledHeight; j > -ScaledHeight; j -= ScaledHeight)
                {
                    yield return new(i, j);
                }
                for (j = posY; j < viewport.Height; j += ScaledHeight)
                {
                    yield return new(i, j);
                }
            }
            yield break;
        }
        // repeat only X or only Y or neither
        yield return Position + Data.DrawOffset + ScrollOffset;
        if (Data.RepeatX)
        {
            for (i = posX - ScaledWidth; i > -ScaledWidth; i -= ScaledWidth)
                yield return new(i, posY);
            for (i = posX + ScaledWidth; i < viewport.Width; i += ScaledWidth)
                yield return new(i, posY);
        }
        else if (Data.RepeatY)
        {
            for (i = posY - ScaledHeight; i > -ScaledHeight; i -= ScaledHeight)
                yield return new(posX, i);
            for (i = posX + ScaledHeight; i < viewport.Height; i += ScaledHeight)
                yield return new(posX, i);
        }
    }

    internal void Draw(SpriteBatch b)
    {
        // Vector2 zero = Vector2.Zero;
        // Rectangle sourceRect = new(0, 0, ChunkWidth, ChunkHeight);
        // int[] theChunks = Chunks;
        // int imgWidth = BgImage.Width - XSource;
        // for (int j = 0; j < theChunks.Length; j++)
        // {
        //     zero.X = Position.X + j * ChunkWidth % (ChunksWide * ChunkWidth) * Scale;
        //     zero.Y = Position.Y + j * ChunkWidth / (ChunksWide * ChunkWidth) * ChunkHeight * Scale;
        //     sourceRect.X = XSource + theChunks[j] * ChunkWidth % imgWidth;
        //     sourceRect.Y = YSource + theChunks[j] * ChunkWidth / imgWidth * ChunkHeight;
        //     b.Draw(BgImage, zero, sourceRect, Clr, 0f, Vector2.Zero, Scale, SpriteEffects.None, 0f);
        // }

        // repeat X
        // no tiling
        foreach (Vector2 pos in DrawPositions())
        {
            b.Draw(Texture, pos, SourceRect, TxColor, 0f, Vector2.Zero, Data.Scale, SpriteEffects.None, 0f);
        }
    }
}

internal sealed class PanoramaBackground(GameLocation location) : Background(location, Color.Black, false)
{
    private readonly List<PanoramaParallaxContext> parallaxCtx = [];
    private readonly List<ValueTuple<MapWideTAS, TASContext>> respawningTAS = [];
    private Rectangle backingSourceRect = Rectangle.Empty;

    internal void SetData(PanoramaData data)
    {
        if (Game1.temporaryContent.DoesAssetExist<Texture2D>(data.BackingTexture))
        {
            backgroundImage = Game1.temporaryContent.Load<Texture2D>(data.BackingTexture);
            backingSourceRect = data.BackingSourceRect.IsEmpty ? backgroundImage.Bounds : backingSourceRect;
        }
        parallaxCtx.Clear();
        c = Utility.StringToColor(data.BackingColor) ?? Color.Black;
        if (data.ParallaxLayers != null)
        {
            foreach (var parallax in data.ParallaxLayers)
            {
                if (PanoramaParallaxContext.FromData(parallax) is PanoramaParallaxContext pCtx)
                {
                    parallaxCtx.Add(pCtx);
                }
            }
        }
        respawningTAS.Clear();
        if (data.RespawnTAS != null)
        {
            foreach (MapWideTAS mwTAS in data.RespawnTAS)
            {
                foreach (TASExt tasExt in TASAssetManager.GetTASExtList(mwTAS.TAS))
                {
                    respawningTAS.Add(new(mwTAS, new(tasExt) { Pos = Vector2.Zero }));
                }
            }
        }
    }

    public override void update(xTile.Dimensions.Rectangle viewport)
    {
        // Update Parallax
        xTile.Layers.Layer layer = Game1.currentLocation.map.RequireLayer("Back");
        foreach (var pCtx in parallaxCtx)
        {
            pCtx.UpdatePosition(viewport, layer);
        }

        // TAS
        for (int i = tempSprites.Count - 1; i >= 0; i--)
        {
            if (tempSprites[i].update(Game1.currentGameTime))
            {
                tempSprites.RemoveAt(i);
            }
        }
        if (respawningTAS.Any())
        {
            float width = layer.LayerWidth * 64f;
            float height = layer.LayerHeight * 64f;
            foreach (var mwTAS in respawningTAS)
                SpawnTAS(mwTAS.Item1, mwTAS.Item2, width, height, true);
        }
    }

    internal void InsertTAS(TemporaryAnimatedSprite tas) => tempSprites.Insert(0, tas);

    internal void SpawnTAS(MapWideTAS mwTAS, TASContext tileTAS, float viewWidth, float viewHeight, bool respawning)
    {
        Vector2 minOffset;
        Vector2 maxOffset;
        float tasWidth = tileTAS.Def.SourceRect.Width * tileTAS.Def.Scale * 4;
        float tasHeight = tileTAS.Def.SourceRect.Height * tileTAS.Def.Scale * 4;
        switch (mwTAS.Mode)
        {
            case MapWideTASMode.Below:
                minOffset = new(viewWidth * mwTAS.XStart - tasWidth, viewHeight);
                maxOffset = new(viewWidth * mwTAS.XEnd, viewHeight);
                break;
            case MapWideTASMode.Right:
                minOffset = new(viewWidth, viewHeight * mwTAS.YStart - tasHeight);
                maxOffset = new(viewWidth, viewHeight * mwTAS.YEnd);
                break;
            case MapWideTASMode.Above:
                minOffset = new(viewWidth * mwTAS.XStart - tasWidth, -tasHeight);
                maxOffset = new(viewWidth * mwTAS.XEnd, 0);
                break;
            case MapWideTASMode.Left:
                minOffset = new(-tasWidth, viewHeight * mwTAS.YStart - tasHeight);
                maxOffset = new(0, viewHeight * mwTAS.YEnd);
                break;
            default:
                minOffset = new(viewWidth * mwTAS.XStart - tasWidth, viewHeight * mwTAS.YStart - tasHeight);
                maxOffset = new(viewWidth * mwTAS.XEnd, viewHeight * mwTAS.YEnd);
                break;
        }

        // tileTAS.Def.RandMin!.PositionOffset = minOffset / 4f;
        // tileTAS.Def.RandMax!.PositionOffset = maxOffset / 4f;
        // ModEntry.LogOnce(
        //     $"{viewWidth}x{viewHeight}={mwTAS.Mode}({mwTAS.XStart}-{mwTAS.XEnd},{mwTAS.YStart}-{mwTAS.YEnd}): {tileTAS.Def.RandMin!.PositionOffset} - {tileTAS.Def.RandMax!.PositionOffset}"
        // );
        tileTAS.PosOffsetMin = minOffset;
        tileTAS.PosOffsetMax = maxOffset;
        GameStateQueryContext context = new(location, null, null, null, Game1.random);
        if (respawning)
        {
            for (int i = 0; i < mwTAS.Count; i++)
            {
                tileTAS.TryCreateRespawning(Game1.currentGameTime, context, InsertTAS);
            }
        }
        else
        {
            for (int i = 0; i < mwTAS.Count; i++)
            {
                if (tileTAS.TryCreate(context, out TemporaryAnimatedSprite? tas))
                {
                    InsertTAS(tas);
                }
            }
        }
    }

    public override void draw(SpriteBatch b)
    {
        // static
        if (backgroundImage != null || c != Color.Black)
        {
            b.Draw(
                backgroundImage ?? Game1.staminaRect,
                new(0, 0, Game1.viewport.Width, Game1.viewport.Height),
                backgroundImage != null ? backingSourceRect : Game1.staminaRect.Bounds,
                c,
                0f,
                Vector2.Zero,
                SpriteEffects.None,
                0f
            );
        }

        // paralax
        foreach (var bgDef in parallaxCtx)
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
    internal static readonly string MapProp_PanoramaPrefix = $"{ModEntry.ModId}_Panorama";

    internal static readonly string Asset_Panorama = $"{ModEntry.ModId}/Panorama";

    internal static void Register()
    {
        ModEntry.help.Events.Content.AssetRequested += OnAssetRequested;
        ModEntry.help.Events.Content.AssetsInvalidated += OnAssetInvalidated;

        ModEntry.help.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        CommonPatch.GameLocation_resetLocalState += GameLocation_resetLocalState_Postfix;

        ModEntry.harm.Patch(
            AccessTools.DeclaredMethod(typeof(Game1), nameof(Game1.updateWeather)),
            transpiler: new HarmonyMethod(typeof(Panorama), nameof(Game1_updateWeather_Transpiler))
        );

        ModEntry.help.ConsoleCommands.Add("mmap_reset_bg", "Reload current area background", ConsoleReloadBg);
    }

    private static void ConsoleReloadBg(string arg1, string[] arg2)
    {
        // patch reload mushymato.MMAP.Example
        // mmap_reset_bg
        if (!Context.IsWorldReady)
            return;
        if (Game1.currentLocation != null)
        {
            _bgData = null;
            ApplyPanoramaBackground(Game1.currentLocation);
        }
    }

    private static Dictionary<string, PanoramaData>? _bgData = null;
    internal static Dictionary<string, PanoramaData> BgData =>
        _bgData ??= Game1.content.Load<Dictionary<string, PanoramaData>>(Asset_Panorama);

    private static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.Name.IsEquivalentTo(Asset_Panorama))
            e.LoadFromModFile<Dictionary<string, PanoramaData>>("assets/panorama.json", AssetLoadPriority.Exclusive);
    }

    private static void OnAssetInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        if (e.NamesWithoutLocale.Any(an => an.IsEquivalentTo(Asset_Panorama)))
            _bgData = null;
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

    private static void GetPanoramaData(GameLocation location) { }

    private static void ApplyPanoramaBackground(GameLocation location)
    {
        // GameLocation location, PanoramaBgStaticDef bgStatic, List<TileTAS>? respawningTAS
        if (CommonPatch.TryGetCustomFieldsOrMapProperty(location, MapProp_PanoramaPrefix, out string? bgId))
        {
            if (BgData.TryGetValue(bgId, out PanoramaData? data))
            {
                if (Game1.background is not PanoramaBackground Panorama)
                    Panorama = new(location);
                Panorama.SetData(data);
                if (data.OnetimeTAS != null)
                {
                    xTile.Layers.Layer layer = Game1.currentLocation.map.RequireLayer("Back");
                    float width = layer.LayerWidth * 64f;
                    float height = layer.LayerHeight * 64f;
                    foreach (var mwTAS in data.OnetimeTAS)
                    {
                        foreach (TASExt tasExt in TASAssetManager.GetTASExtList(mwTAS.TAS))
                        {
                            Panorama.SpawnTAS(mwTAS, new(tasExt) { Pos = Vector2.Zero }, width, height, false);
                        }
                    }
                }
                Game1.background = Panorama;
            }
            else
            {
                ModEntry.Log($"No {ModEntry.ModId}/Panorama with Id '{bgId}' found", LogLevel.Warn);
            }
        }
        else
        {
            Game1.background = null;
        }
    }
}

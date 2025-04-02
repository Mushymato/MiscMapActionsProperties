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

public abstract class PanoramaSharedData
{
    private string? IdImpl = null;
    public string Id
    {
        get => IdImpl ??= (Texture ?? Color ?? "None");
        set => IdImpl = value;
    }
    public string? Condition = null;
    public string? Texture = null;
    public Rectangle SourceRect = Rectangle.Empty;
    public string? Color = null;
}

public sealed class BackingData : PanoramaSharedData;

public enum ParallaxAlignMode
{
    Start = 0,
    Middle = 1,
    End = 2,
}

public sealed class ParallaxLayerData : PanoramaSharedData
{
    public float Scale = 4f;
    public Vector2 DrawOffset = Vector2.Zero;
    public Vector2 ParallaxRate = Vector2.One;
    public bool RepeatX = false;
    public ParallaxAlignMode AlignX = ParallaxAlignMode.Middle;
    public bool RepeatY = false;
    public ParallaxAlignMode AlignY = ParallaxAlignMode.Middle;
    public Vector2 Velocity = Vector2.Zero;
    public float Alpha = 1f;
}

public sealed class PanoramaData
{
    public List<BackingData>? BackingDay = null;
    public List<BackingData>? BackingSunset = null;
    public List<BackingData>? BackingNight = null;
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

    internal static PanoramaParallaxContext? FromData(ParallaxLayerData data, GameStateQueryContext context)
    {
        if (
            !GameStateQuery.CheckConditions(data.Condition, context)
            && !Game1.temporaryContent.DoesAssetExist<Texture2D>(data.Texture)
        )
            return null;
        Texture2D texture =
            data.Texture == "LooseSprites/Cursors"
                ? Game1.mouseCursors
                : Game1.temporaryContent.Load<Texture2D>(data.Texture);
        return new(data, texture, data.SourceRect.IsEmpty ? texture.Bounds : data.SourceRect);
    }

    internal void UpdatePosition(xTile.Dimensions.Rectangle viewport, xTile.Layers.Layer layer)
    {
        // csharpier-ignore
        switch (Data.AlignX)
        {
            case ParallaxAlignMode.Start:
                Position.X = 0f;
                break;
            case ParallaxAlignMode.Middle:
                Position.X = 0f - (viewport.X + viewport.Width / 2) / (layer.LayerWidth * 64f) * (Data.ParallaxRate.X * ScaledWidth - viewport.Width);
                break;
            case ParallaxAlignMode.End:
                Position.X = viewport.Width - ScaledWidth;
                break;
        }
        // csharpier-ignore
        switch (Data.AlignY)
        {
            case ParallaxAlignMode.Start:
                Position.Y = 0f;
                break;
            case ParallaxAlignMode.Middle:
                Position.Y = 0f - (viewport.Y + viewport.Height / 2) / (layer.LayerHeight * 64f) * (Data.ParallaxRate.Y * ScaledHeight - viewport.Height);
                break;
            case ParallaxAlignMode.End:
                Position.Y = viewport.Height - ScaledHeight;
                break;
        }

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
        // repeat X
        // no tiling
        foreach (Vector2 pos in DrawPositions())
        {
            b.Draw(
                Texture,
                pos,
                SourceRect,
                TxColor * Data.Alpha,
                0f,
                Vector2.Zero,
                Data.Scale,
                SpriteEffects.None,
                0f
            );
        }
    }
}

internal sealed record BackingContext(Texture2D Texture, Rectangle SourceRect, Color Color)
{
    internal static TArgs? GetFirstMatchingData<TArgs>(List<TArgs>? dataList, GameStateQueryContext context)
        where TArgs : PanoramaSharedData
    {
        if (dataList == null)
            return default;
        return dataList.FirstOrDefault(data => GameStateQuery.CheckConditions(data.Condition, context));
    }

    internal static BackingContext? FromDataList(List<BackingData>? dataList, GameStateQueryContext context)
    {
        if (GetFirstMatchingData(dataList, context) is BackingData backing)
        {
            if (Game1.temporaryContent.DoesAssetExist<Texture2D>(backing.Texture))
            {
                Texture2D texture = Game1.temporaryContent.Load<Texture2D>(backing.Texture);
                return new(
                    texture,
                    backing.SourceRect.IsEmpty ? texture.Bounds : backing.SourceRect,
                    Utility.StringToColor(backing.Color) ?? Color.White
                );
            }
            else
            {
                return new(
                    Game1.staminaRect,
                    Game1.staminaRect.Bounds,
                    Utility.StringToColor(backing.Color) ?? Color.Black
                );
            }
        }
        return null;
    }

    internal void Draw(SpriteBatch b, Rectangle targetRect, float colorMult)
    {
        b.Draw(Texture, targetRect, SourceRect, Color * colorMult, 0f, Vector2.Zero, SpriteEffects.None, 0f);
    }
}

internal sealed class PanoramaBackground(GameLocation location) : Background(location, Color.Black, false)
{
    private readonly List<PanoramaParallaxContext> parallaxCtx = [];
    private readonly List<ValueTuple<MapWideTAS, TASContext>> respawningTAS = [];

    private BackingContext? Day = null;
    private BackingContext? Sunset = null;
    private BackingContext? Night = null;

    private readonly int startingMinutes = Utility.ConvertTimeToMinutes(Game1.getStartingToGetDarkTime(location));
    private readonly int moderatelyMinutes = Utility.ConvertTimeToMinutes(Game1.getModeratelyDarkTime(location));
    private readonly int trulyMinutes = Utility.ConvertTimeToMinutes(Game1.getTrulyDarkTime(location));

    private readonly int startingToTrulyMinutes = Utility.CalculateMinutesBetweenTimes(
        Game1.getStartingToGetDarkTime(location),
        Game1.getTrulyDarkTime(location)
    );
    private readonly int startingToModerateMinutes = Utility.CalculateMinutesBetweenTimes(
        Game1.getStartingToGetDarkTime(location),
        Game1.getModeratelyDarkTime(location)
    );
    private readonly int moderateToTrulyMinutes = Utility.CalculateMinutesBetweenTimes(
        Game1.getModeratelyDarkTime(location),
        Game1.getTrulyDarkTime(location)
    );

    internal static IEnumerable<TArgs> GetAllMatchingData<TArgs>(List<TArgs>? dataList, GameStateQueryContext context)
        where TArgs : PanoramaSharedData
    {
        if (dataList == null)
            return [];
        return dataList.Where(data => GameStateQuery.CheckConditions(data.Condition, context));
    }

    internal void SetData(PanoramaData data, GameStateQueryContext context)
    {
        Day = BackingContext.FromDataList(data.BackingDay, context);
        Sunset = BackingContext.FromDataList(data.BackingSunset, context);
        Night = BackingContext.FromDataList(data.BackingNight, context);

        parallaxCtx.Clear();
        foreach (var parallax in GetAllMatchingData(data.ParallaxLayers, context))
        {
            if (PanoramaParallaxContext.FromData(parallax, context) is PanoramaParallaxContext pCtx)
            {
                parallaxCtx.Add(pCtx);
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
        // if (hasbacking)
        // {
        //     float gettingDark = GettingDarkMult();
        //     Rectangle viewportRect = new(0, 0, Game1.viewport.Width, Game1.viewport.Height);
        //     if (gettingDark != 1f)
        //     {
        //         b.Draw(bDayTexture, viewportRect, bDayRectangle, bDayColor, 0f, Vector2.Zero, SpriteEffects.None, 0f);
        //     }
        //     if (gettingDark > 0f)
        //     {
        //         b.Draw(
        //             bNightTexture,
        //             viewportRect,
        //             bNightRectangle,
        //             bNightColor * gettingDark,
        //             0f,
        //             Vector2.Zero,
        //             SpriteEffects.None,
        //             0f
        //         );
        //     }
        // }

        // internal float GettingDarkMult()
        // {
        //     float currMinutes =
        //         Utility.ConvertTimeToMinutes(Game1.timeOfDay)
        //         + ((float)Game1.gameTimeInterval / Game1.realMilliSecondsPerGameMinute);
        //     if (currMinutes > trulyDarkMinutes)
        //         return 1f;
        //     if (currMinutes > gettingDarkMinutes)
        //         return (currMinutes - gettingDarkMinutes) / totalDarkenMinutes;
        //     return 0f;
        // }
        Rectangle viewportRect = new(0, 0, Game1.viewport.Width, Game1.viewport.Height);
        if (Day != null && Night != null)
        {
            float currMinutes =
                Utility.ConvertTimeToMinutes(Game1.timeOfDay)
                + ((float)Game1.gameTimeInterval / Game1.realMilliSecondsPerGameMinute);

            // Day/Night Curve
            if (currMinutes >= trulyMinutes)
            {
                Night.Draw(b, viewportRect, 1f);
            }
            else if (currMinutes <= startingMinutes)
            {
                Day.Draw(b, viewportRect, 1f);
            }
            else
            {
                float multDarken = (currMinutes - startingMinutes) / startingToTrulyMinutes;
                Day.Draw(b, viewportRect, 1f);
                Night.Draw(b, viewportRect, multDarken);
                if (Sunset != null)
                {
                    if (currMinutes < moderatelyMinutes)
                    {
                        float multSunset = (currMinutes - startingMinutes) / startingToModerateMinutes;
                        Sunset.Draw(b, viewportRect, multSunset);
                    }
                    else
                    {
                        float multSunset = (trulyMinutes - currMinutes) / moderateToTrulyMinutes;
                        Sunset.Draw(b, viewportRect, multSunset);
                    }
                }
            }
        }
        else
        {
            Day?.Draw(b, viewportRect, 1f);
        }

        // parallax
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
/// Add a new map property for displaying a BG in the current area.
/// mushymato.MMAP_Panorama [panoramaKey]
///     Choose which panorama data to use.
/// mushymato.MMAP/Panorama
///     Panorama asset
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
            RecheckPanoramaBackground(Game1.currentLocation);
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
            RecheckPanoramaBackground(Game1.currentLocation);
    }

    private static void GameLocation_resetLocalState_Postfix(object? sender, CommonPatch.ResetLocalStateArgs e)
    {
        RecheckPanoramaBackground(e.Location);
    }

    private static void RecheckPanoramaBackground(GameLocation location)
    {
        // GameLocation location, PanoramaBgStaticDef bgStatic, List<TileTAS>? respawningTAS
        if (CommonPatch.TryGetCustomFieldsOrMapProperty(location, MapProp_PanoramaPrefix, out string? bgId))
        {
            if (BgData.TryGetValue(bgId, out PanoramaData? data))
            {
                GameStateQueryContext context = new(location, Game1.player, null, null, null);
                if (Game1.background is not PanoramaBackground Panorama)
                    Panorama = new(location);
                Panorama.SetData(data, context);
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

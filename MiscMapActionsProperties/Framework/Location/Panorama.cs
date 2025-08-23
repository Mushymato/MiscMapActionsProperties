using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MiscMapActionsProperties.Framework.Wheels;
using Mushymato.ExtendedTAS;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Extensions;
using StardewValley.Locations;
using StardewValley.Triggers;

namespace MiscMapActionsProperties.Framework.Location;

public sealed class MapWideTAS
{
    public string? IdImpl = null;
    public string Id
    {
        get => IdImpl ??= string.Join('-', TAS);
        set => IdImpl = value;
    }
    public string? Condition = null;
    public List<string> TAS = [];
    public int Count = 1;
    public float XStart = 0f;
    public float XEnd = 1f;
    public float YStart = 0f;
    public float YEnd = 1f;
}

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
    Start,
    End,
}

public enum ShowDuringMode
{
    Any,
    Day,
    Sunset,
    Night,
}

public sealed class ParallaxLayerData : PanoramaSharedData
{
    public float Scale = 4f;
    public float Alpha = 1f;
    public bool RepeatX = false;
    public bool RepeatY = false;
    public ParallaxAlignMode AlignX = ParallaxAlignMode.Start;
    public ParallaxAlignMode AlignY = ParallaxAlignMode.End;
    public Vector2 DrawOffset = Vector2.Zero;
    public Vector2 DrawPercentOffset = Vector2.Zero;
    public Vector2 DrawViewportOffset = Vector2.Zero;
    public Vector2 Velocity = Vector2.Zero;
    public ShowDuringMode ShowDuring = ShowDuringMode.Any;
    public bool DrawInMapScreenshot = false;
}

public sealed class PanoramaData
{
    public string? BasedOn = null;
    public List<BackingData>? BackingDay = null;
    public List<BackingData>? BackingSunset = null;
    public List<BackingData>? BackingNight = null;
    public List<ParallaxLayerData>? ParallaxLayers = null;
    public List<MapWideTAS>? OnetimeTAS = null;
    public List<MapWideTAS>? RespawnTAS = null;
}

internal sealed record ParallaxContext(ParallaxLayerData Data, Texture2D Texture, Rectangle SourceRect)
{
    private Vector2 Position = Vector2.Zero;
    private Vector2 ScrollOffset = Vector2.Zero;
    private readonly Color TxColor = Utility.StringToColor(Data.Color) ?? Color.White;
    private readonly float ScaledWidth = SourceRect.Width * Data.Scale;
    private readonly float ScaledHeight = SourceRect.Height * Data.Scale;

    internal static IEnumerable<ParallaxLayerData> GetAllMatchingData(
        List<ParallaxLayerData>? dataList,
        GameStateQueryContext context
    )
    {
        if (dataList == null)
            return [];
        return dataList.Where(data => GameStateQuery.CheckConditions(data.Condition, context));
    }

    internal static ParallaxContext? FromData(ParallaxLayerData data, GameStateQueryContext context)
    {
        if (
            !GameStateQuery.CheckConditions(data.Condition, context)
            && !Game1.content.DoesAssetExist<Texture2D>(data.Texture)
        )
            return null;
        Texture2D texture =
            data.Texture == "LooseSprites/Cursors" ? Game1.mouseCursors : Game1.content.Load<Texture2D>(data.Texture);
        return new(data, texture, data.SourceRect.IsEmpty ? texture.Bounds : data.SourceRect);
    }

    internal void UpdatePosition(xTile.Dimensions.Rectangle viewport)
    {
        Position.X = Data.AlignX == ParallaxAlignMode.Start ? 0f : viewport.Width - ScaledWidth;
        Position.Y = Data.AlignY == ParallaxAlignMode.Start ? 0f : viewport.Height - ScaledHeight;

        GameTime time = Game1.currentGameTime;
        ScrollOffset.X = (ScrollOffset.X + time.ElapsedGameTime.Milliseconds * Data.Velocity.X) % ScaledWidth;
        ScrollOffset.Y = (ScrollOffset.Y + time.ElapsedGameTime.Milliseconds * Data.Velocity.Y) % ScaledHeight;
    }

    private IEnumerable<Vector2> DrawPositions()
    {
        int refWidth = Game1.viewport.Width;
        int refHeight = Game1.viewport.Height;
        Vector2 drawOffset =
            new(
                Data.DrawOffset.X + refWidth * Data.DrawPercentOffset.X + Game1.viewport.X * Data.DrawViewportOffset.X,
                Data.DrawOffset.Y + refHeight * Data.DrawPercentOffset.Y + Game1.viewport.Y * Data.DrawViewportOffset.Y
            );
        float posX = Position.X + drawOffset.X + ScrollOffset.X;
        float posY = Position.Y + drawOffset.Y + ScrollOffset.Y;
        float i;
        float j;
        // repeat both, i.e. tile to fill screen
        if (Data.RepeatX && Data.RepeatY)
        {
            for (i = posX - ScaledWidth; i > -ScaledWidth; i -= ScaledWidth)
            {
                for (j = posY - ScaledHeight; j > -ScaledHeight; j -= ScaledHeight)
                {
                    yield return new(i, j);
                }
                for (j = posY; j < refHeight; j += ScaledHeight)
                {
                    yield return new(i, j);
                }
            }
            for (i = posX; i < refWidth; i += ScaledWidth)
            {
                for (j = posY - ScaledHeight; j > -ScaledHeight; j -= ScaledHeight)
                {
                    yield return new(i, j);
                }
                for (j = posY; j < refHeight; j += ScaledHeight)
                {
                    yield return new(i, j);
                }
            }
            yield break;
        }
        // repeat only X or only Y or neither
        yield return Position + drawOffset + ScrollOffset;
        if (Data.RepeatX)
        {
            for (i = posX - ScaledWidth; i > -ScaledWidth; i -= ScaledWidth)
                yield return new(i, posY);
            for (i = posX + ScaledWidth; i < refWidth; i += ScaledWidth)
                yield return new(i, posY);
        }
        else if (Data.RepeatY)
        {
            for (i = posY - ScaledHeight; i > -ScaledHeight; i -= ScaledHeight)
                yield return new(posX, i);
            for (i = posX + ScaledHeight; i < refHeight; i += ScaledHeight)
                yield return new(posX, i);
        }
    }

    internal void Draw(SpriteBatch b, float colorMult = 1f)
    {
        if (colorMult == 0f)
            return;
        foreach (Vector2 pos in DrawPositions())
        {
            b.Draw(
                Texture,
                pos,
                SourceRect,
                TxColor * Data.Alpha * colorMult,
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
    internal static BackingData? GetFirstMatchingData(List<BackingData>? dataList, GameStateQueryContext context)
    {
        if (dataList == null || !dataList.Any())
            return null;
        return dataList.FirstOrDefault(data => GameStateQuery.CheckConditions(data.Condition, context));
    }

    internal static BackingContext? FromDataList(List<BackingData>? dataList, GameStateQueryContext context)
    {
        if (GetFirstMatchingData(dataList, context) is BackingData backing)
        {
            if (Game1.content.DoesAssetExist<Texture2D>(backing.Texture))
            {
                Texture2D texture = Game1.content.Load<Texture2D>(backing.Texture);
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
        if (colorMult == 0f)
            return;
        b.Draw(Texture, targetRect, SourceRect, Color * colorMult, 0f, Vector2.Zero, SpriteEffects.None, 0f);
    }
}

internal sealed class PanoramaBackground(GameLocation location) : Background(location, Color.Black, false)
{
    internal string? BgId { get; private set; } = null;
    private readonly List<ParallaxContext> parallaxCtx = [];
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

    internal void SetData(string bgId, PanoramaData data, GameStateQueryContext context)
    {
        BgId = bgId;

        Day = BackingContext.FromDataList(data.BackingDay, context);
        Sunset = BackingContext.FromDataList(data.BackingSunset, context);
        Night = BackingContext.FromDataList(data.BackingNight, context);

        parallaxCtx.Clear();
        foreach (ParallaxLayerData parallax in ParallaxContext.GetAllMatchingData(data.ParallaxLayers, context))
        {
            if (ParallaxContext.FromData(parallax, context) is ParallaxContext pCtx)
            {
                parallaxCtx.Add(pCtx);
            }
        }

        respawningTAS.Clear();
        if (data.RespawnTAS != null)
        {
            foreach (MapWideTAS mwTAS in data.RespawnTAS)
            {
                if (!GameStateQuery.CheckConditions(mwTAS.Condition, context))
                    continue;
                foreach (TASExt tasExt in ModEntry.TAS.GetTASExtList(mwTAS.TAS))
                {
                    respawningTAS.Add(new(mwTAS, new(tasExt) { Pos = Vector2.Zero }));
                }
            }
        }
    }

    public override void update(xTile.Dimensions.Rectangle viewport)
    {
        // Update Parallax
        xTile.Layers.Layer layer = Game1.currentLocation.Map.RequireLayer("Back");
        foreach (ParallaxContext pCtx in parallaxCtx)
        {
            pCtx.UpdatePosition(viewport);
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
            foreach ((MapWideTAS, TASContext) mwTAS in respawningTAS)
                SpawnTAS(mwTAS.Item1, mwTAS.Item2, width, height, true);
        }
    }

    internal void InsertTAS(TemporaryAnimatedSprite tas) => tempSprites.Insert(0, tas);

    internal void SpawnTAS(MapWideTAS mwTAS, TASContext tileTAS, float viewWidth, float viewHeight, bool respawning)
    {
        float tasWidth = tileTAS.Def.SourceRect.Width * tileTAS.Def.Scale * 4;
        float tasHeight = tileTAS.Def.SourceRect.Height * tileTAS.Def.Scale * 4;
        tileTAS.PosOffsetMin = new(viewWidth * mwTAS.XStart - tasWidth, viewHeight * mwTAS.YStart - tasHeight);
        tileTAS.PosOffsetMax = new(viewWidth * mwTAS.XEnd, viewHeight * mwTAS.YEnd);
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
                tileTAS.TryCreate(context, InsertTAS);
            }
        }
    }

    public override void draw(SpriteBatch b)
    {
        float multDay;
        float multNight = 0f;
        float multSunset = 0f;

        // Day/Night/Sunset Curve
        float currMinutes =
            Utility.ConvertTimeToMinutes(Game1.timeOfDay)
            + ((float)Game1.gameTimeInterval / Game1.realMilliSecondsPerGameMinute);
        if (currMinutes >= trulyMinutes)
        {
            multDay = 0f;
            multNight = 1f;
        }
        else if (currMinutes <= startingMinutes)
        {
            multDay = 1f;
        }
        else
        {
            multNight = (currMinutes - startingMinutes) / startingToTrulyMinutes;
            multDay = 1f - multNight;
            if (currMinutes < moderatelyMinutes)
                multSunset = (currMinutes - startingMinutes) / startingToModerateMinutes;
            else
                multSunset = (trulyMinutes - currMinutes) / moderateToTrulyMinutes;
        }

        // backing
        Rectangle viewportRect = Game1.graphics.GraphicsDevice.Viewport.Bounds;
        if (Night == null)
        {
            Day?.Draw(b, viewportRect, 1f);
        }
        else
        {
            if (multNight < 1f)
                Day?.Draw(b, viewportRect, multDay);
            Night?.Draw(b, viewportRect, multNight);
            Sunset?.Draw(b, viewportRect, multSunset);
        }

        // parallax
        foreach (ParallaxContext bgDef in parallaxCtx)
        {
            if (!Game1.game1.takingMapScreenshot || bgDef.Data.DrawInMapScreenshot)
            {
                switch (bgDef.Data.ShowDuring)
                {
                    case ShowDuringMode.Day:
                        bgDef.Draw(b, multDay);
                        break;
                    case ShowDuringMode.Night:
                        bgDef.Draw(b, multNight);
                        break;
                    case ShowDuringMode.Sunset:
                        bgDef.Draw(b, multSunset);
                        break;
                    default:
                        bgDef.Draw(b);
                        break;
                }
            }
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
    internal const string MapProp_Panorama = $"{ModEntry.ModId}_Panorama";

    internal const string Asset_Panorama = $"{ModEntry.ModId}/Panorama";

    internal static void Register()
    {
        ModEntry.help.Events.Content.AssetRequested += OnAssetRequested;
        ModEntry.help.Events.Content.AssetsInvalidated += OnAssetInvalidated;
        ModEntry.help.Events.GameLoop.DayStarted += OnDayStarted;
        ModEntry.help.Events.Player.Warped += OnWarped;

        ModEntry.help.ConsoleCommands.Add("mmap.reset_bg", "Reload current area background", ConsoleReloadBg);
        TriggerActionManager.RegisterAction(MapProp_Panorama, DoSetPanoramaT);
        CommonPatch.RegisterTileAndTouch(MapProp_Panorama, DoSetPanoramaM);

        try
        {
            ModEntry.harm.Patch(
                AccessTools.DeclaredMethod(typeof(Game1), nameof(Game1.updateWeather)),
                transpiler: new HarmonyMethod(typeof(Panorama), nameof(Game1_updateWeather_Transpiler))
            );
            ModEntry.harm.Patch(
                AccessTools.DeclaredMethod(typeof(Game1), nameof(Game1.isOutdoorMapSmallerThanViewport)),
                postfix: new HarmonyMethod(typeof(Panorama), nameof(Game1_isOutdoorMapSmallerThanViewport_Postfix))
            );
            ModEntry.harm.Patch(
                AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.addClouds)),
                prefix: new HarmonyMethod(typeof(Panorama), nameof(GameLocation_addClouds_Prefix))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch Panorama:\n{err}", LogLevel.Error);
        }
    }

    private static bool GameLocation_addClouds_Prefix()
    {
        return Game1.background is not PanoramaBackground;
    }

    private static void Game1_isOutdoorMapSmallerThanViewport_Postfix(ref bool __result)
    {
        if (__result && Game1.background is PanoramaBackground)
        {
            __result = false;
        }
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

    private static void OnWarped(object? sender, WarpedEventArgs e)
    {
        RecheckPanoramaBackground(e.NewLocation);
    }

    private static void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        RecheckPanoramaBackground(Game1.currentLocation);
    }

    private static void ConsoleReloadBg(string arg1, string[] arg2)
    {
        // patch reload mushymato.MMAP.Example
        // mmap_reset_bg
        if (!Context.IsWorldReady)
            return;
        // Game1.player.team.farmPerfect.Value = true;
        if (Game1.currentLocation != null)
        {
            ModEntry.help.GameContent.InvalidateCache(Asset_Panorama);
            RecheckPanoramaBackground(Game1.currentLocation);
        }
    }

    private static Dictionary<string, PanoramaData>? _bgData = null;
    internal static Dictionary<string, PanoramaData> BgData
    {
        get
        {
            _bgData ??= Game1.content.Load<Dictionary<string, PanoramaData>>(Asset_Panorama);

            foreach ((string key, PanoramaData panorama) in _bgData)
            {
                if (
                    !string.IsNullOrEmpty(panorama.BasedOn)
                    && _bgData.TryGetValue(panorama.BasedOn, out PanoramaData? basedOn)
                )
                {
                    if (!string.IsNullOrEmpty(basedOn.BasedOn))
                    {
                        ModEntry.Log(
                            $"Panorama '{key}' has BasedOn={panorama.BasedOn} refering to a panorama that has it's own BasedOn, no copying of fields performed",
                            LogLevel.Warn
                        );
                    }
                    else
                    {
                        panorama.BackingDay ??= basedOn.BackingDay;
                        panorama.BackingSunset ??= basedOn.BackingSunset;
                        panorama.BackingNight ??= basedOn.BackingNight;
                        panorama.ParallaxLayers ??= basedOn.ParallaxLayers;
                        panorama.OnetimeTAS ??= basedOn.OnetimeTAS;
                        panorama.RespawnTAS ??= basedOn.RespawnTAS;
                    }
                }
            }

            return _bgData;
        }
    }

    private static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.Name.IsEquivalentTo(Asset_Panorama))
            e.LoadFromModFile<Dictionary<string, PanoramaData>>("assets/panorama.json", AssetLoadPriority.Exclusive);
    }

    private static void OnAssetInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        if (e.NamesWithoutLocale.Any(an => an.IsEquivalentTo(Asset_Panorama)))
        {
            _bgData = null;
            RecheckPanoramaBackground(Game1.currentLocation);
        }
    }

    private static void RecheckPanoramaBackground(GameLocation location)
    {
        // GameLocation location, PanoramaBgStaticDef bgStatic, List<TileTAS>? respawningTAS
        if (CommonPatch.TryGetCustomFieldsOrMapProperty(location, MapProp_Panorama, out string? bgId))
        {
            SetPanorama(location, bgId);
        }
        else
        {
            ClearPanorama();
        }
    }

    private static bool DoSetPanoramaT(string[] args, TriggerActionContext context, out string error)
    {
        if (
            !ArgUtility.TryGet(args, 1, out string bgId, out error, allowBlank: false, name: "string bgId")
            || !ArgUtility.TryGetOptionalBool(args, 2, out bool force, out error, defaultValue: false, "bool force")
        )
            return false;
        SetPanorama(Game1.currentLocation, bgId, force: force);
        return true;
    }

    private static bool DoSetPanoramaM(GameLocation location, string[] args, Farmer farmer, Point point)
    {
        if (
            !ArgUtility.TryGet(args, 1, out string bgId, out _, allowBlank: false, name: "string bgId")
            || !ArgUtility.TryGetOptionalBool(args, 2, out bool force, out _, defaultValue: false, "bool force")
        )
            return false;
        SetPanorama(location, bgId, force: force);
        return true;
    }

    private sealed class SummitBG : Background
    {
        public SummitBG(GameLocation location)
            : base(location, Color.White, false)
        {
            summitBG = true;
            initialViewportY = Game1.viewport.Y;
            cloudsTexture = Game1.content.Load<Texture2D>("Minigames\\Clouds");
        }
    }

    private static bool IsNullOrCustomBG =>
        Game1.background is not null || Game1.background is PanoramaBackground || Game1.background is SummitBG;

    private static void SetPanorama(GameLocation location, string bgId, bool force = false)
    {
        if (bgId == "SUMMIT" && (force || IsNullOrCustomBG))
        {
            Game1.background = new SummitBG(location);
        }
        else if (BgData.TryGetValue(bgId, out PanoramaData? data))
        {
            if (Game1.background is PanoramaBackground Panorama)
            {
                if (!force && Panorama.BgId == bgId)
                    return;
            }
            else
            {
                if (!force && !IsNullOrCustomBG)
                {
                    return;
                }
                Panorama = new(location);
            }

            GameStateQueryContext context = new(location, Game1.player, null, null, null);
            Panorama.SetData(bgId, data, context);
            if (data.OnetimeTAS != null)
            {
                xTile.Layers.Layer layer = Game1.currentLocation.Map.RequireLayer("Back");
                float width = layer.LayerWidth * 64f;
                float height = layer.LayerHeight * 64f;
                foreach (MapWideTAS mwTAS in data.OnetimeTAS)
                {
                    if (!GameStateQuery.CheckConditions(mwTAS.Condition, context))
                        continue;
                    foreach (TASExt tasExt in ModEntry.TAS.GetTASExtList(mwTAS.TAS))
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
            ClearPanorama();
        }
    }

    private static void ClearPanorama()
    {
        if (Game1.background is PanoramaBackground bg)
        {
            bg.tempSprites.Clear();
            Game1.background = null;
        }
        else if (Game1.background is SummitBG)
        {
            Game1.background = null;
        }
    }
}

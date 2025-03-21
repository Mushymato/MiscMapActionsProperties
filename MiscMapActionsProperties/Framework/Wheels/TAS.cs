using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.GameData;

namespace MiscMapActionsProperties.Framework.Wheels;

public class TASExtSub : TemporaryAnimatedSpriteDefinition
{
    public float ScaleChangeChange { get; set; } = 0f;
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
    public float ScaleChangeChange { get; set; } = 0f;
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

internal sealed record TileTAS(TASExt Def, Vector2 Pos)
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
            Pos + (Def.PositionOffset + (Def.HasRand ? Random.Shared.NextVector2(Def.RandMin!.PositionOffset, Def.RandMax!.PositionOffset) : Vector2.Zero)) * 4f,
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
            scaleChangeChange = Def.HasRand ? Random.Shared.NextSingle(Def.RandMin!.ScaleChangeChange, Def.RandMax!.ScaleChangeChange) : 0,
            pingPong = Def.PingPong,
            alpha = Def.Alpha + (Def.HasRand ? Random.Shared.NextSingle(Def.RandMin!.Alpha, Def.RandMax!.Alpha) : 0),
            layerDepth = Def.LayerDepth ?? (Pos.Y + 0.66f * Game1.tileSize) / 10000f + Pos.X / Game1.tileSize * 1E-05f,
            motion = Def.Motion + (Def.HasRand ? Random.Shared.NextVector2(Def.RandMin!.Motion, Def.RandMax!.Motion) : Vector2.Zero),
            acceleration = Def.Acceleration + (Def.HasRand ? Random.Shared.NextVector2(Def.RandMin!.Acceleration, Def.RandMax!.Acceleration) : Vector2.Zero),
            accelerationChange = Def.AccelerationChange + (Def.HasRand ? Random.Shared.NextVector2(Def.RandMin!.AccelerationChange, Def.RandMax!.AccelerationChange) : Vector2.Zero),
        };
    }

    internal bool TryCreate(GameStateQueryContext context, [NotNullWhen(true)] out TemporaryAnimatedSprite? tas)
    {
        if (GameStateQuery.CheckConditions(Def.Condition, context))
        {
            tas = Create();
            return true;
        }
        tas = null;
        return false;
    }

    internal bool TryCreateRespawning(
        GameTime time,
        GameStateQueryContext context,
        [NotNullWhen(true)] out TemporaryAnimatedSprite? tas
    )
    {
        if (spawnTimeout <= TimeSpan.Zero)
        {
            spawnTimeout = TimeSpan.FromMilliseconds(Def.SpawnInterval);
            return TryCreate(context, out tas);
        }
        spawnTimeout -= time.ElapsedGameTime;
        tas = null;
        return false;
    }
}

internal record TileTASLists(List<TileTAS> Onetime, List<TileTAS> Respawning);

internal static class TASAssetManager
{
    internal static readonly string Asset_TAS = $"{ModEntry.ModId}/TAS";

    internal static void Register()
    {
        ModEntry.help.Events.Content.AssetRequested += OnAssetRequested;
        ModEntry.help.Events.Content.AssetsInvalidated += OnAssetInvalidated;
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
}

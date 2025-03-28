using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.GameData;

namespace MiscMapActionsProperties.Framework.Wheels;

public sealed class TASExtRand
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
    public double SpawnInterval { get; set; } = 0;
    public int SpawnDelay { get; set; } = -1;
}

public sealed class TASExt : TemporaryAnimatedSpriteDefinition
{
    public float ScaleChangeChange { get; set; } = 0f;
    public Vector2 Motion { get; set; } = Vector2.Zero;
    public Vector2 Acceleration { get; set; } = Vector2.Zero;
    public Vector2 AccelerationChange { get; set; } = Vector2.Zero;
    public float? LayerDepth { get; set; } = null;

    // actually opacity
    public float Alpha { get; set; } = 1f;
    public bool PingPong { get; set; }
    public double SpawnInterval { get; set; } = -1;
    public int SpawnDelay { get; set; } = -1;

    internal bool HasRand => RandMin != null && RandMax != null;
    public TASExtRand? RandMin { get; set; } = null;
    public TASExtRand? RandMax { get; set; } = null;
}

public enum MapWideTASMode
{
    Everywhere,
    Below,
    Right,
    Left,
    Above,
}

public sealed class MapWideTAS
{
    public string? IdImpl = null;
    public string Id
    {
        get => IdImpl ??= TAS;
        set => IdImpl = value;
    }
    public string TAS = "Unknown";
    public int Count = 1;
    public MapWideTASMode Mode = MapWideTASMode.Everywhere;
    public float XStart = 0f;
    public float XEnd = 1f;
    public float YStart = 0f;
    public float YEnd = 1f;
}

internal sealed record TileTAS(TASExt Def, Vector2 Pos)
{
    private TimeSpan spawnTimeout = TimeSpan.Zero;

    // csharpier-ignore
    internal TemporaryAnimatedSprite Create()
    {
        TemporaryAnimatedSprite tas = TemporaryAnimatedSprite.GetTemporaryAnimatedSprite(
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
        );
        tas.scaleChangeChange = Def.HasRand ? Random.Shared.NextSingle(Def.RandMin!.ScaleChangeChange, Def.RandMax!.ScaleChangeChange) : 0;
        tas.pingPong = Def.PingPong;
        tas.alpha = Def.Alpha + (Def.HasRand ? Random.Shared.NextSingle(Def.RandMin!.Alpha, Def.RandMax!.Alpha) : 0);
        tas.layerDepth = Def.LayerDepth ?? (Pos.Y + 0.66f * Game1.tileSize) / 10000f + Pos.X / Game1.tileSize * 1E-05f;
        tas.motion = Def.Motion + (Def.HasRand ? Random.Shared.NextVector2(Def.RandMin!.Motion, Def.RandMax!.Motion) : Vector2.Zero);
        tas.acceleration = Def.Acceleration + (Def.HasRand ? Random.Shared.NextVector2(Def.RandMin!.Acceleration, Def.RandMax!.Acceleration) : Vector2.Zero);
        tas.accelerationChange = Def.AccelerationChange + (Def.HasRand ? Random.Shared.NextVector2(Def.RandMin!.AccelerationChange, Def.RandMax!.AccelerationChange) : Vector2.Zero);
        return tas;
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

    internal bool TryCreateDelayed(GameStateQueryContext context, Action<TemporaryAnimatedSprite> addSprite)
    {
        if (TryCreate(context, out TemporaryAnimatedSprite? tas) && Def.SpawnDelay > 0)
        {
            // DelayedAction.addTemporarySpriteAfterDelay(tas, location, Def.SpawnDelay + (Def.HasRand ? Random.Shared.Next(Def.RandMin!.SpawnDelay, Def.RandMax!.SpawnDelay) : 0), true);
            DelayedAction.functionAfterDelay(
                () => addSprite(tas),
                Def.SpawnDelay
                    + (Def.HasRand ? Random.Shared.Next(Def.RandMin!.SpawnDelay, Def.RandMax!.SpawnDelay) : 0)
            );
            return true;
        }
        return false;
    }

    internal bool TryCreateRespawning(
        GameTime time,
        GameStateQueryContext context,
        Action<TemporaryAnimatedSprite> addSprite
    )
    {
        if (spawnTimeout <= TimeSpan.Zero)
        {
            spawnTimeout = TimeSpan.FromMilliseconds(
                Def.SpawnInterval
                    + (
                        Def.HasRand
                            ? Random.Shared.NextDouble(Def.RandMin!.SpawnInterval, Def.RandMax!.SpawnInterval)
                            : 0
                    )
            );
            if (TryCreate(context, out TemporaryAnimatedSprite? tas))
            {
                addSprite(tas);
                return true;
            }
        }
        spawnTimeout -= time.ElapsedGameTime;
        return false;
    }
}

internal static class TASAssetManager
{
    internal static readonly string Asset_TAS = $"{ModEntry.ModId}/TAS";

    internal static void Register()
    {
        ModEntry.help.Events.Content.AssetRequested += OnAssetRequested;
        ModEntry.help.Events.Content.AssetsInvalidated += OnAssetInvalidated;
    }

    private static Dictionary<string, TASExt>? _tasData = null;
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
            e.LoadFrom(() => new Dictionary<string, TASExt>(), AssetLoadPriority.Exclusive);
    }

    private static void OnAssetInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        if (e.NamesWithoutLocale.Any(an => an.IsEquivalentTo(Asset_TAS)))
            _tasData = null;
    }
}

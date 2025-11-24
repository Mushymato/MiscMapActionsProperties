using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI.Events;
using StardewValley;

namespace MiscMapActionsProperties.Framework.Location;

internal record BaublesCtx(List<Vector2> Baubles, int DisplayHeight, int DisplayWidth)
{
    internal void Update(GameTime time)
    {
        for (int i = 0; i < Baubles.Count; i++)
        {
            Vector2 value = new()
            {
                X =
                    Baubles[i].X
                    - Math.Max(0.4f, Math.Min(1f, i * 0.01f))
                    - (float)((double)(i * 0.01f) * Math.Sin(Math.PI * 2.0 * time.TotalGameTime.Milliseconds / 8000.0)),
                Y = Baubles[i].Y + Math.Max(0.5f, Math.Min(1.2f, i * 0.02f)),
            };
            if (value.Y > DisplayHeight || value.X < 0f)
            {
                value.X = Game1.random.Next(0, DisplayWidth);
                value.Y = -64f;
            }
            Baubles[i] = value;
        }
    }

    internal void Draw(SpriteBatch b)
    {
        for (int i = 0; i < Baubles.Count; i++)
        {
            b.Draw(
                Game1.mouseCursors,
                Game1.GlobalToLocal(Game1.viewport, Baubles[i]),
                new Rectangle(
                    346 + (int)((Game1.currentGameTime.TotalGameTime.TotalMilliseconds + i * 25) % 600.0) / 150 * 5,
                    1971,
                    5,
                    5
                ),
                Color.White,
                i * ((float)Math.PI / 8f),
                Vector2.Zero,
                4f,
                SpriteEffects.None,
                1f
            );
        }
    }
}

/// <summary>
/// Add new map property mushymato.MMAP_WoodsBaubles T
/// When this is set on a location, draws magical looking sparkles
/// </summary>
internal static class WoodsBaubles
{
    internal const string MapProp_WoodsBaubles = $"{ModEntry.ModId}_WoodsBaubles";
    internal static readonly PerScreenCache<BaublesCtx?> _baubles = PerScreenCache.Make<BaublesCtx?>();

    internal static void Register()
    {
        CommonPatch.GameLocation_resetLocalState += GameLocation_resetLocalState;
        CommonPatch.GameLocation_UpdateWhenCurrentLocationPostfix += GameLocation_UpdateWhenCurrentLocationFinalizer;
        ModEntry.help.Events.Display.RenderedStep += OnRenderedStep;
    }

    private static void GameLocation_resetLocalState(object? sender, GameLocation e)
    {
        _baubles.Value = null;
        if (CommonPatch.TryGetLocationalProperty(e, MapProp_WoodsBaubles, out string? prop))
        {
            int mincount;
            int maxcount;
            if (prop == "T")
            {
                mincount = 25;
                maxcount = 75;
                if (e.IsRainingHere())
                {
                    return;
                }
            }
            else
            {
                string[] args = ArgUtility.SplitBySpaceQuoteAware(prop);
                if (
                    !ArgUtility.TryGetInt(args, 0, out mincount, out string err, name: "int mincount")
                    || !ArgUtility.TryGetOptionalInt(args, 1, out maxcount, out err, name: "int maxcount")
                    || !ArgUtility.TryGetOptional(
                        args,
                        2,
                        out string gsq,
                        out err,
                        defaultValue: "TRUE",
                        allowBlank: false,
                        name: "string gsq"
                    )
                )
                {
                    ModEntry.Log(err, StardewModdingAPI.LogLevel.Error);
                    return;
                }
                if (!GameStateQuery.CheckConditions(gsq, e))
                {
                    return;
                }
            }

            if (maxcount < mincount)
            {
                maxcount = mincount;
            }
            if (mincount == maxcount && mincount == 0)
            {
                return;
            }
            Random random = Utility.CreateDaySaveRandom();
            int num = mincount + random.Next(0, maxcount - mincount);
            List<Vector2> baubles = [];
            for (int i = 0; i < num; i++)
            {
                baubles.Add(
                    new Vector2(Game1.random.Next(0, e.map.DisplayWidth), Game1.random.Next(0, e.map.DisplayHeight))
                );
            }
            _baubles.Value = new(baubles, e.map.DisplayWidth, e.map.DisplayHeight);
        }
    }

    private static void OnRenderedStep(object? sender, RenderedStepEventArgs e)
    {
        if (e.Step == StardewValley.Mods.RenderSteps.World_AlwaysFront)
            _baubles.Value?.Draw(e.SpriteBatch);
    }

    private static void GameLocation_UpdateWhenCurrentLocationFinalizer(
        object? sender,
        CommonPatch.UpdateWhenCurrentLocationArgs e
    )
    {
        _baubles.Value?.Update(e.Time);
    }
}

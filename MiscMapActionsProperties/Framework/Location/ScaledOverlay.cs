using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Triggers;

namespace MiscMapActionsProperties.Framework.Location;

/// <summary>
/// Add new map property mushymato.MMAP_ScaledOverlay texture [scaling] [color]
/// The overlay texture should be tileable.
/// Aside from the map property, you can also use it as a trigger action:
/// - mushymato.MMAP_SetScaledOverlay fadeInDuration texture [scaling] [color]
/// - mushymato.MMAP_UnsetScaledOverlay fadeOutDuration
/// </summary>
internal static class ScaledOverlay
{
    internal enum FadeState
    {
        Off = 0,
        On = 1,
    }

    internal enum ScalingMode
    {
        Stretch = 0,
        Fit = 1,
        Fill = 2,
        Absolute = 3,
    }

    internal sealed record ScaledCtx(Texture2D Texture, ScalingMode Scaling, float Scale, Color Clr)
    {
        private FadeState fadeFrom = FadeState.On;
        private FadeState fadeTo = FadeState.On;
        private float fadeDuration = -1f;
        private float fadingTimer = 0f;

        private float alpha = 1f;

        internal void SetFading(FadeState from, FadeState to, float duration)
        {
            fadeFrom = from;
            fadeTo = to;
            fadeDuration = duration;
            fadingTimer = 0;
            alpha = (float)fadeFrom;
        }

        private void UpdateAlpha()
        {
            if (fadeDuration < 0)
            {
                alpha = 1f;
                return;
            }
            if (fadeTo == FadeState.On)
            {
                alpha = 1f * fadingTimer / fadeDuration;
            }
            else
            {
                alpha = 1f * (1f - fadingTimer / fadeDuration);
            }
        }

        public bool Update(GameTime time)
        {
            if (fadeFrom == fadeTo)
                return false;

            if (fadingTimer <= fadeDuration)
            {
                fadingTimer += time.ElapsedGameTime.Milliseconds;
                UpdateAlpha();
                return false;
            }
            else
            {
                fadeFrom = fadeTo;
                alpha = (float)fadeTo;
                return fadeTo == FadeState.Off;
            }
        }

        public void Draw(SpriteBatch b)
        {
            Rectangle viewportRect = Game1.graphics.GraphicsDevice.Viewport.Bounds;
            Color drawColor = Clr * alpha;
            float drawScale = Scale;
            switch (Scaling)
            {
                case ScalingMode.Fit:
                    drawScale = Math.Min(
                        viewportRect.Width / (float)Texture.Bounds.Width,
                        viewportRect.Height / (float)Texture.Bounds.Height
                    );
                    goto case ScalingMode.Absolute;
                case ScalingMode.Fill:
                    drawScale = Math.Max(
                        viewportRect.Width / (float)Texture.Bounds.Width,
                        viewportRect.Height / (float)Texture.Bounds.Height
                    );
                    goto case ScalingMode.Absolute;
                case ScalingMode.Absolute:
                    b.Draw(
                        Texture,
                        new Vector2(viewportRect.Width / 2f, viewportRect.Height / 2f),
                        Texture.Bounds,
                        drawColor,
                        0f,
                        new(Texture.Bounds.Width / 2f, Texture.Bounds.Height / 2f),
                        drawScale,
                        SpriteEffects.None,
                        1f
                    );
                    break;
                case ScalingMode.Stretch:
                    b.Draw(Texture, viewportRect, drawColor);
                    break;
            }
        }

        internal static ScaledCtx? Make(string[] args, int firstIdx, out string error)
        {
            if (
                !ArgUtility.TryGet(args, firstIdx, out string textureName, out error, name: "string texture")
                || !ArgUtility.TryGetOptional(
                    args,
                    firstIdx + 1,
                    out string? scalingDesc,
                    out error,
                    name: "string scalingDesc"
                )
                || !ArgUtility.TryGetOptional(args, firstIdx + 2, out string? colorStr, out error, name: "string Color")
            )
            {
                return null;
            }
            if (!Game1.temporaryContent.DoesAssetExist<Texture2D>(textureName))
            {
                error = $"Texture '{textureName}' does not exist";
                return null;
            }
            Texture2D texture = Game1.content.Load<Texture2D>(textureName);
            ScalingMode scaling = ScalingMode.Fill;
            float scale = 4f;
            if (scalingDesc != null)
            {
                string[] scalingDescParts = scalingDesc.Split(':');
                if (Enum.TryParse(scalingDescParts[0], out ScalingMode parsedScaleMode))
                {
                    scaling = parsedScaleMode;
                }
                if (
                    scaling == ScalingMode.Absolute
                    && scalingDescParts.Length > 1
                    && float.TryParse(scalingDescParts[1], out float parsedScale)
                )
                {
                    scale = parsedScale;
                }
            }
            Color color = Color.White;
            if (colorStr != null)
            {
                color = Utility.StringToColor(colorStr) ?? color;
            }
            return new(texture, scaling, scale, color);
        }
    }

    internal const string MapProp_ScaledOverlay = $"{ModEntry.ModId}_ScaledOverlay";
    internal static PerScreenCache<ScaledCtx?> scaledCtx = PerScreenCache.Make<ScaledCtx?>();
    internal static PerScreenCache<ScaledCtx?> nextScaledCtx = PerScreenCache.Make<ScaledCtx?>();

    internal static void Register()
    {
        CommonPatch.GameLocation_resetLocalState += GameLocation_resetLocalState_Postfix;
        CommonPatch.GameLocation_UpdateWhenCurrentLocationPostfix += GameLocation_UpdateWhenCurrentLocation_Postfix;
        ModEntry.help.Events.Display.RenderedStep += OnRenderedStep;

        TriggerActionManager.RegisterAction(MapProp_ScaledOverlay, ActionSetScaledOverlay);
    }

    private static bool ActionSetScaledOverlay(string[] args, TriggerActionContext context, out string error)
    {
        if (!ArgUtility.TryGetFloat(args, 1, out float fadeDuration, out error, name: "float duration"))
        {
            return false;
        }
        if (args.Length <= 2)
        {
            if (scaledCtx.Value != null)
            {
                if (fadeDuration > 0)
                {
                    scaledCtx.Value.SetFading(FadeState.On, FadeState.Off, fadeDuration);
                    nextScaledCtx.Value = null;
                }
                else
                {
                    scaledCtx.Value = null;
                }
            }
        }
        else
        {
            if (ScaledCtx.Make(args, 2, out error) is not ScaledCtx ctx)
            {
                return false;
            }
            if (fadeDuration > 0)
            {
                ctx.SetFading(FadeState.Off, FadeState.On, fadeDuration);
            }
            if (scaledCtx.Value == null)
            {
                scaledCtx.Value = ctx;
            }
            else
            {
                scaledCtx.Value.SetFading(FadeState.On, FadeState.Off, fadeDuration);
                nextScaledCtx.Value = ctx;
            }
        }
        return true;
    }

    private static void GameLocation_resetLocalState_Postfix(object? sender, GameLocation location)
    {
        if (!CommonPatch.TryGetLocationalProperty(location, MapProp_ScaledOverlay, out string? scaledOverlayProps))
        {
            scaledCtx.Value = null;
            return;
        }
        string[] args = ArgUtility.SplitBySpaceQuoteAware(scaledOverlayProps);
        if (ScaledCtx.Make(args, 0, out string error) is not ScaledCtx ctx)
        {
            ModEntry.Log(error, LogLevel.Error);
            scaledCtx.Value = null;
            return;
        }
        scaledCtx.Value = ctx;
    }

    private static void GameLocation_UpdateWhenCurrentLocation_Postfix(
        object? sender,
        CommonPatch.UpdateWhenCurrentLocationArgs e
    )
    {
        if (scaledCtx.Value?.Update(e.Time) ?? false)
        {
            scaledCtx.Value = nextScaledCtx.Value;
            nextScaledCtx.Value = null;
        }
    }

    private static void OnRenderedStep(object? sender, RenderedStepEventArgs e)
    {
        if (e.Step == StardewValley.Mods.RenderSteps.World_AlwaysFront)
        {
            scaledCtx.Value?.Draw(e.SpriteBatch);
        }
    }
}

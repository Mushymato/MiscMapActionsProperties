using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace MiscMapActionsProperties.Framework.Location;

/// <summary>
/// Add new map property mushymato.MMAP_SteamOverlay TextureName [color]
/// The overlay texture should be tileable
/// </summary>
internal static class SteamOverlay
{
    internal sealed record SteamCtx(Texture2D Texture, Color Color, float Scale, float Alpha, Vector2 Velocity)
    {
        internal Rectangle SourceRect = new(0, 0, Texture.Width, Texture.Height);
        internal float ScaledWidth = Texture.Width * Scale;
        internal float ScaledHeight = Texture.Height * Scale;
        internal Vector2 Position = new(-Game1.viewport.X, -Game1.viewport.Y);
        internal Vector2 Offset = Vector2.Zero;

        internal void Update(GameTime time)
        {
            Position -= Game1.getMostRecentViewportMotion();
            Offset.X = (Offset.X + time.ElapsedGameTime.Milliseconds * Velocity.X) % ScaledWidth;
            Offset.Y = (Offset.Y + time.ElapsedGameTime.Milliseconds * Velocity.Y) % ScaledHeight;
        }

        internal void Draw(SpriteBatch b)
        {
            for (
                float posX = Position.X + Offset.X - ScaledWidth;
                posX < Game1.graphics.GraphicsDevice.Viewport.Width;
                posX += ScaledWidth
            )
            {
                for (
                    float posY = Position.Y + Offset.Y - ScaledHeight;
                    posY < Game1.graphics.GraphicsDevice.Viewport.Height;
                    posY += ScaledHeight
                )
                {
                    b.Draw(
                        Texture,
                        new Vector2(posX, posY),
                        SourceRect,
                        Color * Alpha,
                        0f,
                        Vector2.Zero,
                        Scale,
                        SpriteEffects.None,
                        1f
                    );
                }
            }
        }
    }

    internal static readonly string MapProp_SteamOverlay = $"{ModEntry.ModId}_SteamOverlay";
    private static readonly PerScreen<SteamCtx?> steamCtx = new();

    internal static void Register()
    {
        try
        {
            CommonPatch.GameLocation_resetLocalState += GameLocation_resetLocalState_Postfix;
            CommonPatch.GameLocation_UpdateWhenCurrentLocation += GameLocation_UpdateWhenCurrentLocation_Postfix;
            ModEntry.help.Events.Display.RenderedStep += OnRenderedStep;
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch SteamOverlay:\n{err}", LogLevel.Error);
        }
    }

    private static void GameLocation_resetLocalState_Postfix(object? sender, CommonPatch.ResetLocalStateArgs e)
    {
        if (
            CommonPatch.TryGetCustomFieldsOrMapProperty(e.Location, MapProp_SteamOverlay, out string? steamOverlayProps)
            && !string.IsNullOrWhiteSpace(steamOverlayProps)
        )
        {
            string[] args = ArgUtility.SplitBySpaceQuoteAware(steamOverlayProps);
            if (
                ArgUtility.TryGet(
                    args,
                    0,
                    out string steamTexture,
                    out string error,
                    allowBlank: false,
                    name: "string steamTexture"
                )
                && ArgUtility.TryGetOptionalFloat(args, 1, out float velocityX, out error, 0f, "string velocityX")
                && ArgUtility.TryGetOptionalFloat(args, 2, out float velocityY, out error, 0f, "string velocityY")
                && ArgUtility.TryGetOptional(args, 3, out string steamColor, out error, name: "string steamColor")
                && ArgUtility.TryGetOptionalFloat(args, 4, out float alpha, out error, 1f, "string alpha")
                && ArgUtility.TryGetOptionalFloat(args, 5, out float scale, out error, 4f, "string scale")
            )
            {
                Texture2D texture = Game1.temporaryContent.DoesAssetExist<Texture2D>(steamTexture)
                    ? Game1.temporaryContent.Load<Texture2D>(steamTexture)
                    : Game1.temporaryContent.Load<Texture2D>("LooseSprites\\steamAnimation");
                Color color = Color.White * 0.8f;
                if (!string.IsNullOrEmpty(steamColor) && Utility.StringToColor(steamColor) is Color clr)
                    color = clr;
                steamCtx.Value = new(texture, color, scale, alpha, new(velocityX, velocityY));
                return;
            }
            ModEntry.Log(error);
        }
        steamCtx.Value = null;
    }

    private static void GameLocation_UpdateWhenCurrentLocation_Postfix(
        object? sender,
        CommonPatch.UpdateWhenCurrentLocationArgs e
    )
    {
        steamCtx.Value?.Update(e.Time);
    }

    private static void OnRenderedStep(object? sender, RenderedStepEventArgs e)
    {
        if (e.Step == StardewValley.Mods.RenderSteps.World_AlwaysFront)
        {
            steamCtx.Value?.Draw(e.SpriteBatch);
        }
    }
}

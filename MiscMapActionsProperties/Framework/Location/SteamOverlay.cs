using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace MiscMapActionsProperties.Framework.Location;

/// <summary>
/// Add new map property mushymato.MMAP_SteamOverlay TextureName [color]
/// The overlay texture should be tileable
/// </summary>
internal static class SteamOverlay
{
    internal sealed record SteamCtx(Texture2D Texture, Color Color, float Scale, Vector2 Velocity)
    {
        internal Rectangle SourceRect = new(0, 0, Texture.Width, Texture.Height);
        internal float Width = Texture.Width * Scale;
        internal float Height = Texture.Height * Scale;
        internal Vector2 Position = new(-Game1.viewport.X, -Game1.viewport.Y);
        internal Vector2 Offset = Vector2.Zero;

        internal void Update(GameTime time)
        {
            Position -= Game1.getMostRecentViewportMotion();
            Offset.X = (Offset.X + time.ElapsedGameTime.Milliseconds * Velocity.X) % Width;
            Offset.Y = (Offset.Y + time.ElapsedGameTime.Milliseconds * Velocity.Y) % Height;
        }

        internal void Draw(SpriteBatch b)
        {
            for (
                float posX = Position.X + Offset.X;
                posX < Game1.graphics.GraphicsDevice.Viewport.Width + Width;
                posX += Width
            )
            {
                for (
                    float posY = Position.Y + Offset.Y;
                    posY < Game1.graphics.GraphicsDevice.Viewport.Height + Height;
                    posY += Height
                )
                {
                    b.Draw(
                        Texture,
                        new Vector2(posX, posY),
                        SourceRect,
                        Color,
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
            CommonPatch.GameLocation_DrawAboveAlwaysFrontLayer += GameLocation_drawAboveAlwaysFrontLayer_Postfix;
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
                && ArgUtility.TryGetOptional(args, 1, out string steamColor, out error, name: "string steamColor")
                && ArgUtility.TryGetOptionalFloat(args, 2, out float scale, out error, 4f, "string scale")
                && ArgUtility.TryGetOptionalFloat(args, 3, out float velocityX, out error, 0f, "string velocityX")
                && ArgUtility.TryGetOptionalFloat(args, 4, out float velocityY, out error, 0f, "string velocityY")
            )
            {
                Texture2D texture = Game1.temporaryContent.DoesAssetExist<Texture2D>(steamTexture)
                    ? Game1.temporaryContent.Load<Texture2D>(steamTexture)
                    : Game1.temporaryContent.Load<Texture2D>("LooseSprites\\steamAnimation");
                Color color = Color.White * 0.8f;
                if (!string.IsNullOrEmpty(steamColor) && Utility.StringToColor(steamColor) is Color clr)
                    color = clr;
                steamCtx.Value = new(texture, color, scale, new(velocityX, velocityY));
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

    private static void GameLocation_drawAboveAlwaysFrontLayer_Postfix(
        object? sender,
        CommonPatch.DrawAboveAlwaysFrontLayerArgs e
    )
    {
        steamCtx.Value?.Draw(e.B);
    }
}

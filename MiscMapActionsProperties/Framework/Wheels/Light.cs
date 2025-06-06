using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace MiscMapActionsProperties.Framework.Wheels;

internal static class Light
{
    /// <summary>
    /// Make light source from a string (props)
    /// Format: "[radius] [color] [type|texture] [offsetX] [offsetY]"
    /// </summary>
    /// <param name="lightProps"></param>
    /// <param name="lightName"></param>
    /// <param name="position"></param>
    /// <param name="mapName"></param>
    /// <returns></returns>
    internal static LightSource? MakeLightFromProps(
        string[] args,
        string lightName,
        Vector2 position,
        string? mapName = null
    )
    {
        if (
            !ArgUtility.TryGetOptionalFloat(
                args,
                0,
                out float radius,
                out string error,
                defaultValue: 2f,
                name: "float radius"
            )
            || !ArgUtility.TryGetOptional(
                args,
                1,
                out string colorStr,
                out error,
                defaultValue: "White",
                name: "string color"
            )
            || !ArgUtility.TryGetOptional(
                args,
                2,
                out string textureStr,
                out error,
                defaultValue: "4",
                name: "string texture"
            )
            || !ArgUtility.TryGetOptionalInt(args, 3, out int offsetX, out error, defaultValue: 0, name: "int offsetX")
            || !ArgUtility.TryGetOptionalInt(args, 4, out int offsetY, out error, defaultValue: 0, name: "int offsetY")
            || !ArgUtility.TryGetOptionalEnum(
                args,
                5,
                out LightSource.LightContext lightContext,
                out error,
                defaultValue: LightSource.LightContext.MapLight,
                name: "LightSource.LightContext lightContext"
            )
        )
        {
            ModEntry.Log(error, LogLevel.Error);
            return null;
        }
        Texture2D? customTexture = null;
        if (int.TryParse(textureStr, out int textureIndex))
        {
            if (textureIndex < 1 || textureIndex > 10 || textureIndex == 3)
                textureIndex = 1;
        }
        else
        {
            textureIndex = 1;
            customTexture = Game1.content.Load<Texture2D>(textureStr);
        }
        Color color = Utility.StringToColor(colorStr) ?? Color.White;
        color = new Color(color.PackedValue ^ 0x00FFFFFF);
        if (lightContext == LightSource.LightContext.MapLight && mapName == null)
            lightContext = LightSource.LightContext.None;
        LightSource newLight =
            new(
                lightName,
                textureIndex,
                position + new Vector2(offsetX, offsetY),
                radius,
                color,
                lightContext,
                onlyLocation: mapName
            );
        if (customTexture != null)
            newLight.lightTexture = customTexture;
        return newLight;
    }

    internal static LightSource? MakeMapLightFromProps(
        string[] lightProps,
        string lightName,
        Vector2 position,
        string mapName
    )
    {
        return MakeLightFromProps(lightProps, lightName, position, mapName);
    }
}

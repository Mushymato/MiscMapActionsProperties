using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewValley;

namespace MiscMapActionsProperties.Framework.Location;

/// <summary>
/// Add new map property mushymato.MMAP_WoodsLighting T|Color [T|Color true]
/// If set to T, uses the default ambiant lighting (equiv to setting #6987cd, and thus has actual appearance of  #967832)
/// Otherwise, pass in an ambiant light color, which is inverted
/// </summary>
internal static class WoodsLighting
{
    internal sealed record WoodsLightingCtx(Color DayColor, Color NightColor, bool AffectMapLights);

    internal const string MapProp_WoodsLighting = $"{ModEntry.ModId}_WoodsLighting";
    internal static PerScreenCache<WoodsLightingCtx?> woodsLightingCtx = PerScreenCache.Make<WoodsLightingCtx?>();

    internal static void Register()
    {
        CommonPatch.GameLocation_UpdateWhenCurrentLocationFinalizer += GameLocation_UpdateWhenCurrentLocation_Finalizer;
        CommonPatch.GameLocation_resetLocalState += GameLocation_resetLocalState_Postfix;
    }

    private static void ApplyLighting(GameLocation location, WoodsLightingCtx ctx)
    {
        if (Game1.currentLocation != location)
            return;

        int moderatedarkmin60 = Utility.ConvertTimeToMinutes(Game1.getModeratelyDarkTime(location)) - 60;
        int trulydark = Utility.ConvertTimeToMinutes(Game1.getTrulyDarkTime(location));
        int startingtogetdark = Utility.ConvertTimeToMinutes(Game1.getStartingToGetDarkTime(location));
        int moderatedark = Utility.ConvertTimeToMinutes(Game1.getModeratelyDarkTime(location));
        float current =
            Utility.ConvertTimeToMinutes(Game1.timeOfDay)
            + Game1.gameTimeInterval / (float)Game1.realMilliSecondsPerGameMinute;
        float lerpc = Utility.Clamp((current - moderatedarkmin60) / (trulydark - moderatedarkmin60), 0f, 1f);

        Game1.ambientLight.R = (byte)Utility.Lerp(ctx.DayColor.R, ctx.NightColor.R, lerpc);
        Game1.ambientLight.G = (byte)Utility.Lerp(ctx.DayColor.G, ctx.NightColor.G, lerpc);
        Game1.ambientLight.B = (byte)Utility.Lerp(ctx.DayColor.B, ctx.NightColor.B, lerpc);
        Game1.ambientLight.A = (byte)Utility.Lerp(ctx.DayColor.A, ctx.NightColor.A, lerpc);

        if (location.IsOutdoors && location.IsRainingHere())
            Game1.outdoorLight = Game1.ambientLight;

        if (!ctx.AffectMapLights)
            return;

        float lerpl = Utility.Clamp((current - startingtogetdark) / (moderatedark - startingtogetdark), 0f, 1f);
        Color black = Color.Black;
        black.A = (byte)Utility.Lerp(255f, 0f, lerpl);
        foreach (LightSource value in Game1.currentLightSources.Values)
        {
            if (value.lightContext.Value == LightSource.LightContext.MapLight)
            {
                value.color.Value = black;
            }
        }
    }

    private static void GameLocation_resetLocalState_Postfix(object? sender, GameLocation location)
    {
        woodsLightingCtx.Value = null;
        if (CommonPatch.TryGetLocationalProperty(location, MapProp_WoodsLighting, out string? argString))
        {
            string[] args = ArgUtility.SplitBySpaceQuoteAware(argString);
            if (
                !ArgUtility.TryGet(
                    args,
                    0,
                    out string dayColorStr,
                    out string error,
                    allowBlank: false,
                    "string dayColorStr"
                )
                || !ArgUtility.TryGetOptional(
                    args,
                    1,
                    out string nightColorStr,
                    out error,
                    defaultValue: "T",
                    allowBlank: true,
                    name: "string nightColorStr"
                )
                || !ArgUtility.TryGetOptionalBool(
                    args,
                    2,
                    out bool affectMapLights,
                    out error,
                    defaultValue: true,
                    "bool affectMapLights"
                )
            )
            {
                ModEntry.Log(error, LogLevel.Error);
                return;
            }
            Color dayColor = new Color(150, 120, 50);
            if (dayColorStr != "T" && Utility.StringToColor(dayColorStr) is Color color1)
            {
                dayColor = new Color(color1.PackedValue ^ 0x00FFFFFF);
            }
            Color nightColor = Game1.eveningColor;
            if (nightColorStr != "T" && Utility.StringToColor(nightColorStr) is Color color2)
            {
                nightColor = new Color(color2.PackedValue ^ 0x00FFFFFF);
            }
            woodsLightingCtx.Value = new(dayColor, nightColor, affectMapLights);
            ApplyLighting(location, woodsLightingCtx.Value);
            return;
        }
    }

    private static void GameLocation_UpdateWhenCurrentLocation_Finalizer(
        object? sender,
        CommonPatch.UpdateWhenCurrentLocationArgs e
    )
    {
        if (woodsLightingCtx.Value != null)
        {
            ApplyLighting(e.Location, woodsLightingCtx.Value);
        }
    }
}

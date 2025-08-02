using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace MiscMapActionsProperties.Framework.Location;

/// <summary>
/// Add new map property mushymato.MMAP_WoodsDebris T
/// When this is set on a location, make it ignore the default outdoor debris weather and add debris like secret woods
/// </summary>
internal static class WoodsDebris
{
    internal const string MapProp_WoodsDebris = $"{ModEntry.ModId}_WoodsDebris";
    private static readonly PerScreen<List<WeatherDebris>> _weatherDebris = new(() => []);
    private static List<WeatherDebris> WeatherDebris => _weatherDebris.Value;

    internal static void Register()
    {
        CommonPatch.GameLocation_resetLocalState += GameLocation_resetLocalState;
        CommonPatch.GameLocation_UpdateWhenCurrentLocationFinalizer += GameLocation_UpdateWhenCurrentLocationFinalizer;
        ModEntry.help.Events.Display.RenderedStep += OnRenderedStep;
    }

    private static void GameLocation_resetLocalState(object? sender, GameLocation e)
    {
        if (CommonPatch.TryGetCustomFieldsOrMapProperty(e, MapProp_WoodsDebris, out string? prop))
        {
            ModEntry.Log($"{MapProp_WoodsDebris}:{prop}");
            if (prop != "T")
            {
                return;
            }
            if (e.IsRainingHere())
                return;
            Random random = Utility.CreateDaySaveRandom();
            int num = 25 + random.Next(0, 75);
            e.ignoreDebrisWeather.Value = true;
            Season season = e.GetSeason();
            if (season != Season.Winter)
            {
                int num2 = 192;
                int which = 1;
                if (season == Season.Fall)
                {
                    which = 2;
                }
                for (int j = 0; j < num; j++)
                {
                    WeatherDebris.Add(
                        new WeatherDebris(
                            new Vector2(
                                j * num2 % Game1.graphics.GraphicsDevice.Viewport.Width + Game1.random.Next(num2),
                                j
                                    * num2
                                    / Game1.graphics.GraphicsDevice.Viewport.Width
                                    * num2
                                    % Game1.graphics.GraphicsDevice.Viewport.Height
                                    + Game1.random.Next(num2)
                            ),
                            which,
                            Game1.random.Next(15) / 500f,
                            Game1.random.Next(-10, 0) / 50f,
                            Game1.random.Next(10) / 50f
                        )
                    );
                }
            }
        }
        else
        {
            WeatherDebris.Clear();
        }
    }

    private static void GameLocation_UpdateWhenCurrentLocationFinalizer(
        object? sender,
        CommonPatch.UpdateWhenCurrentLocationArgs e
    )
    {
        foreach (WeatherDebris weatherDebri in WeatherDebris)
        {
            weatherDebri.update();
        }
        Game1.updateDebrisWeatherForMovement(WeatherDebris);
    }

    private static void OnRenderedStep(object? sender, RenderedStepEventArgs e)
    {
        if (e.Step == StardewValley.Mods.RenderSteps.World_AlwaysFront)
        {
            foreach (WeatherDebris weatherDebri in WeatherDebris)
            {
                weatherDebri.draw(e.SpriteBatch);
            }
        }
    }
}

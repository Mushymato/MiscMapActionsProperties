using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Locations;

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
        WeatherDebris.Clear();
        if (CommonPatch.TryGetCustomFieldsOrMapProperty(e, MapProp_WoodsDebris, out string? prop))
        {
            Season season = e.GetSeason();
            bool shouldDebris;
            int which;
            if (prop == "T")
            {
                shouldDebris = !e.IsRainingHere() && season != Season.Winter;
                which = season == Season.Fall ? 2 : 1;
            }
            else
            {
                string[] args = ArgUtility.SplitBySpaceQuoteAware(prop);
                if (
                    !ArgUtility.TryGetInt(args, 0, out which, out string err, name: "int which")
                    || !ArgUtility.TryGetOptional(
                        args,
                        1,
                        out string gsq,
                        out err,
                        defaultValue: "TRUE",
                        allowBlank: false,
                        name: "string gsq"
                    )
                )
                {
                    ModEntry.Log(err, StardewModdingAPI.LogLevel.Error);
                    shouldDebris = false;
                }
                else
                {
                    shouldDebris = GameStateQuery.CheckConditions(gsq, e);
                    if (which == -1)
                    {
                        which = season switch
                        {
                            Season.Fall => 2,
                            Season.Winter => 3,
                            Season.Summer => 1,
                            _ => 0,
                        };
                    }
                    else if (which == -2)
                    {
                        which = season switch
                        {
                            Season.Fall => 2,
                            Season.Winter => 3,
                            _ => 1,
                        };
                    }
                }
            }
            if (shouldDebris)
            {
                e.ignoreDebrisWeather.Value = false;
                return;
            }

            Random random = Utility.CreateDaySaveRandom();
            int num = 25 + random.Next(0, 75);
            e.ignoreDebrisWeather.Value = true;
            int num2 = 192;

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

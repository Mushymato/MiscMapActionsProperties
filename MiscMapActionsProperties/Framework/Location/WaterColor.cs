using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewValley;

namespace MiscMapActionsProperties.Framework.Location;

/// <summary>
/// Add new map property mushymato.MMAP_WaterColor T|color [T|color T|color T|color]
/// Overrides the watercolor
/// Can provide 4 colors for seasonal
/// </summary>
internal static class WaterColor
{
    internal const string MapProp_WaterColor = $"{ModEntry.ModId}_WaterColor";

    internal static void Register()
    {
        CommonPatch.GameLocation_resetLocalState += GameLocation_resetLocalState_Postfix;
    }

    private static void GameLocation_resetLocalState_Postfix(object? sender, GameLocation location)
    {
        if (
            CommonPatch.TryGetCustomFieldsOrMapProperty(location, MapProp_WaterColor, out string? waterColors)
            && !string.IsNullOrWhiteSpace(waterColors)
        )
        {
            string[] args = ArgUtility.SplitBySpace(waterColors);
            Season season = location.GetSeason();
            Color? waterColorOverride = null;
            if (
                ArgUtility.TryGet(
                    args,
                    (int)season,
                    out string seasonColor,
                    out _,
                    allowBlank: false,
                    name: "string seasonWaterColor"
                )
            )
            {
                if (seasonColor != "T")
                    waterColorOverride = Utility.StringToColor(seasonColor);
            }
            else if (
                ArgUtility.TryGet(
                    args,
                    0,
                    out string springColor,
                    out _,
                    allowBlank: false,
                    name: "string seasonWaterColor"
                )
            )
            {
                if (springColor != "T")
                    waterColorOverride = Utility.StringToColor(springColor);
            }
            else
            {
                return;
            }
            if (waterColorOverride.HasValue)
            {
                location.waterColor.Value = waterColorOverride.Value;
            }
        }
    }
}

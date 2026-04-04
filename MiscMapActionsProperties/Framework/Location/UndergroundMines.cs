using StardewModdingAPI;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Locations;
using static StardewValley.GameStateQuery;

namespace MiscMapActionsProperties.Framework.Location;

/// <summary>
/// Backports a few mines related GSQ
/// Adds slimed and dinoed
/// </summary>
internal static class UndergroundMines
{
    internal const string GSQ_MINE_AREA_TYPE = $"{ModEntry.ModId}_MINE_AREA_TYPE";
    internal const string GSQ_MAP_NAME = $"{ModEntry.ModId}_MAP_NAME";

    internal static void Register()
    {
        GameStateQuery.Register(GSQ_MINE_AREA_TYPE, MINE_AREA_TYPE);
        GameStateQuery.Register(GSQ_MAP_NAME, MAP_NAME);

#if SDV16
        GameStateQuery.Register("LOCATION_MINE_DIFFICULTY", LOCATION_MINE_DIFFICULTY);
        static bool LOCATION_MINE_DIFFICULTY(string[] query, GameStateQueryContext context)
        {
            GameLocation location = context.Location;
            if (
                !Helpers.TryGetLocationArg(query, 1, ref location, out string? error)
                || !ArgUtility.TryGetInt(query, 2, out int minDifficulty, out error, "int minDifficulty")
                || !ArgUtility.TryGetOptionalInt(
                    query,
                    3,
                    out int maxDifficulty,
                    out error,
                    int.MaxValue,
                    "int maxDifficulty"
                )
            )
            {
                return Helpers.ErrorResult(query, error);
            }
            if (location is MineShaft mineShaft)
            {
                int additionalDifficulty = mineShaft.GetAdditionalDifficulty();
                if (additionalDifficulty >= minDifficulty)
                {
                    return additionalDifficulty <= maxDifficulty;
                }
                return false;
            }
            return false;
        }

        GameStateQuery.Register("LOCATION_MINE_LEVEL", LOCATION_MINE_LEVEL);
        static bool LOCATION_MINE_LEVEL(string[] query, GameStateQueryContext context)
        {
            GameLocation location = context.Location;
            if (
                !Helpers.TryGetLocationArg(query, 1, ref location, out string? error)
                || !ArgUtility.TryGetInt(query, 2, out int minLevel, out error, "int minLevel")
                || !ArgUtility.TryGetOptionalInt(query, 3, out int maxLevel, out error, int.MaxValue, "int maxLevel")
            )
            {
                return Helpers.ErrorResult(query, error);
            }
            if (location is MineShaft { mineLevel: var mineLevel })
            {
                if (mineLevel >= minLevel)
                {
                    return mineLevel <= maxLevel;
                }
                return false;
            }
            return false;
        }
#endif
    }

    private static bool MAP_NAME(string[] query, GameStateQueryContext context)
    {
        GameLocation location = context.Location;
        if (
            !Helpers.TryGetLocationArg(query, 1, ref location, out string? error)
            || !ArgUtility.TryGet(query, 2, out string mapPath, out error)
        )
        {
            ModEntry.Log(error);
            return false;
        }
        if (location.mapPath.Value == null)
            return false;
        return ModEntry.help.GameContent.ParseAssetName(mapPath).IsEquivalentTo(location.mapPath.Value);
    }

    private static bool MINE_AREA_TYPE(string[] query, GameStateQueryContext context)
    {
        GameLocation location = context.Location;
        if (!Helpers.TryGetLocationArg(query, 1, ref location, out string? error))
        {
            ModEntry.Log(error, LogLevel.Error);
            return false;
        }
        if (location is not MineShaft mineShaft)
        {
            return false;
        }
        foreach (string mineType in query.Skip(2))
        {
            switch (mineType.ToUpper())
            {
                case "SLIME":
                    if (mineShaft.isSlimeArea)
                        return true;
                    break;
                case "DINO":
                    if (mineShaft.isDinoArea)
                        return true;
                    break;
                case "QUARRY":
                    if (mineShaft.isQuarryArea)
                        return true;
                    break;
            }
        }
        return false;
    }
}

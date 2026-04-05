using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Locations;
using StardewValley.Triggers;
using xTile.Tiles;
using static StardewValley.GameStateQuery;

namespace MiscMapActionsProperties.Framework.Location;

/// <summary>
/// Backports a few mines related GSQ
/// Adds slimed and dinoed
/// </summary>
internal static class UndergroundMines
{
    private const string GSQ_MINE_AREA_TYPE = $"{ModEntry.ModId}_MINE_AREA_TYPE";
    private const string GSQ_MAP_NAME = $"{ModEntry.ModId}_MAP_NAME";
    private const string GSQ_TILESHEET_NAME = $"{ModEntry.ModId}_TILESHEET_NAME";
    private const string Action_SetTilesheet = $"{ModEntry.ModId}_SetTilesheet";

    internal static void Register()
    {
        GameStateQuery.Register(GSQ_MINE_AREA_TYPE, MINE_AREA_TYPE);
        GameStateQuery.Register(GSQ_MAP_NAME, MAP_NAME);
        GameStateQuery.Register(GSQ_TILESHEET_NAME, TILESHEET_NAME);
        TriggerActionManager.RegisterAction(Action_SetTilesheet, TriggerSetTilesheet);

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

    private static bool TriggerSetTilesheet(string[] args, TriggerActionContext context, out string error)
    {
        GameLocation location = Game1.currentLocation;
        if (
            !Helpers.TryGetLocationArg(args, 1, ref location, out error)
            || !ArgUtility.TryGet(args, 2, out string tileSheetId, out error)
            || !ArgUtility.TryGet(args, 3, out string assetName, out error)
        )
        {
            ModEntry.Log(error, LogLevel.Error);
            return false;
        }
        if (!Game1.content.DoesAssetExist<Texture2D>(assetName))
        {
            ModEntry.Log($"Tilesheet asset '{assetName}' does not exist", LogLevel.Error);
            return false;
        }
        if (location.Map?.GetTileSheet(tileSheetId) is not TileSheet tileSheet)
            return false;
        if (location is MineShaft mineShaft && tileSheetId == "mine")
        {
            mineShaft.mapImageSource.Value = assetName;
        }
        else
        {
            tileSheet.ImageSource = assetName;
            location.Map.LoadTileSheets(Game1.mapDisplayDevice);
        }
        return true;
    }

    private static bool TILESHEET_NAME(string[] query, GameStateQueryContext context)
    {
        GameLocation location = context.Location;
        if (
            !Helpers.TryGetLocationArg(query, 1, ref location, out string? error)
            || !ArgUtility.TryGet(query, 2, out string tileSheetId, out error)
            || !ArgUtility.TryGet(query, 3, out string assetName, out error)
        )
        {
            ModEntry.Log(error, LogLevel.Error);
            return false;
        }
        if (location?.Map?.GetTileSheet(tileSheetId) is not TileSheet tileSheet)
            return false;
        return ModEntry.help.GameContent.ParseAssetName(assetName).IsEquivalentTo(tileSheet.ImageSource);
    }

    private static bool MAP_NAME(string[] query, GameStateQueryContext context)
    {
        GameLocation location = context.Location;
        if (
            !Helpers.TryGetLocationArg(query, 1, ref location, out string? error)
            || !ArgUtility.TryGet(query, 2, out string mapPath, out error)
        )
        {
            ModEntry.Log(error, LogLevel.Error);
            return false;
        }
        if (location?.mapPath.Value == null)
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

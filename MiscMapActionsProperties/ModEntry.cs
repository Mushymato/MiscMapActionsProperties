global using MapTile = xTile.Tiles.Tile;
using HarmonyLib;
using StardewModdingAPI;

namespace MiscMapActionsProperties;

public class ModEntry : Mod
{
#if DEBUG
    private const LogLevel DEFAULT_LOG_LEVEL = LogLevel.Debug;
#else
    private const LogLevel DEFAULT_LOG_LEVEL = LogLevel.Trace;
#endif
    private static IMonitor mon = null!;
    internal static IManifest manifest = null!;
    internal static IModHelper help = null!;
    internal static Harmony harm = null!;

    internal static string ModId => manifest?.UniqueID ?? "ERROR";

    public override void Entry(IModHelper helper)
    {
        mon = Monitor;
        manifest = ModManifest;
        help = helper;
        harm = new(ModId);

        Framework.Buildings.ChestLight.Register();
        Framework.Buildings.DrawLayerExt.Register();
        Framework.Location.CribPosition.Register();
        Framework.Location.DayToNightTiming.Register();
        Framework.Location.FridgePosition.Register();
        Framework.Location.FruitTreeCosmeticSeason.Register();
        Framework.Location.GrassOverride.Register();
        Framework.Location.HoeDirtOverride.Register();
        Framework.Location.LightRays.Register();
        Framework.Location.MapChangeRelocate.Register();
        Framework.Location.Panorama.Register();
        Framework.Location.SteamOverlay.Register();
        Framework.Location.WoodsLighting.Register();
        Framework.Tile.AnimalSpot.Register();
        Framework.Tile.CritterSpot.Register();
        Framework.Tile.HoleWarp.Register();
        Framework.Tile.LightSpot.Register();
        Framework.Tile.QuestionDialogue.Register();
        Framework.Tile.ShowConstruct.Register();
        Framework.Tile.TASSpot.Register();

        Framework.Wheels.CommonPatch.Register();
        Framework.Wheels.TASAssetManager.Register();
    }

    internal static void Log(string msg, LogLevel level = DEFAULT_LOG_LEVEL)
    {
        mon.Log(msg, level);
    }

    internal static void LogOnce(string msg, LogLevel level = DEFAULT_LOG_LEVEL)
    {
        mon.LogOnce(msg, level);
    }
}

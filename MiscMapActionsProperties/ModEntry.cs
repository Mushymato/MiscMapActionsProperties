global using MapTile = xTile.Tiles.Tile;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

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

    public static event EventHandler<GameLocation>? GameLocation_resetLocalState;

    public override void Entry(IModHelper helper)
    {
        mon = Monitor;
        manifest = ModManifest;
        help = helper;
        harm = new(ModId);

        Framework.Buildings.ChestLight.Register();
        Framework.Buildings.DrawLayerExt.Register();
        Framework.Location.FruitTreeCosmeticSeason.Register();
        Framework.Location.HoeDirtOverride.Register();
        Framework.Location.LightRays.Register();
        Framework.Location.MapChangeRelocate.Register();
        Framework.Location.WoodsLighting.Register();
        Framework.Tile.AnimalSpot.Register();
        Framework.Tile.HoleWarp.Register();
        Framework.Tile.LightSpot.Register();
        Framework.Tile.QuestionDialogue.Register();
        Framework.Tile.ShowConstruct.Register();
        Framework.Tile.TASSpot.Register();

        harm.Patch(
            original: AccessTools.Method(typeof(GameLocation), "resetLocalState"),
            postfix: new HarmonyMethod(typeof(ModEntry), nameof(GameLocation_resetLocalState_Postfix))
        );
    }

    private static void GameLocation_resetLocalState_Postfix(GameLocation __instance)
    {
        GameLocation_resetLocalState?.Invoke(null, __instance);
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

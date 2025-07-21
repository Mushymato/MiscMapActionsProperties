global using MapTile = xTile.Tiles.Tile;
using HarmonyLib;
using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using Mushymato.ExtendedTAS;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;

namespace MiscMapActionsProperties;

public class ModEntry : Mod
{
    public class ModConfig
    {
        public bool DisableDrawPatches { get; set; } = false;
    }

#if DEBUG
    private const LogLevel DEFAULT_LOG_LEVEL = LogLevel.Debug;
#else
    private const LogLevel DEFAULT_LOG_LEVEL = LogLevel.Trace;
#endif
    private static IMonitor mon = null!;
    internal static IManifest manifest = null!;
    internal static IModHelper help = null!;
    internal static Harmony harm = null!;
    internal static TASAssetManager TAS = null!;

    internal static ModConfig Config = null!;

    internal const string ModId = "mushymato.MMAP";

    public override void Entry(IModHelper helper)
    {
        mon = Monitor;
        manifest = ModManifest;
        help = helper;
        harm = new(ModId);
        Config = helper.ReadConfig<ModConfig>();
        helper.ConsoleCommands.Add("mmap.chaired", "Spawn chairs", ConsoleChaired);

        TAS = new(helper, $"{ModId}/TAS");
        Framework.Wheels.CommonPatch.Register();

        Framework.Entities.ChestLight.Register();
        Framework.Entities.DrawLayerExt.Register();
        Framework.Entities.FloorPathProperties.Register();
        Framework.Entities.FurnitureProperties.Register();
        Framework.Entities.HumanDoorExt.Register();

        Framework.Location.CribPosition.Register();
        Framework.Location.DayToNightTiming.Register();
        Framework.Location.FridgePosition.Register();
        Framework.Location.FruitTreeCosmeticSeason.Register();
        Framework.Location.LightRays.Register();
        Framework.Location.MapChangeRelocate.Register();
        Framework.Location.Panorama.Register();
        Framework.Location.ProtectTree.Register();
        Framework.Location.FarmHouseFurniture.Register();
        Framework.Location.SteamOverlay.Register();
        Framework.Location.WaterColor.Register();
        Framework.Location.WoodsLighting.Register();

        Framework.Tile.ActionCond.Register();
        Framework.Tile.AnimalSpot.Register();
        Framework.Tile.CritterSpot.Register();
        Framework.Tile.GrassSpread.Register();
        Framework.Tile.HoleWrp.Register();
        Framework.Tile.LightSpot.Register();
        Framework.Tile.PaddySpot.Register();
        Framework.Tile.PoolEntry.Register();
        Framework.Tile.QuestionDialogue.Register();
        Framework.Tile.ShowConstruct.Register();
        Framework.Tile.ShowGlobalInventory.Register();
        Framework.Tile.TASSpot.Register();
    }

    private void ConsoleChaired(string arg1, string[] arg2)
    {
        if (Game1.currentLocation is not GameLocation location)
            return;
        foreach ((Vector2 pos, MapTile tile) in CommonPatch.IterateMapTiles(location.Map, "Back"))
        {
            if (location.isTilePlaceable(pos))
            {
                location.furniture.Add(ItemRegistry.Create<Furniture>("PlasticLawnChair").SetPlacement(pos));
            }
        }
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

global using MapTile = xTile.Tiles.Tile;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Mushymato.ExtendedTAS;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;

namespace MiscMapActionsProperties;

public sealed class ModConfig
{
    public bool Enable_doesTileHaveProperty_Optimization = true;
}

public sealed class ModEntry : Mod
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
    internal static TASAssetManager TAS = null!;

    internal const string ModId = "mushymato.MMAP";

    public override void Entry(IModHelper helper)
    {
        ModConfig config = helper.ReadConfig<ModConfig>();

        mon = Monitor;
        manifest = ModManifest;
        help = helper;
        harm = new(ModId);

        helper.ConsoleCommands.Add(
            "mmap.chaired",
            "Spawn stuff at every tile in the current map for performance testing, DO NOT USE IN NORMAL GAMEPLAY",
            ConsoleChaired
        );

        TAS = new(helper, $"{ModId}/TAS");
        Framework.Wheels.CommonPatch.Setup();
        if (config.Enable_doesTileHaveProperty_Optimization)
        {
            Framework.Wheels.Optimization.Setup();
        }

        Framework.Entities.ChestLight.Register();
        Framework.Entities.ConnectedTextures.Register();
        Framework.Entities.DrawLayerExt.Register();
        Framework.Entities.TerrainFeatureProperties.Register();
        Framework.Entities.FurnitureProperties.Register();
        Framework.Entities.HumanDoorExt.Register();

        Framework.Location.CribPosition.Register();
        Framework.Location.DayToNightTiming.Register();
        Framework.Location.FridgePosition.Register();
        Framework.Location.FruitTreeCosmeticSeason.Register();
        Framework.Location.LightRays.Register();
        Framework.Location.MapChangeRelocate.Register();
        Framework.Location.MapOverride.Register();
        Framework.Location.Panorama.Register();
        Framework.Location.ProtectTree.Register();
        Framework.Location.FarmHouseFurniture.Register();
        Framework.Location.SteamOverlay.Register();
        Framework.Location.WaterColor.Register();
        Framework.Location.WoodsBaubles.Register();
        Framework.Location.WoodsDebris.Register();
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
        Framework.Tile.ShowShipping.Register();
        Framework.Tile.TASSpot.Register();
    }

    private void ConsoleChaired(string arg1, string[] arg2)
    {
        if (Game1.currentLocation is not GameLocation location)
            return;
        foreach ((Vector2 pos, _) in Framework.Wheels.CommonPatch.IterateMapTiles(location.Map, "Back"))
        {
            if (location.isTilePlaceable(pos))
            {
                Furniture newFurni = ItemRegistry.Create<Furniture>(arg2[0]).SetPlacement(pos);
                if (arg2.Length >= 2)
                    newFurni.SetHeldObject(ItemRegistry.Create<Furniture>(arg2[1]));
                location.furniture.Add(newFurni);
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

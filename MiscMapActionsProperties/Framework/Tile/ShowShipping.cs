using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Delegates;
using StardewValley.Triggers;

namespace MiscMapActionsProperties.Framework.Tile;

/// <summary>
/// Add new tile action mushymato.MMAP_ShowShipping
/// Shows shipping bin menu, as long as the player has a shipping bin on the farm.
/// </summary>
internal static class ShowShipping
{
    internal const string TileAction_ShowShipping = $"{ModEntry.ModId}_ShowShipping";

    internal static void Register()
    {
        CommonPatch.RegisterTileAndTouch(TileAction_ShowShipping, TileShowShipping);
        TriggerActionManager.RegisterAction(TileAction_ShowShipping, DoShowShipping);
    }

    private static bool DoShowShipping(string[] args, TriggerActionContext context, out string error)
    {
        error = null!;
        Farm farm = Game1.getFarm();
        if (farm.buildings.FirstOrDefault(bld => bld is ShippingBin) is ShippingBin bin)
        {
            bin.doAction(new Vector2(bin.tileX.Value, bin.tileY.Value), Game1.player);
            return true;
        }
        else
        {
            error = "The player has no shipping bin on the farm!";
            return false;
        }
    }

    private static bool TileShowShipping(GameLocation location, string[] arg2, Farmer farmer, Point point)
    {
        Farm farm = Game1.getFarm();
        if (farm.buildings.FirstOrDefault(bld => bld is ShippingBin) is ShippingBin bin)
        {
            bin.doAction(new Vector2(bin.tileX.Value, bin.tileY.Value), farmer);
            return true;
        }
        else
        {
            ModEntry.Log("The player has no shipping bin on the farm!", LogLevel.Error);
            return false;
        }
    }
}

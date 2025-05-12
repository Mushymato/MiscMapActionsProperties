using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Objects;
using StardewValley.Triggers;

namespace MiscMapActionsProperties.Framework.Tile;

/// <summary>
/// Add new tile action mushymato.MMAP_ShowBag
/// This displays a "bag" (global inventory) that can accept items
/// </summary>
internal static class ShowGlobalInventory
{
    internal static readonly string TileAction_ShowBag = $"{ModEntry.ModId}_ShowBag";
    private static readonly PerScreen<WeakReference<Chest?>> placeholderChest = new() { Value = new(null) };

    internal static void Register()
    {
        CommonPatch.RegisterTileAndTouch(TileAction_ShowBag, ShowBag);
        TriggerActionManager.RegisterAction(TileAction_ShowBag, TriggerShowBag);
    }

    private static bool TriggerShowBag(string[] args, TriggerActionContext context, out string error)
    {
        if (!ArgUtility.TryGetPoint(args, 1, out Point bagPoint, out error, "Point bagPoint"))
            return false;
        return ShowBag(Game1.currentLocation, [.. args.Skip(2)], Game1.player, bagPoint);
    }

    internal static string GetBagInventoryId(GameLocation location, string bagInvId)
    {
        return string.Join('#', ModEntry.ModId, location.NameOrUniqueName, bagInvId);
    }

    private static Chest GetPlaceholderChest()
    {
        if (!placeholderChest.Value.TryGetTarget(out Chest? chest) || chest == null)
        {
            chest = new Chest(playerChest: true);
        }
        return chest;
    }

    private static bool ShowBag(GameLocation location, string[] args, Farmer farmer, Point point)
    {
        if (!ArgUtility.TryGet(args, 1, out string bagInvId, out string error, allowBlank: false, "string bagInvId"))
        {
            ModEntry.Log(error, LogLevel.Error);
            return false;
        }
        Chest phChest = GetPlaceholderChest();

        bool before = Game1.player.showChestColorPicker;
        Game1.player.showChestColorPicker = false;

        phChest.GlobalInventoryId = GetBagInventoryId(location, bagInvId);
        phChest.SpecialChestType = Chest.SpecialChestTypes.None;
        phChest
            .GetMutex()
            .RequestLock(() =>
            {
                phChest.ShowMenu();
                DelayedAction delayedAction = DelayedAction.functionAfterDelay(
                    () =>
                    {
                        Game1.player.showChestColorPicker = before;
                        phChest.GetMutex().ReleaseLock();
                    },
                    1
                );
                delayedAction.waitUntilMenusGone = true;
            });
        return true;
    }
}

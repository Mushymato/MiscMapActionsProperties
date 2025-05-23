using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.Triggers;

namespace MiscMapActionsProperties.Framework.Tile;

/// <summary>
/// Add new tile action mushymato.MMAP_ShowBag
/// This displays a "bag" (global inventory) that can accept items
/// </summary>
internal static class ShowGlobalInventory
{
    internal const string TileAction_ShowBag = $"{ModEntry.ModId}_ShowBag";

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

    internal static string GetBagInventoryId(string bagInvId)
    {
        return string.Join('#', ModEntry.ModId, bagInvId);
    }

    private static bool ShowBag(GameLocation location, string[] args, Farmer farmer, Point point)
    {
        if (
            !ArgUtility.TryGet(args, 1, out string bagInvId, out string error, allowBlank: false, "string bagInvId")
            || !ArgUtility.TryGetOptionalEnum(
                args,
                2,
                out Chest.SpecialChestTypes bagInvType,
                out error,
                defaultValue: Chest.SpecialChestTypes.None,
                "Chest.SpecialChestTypes bagInvType"
            )
        )
        {
            ModEntry.Log(error, LogLevel.Error);
            return false;
        }
        Chest phChest = new(playerChest: true);

        bool before = Game1.player.showChestColorPicker;
        Game1.player.showChestColorPicker = false;

        phChest.GlobalInventoryId = GetBagInventoryId(bagInvId);
        phChest.SpecialChestType = bagInvType;
        ModEntry.Log($"Open global inventory {phChest.GlobalInventoryId} ({phChest.SpecialChestType})");
        phChest
            .GetMutex()
            .RequestLock(() =>
            {
                phChest.ShowMenu();
                if (Game1.activeClickableMenu is ItemGrabMenu igm)
                {
                    igm.exitFunction = (IClickableMenu.onExit)
                        Delegate.Combine(
                            igm.exitFunction,
                            (IClickableMenu.onExit)
                                delegate
                                {
                                    Game1.player.showChestColorPicker = before;
                                    phChest.GetMutex().ReleaseLock();
                                }
                        );
                }
            });
        return true;
    }
}

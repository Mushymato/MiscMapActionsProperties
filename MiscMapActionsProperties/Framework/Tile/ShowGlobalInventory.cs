using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Inventories;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.Triggers;

namespace MiscMapActionsProperties.Framework.Tile;

/// <summary>
/// Add new tile action mushymato.MMAP_ShowBag
/// This displays a "bag" (global inventory) that can hold items
/// </summary>
internal static class ShowGlobalInventory
{
    internal const string TileAction_ShowBag = $"{ModEntry.ModId}_ShowBag";
    internal const string GSQ_BAG_HAS_ITEM = $"{ModEntry.ModId}_BAG_HAS_ITEM";

    internal static void Register()
    {
        CommonPatch.RegisterTileAndTouch(TileAction_ShowBag, TileShowBag);
        TriggerActionManager.RegisterAction(TileAction_ShowBag, TriggerShowBag);
        GameStateQuery.Register(GSQ_BAG_HAS_ITEM, BAG_HAS_ITEM);
        ModEntry.help.ConsoleCommands.Add(
            "mmap.list_bags",
            "List MMAP bags (global inventories), you can open them with 'debug action mushymato.MMAP_ShowBag <bagId>'",
            ConsoleListBags
        );
    }

    private static void ConsoleListBags(string arg1, string[] arg2)
    {
        if (!Context.IsWorldReady)
            return;

        foreach (var key in Game1.player.team.globalInventories.Keys)
        {
            Inventory inventory = Game1.player.team.globalInventories[key];
            if (inventory == null)
                continue;
            if (key.StartsWith($"{ModEntry.ModId}#"))
            {
                string mmapKey = key.Split('#').Last();
                ModEntry.Log(
                    $"{mmapKey} ({inventory.Count} items) - 'debug action mushymato.MMAP_ShowBag \"{mmapKey}\"'",
                    LogLevel.Info
                );
            }
        }
    }

    private static bool BAG_HAS_ITEM(string[] query, GameStateQueryContext context)
    {
        if (
            !ArgUtility.TryGet(query, 1, out string bagInvId, out string error, allowBlank: false, "string bagInvId")
            || !ArgUtility.TryGet(query, 2, out var itemId, out error, allowBlank: true, "string itemId")
            || !ArgUtility.TryGetOptionalInt(query, 3, out var minCount, out error, 1, "int minCount")
            || !ArgUtility.TryGetOptionalInt(query, 4, out var maxCount, out error, int.MaxValue, "int maxCount")
        )
        {
            ModEntry.Log(error, LogLevel.Error);
            return false;
        }
        if (!Game1.player.team.globalInventories.TryGetValue(GetBagInventoryId(bagInvId), out Inventory inventory))
            return false;
        if (maxCount != int.MaxValue)
        {
            int num = inventory.CountId(itemId);
            return num >= minCount && num <= maxCount;
        }
        return inventory.ContainsId(itemId, minCount);
    }

    private static bool TriggerShowBag(string[] args, TriggerActionContext context, out string error) =>
        ShowBag(args, out error);

    private static bool TileShowBag(GameLocation location, string[] args, Farmer farmer, Point point) =>
        ShowBag(args, out _);

    internal static string GetBagInventoryId(string bagInvId)
    {
        return string.Join('#', ModEntry.ModId, bagInvId);
    }

    private static bool ShowBag(string[] args, out string error)
    {
        if (
            !ArgUtility.TryGet(args, 1, out string bagInvId, out error, allowBlank: false, "string bagInvId")
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

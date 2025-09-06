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
    internal const string TileAction_AddItemToBag = $"{ModEntry.ModId}_AddItemToBag";
    internal const string TileAction_RemoveItemFromBag = $"{ModEntry.ModId}_RemoveItemFromBag";
    internal const string GSQ_BAG_HAS_ITEM = $"{ModEntry.ModId}_BAG_HAS_ITEM";

    internal static void Register()
    {
        CommonPatch.RegisterTileAndTouch(TileAction_ShowBag, TileShowBag);
        TriggerActionManager.RegisterAction(TileAction_ShowBag, TriggerShowBag);
        CommonPatch.RegisterTileAndTouch(TileAction_AddItemToBag, TileAddItemToBag);
        TriggerActionManager.RegisterAction(TileAction_AddItemToBag, TriggerAddItemToBag);
        CommonPatch.RegisterTileAndTouch(TileAction_RemoveItemFromBag, TileRemoveItemFromBag);
        TriggerActionManager.RegisterAction(TileAction_RemoveItemFromBag, TriggerRemoveItemFromBag);
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

        foreach (string key in Game1.player.team.globalInventories.Keys)
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
            || !ArgUtility.TryGet(query, 2, out string itemId, out error, allowBlank: true, "string itemId")
            || !ArgUtility.TryGetOptionalInt(query, 3, out int minCount, out error, 1, "int minCount")
            || !ArgUtility.TryGetOptionalInt(query, 4, out int maxCount, out error, int.MaxValue, "int maxCount")
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

    private static bool TileShowBag(GameLocation location, string[] args, Farmer farmer, Point point) =>
        ShowBag(args, out _);

    private static bool TriggerShowBag(string[] args, TriggerActionContext context, out string error) =>
        ShowBag(args, out error);

    private static bool TileAddItemToBag(GameLocation location, string[] args, Farmer farmer, Point point) =>
        ModifyItemsInBag(args, AddItems, out _);

    private static bool TriggerAddItemToBag(string[] args, TriggerActionContext context, out string error) =>
        ModifyItemsInBag(args, AddItems, out error);

    private static bool TileRemoveItemFromBag(GameLocation location, string[] args, Farmer farmer, Point point) =>
        ModifyItemsInBag(args, RemoveItems, out _);

    private static bool TriggerRemoveItemFromBag(string[] args, TriggerActionContext context, out string error) =>
        ModifyItemsInBag(args, RemoveItems, out error);

    internal static string GetBagInventoryId(string bagInvId)
    {
        return string.Join('#', ModEntry.ModId, bagInvId);
    }

    private static void AddItems(Inventory items, string qId, int amount, int quality)
    {
        items.Add(ItemRegistry.Create(qId, amount, quality));
    }

    private static void RemoveItems(Inventory items, string qId, int amount, int quality)
    {
        items.ReduceId(qId, amount);
    }

    private static bool ModifyItemsInBag(string[] args, Action<Inventory, string, int, int> modifyBy, out string error)
    {
        if (
            !ArgUtility.TryGet(args, 1, out string bagInvId, out error, allowBlank: false, "string bagInvId")
            || !ArgUtility.TryGet(args, 2, out string qId, out error, allowBlank: false, "string qualifiedItemId")
            || !ArgUtility.TryGetOptionalInt(args, 3, out int amount, out error, defaultValue: 1, name: "int amount")
            || !ArgUtility.TryGetOptionalInt(args, 4, out int quality, out error, defaultValue: 0, name: "int quality")
        )
        {
            ModEntry.Log(error, LogLevel.Error);
            return false;
        }
        string globalInvId = GetBagInventoryId(bagInvId);
        Game1
            .player.team.GetOrCreateGlobalInventoryMutex(globalInvId)
            .RequestLock(() =>
            {
                Inventory items = Game1.player.team.GetOrCreateGlobalInventory(globalInvId);
                modifyBy(items, qId, amount, quality);
            });
        return true;
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

using HarmonyLib;
using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.Triggers;

namespace MiscMapActionsProperties.Framework.Location;

/// <summary>
/// New map property mushymato.MMAP_FarmHouseFurnitureAdd <furniture>
/// Add farmhouse furniture, works just like FarmHouseFurniture
/// New map property mushymato.MMAP_FarmHouseFurnitureRemove <coordinates>
/// Remove furniture at these coords, or all furniture if set to ALL
/// If the final furniture list has no bed, a bed will be added to the default position
/// New trigger action mushymato.MMAP_FarmHouseUpgrade, makes farmhouse upgrade tomorrow without going to robin's
/// </summary>
internal static class FarmHouseFurniture
{
    internal const string Action_FarmHouseUpgrade = $"{ModEntry.ModId}_FarmHouseUpgrade";
    internal const string MapProp_FarmHouseFurnitureAdd = $"{ModEntry.ModId}_FarmHouseFurnitureAdd";
    internal const string MapProp_FarmHouseFurnitureRemove = $"{ModEntry.ModId}_FarmHouseFurnitureRemove";
    internal const string Action_SetFlooring = $"{ModEntry.ModId}_SetFlooring";
    internal const string Action_SetWallpaper = $"{ModEntry.ModId}_SetWallpaper";

    internal static void Register()
    {
        TriggerActionManager.RegisterAction(Action_FarmHouseUpgrade, DoFarmHouseUpgrade);
        TriggerActionManager.RegisterAction(Action_SetFlooring, DoSetFlooring);
        TriggerActionManager.RegisterAction(Action_SetWallpaper, DoSetWallpaper);
        try
        {
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(FarmHouse), "AddStarterFurniture"),
                postfix: new HarmonyMethod(typeof(FarmHouseFurniture), nameof(FarmHouse_AddStarterFurniture_Postfix))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch FarmHouseFurniture:\n{err}", LogLevel.Error);
        }
    }

    private static bool DoSetWallpaper(string[] args, TriggerActionContext context, out string error)
    {
        if (
            !ArgUtility.TryGet(args, 1, out string wallpaper, out error, allowBlank: false, name: "string wallpaper")
            || !ArgUtility.TryGetOptional(args, 2, out string wallId, out error, name: "string wallId")
        )
        {
            return false;
        }
        FarmHouse farmHouse = Utility.getHomeOfFarmer(Game1.player);
        farmHouse.SetWallpaper(wallpaper, wallId);
        return true;
    }

    private static bool DoSetFlooring(string[] args, TriggerActionContext context, out string error)
    {
        if (
            !ArgUtility.TryGet(args, 1, out string flooring, out error, allowBlank: false, name: "string flooring")
            || !ArgUtility.TryGetOptional(args, 2, out string floorId, out error, name: "string floorId")
        )
        {
            return false;
        }
        FarmHouse farmHouse = Utility.getHomeOfFarmer(Game1.player);
        farmHouse.SetFloor(flooring, floorId);
        return true;
    }

    private static bool DoFarmHouseUpgrade(string[] args, TriggerActionContext context, out string error)
    {
        if (Context.IsWorldReady)
        {
            if (
                !ArgUtility.TryGetOptionalInt(
                    args,
                    1,
                    out int daysUntil,
                    out error,
                    defaultValue: 1,
                    name: "int daysUntil"
                )
            )
                return false;
            error = null!;
            Game1.player.daysUntilHouseUpgrade.Value = daysUntil;
            return true;
        }
        else
        {
            error = "Must have loaded a save.";
            return false;
        }
    }

    private static void FarmHouse_AddStarterFurniture_Postfix(FarmHouse __instance)
    {
        bool bedCheck = false;
        if (CommonPatch.TryGetLocationalProperty(__instance, MapProp_FarmHouseFurnitureRemove, out string? propStr1))
        {
            ModEntry.Log($"{MapProp_FarmHouseFurnitureRemove}: {propStr1}");
            if (propStr1 == "ALL")
            {
                __instance.furniture.Clear();
            }
            else
            {
                string[] mapPropertySplitBySpaces = ArgUtility.SplitBySpaceQuoteAware(propStr1);
                if (!mapPropertySplitBySpaces.Any())
                    return;
                bedCheck = true;

                HashSet<Furniture> toBeRemoved = [];
                for (int i = 0; i < mapPropertySplitBySpaces.Length; i += 2)
                {
                    if (
                        !ArgUtility.TryGetVector2(
                            mapPropertySplitBySpaces,
                            i,
                            out Vector2 value2,
                            out string error,
                            integerOnly: false,
                            "Vector2 tile"
                        )
                    )
                    {
                        __instance.LogMapPropertyError(
                            MapProp_FarmHouseFurnitureRemove,
                            mapPropertySplitBySpaces,
                            error
                        );
                        continue;
                    }
                    toBeRemoved.Add(__instance.GetFurnitureAt(value2));
                }
                if (toBeRemoved.Any())
                {
                    __instance.furniture.RemoveWhere(toBeRemoved.Contains);
                }
            }
        }
        if (CommonPatch.TryGetLocationalProperty(__instance, MapProp_FarmHouseFurnitureAdd, out string? propStr2))
        {
            ModEntry.Log($"{MapProp_FarmHouseFurnitureAdd}: {propStr2}");
            string[] mapPropertySplitBySpaces = ArgUtility.SplitBySpaceQuoteAware(propStr2);
            if (!mapPropertySplitBySpaces.Any())
                return;
            bedCheck = true;

            for (int i = 0; i < mapPropertySplitBySpaces.Length; i += 4)
            {
                if (
                    !ArgUtility.TryGet(
                        mapPropertySplitBySpaces,
                        i,
                        out string value,
                        out string error,
                        allowBlank: false,
                        name: "string id"
                    )
                    || !ArgUtility.TryGetVector2(
                        mapPropertySplitBySpaces,
                        i + 1,
                        out Vector2 value2,
                        out error,
                        integerOnly: false,
                        "Vector2 tile"
                    )
                    || !ArgUtility.TryGetInt(
                        mapPropertySplitBySpaces,
                        i + 3,
                        out int value3,
                        out error,
                        "int rotations"
                    )
                )
                {
                    __instance.LogMapPropertyError(MapProp_FarmHouseFurnitureAdd, mapPropertySplitBySpaces, error);
                    continue;
                }
                if (ItemRegistry.IsQualifiedItemId(value) && ItemRegistry.Create(value) is StardewValley.Object obj)
                {
                    __instance.Objects.Add(value2, obj);
                }
                else if (
                    ItemRegistry.Create<Furniture>(string.Concat("(F)", value), allowNull: true) is Furniture furniture
                )
                {
                    furniture.InitializeAtTile(value2);
                    furniture.IsOn = true;
                    for (int j = 0; j < value3; j++)
                    {
                        furniture.rotate();
                    }
                    Furniture furnitureAt = __instance.GetFurnitureAt(value2);
                    if (furnitureAt != null)
                    {
                        furnitureAt.heldObject.Value = furniture;
                    }
                    else
                    {
                        __instance.furniture.Add(furniture);
                    }
                }
            }
        }

        if (
            bedCheck
            && !__instance.furniture.Any(
                (furni) => furni is BedFurniture bedFurniture && bedFurniture.bedType == BedFurniture.BedType.Single
            )
        )
        {
            ModEntry.Log("Add default bed to 9 8", LogLevel.Warn);
            __instance.furniture.Add(new BedFurniture(BedFurniture.DEFAULT_BED_INDEX, new Vector2(9f, 8f)));
        }
    }
}

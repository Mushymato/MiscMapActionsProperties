using HarmonyLib;
using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;

namespace MiscMapActionsProperties.Framework.Location;

/// <summary>
/// Add new map property mushymato.MMAP_FarmHouseFurniture <furniture>
/// Add farmhouse furniture
/// A bed is added only if the list doesn't already provide bed
/// </summary>
internal static class FarmHouseFurniture
{
    internal const string MapProp_FarmHouseFurnitureAdd = $"{ModEntry.ModId}_FarmHouseFurnitureAdd";
    internal const string MapProp_FarmHouseFurnitureRemove = $"{ModEntry.ModId}_FarmHouseFurnitureRemove";

    internal static void Register()
    {
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

    private static void FarmHouse_AddStarterFurniture_Postfix(FarmHouse __instance)
    {
        bool bedCheck = false;
        if (
            CommonPatch.TryGetCustomFieldsOrMapProperty(
                __instance,
                MapProp_FarmHouseFurnitureRemove,
                out string? propStr1
            )
        )
        {
            ModEntry.Log($"{MapProp_FarmHouseFurnitureRemove}:{propStr1}");
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
                            out var value2,
                            out var error,
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
        if (
            CommonPatch.TryGetCustomFieldsOrMapProperty(__instance, MapProp_FarmHouseFurnitureAdd, out string? propStr2)
        )
        {
            ModEntry.Log($"{MapProp_FarmHouseFurnitureAdd}:{propStr2}");
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
                        out var value,
                        out var error,
                        allowBlank: false,
                        name: "int index"
                    )
                    || !ArgUtility.TryGetVector2(
                        mapPropertySplitBySpaces,
                        i + 1,
                        out var value2,
                        out error,
                        integerOnly: false,
                        "Vector2 tile"
                    )
                    || !ArgUtility.TryGetInt(
                        mapPropertySplitBySpaces,
                        i + 3,
                        out var value3,
                        out error,
                        "int rotations"
                    )
                )
                {
                    __instance.LogMapPropertyError(MapProp_FarmHouseFurnitureAdd, mapPropertySplitBySpaces, error);
                    continue;
                }
                if (ItemRegistry.Create<Furniture>(string.Concat("(F)", value), allowNull: true) is Furniture furniture)
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

using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Mushymato.ExtendedTAS;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Extensions;
using xTile;

namespace MiscMapActionsProperties.Framework.Entities;

/// <summary>
/// Let you hook interior doors to a MMAP TAS
/// </summary>
internal static class InteriorDoorExt
{
    internal const string TileProp_HasDoors = $"{ModEntry.ModId}_HasDoors";
    internal const string TileProp_Door = $"{ModEntry.ModId}_Door";
    internal const string TileProp_LinkedDoors = $"{ModEntry.ModId}_LinkedDoors";

    internal record InteriorDoorCtx(
        string DoorTAS,
        string DoorSound,
        IReadOnlyList<Point> AdditionalCollision,
        IReadOnlyList<Point> LinkedDoors
    )
    {
        internal static InteriorDoorCtx? Make(InteriorDoor door)
        {
            if (door.Tile == null)
                return null;
            if (!door.Tile.Properties.TryGetValue(TileProp_Door, out string doorExt))
                return null;
            string[] doorArgs = ArgUtility.SplitBySpaceQuoteAware(doorExt);
            if (
                !ArgUtility.TryGet(
                    doorArgs,
                    0,
                    out string doorTAS,
                    out string? error,
                    allowBlank: false,
                    name: "string doorTAS"
                )
                || !ArgUtility.TryGetOptional(
                    doorArgs,
                    1,
                    out string? doorFootprintStr,
                    out error,
                    defaultValue: null,
                    allowBlank: false,
                    name: "string doorFootprint"
                )
                || !ArgUtility.TryGetOptional(
                    doorArgs,
                    2,
                    out string? doorSound,
                    out error,
                    defaultValue: "doorOpen",
                    allowBlank: false,
                    name: "string doorSound"
                )
            )
            {
                ModEntry.Log(error, LogLevel.Error);
                return null;
            }

            List<Point> additionalDoorTiles = [];
            List<Point> linkedDoorTiles = [];

            if (doorFootprintStr != null)
            {
                Point? doorFootprintPos = null;
                List<Point> doorFootprintRel = [];

                string[] array = doorFootprintStr.Trim().Split('\n');
                for (int y = 0; y < array.Length; y++)
                {
                    string text = array[y].Trim();
                    for (int x = 0; x < text.Length; x++)
                    {
                        if (text[x] == 'D')
                        {
                            doorFootprintPos = new(x, y);
                        }
                        else if (text[x] == 'X')
                        {
                            doorFootprintRel.Add(new(x, y));
                        }
                    }
                }
                if (doorFootprintPos == null)
                {
                    ModEntry.Log(
                        $"Did not find door 'D' in door footprint '{doorFootprintStr}' for door at {door.Position}"
                    );
                    return null;
                }
                Point originPos = door.Position - doorFootprintPos.Value;
                xTile.Layers.Layer backlayer = door.Location.Map.RequireLayer("Back");
                foreach (Point relPnt in doorFootprintRel)
                {
                    Point pnt = new(originPos.X + relPnt.X, originPos.Y + relPnt.Y);
                    if (backlayer.Tiles[pnt.X, pnt.Y] != null)
                        additionalDoorTiles.Add(pnt);
                }
            }

            if (door.Tile.Properties.TryGetValue(TileProp_LinkedDoors, out string? linkedDoorsString))
            {
                string[] linkedDoors = ArgUtility.SplitBySpaceQuoteAware(linkedDoorsString);
                for (int i = 0; i < linkedDoors.Length; i += 2)
                {
                    if (ArgUtility.TryGetPoint(linkedDoors, i, out Point pnt, out _))
                    {
                        linkedDoorTiles.Add(pnt);
                    }
                }
            }

            return new InteriorDoorCtx(doorTAS, doorSound, additionalDoorTiles, linkedDoorTiles);
        }

        internal void ResetLocalState(InteriorDoor door)
        {
            if (DoorTAS == "T" || !ModEntry.TAS.TryGetTASExt(DoorTAS, out TASExt? def))
                return;
            TemporaryAnimatedSprite tas = new TASContext(def)
            {
                Pos = door.Sprite.Position,
                OverrideDrawLayer = def.LayerDepth ?? door.Sprite.layerDepth,
            }.Create();
            tas.holdLastFrame = true;
            tas.paused = true;
            if (door.Value)
            {
                tas.paused = false;
                tas.resetEnd();
            }
            door.Sprite = tas;

            CloseDoorTiles(door);
        }

        internal void OpenDoorTiles(InteriorDoor door)
        {
            GameLocation location = door.Location;
            foreach (Point pnt in AdditionalCollision)
            {
                location.removeTileProperty(pnt.X, pnt.Y, "Back", "Passable");
                location.setTileProperty(pnt.X, pnt.Y, "Back", "TemporaryBarrier", "T");
            }
            DelayedAction.functionAfterDelay(
                delegate
                {
                    foreach (Point pnt in AdditionalCollision)
                    {
                        location.removeTileProperty(pnt.X, pnt.Y, "Back", "TemporaryBarrier");
                    }
                },
                400
            );
        }

        internal void CloseDoorTiles(InteriorDoor door)
        {
            GameLocation location = door.Location;
            Point pos0 = door.Position;
            // yeet the original door front stuff (but keep building bc we need that)
            location.removeTile(pos0.X, pos0.Y - 1, "Front");
            location.removeTile(pos0.X, pos0.Y - 2, "Front");
            // custom door points
            foreach (Point pnt in AdditionalCollision)
            {
                location.removeTileProperty(pnt.X, pnt.Y, "Back", "TemporaryBarrier");
                location.setTileProperty(pnt.X, pnt.Y, "Back", "Passable", "F");
            }
        }
    }

    internal static ConditionalWeakTable<InteriorDoor, InteriorDoorCtx?> interiorDoorCtxCache = [];
    internal static ConditionalWeakTable<xTile.Map, IReadOnlyList<Point>> interiorDoorPointCache = [];

    internal static void Register()
    {
        ModEntry.help.Events.GameLoop.ReturnedToTitle += static (sender, e) =>
        {
            interiorDoorCtxCache.Clear();
            interiorDoorPointCache.Clear();
        };
        try
        {
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(InteriorDoor), nameof(InteriorDoor.ResetLocalState)),
                postfix: new HarmonyMethod(typeof(InteriorDoorExt), nameof(InteriorDoor_ResetLocalState_Postfix))
            );
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(InteriorDoor), "openDoorTiles"),
                postfix: new HarmonyMethod(typeof(InteriorDoorExt), nameof(InteriorDoor_openDoorTiles_Postfix))
            );
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(InteriorDoor), "closeDoorTiles"),
                postfix: new HarmonyMethod(typeof(InteriorDoorExt), nameof(InteriorDoor_closeDoorTiles_Postfix))
            );

            ModEntry.harm.Patch(
                original: AccessTools.Method(
                    typeof(InteriorDoorDictionary),
                    nameof(InteriorDoorDictionary.ResetSharedState)
                ),
                postfix: new HarmonyMethod(
                    typeof(InteriorDoorExt),
                    nameof(InteriorDoorDictionary_ResetSharedState_Postfix)
                )
            );
            ModEntry.harm.Patch(
                original: AccessTools.Method(
                    typeof(InteriorDoorDictionary),
                    nameof(InteriorDoorDictionary.ResetLocalState)
                ),
                postfix: new HarmonyMethod(
                    typeof(InteriorDoorExt),
                    nameof(InteriorDoorDictionary_ResetLocalState_Postfix)
                )
            );

            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.openDoor)),
                postfix: new HarmonyMethod(typeof(InteriorDoorExt), nameof(GameLocation_openDoor_Prefix))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch InteriorDoorExt Props:\n{err}", LogLevel.Error);
        }
    }

    private static void InteriorDoor_ResetLocalState_Postfix(InteriorDoor __instance)
    {
        if (__instance.Tile == null)
            return;
        interiorDoorCtxCache.GetValue(__instance, InteriorDoorCtx.Make)?.ResetLocalState(__instance);
    }

    private static void InteriorDoor_openDoorTiles_Postfix(InteriorDoor __instance)
    {
        interiorDoorCtxCache.GetValue(__instance, InteriorDoorCtx.Make)?.OpenDoorTiles(__instance);
    }

    private static void InteriorDoor_closeDoorTiles_Postfix(InteriorDoor __instance)
    {
        interiorDoorCtxCache.GetValue(__instance, InteriorDoorCtx.Make)?.CloseDoorTiles(__instance);
    }

    private static IReadOnlyList<Point> GetDoorPoints(Map map)
    {
        xTile.Layers.Layer layer = map.RequireLayer("Buildings");
        List<Point> doorPoints = [];
        for (int x = 0; x < layer.LayerWidth; x++)
        {
            for (int y = 0; y < layer.LayerHeight; y++)
            {
                Point pnt = new(x, y);
                if (layer.Tiles[x, y] is not MapTile tile || !tile.Properties.ContainsKey(TileProp_Door))
                    continue;
                doorPoints.Add(pnt);
            }
        }
        ModEntry.Log($"Doors: {string.Join(',', doorPoints)}");
        return doorPoints;
    }

    private static void InteriorDoorDictionary_ResetSharedState_Postfix(
        InteriorDoorDictionary __instance,
        GameLocation ___location
    )
    {
        if (!___location.HasMapPropertyWithValue(TileProp_HasDoors))
        {
            return;
        }
        foreach (Point pnt in interiorDoorPointCache.GetValue(___location.Map, GetDoorPoints))
        {
            __instance[pnt] = false;
        }
    }

    private static void InteriorDoorDictionary_ResetLocalState_Postfix(
        InteriorDoorDictionary __instance,
        GameLocation ___location
    )
    {
        if (!___location.HasMapPropertyWithValue(TileProp_HasDoors))
        {
            return;
        }
        HashSet<Point> vanillaDoors = InteriorDoorDictionary.GetDoorTilesFromMapProperty(___location).ToHashSet();
        foreach (Point pnt in interiorDoorPointCache.GetValue(___location.Map, GetDoorPoints))
        {
            if (!vanillaDoors.Contains(pnt) && __instance.ContainsKey(pnt))
            {
                InteriorDoor interiorDoor = __instance.FieldDict[pnt];
                interiorDoor.Location = ___location;
                interiorDoor.Position = pnt;
                interiorDoor.ResetLocalState();
            }
        }
    }

    private static void GameLocation_openDoor_Prefix(
        GameLocation __instance,
        xTile.Dimensions.Location tileLocation,
        ref bool playSound
    )
    {
        Point key = new(tileLocation.X, tileLocation.Y);
        if (!__instance.interiorDoors.ContainsKey(key))
            return;
        if (
            interiorDoorCtxCache.GetValue(__instance.interiorDoors.FieldDict[key], InteriorDoorCtx.Make)
            is not InteriorDoorCtx ctx
        )
            return;
        if (playSound)
        {
            __instance.playSound(ctx.DoorSound, key.ToVector2());
            playSound = false;
        }
        foreach (Point linked in ctx.LinkedDoors)
        {
            if (!__instance.interiorDoors.ContainsKey(key) || __instance.interiorDoors[linked])
            {
                continue;
            }
            __instance.interiorDoors[linked] = true;
        }
    }
}

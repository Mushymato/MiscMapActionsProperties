using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;

namespace MiscMapActionsProperties.Framework.Location;

/// <summary>
/// Add two new map properties
/// - mushymato.MMAP_FridgePosition x y
/// - mushymato.MMAP_FridgeDoorSprite Texture
/// Changes where the farmhouse fridge is, independent of what map tile is used.
/// Also allows changing the fridge door's sprite, or hiding it entirely
/// Only works in farmhouse/cabins
/// </summary>
internal static class FridgePosition
{
    internal record FridgeDoorSprite(Texture2D? Texture, Vector2 Offset);

    internal const string MapProp_FridgePosition = $"{ModEntry.ModId}_FridgePosition";
    internal const string MapProp_FridgeDoorSprite = $"{ModEntry.ModId}_FridgeDoorSprite";
    internal static readonly Vector2 ChildOffset = new Vector2(1f, 2f) * Game1.tileSize;
    internal static PerScreenCache<FridgeDoorSprite?> DoorSprite = PerScreenCache.Make<FridgeDoorSprite?>();

    internal static void Register()
    {
        try
        {
            CommonPatch.GameLocation_resetLocalState += GameLocation_resetLocalState_Postfix;
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(FarmHouse), nameof(FarmHouse.GetFridgePositionFromMap)),
                prefix: new HarmonyMethod(typeof(FridgePosition), nameof(FarmHouse_GetFridgePositionFromMap_Prefix))
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(FarmHouse), nameof(FarmHouse.checkAction)),
                prefix: new HarmonyMethod(typeof(FridgePosition), nameof(FarmHouse_checkAction_Prefix))
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(FarmHouse), nameof(FarmHouse.drawAboveFrontLayer)),
                transpiler: new HarmonyMethod(typeof(FridgePosition), nameof(FarmHouse_drawAboveFrontLayer_Transpiler))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch FridgePosition:\n{err}", LogLevel.Error);
        }
    }

    private static void GameLocation_resetLocalState_Postfix(object? sender, GameLocation location)
    {
        if (location is not FarmHouse farmHouse)
            return;
        if (CommonPatch.TryGetLocationalProperty(farmHouse, MapProp_FridgeDoorSprite, out string? fridgeDoorProp))
        {
            if (fridgeDoorProp == "F")
            {
                DoorSprite.Value = new(null, Vector2.Zero);
            }
            else
            {
                string[] args = ArgUtility.SplitBySpaceQuoteAware(fridgeDoorProp);
                if (
                    !ArgUtility.TryGet(args, 0, out string fridgeDoorTx, out string _, name: "fridgeDoorTx")
                    || !Game1.content.DoesAssetExist<Texture2D>(fridgeDoorTx)
                )
                {
                    return;
                }
                ArgUtility.TryGetVector2(
                    args,
                    1,
                    out Vector2 offset,
                    out string _,
                    integerOnly: false,
                    name: "Vector2 offset"
                );
                DoorSprite.Value = new(Game1.content.Load<Texture2D>(fridgeDoorTx), offset);
            }
        }
        else
        {
            DoorSprite.Value = null;
        }
        farmHouse.fridgePosition = GetOverrideFridgePosition(farmHouse) ?? farmHouse.fridgePosition;
    }

    private static void FridgePosition_Draw(
        SpriteBatch b,
        Texture2D texture,
        Vector2 position,
        Rectangle? sourceRectangle,
        Color color,
        float rotation,
        Vector2 origin,
        float scale,
        SpriteEffects effects,
        float layerDepth
    )
    {
        Vector2 offset = Vector2.Zero;
        if (DoorSprite.Value != null)
        {
            if (DoorSprite.Value.Texture == null)
                return;
            texture = DoorSprite.Value.Texture;
            offset = DoorSprite.Value.Offset;
            sourceRectangle = texture.Bounds;
        }
        b.Draw(texture, position + offset, sourceRectangle, color, rotation, origin, scale, effects, layerDepth);
    }

    private static IEnumerable<CodeInstruction> FarmHouse_drawAboveFrontLayer_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        try
        {
            CodeMatcher matcher = new(instructions, generator);

            matcher.MatchStartForward(
                [
                    new(
                        OpCodes.Callvirt,
                        AccessTools.DeclaredMethod(
                            typeof(StardewValley.Network.NetMutex),
                            nameof(StardewValley.Network.NetMutex.IsLocked)
                        )
                    ),
                ]
            );
            if (!matcher.IsValid)
                return instructions;
            matcher.MatchStartForward(
                [
                    new(
                        OpCodes.Callvirt,
                        AccessTools.Method(
                            typeof(SpriteBatch),
                            nameof(SpriteBatch.Draw),
                            [
                                typeof(Texture2D),
                                typeof(Vector2),
                                typeof(Rectangle?),
                                typeof(Color),
                                typeof(float),
                                typeof(Vector2),
                                typeof(float),
                                typeof(SpriteEffects),
                                typeof(float),
                            ]
                        )
                    ),
                ]
            );
            matcher.Opcode = OpCodes.Call;
            matcher.Operand = AccessTools.DeclaredMethod(typeof(FridgePosition), nameof(FridgePosition_Draw));

            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in FarmHouse_drawAboveFrontLayer_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
        }
    }

    private static Point? GetOverrideFridgePosition(FarmHouse __instance)
    {
        if (CommonPatch.TryGetLocationalPropertyVector2(__instance, MapProp_FridgePosition, out Vector2 position))
        {
            return position.ToPoint();
        }
        return null;
    }

    private static bool FarmHouse_checkAction_Prefix(
        FarmHouse __instance,
        xTile.Dimensions.Location tileLocation,
        Farmer who,
        ref bool __result
    )
    {
        if (tileLocation.X == __instance.fridgePosition.X && tileLocation.Y == __instance.fridgePosition.Y)
        {
            __instance.fridge.Value.fridge.Value = true;
            __instance.fridge.Value.checkForAction(who);
            __result = true;
            return false;
        }
        return true;
    }

    private static bool FarmHouse_GetFridgePositionFromMap_Prefix(FarmHouse __instance, ref Point? __result)
    {
        if (CommonPatch.TryGetLocationalPropertyVector2(__instance, MapProp_FridgePosition, out Vector2 position))
        {
            __result = position.ToPoint();
            return false;
        }
        return true;
    }
}

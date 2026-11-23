using HarmonyLib;
using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using Mushymato.ExtendedTAS;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.GameData.Objects;

namespace MiscMapActionsProperties.Framework.Entities;

internal static class CustomBomb
{
    private const string Action_Bomb = $"{ModEntry.ModId}_Bomb";
    private const string Action_BombAtTile = $"{ModEntry.ModId}_BombAtTile";
    private const string CustomField_Bomb = $"{ModEntry.ModId}/Bomb";

    internal static void Register()
    {
        ModEntry.harm.Patch(
            original: AccessTools.DeclaredMethod(typeof(SObject), nameof(SObject.placementAction)),
            prefix: new HarmonyMethod(typeof(CustomBomb), nameof(SObject_placementAction_Prefix))
        );
        CommonPatch.RegisterTileAndTouch(Action_Bomb, TileActionBomb);
        CommonPatch.RegisterTileAndTouch(Action_BombAtTile, TileActionBombAtTile);
    }

    private static bool TileActionBomb(GameLocation location, string[] bombFieldParts, Farmer who, Point point)
    {
        return DoPlaceBomb(location, bombFieldParts, 1, who, point.X, point.Y);
    }

    private static bool TileActionBombAtTile(GameLocation location, string[] bombFieldParts, Farmer who, Point point)
    {
        if (
            !ArgUtility.TryGetPoint(bombFieldParts, 1, out Point atTile, out string error)
            || !ArgUtility.TryGetOptionalBool(
                bombFieldParts,
                3,
                out bool relative,
                out error,
                defaultValue: false,
                name: "bool relative"
            )
        )
        {
            ModEntry.Log(error, LogLevel.Error);
            return false;
        }
        if (relative)
        {
            atTile = point + atTile;
        }
        return DoPlaceBomb(location, bombFieldParts, 4, who, atTile.X, atTile.Y);
    }

    private static bool SObject_placementAction_Prefix(
        SObject __instance,
        GameLocation location,
        int x,
        int y,
        Farmer? who
    )
    {
        if (
            __instance.ItemId == null
            || __instance.TypeDefinitionId != "(O)"
            || !Game1.objectData.TryGetValue(__instance.ItemId, out ObjectData? data)
            || !(data.CustomFields?.TryGetValue(CustomField_Bomb, out string? bombField) ?? false)
            || string.IsNullOrEmpty(bombField)
        )
        {
            return true;
        }

        string[] bombFieldParts = ArgUtility.SplitBySpaceQuoteAware(bombField);

        return !DoPlaceBomb(location, bombFieldParts, 0, who, x / 64, y / 64);
    }

    private static bool DoPlaceBomb(
        GameLocation location,
        string[] bombFieldParts,
        int firstIdx,
        Farmer? who,
        int x,
        int y
    )
    {
        Vector2 tile = new(x, y);
        Vector2 pos = new(tile.X * Game1.tileSize, tile.Y * Game1.tileSize);
        foreach (TemporaryAnimatedSprite temporarySprite in location.temporarySprites)
        {
            if (temporarySprite.position.Equals(pos))
            {
                return false;
            }
        }

        if (
            !ArgUtility.TryGet(bombFieldParts, firstIdx, out string tasId, out string error)
            || !ArgUtility.TryGet(bombFieldParts, firstIdx + 1, out string explodeMap, out error)
            || !ArgUtility.TryGetOptionalInt(
                bombFieldParts,
                firstIdx + 2,
                out int bombDamage,
                out error,
                defaultValue: -1
            )
        )
        {
            ModEntry.Log(error, LogLevel.Error);
            return false;
        }

        if (!ModEntry.TAS.TryGetTASExt(tasId, out TASExt? def))
        {
            return false;
        }

        TASContext ctx = new(def) { Pos = pos };
        ctx.TryCreate(
            new GameStateQueryContext(),
            (tas) =>
            {
                tas.Parent = location;
                tas.owner = who;
                if (bombDamage > 0)
                    tas.bombDamage = bombDamage;
                if (int.TryParse(explodeMap, out int radius))
                {
                    tas.bombRadius = radius;
                }
                else if (ParseExplodeMap(tile, explodeMap) is IList<Vector2> parsedExplodeMap)
                {
                    if (tas.endFunction == null)
                        tas.endFunction = (_) => DoExplode(tas, parsedExplodeMap);
                    else
                        tas.endFunction = (TemporaryAnimatedSprite.endBehavior)
                            Delegate.Combine(
                                tas.endFunction,
                                (TemporaryAnimatedSprite.endBehavior)(_ => DoExplode(tas, parsedExplodeMap))
                            );
                }
                location.playSound("thudStep");
                Game1.Multiplayer.broadcastSprites(location, tas);
                location.netAudio.StartPlaying("fuse");
            }
        );
        return true;
    }

    private static IList<Vector2>? ParseExplodeMap(Vector2 tile, string explodeMap)
    {
        string[] array = explodeMap.Trim().Split('\n');

        Vector2? bombPos = null;
        List<Vector2> explodePos = [];
        for (int i = 0; i < array.Length; ++i)
        {
            string text = array[i].Trim();
            for (int j = 0; j < text.Length; ++j)
            {
                switch (text[j])
                {
                    case 'B':
                        bombPos = new(j, i);
                        explodePos.Add(new(j, i));
                        break;
                    case 'X':
                        explodePos.Add(new(j, i));
                        break;
                }
            }
        }

        if (!bombPos.HasValue)
            return null;

        for (int i = 0; i < explodePos.Count; ++i)
        {
            Vector2 pos = explodePos[i];
            pos.X += tile.X;
            pos.Y += tile.Y;
            pos.X -= bombPos.Value.X;
            pos.Y -= bombPos.Value.Y;
            explodePos[i] = pos;
        }

        return explodePos;
    }

    private static void DoExplode(TemporaryAnimatedSprite tas, IList<Vector2> explodeMap)
    {
        GameLocation parentLoc = tas.Parent;
        if (Game1.currentLocation == parentLoc)
        {
            Game1.flashAlpha = 1f;
        }
        if (Game1.IsMasterGame)
        {
            parentLoc.netAudio.StopPlaying("fuse");
            parentLoc.playSound("explosion");
            Farmer who = tas.owner;
            int bombDamage = tas.bombDamage;
            foreach (Vector2 vec2 in explodeMap)
            {
                parentLoc.explode(vec2, 0, who, damageFarmers: true, bombDamage);
            }
        }
    }
}

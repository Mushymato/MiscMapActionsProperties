using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Delegates;
using StardewValley.Extensions;
using StardewValley.Triggers;

namespace MiscMapActionsProperties.Framework.Tile;

internal enum SupportedCritter
{
    Firefly,
}

/// <summary>
/// Add new back layer tile property mushymato.MMAP_Critter <critter type> [type dependent args]+
/// Add critter at tile, supports
/// - Firefly: color count
/// </summary>
internal static class CritterSpot
{
    internal static readonly string TileProp_Critter = $"{ModEntry.ModId}_Critter";
    private static readonly FieldInfo fireflyLight = AccessTools.DeclaredField(typeof(Firefly), "light");

    internal static void Register()
    {
        CommonPatch.GameLocation_resetLocalState += GameLocation_resetLocalState_Postfix;
        CommonPatch.RegisterTileAndTouch(TileProp_Critter, TileAndTouchCritter);
        TriggerActionManager.RegisterAction(TileProp_Critter, TriggerActionCritter);
    }

    private static void GameLocation_resetLocalState_Postfix(object? sender, CommonPatch.ResetLocalStateArgs e)
    {
        var backLayer = e.Location.map.RequireLayer("Back");
        for (int x = 0; x < backLayer.LayerWidth; x++)
        {
            for (int y = 0; y < backLayer.LayerHeight; y++)
            {
                Vector2 pos = new(x, y);
                if (pos.Equals(Vector2.Zero))
                    continue;
                MapTile tile = backLayer.Tiles[x, y];
                if (tile == null)
                    continue;
                if (tile.Properties.TryGetValue(TileProp_Critter, out string critterProp))
                {
                    string[] critterArgs = ArgUtility.SplitBySpaceQuoteAware(critterProp);
                    SpawnCritter(e.Location, pos, critterArgs, 0, out string _);
                }
            }
        }
    }

    private static bool TriggerActionCritter(string[] args, TriggerActionContext context, out string error)
    {
        if (!ArgUtility.TryGetVector2(args, 1, out Vector2 position, out error, integerOnly: true, "Vector2 position"))
            return false;
        return SpawnCritter(Game1.currentLocation, position, args, 3, out error);
    }

    private static bool TileAndTouchCritter(GameLocation location, string[] args, Farmer farmer, Point source)
    {
        return SpawnCritter(location, source.ToVector2(), args, 1, out _);
    }

    private static bool SpawnCritter(
        GameLocation location,
        Vector2 position,
        string[] args,
        int firstIdx,
        out string error
    )
    {
        if (
            !ArgUtility.TryGet(
                args,
                firstIdx,
                out string critterKindStr,
                out error,
                allowBlank: false,
                name: "string critterKind"
            ) || !Enum.TryParse(critterKindStr, true, out SupportedCritter critterKind)
        )
        {
            return false;
        }
        location.instantiateCrittersList();
        return critterKind switch
        {
            SupportedCritter.Firefly => SpawnCritterFirefly(location, position, args, firstIdx + 1, out error),
            _ => false,
        };
    }

    private static bool SpawnCritterFirefly(
        GameLocation location,
        Vector2 position,
        string[] args,
        int firstIdx,
        out string error
    )
    {
        if (
            !ArgUtility.TryGetOptional(args, firstIdx, out string? color, out error, name: "string color")
            || !ArgUtility.TryGetOptionalInt(
                args,
                firstIdx + 1,
                out int count,
                out error,
                defaultValue: 1,
                name: "int count"
            )
        )
        {
            return false;
        }
        Color? c = null;
        if (color != null && color != "T" && (c = Utility.StringToColor(color)) != null)
        {
            c = new Color(((Color)c).PackedValue ^ 0x00FFFFFF);
        }
        for (int i = 0; i < count; i++)
        {
            Firefly firefly = new(position);
            firefly.position.X += Random.Shared.Next(Game1.tileSize);
            firefly.position.Y += Random.Shared.Next(Game1.tileSize);
            firefly.startingPosition = firefly.position;
            if (c != null && fireflyLight.GetValue(firefly) is LightSource light)
            {
                light.color.Value = (Color)c;
            }
            location.addCritter(firefly);
        }
        return true;
    }
}

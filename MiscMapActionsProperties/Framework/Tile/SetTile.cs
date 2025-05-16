using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Triggers;

namespace MiscMapActionsProperties.Framework.Tile;

/// <summary>
/// debug action mushymato.MMAP_SetTile 18 10 18 Back z_volcano_dungeon
/// Does not sync in multiplayer
/// </summary>
internal static class SetTile
{
    internal const string Action_SetTile = $"{ModEntry.ModId}_SetTile";

    internal static void Register()
    {
        TriggerActionManager.RegisterAction(Action_SetTile, TriggerAction_SetTile);
    }

    private static bool TriggerAction_SetTile(string[] args, TriggerActionContext context, out string error)
    {
        if (Game1.currentLocation?.map == null)
        {
            error = "No map loaded for current location.";
            return false;
        }

        // int tileX, int tileY, int index, string layer, string tileSheetId, string action = null
        if (
            !ArgUtility.TryGetPoint(args, 1, out Point pos, out error, "Point pos")
            || !ArgUtility.TryGetInt(args, 3, out int index, out error, "int index")
            || !ArgUtility.TryGet(args, 4, out string layer, out error, allowBlank: false, name: "string layer")
            || !ArgUtility.TryGet(
                args,
                5,
                out string tileSheetId,
                out error,
                allowBlank: false,
                name: "string tileSheetId"
            )
            || !ArgUtility.TryGetOptional(
                args,
                6,
                out string action,
                out error,
                defaultValue: null,
                allowBlank: false,
                name: "string action"
            )
        )
        {
            return false;
        }

        if (Game1.currentLocation.map.GetLayer(layer) == null)
        {
            error = $"No layer '{layer}' on current map";
            return false;
        }

        if (Game1.currentLocation.map.GetTileSheet(tileSheetId) == null)
        {
            error = $"No tilesheet with id '{tileSheetId}' on current map";
            return false;
        }

        Game1.currentLocation.setMapTile(pos.X, pos.Y, index, layer, tileSheetId, action, true);

        return true;
    }
}

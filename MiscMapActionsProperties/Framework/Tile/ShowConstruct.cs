using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Menus;
using StardewValley.Triggers;

namespace MiscMapActionsProperties.Framework.Tile;

/// <summary>
/// Add new tile actions mushymato.MMAP_ShowConstruct and mushymato.MMAP_ShowConstructForCurrent
/// Usage:
/// - mushymato.MMAP_ShowConstruct <builder> [restrict]
/// - mushymato.MMAP_ShowConstructForCurrent <builder> [restrict]
/// Shows contruct menu (robin, wizard, and any modded builders) when interacted with.
/// mushymato.MMAP_ShowConstruct shows a list of locations marked buildable to select, just like vanilla Robin/Wizard
/// mushymato.MMAP_ShowConstructForCurrent shows the menu for current location only, if the current location is made buildable with map properties
/// The optional third argument restrict building until currenct construction is over.
/// </summary>
internal static class ShowConstruct
{
    internal const string TileAction_ShowConstruct = $"{ModEntry.ModId}_ShowConstruct";
    internal const string TileAction_ShowConstructForCurrent = $"{ModEntry.ModId}_ShowConstructForCurrent";

    internal static void Register()
    {
        CommonPatch.RegisterTileAndTouch(
            TileAction_ShowConstruct,
            (location, args, farmer, tile) =>
                CheckArgsThenShowConstruct(args, (builder) => location.ShowConstructOptions(builder))
        );
        TriggerActionManager.RegisterAction(
            TileAction_ShowConstruct,
            (string[] args, TriggerActionContext ctx, out string err) =>
            {
                err = "";
                return CheckArgsThenShowConstruct(
                    args,
                    (builder) => Game1.currentLocation.ShowConstructOptions(builder)
                );
            }
        );
        CommonPatch.RegisterTileAndTouch(
            TileAction_ShowConstructForCurrent,
            (location, args, farmer, tile) =>
                CheckArgsThenShowConstruct(
                    args,
                    (builder) =>
                    {
                        if (location.IsBuildableLocation())
                            Game1.activeClickableMenu = new CarpenterMenu(builder, location);
                        else
                            Game1.drawObjectDialogue(Game1.content.LoadString("Strings/UI:Carpenter_CantBuild"));
                    }
                )
        );
        TriggerActionManager.RegisterAction(
            TileAction_ShowConstructForCurrent,
            (string[] args, TriggerActionContext ctx, out string err) =>
            {
                err = "";
                return CheckArgsThenShowConstruct(
                    args,
                    (builder) =>
                    {
                        if (Game1.currentLocation.IsBuildableLocation())
                            Game1.activeClickableMenu = new CarpenterMenu(builder, Game1.currentLocation);
                        else
                            Game1.drawObjectDialogue(Game1.content.LoadString("Strings/UI:Carpenter_CantBuild"));
                    }
                );
            }
        );
    }

    private static bool CheckArgsThenShowConstruct(string[] args, Action<string> showMenu)
    {
        if (
            !ArgUtility.TryGet(args, 1, out string builder, out string error, allowBlank: true, name: "string builder")
            || !ArgUtility.TryGetOptionalBool(
                args,
                2,
                out bool restrict,
                out error,
                defaultValue: false,
                "bool restrict"
            )
        )
        {
            ModEntry.Log(error, LogLevel.Error);
            return false;
        }
        if (restrict && Game1.IsThereABuildingUnderConstruction(builder))
        {
            Game1.drawObjectDialogue(Game1.content.LoadString("Strings/UI:NPC_Busy", builder));
        }
        else
        {
            try
            {
                showMenu(builder);
            }
            catch (DivideByZeroException)
            {
                ModEntry.Log($"Failed to open construct menu, invalid builder {builder}", LogLevel.Error);
                return false;
            }
        }
        return true;
    }
}

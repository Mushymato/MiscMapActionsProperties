using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Extensions;
using StardewValley.Triggers;

namespace MiscMapActionsProperties.Framework.Tile;

internal static class PlaySound
{
    private const string Action_PlaySound = $"{ModEntry.ModId}_PlaySound";

    internal static void Register()
    {
        CommonPatch.RegisterTileAndTouch(Action_PlaySound, DoPlaySound);
        TriggerActionManager.RegisterAction(Action_PlaySound, TriggerPlaySound);
    }

    private static int? GetPitchOrNull(string? pitches)
    {
        if (string.IsNullOrEmpty(pitches))
            return null;
        List<int> pitchList = [];
        foreach (string part in pitches.Split('|'))
        {
            if (int.TryParse(part, out int pitchValue))
                pitchList.Add(pitchValue);
        }
        if (pitchList.Count == 0)
            return null;
        return Random.Shared.ChooseFrom(pitchList);
    }

    private static bool DoPlaySound(GameLocation location, string[] args, Farmer farmer, Point point)
    {
        if (
            !ArgUtility.TryGet(args, 1, out string? audioName, out string? error, allowBlank: false, "string audioName")
            || !ArgUtility.TryGetOptional(
                args,
                2,
                out string? pitches,
                out error,
                defaultValue: null,
                name: "string pitches"
            )
            || !ArgUtility.TryGetOptionalBool(
                args,
                3,
                out bool isGlobal,
                out error,
                defaultValue: false,
                "bool isGlobal"
            )
        )
        {
            ModEntry.Log(error, LogLevel.Error);
            return false;
        }
        int? pitch = GetPitchOrNull(pitches);
        Vector2 tile = point.ToVector2();
        if (isGlobal)
            location.playSound(audioName, tile, pitch);
        else
            location.localSound(audioName, tile, pitch);
        return true;
    }

    private static bool TriggerPlaySound(string[] args, TriggerActionContext context, out string error)
    {
        if (
            !ArgUtility.TryGet(args, 1, out string? audioName, out error, allowBlank: false, "string audioName")
            || !ArgUtility.TryGetOptional(
                args,
                2,
                out string? pitches,
                out error,
                defaultValue: null,
                name: "string pitches"
            )
            || !ArgUtility.TryGetOptionalBool(
                args,
                3,
                out bool isGlobal,
                out error,
                defaultValue: false,
                "bool isGlobal"
            )
        )
        {
            return false;
        }
        int? pitch = GetPitchOrNull(pitches);
        GameLocation location = Game1.currentLocation;
        if (isGlobal)
            location.playSound(audioName, null, pitch);
        else
            location.localSound(audioName, null, pitch);
        return true;
    }
}

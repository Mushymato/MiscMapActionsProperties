using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.TokenizableStrings;
using StardewValley.Triggers;

namespace MiscMapActionsProperties.Framework.Tile;

/// <summary>
/// Add new tile action mushymato.MMAP_QuestionDialogue <id>
/// Opens a dialog on interaction, which can be set to trigger actions/tileactions
/// </summary>
internal static class QuestionDialogue
{
    internal static readonly string TileAction_QuestionDialogue = $"{ModEntry.ModId}_QuestionDialogue";
    internal static readonly string Asset_QuestionDialogue = $"{ModEntry.ModId}/QuestionDialogue";

    internal static void Register()
    {
        ModEntry.help.Events.Content.AssetRequested += OnAssetRequested;
        ModEntry.help.Events.Content.AssetsInvalidated += OnAssetInvalidated;
        GameLocation.RegisterTileAction(TileAction_QuestionDialogue, ShowQuestionDialogueTile);
        GameLocation.RegisterTouchAction(TileAction_QuestionDialogue, ShowQuestionDialogueTouch);
        TriggerActionManager.RegisterAction(TileAction_QuestionDialogue, ShowQuestionDialogueAction);
    }

    private static Dictionary<string, QuestionDialogueData>? _qdData = null;

    /// <summary>Question dialogue data</summary>
    internal static Dictionary<string, QuestionDialogueData> QDData
    {
        get
        {
            _qdData ??= Game1.content.Load<Dictionary<string, QuestionDialogueData>>(Asset_QuestionDialogue);
            return _qdData;
        }
    }

    private static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.Name.IsEquivalentTo(Asset_QuestionDialogue))
            e.LoadFrom(() => new Dictionary<string, QuestionDialogueData>(), AssetLoadPriority.Low);
    }

    private static void OnAssetInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        if (e.NamesWithoutLocale.Any(an => an.IsEquivalentTo(Asset_QuestionDialogue)))
            _qdData = null;
    }

    private static bool ShowQuestionDialogueAction(string[] args, TriggerActionContext context, out string? error)
    {
        error = null;
        return ShowQuestionDialogue(
            Game1.currentLocation,
            args,
            Game1.player,
            Game1.player.TilePoint,
            Game1.player.Tile
        );
    }

    private static bool ShowQuestionDialogueTile(
        GameLocation location,
        string[] args,
        Farmer farmer,
        Point tilePosition
    )
    {
        return ShowQuestionDialogue(location, args, farmer, tilePosition, farmer.Tile);
    }

    private static void ShowQuestionDialogueTouch(
        GameLocation location,
        string[] args,
        Farmer farmer,
        Vector2 playerStandingPosition
    )
    {
        ShowQuestionDialogue(location, args, farmer, farmer.TilePoint, playerStandingPosition);
    }

    private static bool ShowQuestionDialogue(
        GameLocation location,
        string[] args,
        Farmer farmer,
        Point tilePosition,
        Vector2 playerStandingPosition
    )
    {
        if (!ArgUtility.TryGet(args, 1, out string qdId, out string error, allowBlank: false, "string qdId"))
        {
            ModEntry.Log(error, LogLevel.Error);
            return false;
        }
        if (!QDData.TryGetValue(qdId, out QuestionDialogueData? qdData))
        {
            ModEntry.Log($"No entry '{qdId}' in asset {Asset_QuestionDialogue}", LogLevel.Error);
            return false;
        }
        if (!GameStateQuery.CheckConditions(qdData.Condition, location, farmer))
        {
            return false;
        }

        GameStateQueryContext context = new(location, farmer, null, null, null, null, null);

        IDictionary<string, QuestionDialogueEntry> validEntries = qdData.ValidEntries(context);
        if (validEntries.Count == 0)
            return false;
        if (validEntries.Count == 1)
        {
            QuestionDialogueEntry qde = validEntries.Values.First();
            if (qde.Actions == null && qde.TileActions == null && qde.TouchActions == null)
                return false;
        }
        location.createQuestionDialogue(
            TokenParser.ParseText(qdData.Question) ?? "",
            validEntries.Select(MakeResponse).ToArray(),
            (Farmer who, string whichAnswer) =>
                AfterQuestionBehavior(location, tilePosition, playerStandingPosition, validEntries, who, whichAnswer),
            speaker: Game1.getCharacterFromName(qdData.Speaker)
        );

        return true;
    }

    public static Response MakeResponse(KeyValuePair<string, QuestionDialogueEntry> qde) =>
        new(qde.Key, TokenParser.ParseText(qde.Value.Label));

    /// <summary>functools.partial my dead girlfriend...</summary>
    internal static void AfterQuestionBehavior(
        GameLocation location,
        Point point,
        Vector2 standing,
        IDictionary<string, QuestionDialogueEntry> validEntries,
        Farmer who,
        string whichAnswer
    )
    {
        if (validEntries.TryGetValue(whichAnswer, out QuestionDialogueEntry? qde))
        {
            if (qde.Actions != null)
            {
                // Perform all (trigger) actions
                foreach (string action in qde.Actions)
                {
                    if (!TriggerActionManager.TryRunAction(action, out string error, out Exception _))
                    {
                        ModEntry.Log(error, LogLevel.Error);
                    }
                }
            }
            if (qde.TileActions != null)
            {
                // Return after first successful tile action
                xTile.Dimensions.Location loc = new(point.X, point.Y);
                foreach (string action in qde.TileActions)
                {
                    if (location.performAction(action, who, loc))
                        return;
                }
            }
            if (qde.TouchActions != null)
            {
                // Perform all touch tile actions
                foreach (string action in qde.TouchActions)
                {
                    location.performTouchAction(action, standing);
                }
            }
        }
    }
}

public class QuestionDialogueEntry
{
    /// <summary>Response label</summary>
    public string Label { get; set; } = "[LocalizedText Strings/UI:Cancel]";

    /// <summary>Response GSQ condition</summary>
    public string? Condition { get; set; } = null;

    /// <summary>List of (trigger) actions</summary>
    public List<string>? Actions { get; set; } = null;

    /// <summary>List of tile actions</summary>
    public List<string>? TileActions { get; set; } = null;

    /// <summary>List of touch actions</summary>
    public List<string>? TouchActions { get; set; } = null;
}

public class QuestionDialogueData
{
    /// <summary>Response GSQ condition</summary>
    public string? Condition { get; set; } = null;

    /// <summary>Question string</summary>
    public string? Question { get; set; } = null;

    /// <summary>Speaking NPC (unclear if this does anything)</summary>
    public string? Speaker { get; set; } = null;

    /// <summary>List of responses</summary>
    public Dictionary<string, QuestionDialogueEntry> ResponseEntries { get; set; } = [];

    /// <summary>Get all valid entries per GSQ</summary>
    /// <param name="context"></param>
    /// <returns></returns>
    internal IDictionary<string, QuestionDialogueEntry> ValidEntries(GameStateQueryContext context)
    {
        return ResponseEntries
            .Where((qde) => GameStateQuery.CheckConditions(qde.Value.Condition, context))
            .ToDictionary(qde => qde.Key, qde => qde.Value);
    }
}

using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.TokenizableStrings;
using StardewValley.Triggers;

namespace MiscMapActionsProperties.Framework.Tile;

/// <summary>
/// Add new tile action that gives a dialog box to further actions.
/// </summary>
internal static class QuestionDialogue
{
    internal static readonly string TileAction_QuestionDialogue = $"{ModEntry.ModId}_QuestionDialogue";
    internal static readonly string Asset_QuestionDialogue = $"{ModEntry.ModId}/QuestionDialogue";

    internal static void Register(IModHelper helper)
    {
        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.Content.AssetsInvalidated += OnAssetInvalidated;
        GameLocation.RegisterTileAction(TileAction_QuestionDialogue, ShowQuestionDialogue);
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
            e.LoadFrom(() => new Dictionary<string, QuestionDialogueData>(), AssetLoadPriority.Exclusive);
    }

    private static void OnAssetInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        if (e.NamesWithoutLocale.Any(an => an.IsEquivalentTo(Asset_QuestionDialogue)))
            _qdData = null;
    }

    private static bool ShowQuestionDialogue(GameLocation location, string[] args, Farmer farmer, Point point)
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

        GameStateQueryContext context = new(location, farmer, null, null, null, null, null);

        IDictionary<string, QuestionDialogueEntry> validEntries = qdData.ValidEntries(context);
        location.createQuestionDialogue(
            TokenParser.ParseText(qdData.Question),
            validEntries.Select(MakeResponse).ToArray(),
            (Farmer who, string whichAnswer) => AfterQuestionBehavior(location, point, validEntries, who, whichAnswer),
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
        }
    }
}

public class QuestionDialogueEntry
{
    public string Label { get; set; } = "[LocalizedText Strings/UI:Cancel]";
    public string? Condition { get; set; } = null;
    public List<string>? Actions { get; set; } = null;
    public List<string>? TileActions { get; set; } = null;
}

public class QuestionDialogueData
{
    public string Question { get; set; } = "";
    public string? Speaker { get; set; } = null;
    public Dictionary<string, QuestionDialogueEntry> ResponseEntries { get; set; } = [];

    public IDictionary<string, QuestionDialogueEntry> ValidEntries(GameStateQueryContext context)
    {
        return ResponseEntries
            .Where((qde) => GameStateQuery.CheckConditions(qde.Value.Condition, context))
            .ToDictionary(qde => qde.Key, qde => qde.Value);
    }
}

using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MiscMapActionsProperties.Framework.Wheels;
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
    internal const string TileAction_QuestionDialogue = $"{ModEntry.ModId}_QuestionDialogue";
    internal const string Asset_QuestionDialogue = $"{ModEntry.ModId}/QuestionDialogue";
    internal static PerScreenCache<GameLocation.afterQuestionBehavior?> heldAfterQuestionBehavior =
        PerScreenCache.Make<GameLocation.afterQuestionBehavior?>();

    internal static void Register()
    {
        heldAfterQuestionBehavior.Value = null;
        ModEntry.help.Events.Content.AssetRequested += OnAssetRequested;
        ModEntry.help.Events.Content.AssetsInvalidated += OnAssetInvalidated;
        CommonPatch.RegisterTileAndTouch(TileAction_QuestionDialogue, ShowQuestionDialogue);
        TriggerActionManager.RegisterAction(TileAction_QuestionDialogue, ShowQuestionDialogueAction);
        try
        {
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.answerDialogue)),
                postfix: new HarmonyMethod(typeof(QuestionDialogue), nameof(GameLocation_answerDialogue_Postfix))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch FurnitureTileData:\n{err}", LogLevel.Error);
        }
    }

    private static void GameLocation_answerDialogue_Postfix(GameLocation __instance)
    {
        if (heldAfterQuestionBehavior.Value != null)
        {
            __instance.afterQuestion = heldAfterQuestionBehavior.Value;
            heldAfterQuestionBehavior.Value = null;
        }
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

    private static bool ShowQuestionDialogueAction(string[] args, TriggerActionContext context, out string? error)
    {
        error = null;
        return ShowQuestionDialogue(Game1.currentLocation, args, Game1.player, Game1.player.TilePoint);
    }

    private static bool ShowQuestionDialogue(GameLocation location, string[] args, Farmer farmer, Point tilePosition)
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
            ModEntry.Log($"ShowQuestionDialogue({qdId}): Disabled by condition");
            return false;
        }

        GameStateQueryContext context = new(location, farmer, null, null, null, null, null);

        IDictionary<string, QuestionDialogueEntry> validEntries = qdData.ValidEntries(
            context,
            args.Skip(2).ToHashSet()
        );
        if (validEntries.Count == 0)
        {
            ModEntry.Log($"ShowQuestionDialogue({qdId}): No valid entries found");
            return false;
        }

        string? speakerId = qdData.Speaker;
        string? speakerName = TokenParser.ParseText(qdData.SpeakerDisplayName ?? qdData.Speaker);
        Texture2D? speakerPortrait = null;
        if (
            !string.IsNullOrEmpty(qdData.SpeakerPortrait)
            && Game1.content.DoesAssetExist<Texture2D>(qdData.SpeakerPortrait)
        )
        {
            speakerPortrait = Game1.content.Load<Texture2D>(qdData.SpeakerPortrait);
        }
        if (qdData.SpeakerDisplayName == null || speakerPortrait == null)
        {
            NPC? speakerRef = speakerId != null ? Game1.getCharacterFromName(speakerId) : null;
            if (speakerRef != null)
            {
                if (qdData.SpeakerDisplayName == null)
                {
                    speakerName = speakerRef.displayName;
                }
                speakerPortrait ??= speakerRef.Portrait;
            }
        }

        NPC? speaker = null;
        if (speakerPortrait != null)
        {
            speaker = new(
                new AnimatedSprite("Characters\\Abigail", 0, 16, 16),
                Vector2.Zero,
                "",
                0,
                "",
                speakerPortrait,
                eventActor: false
            )
            {
                Name = speakerId ?? "???",
                displayName = speakerName,
            };
        }

        if (string.IsNullOrEmpty(qdData.DialogueBefore))
        {
            return MakeQuestion(location, farmer, tilePosition, qdId, qdData, validEntries, speaker);
        }
        else
        {
            if (speaker == null)
            {
                Game1.drawObjectDialogue(TokenParser.ParseText(qdData.DialogueBefore) ?? "");
            }
            else
            {
                Dialogue dialogueBefore = new(
                    speaker,
                    qdData.DialogueBefore,
                    TokenParser.ParseText(qdData.DialogueBefore) ?? ""
                );
                Game1.DrawDialogue(dialogueBefore);
            }
            Game1.afterDialogues = (Game1.afterFadeFunction)
                Delegate.Combine(
                    Game1.afterDialogues,
                    (Game1.afterFadeFunction)
                        delegate
                        {
                            MakeQuestion(location, farmer, tilePosition, qdId, qdData, validEntries, speaker);
                        }
                );

            return true;
        }
    }

    public static bool MakeQuestion(
        GameLocation location,
        Farmer farmer,
        Point tilePosition,
        string qdId,
        QuestionDialogueData qdData,
        IDictionary<string, QuestionDialogueEntry> validEntries,
        NPC? speaker
    )
    {
        if (validEntries.Count == 1 && qdData.ImmediatelyPerformSingleValidResponse)
        {
            KeyValuePair<string, QuestionDialogueEntry> qde = validEntries.First();
            ModEntry.Log($"ShowQuestionDialogue({qdId}): Got 1 valid entry '{qde.Key}' and will immediately perform");
            qde.Value.PerformQDActions(location, tilePosition, farmer);
            return true;
        }

        ModEntry.Log(
            $"ShowQuestionDialogue({qdId}): Got {validEntries.Count} valid entries '{string.Join(',', validEntries.Keys)}'"
        );

        void afterQBehavior(Farmer who, string whichAnswer) =>
            AfterQuestionBehavior(location, tilePosition, validEntries, who, whichAnswer);

        if (location.afterQuestion != null)
            heldAfterQuestionBehavior.Value = afterQBehavior;

        string question = TokenParser.ParseText(qdData.Question) ?? "";
        if (qdData.Pagination > 0)
        {
            bool addCancel = false;
            QuestionDialogueEntry finalResponse = validEntries.Values.Last();
            List<KeyValuePair<string, string>> keyValuePairs;
            if (
                finalResponse.Condition == null
                && finalResponse.Actions == null
                && finalResponse.TileActions == null
                && finalResponse.TouchActions == null
            )
            {
                addCancel = true;
                keyValuePairs = validEntries.SkipLast(1).Select(MakeResponseKeyValuePair).ToList();
            }
            else
            {
                keyValuePairs = validEntries.Select(MakeResponseKeyValuePair).ToList();
            }
            location.ShowPagedResponses(
                question,
                keyValuePairs,
                (response) => afterQBehavior(farmer, response),
                addCancel: addCancel,
                itemsPerPage: qdData.Pagination
            );
        }
        else
        {
            location.createQuestionDialogue(
                question,
                validEntries.Select(MakeResponse).ToArray(),
                afterQBehavior,
                speaker: speaker
            );
        }

        return true;
    }

    public static Response MakeResponse(KeyValuePair<string, QuestionDialogueEntry> qde) =>
        new(qde.Key, TokenParser.ParseText(qde.Value.Label));

    public static KeyValuePair<string, string> MakeResponseKeyValuePair(
        KeyValuePair<string, QuestionDialogueEntry> qde
    ) => new(qde.Key, TokenParser.ParseText(qde.Value.Label));

    /// <summary>functools.partial my dead girlfriend...</summary>
    internal static void AfterQuestionBehavior(
        GameLocation location,
        Point point,
        IDictionary<string, QuestionDialogueEntry> validEntries,
        Farmer who,
        string whichAnswer
    )
    {
        ModEntry.Log($"AfterQuestionBehavior: {whichAnswer}");
        if (validEntries.TryGetValue(whichAnswer, out QuestionDialogueEntry? qde))
        {
            qde.PerformQDActions(location, point, who);
        }
    }
}

public sealed class QuestionDialogueEntry
{
    /// <summary>Response label</summary>
    public string Label = "[LocalizedText Strings/UI:Cancel]";

    /// <summary>Response GSQ condition</summary>
    public string? Condition = null;

    /// <summary>List of (trigger) actions</summary>
    public List<string>? Actions = null;

    /// <summary>List of tile actions</summary>
    public List<string>? TileActions = null;

    /// <summary>Stop at the first successful tile action</summary>
    public bool TileActionStopAtFirstSuccess = true;

    /// <summary>List of touch actions</summary>
    public List<string>? TouchActions = null;

    internal bool IsValid(GameStateQueryContext context)
    {
        bool hasAnyAction = Actions != null || TileActions != null || TouchActions != null;
        bool isCancel = Actions == null && TileActions == null && TouchActions == null;
        return (hasAnyAction || isCancel) && GameStateQuery.CheckConditions(Condition, context);
    }

    internal void PerformQDActions(GameLocation location, Point point, Farmer who)
    {
        if (Actions != null)
        {
            // Perform all (trigger) actions
            foreach (string action in Actions)
            {
                if (!TriggerActionManager.TryRunAction(action, out string error, out Exception _))
                {
                    ModEntry.Log(error, LogLevel.Error);
                }
            }
        }
        if (TileActions != null)
        {
            // Return after first successful tile action
            xTile.Dimensions.Location loc = new(point.X, point.Y);
            foreach (string action in TileActions)
            {
                if (location.performAction(action, who, loc) && TileActionStopAtFirstSuccess)
                    return;
            }
        }
        if (TouchActions != null)
        {
            // Perform all touch tile actions
            foreach (string action in TouchActions)
            {
                location.performTouchAction(action, new(point.X, point.Y));
            }
        }
    }
}

public sealed class QuestionDialogueData
{
    /// <summary>Response GSQ condition</summary>
    public string? Condition { get; set; } = null;

    /// <summary>Optional dialogue to display before raising the question</summary>
    public string? DialogueBefore { get; set; } = null;

    /// <summary>Question string</summary>
    public string? Question { get; set; } = null;

    /// <summary>Speaking NPC</summary>
    public string? Speaker { get; set; } = null;

    /// <summary>Speaking Display Name</summary>
    public string? SpeakerDisplayName { get; set; } = null;

    /// <summary>Speaking NPC Portrait</summary>
    public string? SpeakerPortrait { get; set; } = null;

    /// <summary>Number of entries to show before displaying a "next" button to go to next page.</summary>
    public int Pagination { get; set; } = -1;

    /// <summary>If this is true and there is only one entry, immediately perform instead of displaying the choice.</summary>
    public bool ImmediatelyPerformSingleValidResponse { get; set; } = true;

    /// <summary>List of responses</summary>
    public Dictionary<string, QuestionDialogueEntry> ResponseEntries { get; set; } = [];

    /// <summary>Get all valid entries per GSQ</summary>
    /// <param name="context"></param>
    /// <returns></returns>
    internal IDictionary<string, QuestionDialogueEntry> ValidEntries(
        GameStateQueryContext context,
        HashSet<string> pickResponse
    )
    {
        bool hasPickedResponse = pickResponse.Any();
        return ResponseEntries
            .Where((qde) => (!hasPickedResponse || pickResponse.Contains(qde.Key)) && qde.Value.IsValid(context))
            .ToDictionary(qde => qde.Key, qde => qde.Value);
    }
}

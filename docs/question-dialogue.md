## Question Dialogue

Question dialogues allow you to offer player a multiple choice menu, similar to the minecart menu.

To use these, you need to have a `mushymato.MMAP/QuestionDialogue` entry and an action activating the menu.

### mushymato.MMAP_QuestionDialogue \<question_dialog_id\> [included responses]+

- Can be used as either `Action`, `TouchAction`, or `TriggerAction`.
- Opens a question dialog, as defined by the custom asset `mushymato.MMAP/QuestionDialogue`, string -> QuestionDialogueData. Each response triggers additional `Actions`/`TileActions`/`TouchActions` on selection, all 3 kinds can be used together and they are checked/executed in that order. You are allowed to call more `mushymato.MMAP_QuestionDialogue`, and essentially chain as many QuestionDialogue as desired.
- To make a "Cancel" option, have a `ResponseEntries` entry with blank `Actions`/`TileActions`/`TouchActions`. Putting an empty `{}` serves this purpose as `Label` is set to localized `"Cancel"` by default.
- Similar to game, pressing ESC selects the final item in `ResponseEntries`.
- The argus after `question_dialog_id` act as a filter for valid question dialogue entries, helpful if you wish to reuse same question dialogue with different set of responses.

#### QuestionDialogueData

- `Speaker` (`string`, _empty_): NPC name of speaker, or none. If this is a real NPC id, their portrait and display name is used for `DialogueBefore`.
- `SpeakerPortrait` (`string`, _empty_): Portrait to use for `DialogueBefore`, or none. This overrides portrait from `Speaker`.
- `DialogueBefore` (`string`, _empty_): Dialogue to display before the question, or none.
    - __Note:__ If your dialogue contains `$e` all dialogue after is lost.
- `Question` (`string`, _empty_): Question string to display, or none.
- `Condition` (`string`, _empty_): A [Game State Query](https://stardewvalleywiki.com/Modding:Game_state_queries) to determine if this dialogue should be enabled.
- `Pagination` (`int`, -1): Paginate this question dialogue with this many response entries per page.
- `ResponsePick` (One of `AlwaysShowResponses`|`ImmediatelyPerformSingle`|`ChooseOneRandom`, `ImmediatelyPerformSingle`): Changes how choosing response happens:
    - `ImmediatelyPerformSingle`: If your question dialogue has exactly 1 valid entry after GSQs are checked, it will **run immediately without prompt**.
    - `AlwaysShowResponses`: Even if you only have exactly 1 valid entry, show a dialogue.
    - `ChooseOneRandom`: If you have more than 1 valid entry, do not show choices and instead pick one from the list at random.
- `ResponseEntries` (`Dictionary<string, QuestionDialogueEntry>`, _empty_): Response data.

#### QuestionDialogueEntry

- `Label` (`string`, `"[LocalizedText Strings/UI:Cancel]"`): Response text, default `"Cancel"`.
- `Condition` (`string`, _empty_): A [Game State Query](https://stardewvalleywiki.com/Modding:Game_state_queries) to determine if this response option is enabled.
- `Actions` (`List<string>`, _empty_): [Trigger actions](https://stardewvalleywiki.com/Modding:Trigger_actions), run all actions.
- `TileActions` (`List<string>`, _empty_): [Map tile actions](https://stardewvalleywiki.com/Modding:Maps#Action), behavior depends on `TileActionStopAtFirstSuccess`.
- `TouchActions` (`List<string>`, _empty_): [Map touch actions](https://stardewvalleywiki.com/Modding:Maps#TouchAction), run all touch actions.
- `TileActionStopAtFirstSuccess` (`bool`, false): If this is true, stop running further tile actions at the first tile action that succeeds, otherwise run all tile actions in the list.
- `TilePointSubstitution` (`bool`, true): If this is true, replace any `<TILE_X>` in `Actions` with the associated point X, and `<TILE_Y>` with point Y.

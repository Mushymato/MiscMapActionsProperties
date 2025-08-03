## Question Dialogue

Question dialogues allow you to offer player a multiple choice menu, similar to the minecart menu.

To use these, you need to have a `mushymato.MMAP/QuestionDialogue` entry and an action activating the menu.

### mushymato.MMAP_QuestionDialogue \<question_dialog_id\>

- Can be used as either `Action`, `TouchAction`, or `TriggerAction`.
- Opens a question dialog, as defined by the custom asset `mushymato.MMAP/QuestionDialogue`, string -> QuestionDialogueData. Each response triggers additional `Actions`/`TileActions`/`TouchActions` on selection, all 3 kinds can be used together and they are checked/executed in that order. You are allowed to call more `mushymato.MMAP_QuestionDialogue`, and essentially chain as many QuestionDialogue as desired.
- To make a "Cancel" option, have a `ResponseEntries` entry with blank `Actions`/`TileActions`/`TouchActions`. Putting an empty `{}` serves this purpose as `Label` is set to localized `"Cancel"` by default.
- Similar to game, pressing ESC selects the final item in `ResponseEntries`.
- If your question dialogue has exactly 1 valid entry after GSQs are checked, it will **run immediately without prompt**. This is a special case scenario that only happens if you do not include an unconditional "Cancel" entry for whatever reason.

#### QuestionDialogueData

- `Speaker` (`string`, _empty_): NPC name of speaker, or none. If this is a real NPC id, their portrait and display name is used for `DialogueBefore`.
- `SpeakerPortrait` (`string`, _empty_): Portrait to use for `DialogueBefore`, or none. This overrides portrait from `Speaker`.
- `DialogueBefore` (`string`, _empty_): Dialogue to display before the question, or none.
    - __Note:__ If your dialogue contains `$e` all dialogue after is lost.
- `Question` (`string`, _empty_): Question string to display, or none.
- `Condition` (`string`, _empty_): A [Game State Query](https://stardewvalleywiki.com/Modding:Game_state_queries) to determine if this dialogue should be enabled.
- `ResponseEntries` (`Dictionary<string, QuestionDialogueEntry>`, _empty_): Response data.
- `Pagination`(`int`, -1): Paginate this question dialogue with this many response entries per page.

#### QuestionDialogueEntry

- `Label` (`string`, `"[LocalizedText Strings/UI:Cancel]"`): Response text, default `"Cancel"`.
- `Condition` (`string`, _empty_): A [Game State Query](https://stardewvalleywiki.com/Modding:Game_state_queries) to determine if this response option is enabled.
- `Actions` (`List<string>`, _empty_): [Trigger actions](https://stardewvalleywiki.com/Modding:Trigger_actions), run all actions.
- `TileActions` (`List<string>`, _empty_): [Map tile actions](https://stardewvalleywiki.com/Modding:Maps#Action), behavior depends on `TileActionStopAtFirstSuccess`.
- `TileActionStopAtFirstSuccess` (`bool`, true): If this is true, stop running further tile actions at the first tile action that succeeds, otherwise run all tile actions in the list.
- `TouchActions` (`List<string>`, _empty_): [Map touch actions](https://stardewvalleywiki.com/Modding:Maps#TouchAction), run all touch actions.

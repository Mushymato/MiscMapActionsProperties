# Misc Map Actions & Properties

Adds a few map related features, no strong design theme just whatever I happen to want.

See `[CP] MMAP Examples` for samples.

### Map Property

#### mushymato.MMAP_FruitTreeCosmeticSeason \<x\> \<y\>

- For use in maps with map property IsGreenhouse T.
- Make fruit trees use seasonal appearances even in greenhouse.

### Tile Data

#### Back layer: mushymato.MMAP_AnimalSpot T

- For use in animal building maps.
- Changes what tiles the animals will start in.
- If building has less AnimalSpot tiles than animals, the remaining animals get random spots.
- 1 AnimalSpot tile will get 1 animal, 2 AnimalSpot next to each other means 2 animals get to start around that area.
- The spawn point of the animal is based on their top left tile, for 2x2 tile animals it's best to put this tile prop top left of where you want them to go.

#### Front layer: mushymato.MMAP_Light [radius] [color] [type|texture] [offsetX] [offsetY]

- Add a light source at the center of this tile.
- Radius controls size of light.
- Can use hex or [named color](https://docs.monogame.net/api/Microsoft.Xna.Framework.Color.html).
- Colors are inverted before being passed to light, so that "Red" will give red light.
- type|texture is either a light id (1-10 except for 3) or a texture (must be loaded).
- Use offsetX and offsetY to further adjust the position of the light.
- Works in building TileProperties too.

### Action

#### mushymato.MMAP_ShowConstruct \<builder\> [restrict]

- Opens the construction menu for specified builder (`Robin` or `Wizard` in vanilla)
- If restrict is given, prevent multiple buildings from being constructed at the same time.

#### mushymato.MMAP_ShowConstructForCurrent \<builder\> [restrict]

- Opens the construction menu for the current area.
- Does nothing if the current area is not buildable.
- If restrict is given, prevent multiple buildings from being constructed at the same time.

#### mushymato.MMAP_HoleWarp <location> <X> <Y> [mailflag]

- Can be used as either Action or TouchAction
- Arguments are identical to vanilla warp tile actions.
- When used with Action, the warp requires interaction, while TouchAction just sends the player directly down the hole.

#### mushymato.MMAP_QuestionDialogue <question_dialog_id>

- Can be used as either Action or TouchAction
- Opens a question dialog, as defined by the custom asset `mushymato.MMAP/QuestionDialogue`, string -> QuestionDialogueData.
- To make a "Cancel" option, have a `ResponseEntries` entry with blank `Actions`/`TileActions`/`TouchActions`. Putting an empty `{}` serves this purpose as `Label` is set to localized `"Cancel"` by default.
- Similar to game, pressing ESC selects the final item in `ResponseEntries`.

##### QuestionDialogueData

| Property | Type | Default | Notes |
| -------- | ---- | ------- | ----- |
| `Question` | string | _empty_ | Question string to display, or none. |
| `Speaker` | string | _empty_ | NPC name of speaker. |
| `ResponseEntries` | Dictionary<string, QuestionDialogueEntry> | _empty_ | Response data. |

##### QuestionDialogueEntry

| Property | Type | Default | Notes |
| -------- | ---- | ------- | ----- |
| `Label` | string | `"[LocalizedText Strings/UI:Cancel]"` | Response text, default `"Cancel"`. |
| `Condition` | string | _empty_ | A [Game State Query](https://stardewvalleywiki.com/Modding:Game_state_queries) to determine if this option is enabled. |
| `Actions` | List<string> | _empty_ | [Trigger actions](https://stardewvalleywiki.com/Modding:Trigger_actions), run all actions. |
| `TileActions` | List<string> | _empty_ | [Map tile actions](https://stardewvalleywiki.com/Modding:Maps#Action), stops at the first tile action that returns true. |
| `TouchActions` | List<string> | _empty_ | [Map touch actions](https://stardewvalleywiki.com/Modding:Maps#TouchAction), run all touch actions. |

### Data/Locations CustomFields

#### mushymato.MMAP/HoeDirt.texture: \<texture\>

- Location CustomFields, for use in places with tillable soil.
- Changes the appearance of tilled soil in that location.
- Texture should follow vanilla format of 3 sets of 16 tiles: tilled, watered overlay, paddy overlay
- See `[CP] Vulkan Farm Cave` and `[PIF] Vulkan Cave` for example.

### Data/Buildings Metadata

#### mushymato.MMAP/ChestLight.{ChestName}: [radius] [color] [type|texture] [offsetX] [offsetY]

- Buildings Metadata, used over CustomFields because Metadata can be set per building skin if desired.
- Add a light source at the building's tileX/tileY position, only lights up if corresponding building chest has content.
- Radius controls size of light.
- Can use hex or [named color](https://docs.monogame.net/api/Microsoft.Xna.Framework.Color.html).
- Colors are inverted before being passed to light, so that "Red" will give red light.
- type|texture is either a light id (1-10 except for 3) or a texture (must be loaded).
- Use offsetX and offsetY to further adjust the position of the light.

#### mushymato.MMAP/DrawLayerRotate.{DrawLayerId}: <rotation> <originX> <originY>

- Buildings Metadata, used over CustomFields because Metadata can be set per building skin if desired.
- Rotates the specified draw layer layer by rotation every second (rotation/60 every tick) around originX, originY
- Can be used with regular draw layer things.


## DEPRECATED

If you need something from here ask me about it and I'll try to think of better implementation.

### Map Property

#### mushymato.MMAP_BuildingEntry \<x\> \<y\>

- For use in building maps.
- Changes where the player arrives on entry, away from the default 1 tile above first warp.

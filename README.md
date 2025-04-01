# Misc Map Actions & Properties

Adds a few map related features, no strong design theme just whatever I happen to want.

See `[CP] MMAP Examples` for samples.

### Map Property

All map properties are also Data/Location custom fields. In the case where both map property and Data/Location custom fields exist, custom fields are prioritized.

#### mushymato.MMAP_FruitTreeCosmeticSeason \<x\> \<y\>

- For use in maps with map property IsGreenhouse T.
- Make fruit trees use seasonal appearances even in greenhouse.

#### mushymato.MMAP/HoeDirt.texture: \<texture\>

- For use in places with tillable soil.
- Changes the appearance of tilled soil in that location.
- Texture should follow vanilla format of 3 sets of 16 tiles: tilled, watered overlay, paddy overlay
- See `[CP] Vulkan Farm Cave` and `[PIF] Vulkan Cave` for example.

#### mushymato.MMAP_WoodsLighting T|Color

- Forces a certain ambiant lighting color, identical logic to 
- 

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

#### Back Layer: mushymato.MMAP_TAS

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
- Can also be used as trigger Action
- Opens a question dialog, as defined by the custom asset `mushymato.MMAP/QuestionDialogue`, string -> QuestionDialogueData. Each response triggers additional `Actions`/`TileActions`/`TouchActions` on selection, all 3 kinds can be used together and they are checked/executed in that order. You are allowed to call more `mushymato.MMAP_QuestionDialogue`, and essentially chain as many QuestionDialogue as desired.
- To make a "Cancel" option, have a `ResponseEntries` entry with blank `Actions`/`TileActions`/`TouchActions`. Putting an empty `{}` serves this purpose as `Label` is set to localized `"Cancel"` by default.
- Similar to game, pressing ESC selects the final item in `ResponseEntries`.

##### QuestionDialogueData

- `Question` (`string`, _empty_): Question string to display, or none.
- `Speaker` (`string`, _empty_): NPC name of speaker.
- `Condition` (`string`, _empty_): A [Game State Query](https://stardewvalleywiki.com/Modding:Game_state_queries) to determine if this option is enabled.
- `ResponseEntries` (`Dictionary<string, QuestionDialogueEntry>`, _empty_): Response data.

##### QuestionDialogueEntry

- `Label` (`string`, `"[LocalizedText Strings/UI:Cancel]"`): Response text, default `"Cancel"`.
- `Condition` (`string`, _empty_): A [Game State Query](https://stardewvalleywiki.com/Modding:Game_state_queries) to determine if this option is enabled.
- `Actions` (`List<string>`, _empty_): [Trigger actions](https://stardewvalleywiki.com/Modding:Trigger_actions), run all actions.
- `TileActions` (`List<string>`, _empty_): [Map tile actions](https://stardewvalleywiki.com/Modding:Maps#Action), stops at the first tile action that returns true.
- `TouchActions` (`List<string>`, _empty_): [Map touch actions](https://stardewvalleywiki.com/Modding:Maps#TouchAction), run all touch actions.

### Map Property

#### mushymato.MMAP_WoodsLighting: \<T|color\>

- Changes the map's ambiant lighting
- `T` uses the default value as seen in woods/island forest maps
- Otherwise, can use hex or [named color](https://docs.monogame.net/api/Microsoft.Xna.Framework.Color.html).
- Colors are inverted before being passed to light, so that "Red" will give red light.

#### mushymato.MMAP_LightRays: \<T|texture\>

- Add some light rays to the map
- `T` uses `LooseSprites/LightRays`, as seen in island forest
- Otherwise, supply a valid texture asset name

#### mushymato.MMAP_SteamOverlay: \<T|texture\> [velocityX] [velocityY] [color] [alpha] [scale]

- Adds a tiling overlay drawn above the map.
- If `velocityX` and/or `velocityY` are given, move the texture by that many pixels every tick to create scrolling effect.
- By default, `color` is 80% white, `alpha` is 1 by default, and `scale` is 4 by default

#### mushymato.MMAP_CribPosition: \<X\> \<Y\>

- Farmhouse only, repositions the crib (vanilla is 30 12), size is still 3x4.
- The default farmhouse has a crib baked in, which needs to be removed if you don't want duplicate cribs.
- For wall and floor, there are 2 options:
    1. Place the renovation below a row of tiles that have `WallID`, and make sure `FloorID` matches the room it is in.
    2. Completely remove `FloorID` and all the wall/floor tiles from `FarmHouse_Crib_0` and `FarmHouse_Crib_1`
- For option 2, there are sample maps that can be used for your own mods in `[CP] MMAP Examples/assets/` (`FarmHouse_Crib_0.tmx` and `FarmHouse_Crib_1.tmx`)

### Data/Buildings Metadata

Buildings Metadata are like CustomFields, except they also appear on skins and can be overwritten if needed.

#### mushymato.MMAP/ChestLight.{ChestName}: [radius] [color] [type|texture] [offsetX] [offsetY]

- Add a light source at the building's tileX/tileY position, only lights up if corresponding building chest has content.
- Radius controls size of light, 2 by default.
- Can use hex or [named color](https://docs.monogame.net/api/Microsoft.Xna.Framework.Color.html), white by default
- Colors are inverted before being passed to light, so that "Red" will give red light.
- `type|texture` is either a light id (1-10 except for 3) or a texture (must be loaded).
- Use `offsetX` and `offsetY` to further adjust the position of the light.

#### mushymato.MMAP/DrawLayerRotate.{DrawLayerId}.{override}

Various draw layer overriding fields, can be used with regular draw layer things.

##### string values

- `mushymato.MMAP/DrawLayerRotate.{DrawLayerId}.condition`: A [Game State Query](https://stardewvalleywiki.com/Modding:Game_state_queries) to determine if this layer should draw. Rechecked on time changed (e.g. 10 in game minutes).

##### float values

These are all support passing either `0.1` float, or `"0.1 0.4"` for random value between first and second.

- `mushymato.MMAP/DrawLayerRotate.{DrawLayerId}.alpha`: transparency (0 to 1)
- `mushymato.MMAP/DrawLayerRotate.{DrawLayerId}.rotate`: rotation (0 to 6.28318), around the origin.
- `mushymato.MMAP/DrawLayerRotate.{DrawLayerId}.rotateRate`: rotation change per second, positive is clockwise, negative is counter clockwise.
- `mushymato.MMAP/DrawLayerRotate.{DrawLayerId}.scale`: draw scale (default 4f)

##### Vector2 values

These are Vector2 coordinates, takes 2 integers like `"0 0"`.

- `mushymato.MMAP/DrawLayerRotate.{DrawLayerId}.origin`: defines the origin of the sprite, relevant if you also use rotate, but can act as a secondary offset if the draw layer 

##### SpriteEffects values

SpriteEffects, one of "None", "FlipHorizontally", "FlipVertically"

- `mushymato.MMAP/DrawLayerRotate.{DrawLayerId}.effect`: sprite effects for flipping the sprite

### Farmhouse Upgrade Relocation Solution

The following features combined make it possible for content mod authors to relocate things when farmhouse is upgrade, see `[CP] MMAP Examples/cr_rustic_relocate.json` for a full example.

#### Map Property mushymato.MMAP_SkipMoveObjectsForHouseUpgrade

This map property disables the vanilla farmhouse object relocation logic, set it to any non empty value (e.g. `"T"`) on at least the first 2 farmhouse maps.

#### Trigger mushymato.MMAP_MoveObjectsForHouseUpgrade

This trigger is raised when house is upgraded and game attempts to move farmhouse objects. It lets you perform the following action at the same timing as vanilla game.

#### TriggerAction mushymato.MMAP_ShiftContents: \<SourceX\> \<SourceY\> \<TargetX\> \<TargetY\> \<AreaWidth\> \<AreaHeight\> [locationName]

This trigger action shifts things on map from one area to another area of the same shape, this is the same thing used by game for house upgrades, but it is more targeted. To properly relocate everything, slice up the farmhouse into matching rectangles and call this action as many time as needed in the trigger action.

- `SourceX`, `SourceY`: This is the top left corner of the area to move, on the previous level
- `TargetY`, `TargetX`: This is the top left corner of the area to move to, on the new level
- `AreaWidth`, `AreaHeight`: This is the size of the area to move.
- `locationName`: This is the (optional) location name. It defaults to the player's farmhouse and only need to be provided if you want to use this action on a different location.

## DEPRECATED

If you need something from here ask me about it and I'll try to think of better implementation.

### Map Property

#### mushymato.MMAP_BuildingEntry \<x\> \<y\>

- For use in building maps.
- Changes where the player arrives on entry, away from the default 1 tile above first warp.

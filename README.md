# Misc Map Actions & Properties

Adds a few map related features, no strong design theme just whatever I happen to want.

See `[CP] MMAP Examples` for samples.

### Map Property

All map properties are also `Data/Location` custom fields. In the case where both map property and `Data/Location` custom fields exist, custom fields are prioritized.

#### mushymato.MMAP_ProtectTree [T|message]

- Protects wild trees from being chopped by an axe in a location.
- If the value is not `"T"`, it is treated as a HUD message to be shown if the player attempts to chop a tree.
- If the tree is protected and chop is attempted, a trigger named `mushymato.MMAP_ProtectTree` is raised.
- Will not affect custom ways that remove trees (i.e. anything that does not go through `Tree.performToolAction`).

#### mushymato.MMAP_ProtectFruitTree [T|message]

- Protects fruit trees from being chopped by an axe in a location.
- If the value is not `"T"`, it is treated as a HUD message to be shown if the player attempts to chop a tree.
- If the tree is protected and chop is attempted, a trigger named `mushymato.MMAP_ProtectFruitTree` is raised.
- Will not affect custom ways that remove trees (i.e. anything that does not go through `FruitTree.performToolAction`).

#### mushymato.MMAP_FruitTreeCosmeticSeason \<x\> \<y\>

- For use in maps with map property IsGreenhouse T.
- Make fruit trees use seasonal appearances even in greenhouse.

#### mushymato.MMAP_LightRays \<texture\>

- Draws some light rays near top of map, identical logic to vanilla effect in island woods.
- The texture can be changed for something else.
- Setting `T` gives the default texture of `LooseSprites\\LightRays`.

#### mushymato.MMAP_WoodsLighting T|Color

- Forces a certain ambiant lighting color, identical logic to vanilla lighting in the secret woods.
- Colors are inverted, much like for light
- Setting `T` gives the default color of #967832 (equivalent to setting #6987cd as argument).
- **Note:** woods lighting causes map lights to turn off at night, this affects the `Light` map property and path layer lights. To keep your lights on at night, use [mushymato.MMAP_Light](#mushymato.MMAP_Light) with these arguments:
    - `Light`: `mushymato.MMAP_Light 1 White <light index> 0 0 None` on the desired tile's TileData, Back or Front layer.
    - Path light: `mushymato.MMAP_Light 1 White 4 0 0 None` on the desired tile's TileData, Back or Front layer.

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
- For option 2, there are sample edited crib tmx that can be used for your own mods in `[CP] MMAP Examples/assets/` (`FarmHouse_Crib_0.tmx` and `FarmHouse_Crib_1.tmx`)

#### mushymato.MMAP_FridgePosition: \<X\> \<Y\>

- Farmhouse only, repositions the fridge independent of the map check for `untitled tile sheet` tile id 173 fridge.
- The vanilla fridge logic still works and this position does not add or change map tiles, so you would actually need corresponding edit to make use of this.

#### mushymato.MMAP_FridgeDoorSprite: \<F|texture\> [offsetX] [offsetY]

- Farmhouse only, changes the fridge door's open sprite.
- Only required if you need a fridge door larger than the vanilla 16x32 rectangle.

#### mushymato.MMAP_Panorama \<panoramaId\>

- Draw a parallax background behind the map.
- The `panoramaId` arguments refer to an entry in the `mushymato.MMAP/Panorama` custom asset, see [panorama docs](docs/panorama.md) for details.
- There are some some panoramas provided by MMAP that can be used out of the box.
    - `MMAP_MountainView`: shows seasonal sky with some animated clouds, mountains, sunset, and stars at night.
    - `MMAP_ClearSky`: like `MMAP_MountainView` but without mountains and clouds.
    - `MMAP_IslandHorizon`: shows the island ocean horizon with clouds background.

#### mushymato.MMAP_NightTime* \<time\> <a name="mushymato.MMAP_NightTime"></a>

There are 3 similar map properties for setting phases of day transitioning to night.
1. mushymato.MMAP_NightTimeStarting
2. mushymato.MMAP_NightTimeModerate
3. mushymato.MMAP_NightTimeTruly

- All 3 of these take time in the typical military time format (0600) used by many SDV things.
- The vanilla values are
    - starting: 1800 spring summer island, 1700 fall, 1500 winter
    - moderate: halfway between starting and truly
    - truly: starting + 200
- If you only set starting, the other two will be calculated according to vanilla logic.

There are also related Triggers for use in Data/TriggerActions
- mushymato.MMAP_NightTimeStarting: raised at night starting time
- mushymato.MMAP_NightTimeModerate: raised at night moderate time
- mushymato.MMAP_NightTimeLightsOff: raised 100 before night truly time, this is when lights turn off
- mushymato.MMAP_NightTimeTruly: raised at night truly time

And Game State Queries
- mushymato.MMAP_TIME_IS_DAY: true when time of day is less than night starting time
- mushymato.MMAP_TIME_IS_SUNSET: true when time of day is during night starting and truly time
- mushymato.MMAP_TIME_IS_LIGHTS_OFF: true when time of day is after window lights turn off and lamp lights turn on.
- mushymato.MMAP_TIME_IS_NIGHT: true when time of day is later than night truly time

### Tile Data

#### Back layer: mushymato.MMAP_AnimalSpot T

- For use in animal building maps.
- Changes what tiles the animals will start in.
- If building has less AnimalSpot tiles than animals, the remaining animals get random spots.
- 1 AnimalSpot tile will get 1 animal, 2 AnimalSpot next to each other means 2 animals get to start around that area.
- The spawn point of the animal is based on their top left tile, for 2x2 tile animals it's best to put this tile prop top left of where you want them to go.

#### Back layer: mushymato.MMAP_GrassSpread T

- If set, allow this tile to spread grass (without using `Diggable`).
- Ideally this is set on the tile sheet, rather than on a per tile data basis.
- This is disabled if the player has `bcmpinc.GrassGrowth` installed since that mod simply skips the `Diggable` check entirely.

#### Front or Back layer: mushymato.MMAP_Light [radius] [color] [type|texture] [offsetX] [offsetY] [lightContext] <a name="mushymato.MMAP_Light"></a>

- Add a light source at the center of this tile.
- Radius controls size of light.
- Can use hex or [named color](https://docs.monogame.net/api/Microsoft.Xna.Framework.Color.html).
- Colors are inverted before being passed to light, so that "Red" will give red light.
- type|texture is either a light id (1-10 except for 3) or a texture (must be loaded).
- Use offsetX and offsetY to further adjust the position of the light.
- lightContext is an enum, one of the following:
    - `None`: Always on lighting
    - `MapLight`: When under woods lighting (secret woods, island east, `mushymato.MMAP_WoodsLighting`), turn off these lights at night.
    - `WindowLight`: Follows vanilla window light regarding rain and night time.
- Works in building TileProperties too.

#### Back Layer: mushymato.MMAP_TAS \<tasId\>+ <a name="mushymato.MMAP_TAS"></a>

- Add a temporary animated sprite at this tile.
- The layer depth is based on the tile position.
- The `tasId` arguments refer to an entry in the `mushymato.MMAP/TAS` custom asset, see [temporary animated sprites docs](docs/temporary-animated-sprites.md) for details.

#### Back Layer: mushymato.MMAP_Critter [\<critterType\> [type dependent args]]+

- Spawn a certain kind of simple critter on this tile.
- Currently supports Firefly Seagull Crab
- Firefly: [color|T] [count]
- Seagull: [texture|T] [count]
- Crab: [texture|T] [count]
- Birdies: [texture|<number>|T] [count]
    - The number option allows you to give a start index for `TileSheet/critters` which has birdies starting at 25, 45, 125, 135, 165, and 175.
    - When using T, the birdie start index will be picked with logic similar to base game.
- Butterfly: [texture|<number>|T] [count]
    - The number option allows you to give a start index for `TileSheet/critters` (WIP: figure out the indicies)
    - When using texture, the bufferfly will follow summer butterfly rules (4 frames)
    - When using T, the birdie start index will be picked base game logic.
- You can use multiple sets of these args to spawn more critters on the same tile, e.g. `Crab T 3 Firefly T 8` for 3 crabs 8 fireflies on the tile.
- T as first argument is a placeholder and lets you use defaults.
- For critters that support a texture, they need to have same number of frames as the original, see `[CP] MMAP Examples/assets/critters` for example textures you can use as a base.
- This can also be used as:
    - Building layer Action: spawns critter at this tile on interact
    - Back layer TouchAction: spawns critter at this tile when player walks over
    - TriggerAction: takes a coordinate as the first 2 arguments (i.e. `mushymato.MMAP_Critter <X> <Y> [\<critterType\> [type dependent args]]+`)

### Action

#### mushymato.MMAP_ShowConstruct \<builder\> [restrict]

- Can be used as either Action or TouchAction
- Opens the construction menu for specified builder (`Robin` or `Wizard` in vanilla)
- If restrict is given, prevent multiple buildings from being constructed at the same time.

#### mushymato.MMAP_ShowConstructForCurrent \<builder\> [restrict]

- Can be used as either Action or TouchAction
- Opens the construction menu for the current area.
- Does nothing if the current area is not buildable.
- If restrict is given, prevent multiple buildings from being constructed at the same time.

#### mushymato.MMAP_HoleWarp \<location\> \<X\> \<Y\> [mailflag]

- Can be used as either Action or TouchAction
- Arguments are identical to vanilla warp tile actions.
- When used with Action, the warp requires interaction, while TouchAction just sends the player directly down the hole.

#### mushymato.MMAP_ShowBag \<bagInventoryId\> [bagKind]

- Can be used as either Action or TouchAction or TriggerAction.
- Opens a global inventory with the given inventory id. This id is always prefixed with `mushymato.MMAP#`, but it is recommended to prefix it with your own mod id too.
- This can be used to create junimo chest like containers, though automate will not work with it.
- Bag kind will allow you to make this chest a big chest with 70 slots, but it is up to the mod to consistently use `BigChest` for all places where this bag is accessible.

#### mushymato.MMAP_TAS \<X\> \<Y\> \<tasId\>+

- Can be used as either Action or TouchAction or TriggerAction.
- Spawns [TAS](./docs/temporary-animated-sprites.md) on the specified tile.
- Works nearly identical to the tile data version but does not respond to tile data changes.

#### mushymato.MMAP_ToggleTAS \<spawnKey\> \<X\> \<Y\> \<tasId\>+

- Can be used as either Action or TriggerAction.
- Spawns [TAS](./docs/temporary-animated-sprites.md) on the specified tile, associated with a specific key.
- When activated again, remove the spawnned TAS.

#### mushymato.MMAP_ContactTAS \<tasId\>+

- Can be used as TouchAction.
- Spawns the TAS while player is standing on the tile, removed if player moves off the tile.

#### mushymato.MMAP_WarpBuilding [X Y]

Also available as: `mushymato.MMAP_MagicWarpBuilding [X Y]` (does the biiiiu and teleport effect), `mushymato.MMAP_HoleWarpBuilding [X Y]` (does the hole warp).

- Can be used as either Action or TouchAction, mainly for usage in building `ActionTiles` or `TileProperties`.
- Warps the player into the building that is occupying this tile.
- The building must have an interior, and ideally still define a `HumanDoor` which will serve as the exit tile.
- You can override the default behaviour of the warp point from 1 tile north of the first warp to by using the optional X Y arguments, to put the player anywhere inside the building.
- The original human door tile will still work.

_See [[CP] MMAP Examples/building_warp.json]([CP]%20MMAP%20Examples/building_warp.json) for examples of adding this to building tile data_

#### mushymato.MMAP_WarpBuildingOut [X Y]

Also available as: `mushymato.MMAP_MagicWarpBuildingOut [X Y]` (does the biiiiu and teleport effect), `mushymato.MMAP_HoleWarpBuildingOut [X Y]` (does the hole warp).

- Can be used as either Action or TouchAction, only valid in a building interior map.
- Warps the player out of a building, optionally to a position other than the default 1 tile below HumanDoor.
- X and Y arguments are relative to the building's top left tile, it can be negative.
- There's **no guarentee** that the warp out tile is not occupied, it's recommended to use `AdditionalPlacementTiles` to enforce a cleared tile.

#### mushymato.MMAP_If \<GSQ\> ## \<if-case\> [## \<else-case\>]

- Can be used as either Action or TouchAction
- Works like Trigger Action If, blocks the `if-case` Action behind a GSQ.
- Optionally allows a `else-case` Action.
- The action provided must match the usescase, e.g. if this If is on a TileAction, both the if-case and else-case actions should also be tile actions.

#### mushymato.MMAP_QuestionDialogue \<question_dialog_id\>

- Can be used as either Action or TouchAction
- Can also be used as trigger Action
- Opens a question dialog, as defined by the custom asset `mushymato.MMAP/QuestionDialogue`, string -> QuestionDialogueData. Each response triggers additional `Actions`/`TileActions`/`TouchActions` on selection, all 3 kinds can be used together and they are checked/executed in that order. You are allowed to call more `mushymato.MMAP_QuestionDialogue`, and essentially chain as many QuestionDialogue as desired.
- To make a "Cancel" option, have a `ResponseEntries` entry with blank `Actions`/`TileActions`/`TouchActions`. Putting an empty `{}` serves this purpose as `Label` is set to localized `"Cancel"` by default.
- Similar to game, pressing ESC selects the final item in `ResponseEntries`.
- If your question dialogue has exactly 1 valid entry after GSQs are checked, it will **run immediately without prompt**. This is a special case scenario that only happens if you do not include an unconditional "Cancel" entry for whatever reason.

##### QuestionDialogueData

- `Speaker` (`string`, _empty_): NPC name of speaker, or none. If this is a real NPC id, their portrait and display name is used for `DialogueBefore`.
- `SpeakerPortrait` (`string`, _empty_): Portrait to use for `DialogueBefore`, or none. This overrides portrait from `Speaker`.
- `DialogueBefore` (`string`, _empty_): Dialogue to display before the question, or none.
    - __Note:__ If your dialogue contains `$e` all dialogue after is lost.
- `Question` (`string`, _empty_): Question string to display, or none.
- `Condition` (`string`, _empty_): A [Game State Query](https://stardewvalleywiki.com/Modding:Game_state_queries) to determine if this dialogue should be enabled.
- `ResponseEntries` (`Dictionary<string, QuestionDialogueEntry>`, _empty_): Response data.

##### QuestionDialogueEntry

- `Label` (`string`, `"[LocalizedText Strings/UI:Cancel]"`): Response text, default `"Cancel"`.
- `Condition` (`string`, _empty_): A [Game State Query](https://stardewvalleywiki.com/Modding:Game_state_queries) to determine if this response option is enabled.
- `Actions` (`List<string>`, _empty_): [Trigger actions](https://stardewvalleywiki.com/Modding:Trigger_actions), run all actions.
- `TileActions` (`List<string>`, _empty_): [Map tile actions](https://stardewvalleywiki.com/Modding:Maps#Action), behavior depends on `TileActionStopAtFirstSuccess`.
- `TileActionStopAtFirstSuccess` (`bool`, true): If this is true, stop running further tile actions at the first tile action that succeeds, otherwise run all tile actions in the list.
- `TouchActions` (`List<string>`, _empty_): [Map touch actions](https://stardewvalleywiki.com/Modding:Maps#TouchAction), run all touch actions.

### Furniture Properties

You can give furniture tile properties (including action and tile actions) and collision using this feature. This would let you use all the previous tile properties/actions/touch actions listed with furniture, plus various vanilla properties and actions. Besides this, you can also give a collision map to make the furniture passable on some tiles.

To give furniture some properties, add an entry to custom asset `mushymato.MMAP/FurnitureProperties`, [more details here](./docs/furniture-properties.md).

### Floor Path Properties

You can give paths (`Data/FloorsAndPaths`) tile properties (including action and tile actions) using this feature. This would let you use all the previous tile properties/actions/touch actions listed with furniture, plus various vanilla properties and actions.

To give floor and path some properties, add an entry to custom asset `mushymato.MMAP/FloorPathProperties`. Please see [example here](./[CP]%20MMAP%20Examples/floor_path_properties.json).

### Data/Buildings Metadata

Buildings Metadata are like CustomFields, except they also appear on skins and can be overwritten if needed.

#### mushymato.MMAP/ChestLight.{ChestName}: [radius] [color] [type|texture] [offsetX] [offsetY]

- Add a light source at the building's tileX/tileY position, only lights up if corresponding building chest has content.
- Radius controls size of light, 2 by default.
- Can use hex or [named color](https://docs.monogame.net/api/Microsoft.Xna.Framework.Color.html), white by default
- Colors are inverted before being passed to light, so that "Red" will give red light.
- `type|texture` is either a light id (1-10 except for 3) or a texture (must be loaded).
- Use `offsetX` and `offsetY` to further adjust the position of the light.

#### mushymato.MMAP/DrawLayer.{DrawLayerId}.{override}

Various draw layer overriding fields, can be used with regular draw layer things.

##### string values

- `mushymato.MMAP/DrawLayer.{DrawLayerId}.condition`: A [Game State Query](https://stardewvalleywiki.com/Modding:Game_state_queries) to determine if this layer should draw. Rechecked on time changed (e.g. 10 in game minutes).

##### float values

These are all support passing either `0.1` float, or `"0.1 0.4"` for random value between first and second.

- `mushymato.MMAP/DrawLayer.{DrawLayerId}.alpha`: transparency (0 to 1)
- `mushymato.MMAP/DrawLayer.{DrawLayerId}.rotate`: rotation (0 to 6.28318), around the origin.
- `mushymato.MMAP/DrawLayer.{DrawLayerId}.rotateRate`: rotation change per second, positive is clockwise, negative is counter clockwise.
- `mushymato.MMAP/DrawLayer.{DrawLayerId}.scale`: draw scale (default 4f)

##### Vector2 values

These are Vector2 coordinates, takes 2 integers like `"0 0"`.

- `mushymato.MMAP/DrawLayer.{DrawLayerId}.origin`: defines the origin of the sprite. Mainly relevant if you also use rotate, but can act as a secondary offset for the draw layer.

##### SpriteEffects values

SpriteEffects, one of "None", "FlipHorizontally", "FlipVertically"

- `mushymato.MMAP/DrawLayer.{DrawLayerId}.effect`: sprite effects for flipping the sprite

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
- `locationName`: This is the (optional) location name. It defaults to the player's farmhouse and only need to be provided if you want to use this action on a different location. There are 2 special values:
    - `Here`: use current location
    - `Cellar`: use assigned cellar (though this seems oddly unreliable)

## DEPRECATED

If you need something from here ask me about it and I'll try to think of better implementation.

#### Map Property: mushymato.MMAP_BuildingEntry \<x\> \<y\>

- For use in building maps.
- Changes where the player arrives on entry, away from the default 1 tile above first warp.
- DEPRECATED: you can now use action `mushymato.MMAP_WarpBuilding` or `mushymato.MMAP_MagicWarpBuilding` or `mushymato.MMAP_HoleWarpBuilding`

#### Map Property: mushymato.MMAP_HoeDirt: \<texture\>

- For use in places with tillable soil, changes the appearance of tilled soil in that location.
- Texture should follow vanilla format of 3 sets of 16 tiles: tilled, watered overlay, paddy overlay
- See `[CP] Vulkan Farm Cave` and `[PIF] Vulkan Cave` for example.
- DEPRECATED: can use locational content patcher editimage for similar effect.

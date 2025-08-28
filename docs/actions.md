# Actions

Each action will note what they can be used as:
- `Action`: map tile action, i.e. set the tile property `Action` on layer `Buildings`.
- `TouchAction`: map touch action, i.e. set the tile property `TouchAction` on layer `Back`.
- `TriggerAction`: trigger action action, which can be used in `Data/TriggerActions` and many other places.

### mushymato.MMAP_ShowConstruct \<builder\> [restrict]

- Can be used as either `Action` or `TouchAction` or `TriggerAction`.
- Opens the construction menu for specified builder (`Robin` or `Wizard` in vanilla)
- If restrict is given, prevent multiple buildings from being constructed at the same time.

### mushymato.MMAP_ShowConstructForCurrent \<builder\> [restrict]

- Can be used as either `Action` or `TouchAction` or `TriggerAction`.
- Opens the construction menu for the current area.
- Does nothing if the current area is not buildable.
- If restrict is given, prevent multiple buildings from being constructed at the same time.

### mushymato.MMAP_ShowShipping

- Can be used as either `Action` or `TouchAction` or `TriggerAction`.
- Opens the farm shipping bin, as in the one on the actual farm.
- Will fail if player does not have a shipping bin building on the farm.

### mushymato.MMAP_ShowBag \<bagInventoryId\> [bagKind]

- Can be used as either `Action` or `TouchAction` or `TriggerAction`.
- Opens a global inventory with the given inventory id. This id is always prefixed with `mushymato.MMAP#`, but it is recommended to prefix it with your own mod id too.
- This can be used to create junimo chest like containers, though automate will not work with it since they are not true "Chest" entities.
- Using `BigChest` as bag kind will make this chest a big chest with 70 slots, but it is up to the mod to consistently use `BigChest` for all places where this bag is accessible.
- Related GSQ: `mushymato.MMAP_BAG_HAS_ITEM [bagInvId] [itemId] [minCount] [maxCount]`: check that bag has some amount of an item.

### mushymato.MMAP_AddItemToBag \<bagInventoryId\> \<qualifiedItemId\> [amount] [quality]

- Can be used as either `Action` or `TouchAction` or `TriggerAction`.
- Adds an item to a specific MMAP global inventory bag.

### mushymato.MMAP_TAS \<X\> \<Y\> \<tasId\>+ <a name="mushymato.MMAP_TAS"></a>

- Can be used as either `Action` or `TouchAction` or `TriggerAction`.
- Spawns [TAS](temporary-animated-sprites.md) on the specified tile.
- Works nearly identical to the tile data version but does not respond to tile data changes.
- If you use -1000 -1000 as X and Y and this is a map or touch action, the TAS will spawn at the tile of the action.

### mushymato.MMAP_ToggleTAS \<spawnKey\> \<X\> \<Y\> \<tasId\>+

- Can be used as either `Action` or `TriggerAction`.
- Spawns [TAS](temporary-animated-sprites.md) on the specified tile, associated with a specific key.
- When activated again, remove the spawnned TAS.

### mushymato.MMAP_ContactTAS \<tasId\>+

- Can be used as `TouchAction`.
- Spawns the TAS while player is standing on the tile, removed if player moves off the tile.

### mushymato.MMAP_Critter [\<critterType\> [type dependent args]]+ <a name="mushymato.MMAP_Critter"></a>

- Can be used as `Action` or `TouchAction`.
- Does not take any coordinates, i.e. accept same arguments as the tile property version.
- Spawns critter on this tile on interaction.
- See [tile property mushymato.MMAP_Critter](tile-properties.md#mushymato.MMAP_Critter) for details.

### mushymato.MMAP_Critter \<X\> \<Y\> [\<critterType\> [type dependent args]]+

- Can be used as `TriggerAction`.
- Requires an X Y coordinate.
- Spawns critter on specific tile on use.
- See [tile property mushymato.MMAP_Critter](tile-properties.md#mushymato.MMAP_Critter) for details.

### mushymato.MMAP_CritterRandom \<chance\> [\<critterType\> [type dependent args]]+

- Can be used as `TriggerAction`.
- Requires a float chance for the critter to appear at any random tile in current location.
- Spawns critter at any tile in the map, randomly.
- See [tile property mushymato.MMAP_Critter](tile-properties.md#mushymato.MMAP_Critter) for details.

### mushymato.MMAP_HoleWrp \<location\> \<X\> \<Y\> [mailflag]

- Can be used as either `Action` or `TouchAction`.
- Arguments are identical to vanilla warp tile actions.
- When used with Action, the warp requires interaction, while TouchAction just sends the player directly down the hole.

### mushymato.MMAP_WrpBuilding [X Y]

Also available as: `mushymato.MMAP_MagicWrpBuilding [X Y]` (does the biiiiu and teleport effect), `mushymato.MMAP_HoleWrpBuilding [X Y]` (does the hole warp).

- Can be used as either `Action` or `TouchAction`, mainly for usage in building `ActionTiles` or `TileProperties`.
- Warps the player into the building that is occupying this tile.
- The building must have an interior, and ideally still define a `HumanDoor` which will serve as the exit tile.
- You can override the default behaviour of the warp point from 1 tile north of the first warp to by using the optional X Y arguments, to put the player anywhere inside the building.
- The original human door tile will still work.

_See [[CP] MMAP Examples/building_Wrp.json]([CP]%20MMAP%20Examples/building_Wrp.json) for examples of adding this to building tile data_

### mushymato.MMAP_WrpBuildingOut [X Y]

Also available as: `mushymato.MMAP_MagicWrpBuildingOut [X Y]` (does the biiiiu and teleport effect), `mushymato.MMAP_HoleWrpBuildingOut [X Y]` (does the hole warp).

- Can be used as either `Action` or `TouchAction`, only valid in a building interior map.
- Warps the player out of a building, optionally to a position other than the default 1 tile below HumanDoor.
- X and Y arguments are relative to the building's top left tile, it can be negative.
- There's **no guarentee** that the warp out tile is not occupied, it's recommended to use `AdditionalPlacementTiles` to enforce a cleared tile.

### mushymato.MMAP_WrpHere [X Y] [facingDirection] [fadeToBlack]

- Can be used as either `Action` or `TouchAction`.
- Warps the player within the current map.
- This is primarily used to solve issues with warps within an instanced location, as a replacement to writing `X Y CurrentLocation X Y` warps.
- `facingDirection` controls the player's facing direction after the warp
    - 0: Up
    - 1: Right
    - 2: Down
    - 3: Left
    - -1: Keep original direction
- If `fadeToBlack` is false, teleport the player without doing normal warp fade to black, this also does not trigger any on warp effects.

### mushymato.MMAP_PoolEntry [facingDirection] [velocity] [soundcue]

- Can be used as either `Action` or `TouchAction`.
- Combines `ChangeIntoSwimsuit`/`ChangeOutOfSwimsuit`/`PoolEntrance` into one action so that a single tile is enough for entering a pool.
- Use `facingDirection` to control which direction the player can enter from, it depends on the player's facing direction
    - 0: Can enter pool while facing up
    - 1: Can enter pool while facing right
    - 2: Can enter pool while facing down
    - 3: Can enter pool while facing left
    - -1: Any direction is valid
- Use `velocity` to increase how far the player shoots into/out of the pool, default 8
- Use `soundcue` to change the sound of entering/exiting the pool, by default it is `pullItemFromWater`

### mushymato.MMAP_FarmHouseUpgrade [upgradeDays]

- Can be used as `TriggerAction`.
- Makes the farmhouse upgrade overnight, as if robin finished constructing it today.
- Upgrade days will set number of days required

### mushymato.MMAP_SetFlooring [flooringId] [floorId]

- Can be used as `TriggerAction`.
- Sets the flooring of farmhouse

### mushymato.MMAP_SetWallpaper [wallpaperId] [floorId]

- Can be used as `TriggerAction`.
- Sets the wallpaper of farmhouse

### mushymato.MMAP_If \<GSQ\> ## \<if-case\> [## \<else-case\>]

- Can be used as either `Action` or `TouchAction`.
- Works like Trigger Action `If`, blocks the `if-case` Action behind a GSQ.
- Optionally allows a `else-case` Action.
- The action provided must match the usescase, e.g. if this If is on a TileAction, both the if-case and else-case actions should also be tile actions.

### mushymato.MMAP_QuestionDialogue \<question_dialog_id\>

- Can be used as either `Action`, `TouchAction`, or `TriggerAction`.
- See [question dialogue](question-dialogue.md) for more info.

#### mushymato.MMAP_Panorama \<panoramaId\>

- Can be used as either `Action`, `TouchAction`, or `TriggerAction`.
- The `panoramaId` arguments refer to an entry in the `mushymato.MMAP/Panorama` custom asset, see [panorama docs](panorama.md) for details.
- There are some some panoramas provided by MMAP that can be used out of the box.
    - `MMAP_MountainView`: shows seasonal sky with some animated clouds, mountains, sunset, and stars at night.
    - `MMAP_ClearSky`: like `MMAP_MountainView` but without mountains and clouds.
    - `MMAP_IslandHorizon`: shows the island ocean horizon with clouds background.

## Farmhouse Upgrade Relocation Solution

The following features combined make it possible for content mod authors to relocate things when farmhouse is upgrade, see `[CP] MMAP Examples/cr_rustic_relocate.json` for a full example.

### Map Property mushymato.MMAP_SkipMoveObjectsForHouseUpgrade

This map property disables the vanilla farmhouse object relocation logic, set it to any non empty value (e.g. `"T"`) on at least the first 2 farmhouse maps.

### Trigger mushymato.MMAP_MoveObjectsForHouseUpgrade

This trigger is raised when house is upgraded and game attempts to move farmhouse objects. It lets you perform the following action at the same timing as vanilla game.

### TriggerAction mushymato.MMAP_ShiftContents: \<SourceX\> \<SourceY\> \<TargetX\> \<TargetY\> \<AreaWidth\> \<AreaHeight\> [locationName]

This trigger action shifts things on map from one area to another area of the same shape, this is the same thing used by game for house upgrades, but it is more targeted. To properly relocate everything, slice up the farmhouse into matching rectangles and call this action as many time as needed in the trigger action.

- `SourceX`, `SourceY`: This is the top left corner of the area to move, on the previous level
- `TargetY`, `TargetX`: This is the top left corner of the area to move to, on the new level
- `AreaWidth`, `AreaHeight`: This is the size of the area to move.
- `locationName`: This is the (optional) location name. It defaults to the player's farmhouse and only need to be provided if you want to use this action on a different location. There are 2 special values:
    - `Here`: use current location
    - `Cellar`: use assigned cellar (though this seems oddly unreliable)

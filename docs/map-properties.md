# Map Properties

All map properties are also usable as entry in `Data/Location` `CustomFields`. In the case where both map property and `Data/Location` `CustomFields` exist, `CustomFields` are used.

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

#### mushymato.MMAP_WoodsLighting \<T|day color\> [T|night color] [true|false]

- Forces a certain ambiant day color (and optionally night color)
- Colors are inverted, much like for light, so that "Red" will give red ambiant light.
- For day color, `T` gives the default color #6987cd.
- For night color, `T` gives the default night color which is normally #0000ff but #0a1e55 in the winter.
- **Note:** woods lighting causes map lights to turn off at night, this mainly affects the `Light` map property and path layer lights. To keep your lights on at night you can either set the third argument to `false`, or use [mushymato.MMAP_Light](#mushymato.MMAP_Light) with these arguments:
    - `Light`: `mushymato.MMAP_Light 1 White <light index> 0 0 None` on the desired tile's TileData, Back or Front layer.
    - Path light: `mushymato.MMAP_Light 1 White 4 0 0 None` on the desired tile's TileData, Back or Front layer.

#### mushymato.MMAP_WoodsDebris T | [debrisKind] [gsq]

- Spawn leaves on this map and ignore global debris weather effects.
- When the value given is simply `T`, follow vanilla secret woods logic:
    - use summer leaves for spring and summer, fall leaves for fall
    - no leaves when raining or in winter
- When the value given is formatted like `-1 "RANDOM 0.5"` where -1 is `debrisKind` and `\"RANDOM 0.5\"` is a game state query
    - `debrisKind` determines kind of leaf
        - -2: use summer leaves for spring and summer, fall leaves for fall, winter snow particles for winter
        - -1: use seasonal debris
        - 0: spring leaves
        - 1: summer leaves
        - 2: fall leaves
        - 3: winter snow particles


#### mushymato.MMAP_SteamOverlay: \<T|texture\> [velocityX] [velocityY] [color] [alpha] [scale]

- Adds a tiling overlay drawn above the map.
- If `velocityX` and/or `velocityY` are given, move the texture by that many pixels every tick to create scrolling effect.
- By default, `color` is 80% white, `alpha` is 1 by default, and `scale` is 4 by default

#### mushymato.MMAP_WaterColor: \<color\> [color|T] [color|T] [color|T]

- Changes the current location's water overlay draw color, does not change the water texture.
- Can provide up to 4 colors, for each season.
- If you don't provide any color or write `T` in any slot, water will fall back to the spring color (first color) if provided.
- The vanilla seasonal colors are:
    - #3C647F7F spring
    - #1E787F7F summer
    - #7F41647F fall
    - #41287F7F winter

#### mushymato.MMAP_Panorama \<panoramaId\>

- Draw a parallax background behind the map.
- The `panoramaId` arguments refer to an entry in the `mushymato.MMAP/Panorama` custom asset, see [panorama docs](docs/panorama.md) for details.
- There are some some panoramas provided by MMAP that can be used out of the box.
    - `MMAP_MountainView`: shows seasonal sky with some animated clouds, mountains, sunset, and stars at night.
    - `MMAP_ClearSky`: like `MMAP_MountainView` but without mountains and clouds.
    - `MMAP_IslandHorizon`: shows the island ocean horizon with clouds background.

#### mushymato.MMAP_NightTime* \<time\> <a name="mushymato.MMAP_NightTime"></a>

There are 3 similar map properties for setting phases of day transitioning to night.
1. `mushymato.MMAP_NightTimeStarting`
2. `mushymato.MMAP_NightTimeModerate`
3. `mushymato.MMAP_NightTimeTruly`

- All 3 of these take time in the typical military time format (0600) used by many SDV things.
- The vanilla values are
    - starting: 1800 spring summer island, 1700 fall, 1500 winter
    - moderate: halfway between starting and truly
    - truly: starting + 200
- If you only set starting, the other two will be calculated according to vanilla logic.

There are also related Triggers for use in Data/TriggerActions
- `mushymato.MMAP_NightTimeStarting`: raised at night starting time
- `mushymato.MMAP_NightTimeModerate`: raised at night moderate time
- `mushymato.MMAP_NightTimeLightsOff`: raised 100 before night truly time, this is when lights turn off
- `mushymato.MMAP_NightTimeTruly`: raised at night truly time

And Game State Queries
- `mushymato.MMAP_TIME_IS_DAY`: true when time of day is less than night starting time.
- `mushymato.MMAP_TIME_IS_SUNSET`: true when time of day is during night starting and truly time.
- `mushymato.MMAP_TIME_IS_LIGHTS_OFF`: true when time of day is after window lights turn off and lamp lights turn on.
- `mushymato.MMAP_TIME_IS_NIGHT`: true when time of day is later than night truly time.
- `mushymato.MMAP_WINDOW_LIGHTS`: true when window lights should be on (e.g. `!mushymato.MMAP_TIME_IS_LIGHTS_OFF` and not raining).
- `mushymato.MMAP_RAINING_HERE`: true when current location is raining.

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

#### mushymato.MMAP_FarmHouseFurnitureRemove ALL|[\<X\> \<Y\>]+

- Farmhouse only, remove certain furniture from initial farmhouse (`Maps/FarmHouse`).
- Takes either a list of coordinates, or special value `ALL` to remove all furniture.

#### mushymato.MMAP_FarmHouseFurnitureAdd [\<furnitureId\> \<X\> \<Y\> \<rotate\>]+

- Farmhouse only, add certain furniture to the initial farmhouse (`Maps/FarmHouse`).
- Works just like `FarmHouseFurniture` which is on the farm map property rather than on the house.
- Should there be no BedFurniture after `mushymato.MMAP_FarmHouseFurnitureRemove` and `mushymato.MMAP_FarmHouseFurnitureAdd` are applied, a bed will be added to tile 9 8, just like vanilla.


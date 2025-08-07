### Tile Properties

Map properties can be set on the map or through building/furniture/floor path.

#### Back layer: mushymato.MMAP_AnimalSpot T

- For use in animal building maps.
- Changes what tiles the animals will start in.
- If building has less AnimalSpot tiles than animals, the remaining animals get random spots.
- 1 AnimalSpot tile will get 1 animal, 2 AnimalSpot next to each other means 2 animals get to start around that area.
- The spawn point of the animal is based on their top left tile, for 2x2 tile animals it's best to put this tile prop top left of where you want them to go.
- This tile property MUST be set on the actual map, building/furniture/floor path tile properties do not work.

#### Back layer: mushymato.MMAP_GrassSpread T

- If set, allow this tile to spread grass (without using `Diggable`).
- Ideally this is set on the tile sheet, rather than on a per tile data basis.
- This is disabled if the player has `bcmpinc.GrassGrowth` installed since that mod simply skips the `Diggable` check entirely.

#### Back layer: mushymato.MMAP_Paddy T|I

- If set, make this tile a paddy for paddy crops (such as rice). Does not make this tile diggable by itself.
- Ideally this is set on the tile sheet, rather than on a per tile data basis.
- Using `I` allows this tile to work on indoor pots on this tile as well.

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

#### Back Layer: mushymato.MMAP_TAS \<tasId\>+ <a name="mushymato.MMAP_TAS"></a>

- Add a temporary animated sprite at this tile.
- The layer depth is based on the tile position.
- The `tasId` arguments refer to an entry in the `mushymato.MMAP/TAS` custom asset, see [temporary animated sprites docs](docs/temporary-animated-sprites.md) for details.
- There are several actions related to this tile property, see [actions mushymato.MMAP_TAS](actions.md#mushymato.MMAP_TAS) for details.

#### Back Layer: mushymato.MMAP_Critter [\<critterType\> [type dependent args]]+ <a name="mushymato.MMAP_Critter"></a>

- Spawn a certain kind of simple critter on this tile. The positions are slightly randomized within the tile's bounds.
- Currently supports the following critter types:
    - Firefly: [color|T] [count]
    - Seagull: [texture|T] [count]
    - Crab: [texture|T] [count]
    - Birdie: [texture|<number>|T][:YOffset] [count]
        - The number option allows you to give a start index for `TileSheet/critters` which has birdies starting at 25, 45, 125, 135, 165, and 175.
        - When using T, the birdie start index will be picked with logic similar to base game.
        - YOffset is a change on Y axis to the birdy's position, e.g. `T:-128` to make birdie appear 2 tiles above it's tile position.
    - Butterfly: [texture|<number>|T] [count]
        - The number option allows you to give a start index for `TileSheet/critters` (WIP: figure out the indicies)
        - When using texture, the bufferfly will follow summer butterfly rules (4 frames)
        - When using T, the birdie start index will be picked base game logic.
    - Frog: [T|F] [count]
        - T makes frog face right, F makes frog face left. There's no option to change for a different texture.
    - LeaperFrog: [T|F] [count]
        - T makes frog face right, F makes frog face left. There's no option to change for a different texture.
        - This frog jumps farther into water.
    - Rabbit: [texture|T][:T|F]
        - When giving a texture, the first frame is the rabbit's standing frame, while the following 6 frames are the running frames
        - T makes rabbit face right, F makes rabbit face left, e.g. `loadedRabbitTexture:F` gives a rabbit using the specific texture and facing left. When not specified, the direction is random.
- You can use multiple sets of these args to spawn more critters on the same tile, e.g. `Crab T 3 Firefly T 8` for 3 crabs 8 fireflies on the tile.
- T as first argument is a placeholder and lets you use defaults.
- For critters that support a custom texture, they need to have same dimensions as the original, see `[CP] MMAP Examples/assets/critters` for example textures you can use as a base.
- There are several actions related to this tile property, see [actions mushymato.MMAP_Critter](actions.md#mushymato.MMAP_Critter) for details.

# Furniture Properties

You can give furniture tile properties (including action and tile actions) and collision using this feature. This would let you use all the previous tile properties/actions/touch actions listed with furniture, plus various vanilla properties and actions. Besides this, you can also give a collision map to make the furniture passable on some tiles and do animations via draw layers.

To give furniture some properties, add an entry to custom asset `mushymato.MMAP/FurnitureProperties`, where the key is an **unqualified item id** of a furniture item. See examples in [furniture_tile_property.json](../[CP]%20MMAP%20Examples/furniture_properties.json).

This asset is secretly reusing [buildings data](https://stardewvalleywiki.com/Modding:Buildings) but only a few fields are actually read and their meaning may not be the same. More may be added later, if they make sense.

Your furniture still needs to be [added to `Data/Furniture`](https://stardewvalleywiki.com/Modding:Furniture) first to make them a furniture item, before any `mushymato.MMAP/FurnitureProperties` can take effect. At the same time this means uninstalling MMAP won't turn your furniture into error items.

### What can you do with Furniture Properties?

Some of these features overlap with other framework mods, usually they don't conflict and you may use both together.

- Add furniture descriptions (`Description`).
- Add various tile actions and properties (`TileProperties`), which can be used for a variety of purposes like creating catalogues, functional jukeboxes, lights, etc.
- Add seats (`ActionTiles`).
- Create custom TV furniture.
- Create custom fish tank furniture.
- Make furniture obey seasons via `SeasonOffset`, compared to using the `{{Season}}` token, this feature respects the location's season override.
- Change furniture collision to make non-rugs passable (but not make rugs impassable).
- Various advanced drawing controls via `DrawLayers`, see also the [draw layers extension documentation](draw-layers.md).

## Fields Actually in Use

| Property | Type | Default | Notes |
| -------- | ---- | ------- | ----- |
| `Description` | string | _empty_ | Overrides the furniture description. |
| `TileProperties` | BuildingTileProperty | _empty_ | List of tile properties to apply on the furniture. |
| `AdditionalTilePropertyRadius` | int | 0 | Extra tile property radius, needed if any tile property should apply in a bound larger than the furniture's own bounding box. For example, having 1 in this field on a 1x1 furniture means the actual bounds checked is 3x3 starting with tile that's 1 left and 1 up from the furniture's placement tile. This radius also affects checks for shaking/open close anim. |
| `BuildMenuDrawOffset` | Point | 0,0 | For furniture, this field is used to adjust the position of their menu icons. Most useful if the draw layers result in a highly offset icon. |
| `CollisionMap` | string | _empty_ | Collision map string, e.g. `"XOX"` where `X` is impassable and `O` is passable. Rugs ignore this property. You can put other furniture on tiles marked O, but not objects/big craftables. |
| `SeasonOffset` | Point | 0,0 | Adjusts the source rectangle of the furniture plus any draw layers depending on the season. |
| `DrawLayers` | List\<BuildingDrawLayers\> | _empty_ | List of draw layers to show for this furniture, works very similar to [building draw layers](https://stardewvalleywiki.com/Modding:Buildings#Exterior_appearance), but the fields `OnlyDrawIfChestHasContents` and `AnimalDoorOffset` are not used. |
| `DrawShadow` | bool | false | Because furniture do not have draw shadows in the first place, this field is repurposed to mean "when there are draw layers, do not draw the base furniture sprite". |
| `ActionTiles` | List\<BuildingActionTiles\> _empty_ | List of special action tiles. | 
| `Metadata` | Dictionary\<string, string\> | _empty_ | MMAP's [building draw layer extension feature](../README.md#drawlayerext) can be used here too. |
| `CustomFields["TV"]` | string | _empty_ | Sets this furniture as a TV with screen defined as `[posX] [posY] [scale]`, can be used on any non-run furniture. |
| `CustomFields["FishTank"]` | string | _empty_ | Sets this furniture as a FishTank with capacity & tank bounds defined as `[capacity] [posX] [posY] [Width] [Height]`. |

#### TV and FishTankFurniture Quirks

- Only furniture that resolve to `Furniture` type and are not rugs is allowed to become a `TV` or a `FishTankFurniture` by furniture properties
- Because the typing is written into the save, the furniture  will remain `TV`/`FishTankFurniture` unless they are destroyed, i.e. data asset changes don't do anything.
- TV:
    - Position X/Y is relative to the bounding box of the TV and at 1x scale.
    - If you are counting pixels on the texture, multiply it by 4 (regardless of the scale argument).
- FishTank:
    - It is possible to make a fish tank without additional mod by using `fishtank` as the furniture type, this feature merely offers more customization.
    - Bounds X/Y/Width/Height is relative to the draw position of the tank.
    - If you are counting pixels on the texture, multiply it by 4.
    - Capacity is normally tile width - 1 but can be overriden as well via first arg. Setting it to -2 will default to that, setting it to -1 means unlimited.
    - The custom capacity value applies to both swiming and grounded entities. Decoration still depends on the tile width of the tank (unlimited if >2, 1 if <= 2).
    - Fish tank bounds will merge horizontally if they are [connected](connected-textures.md), and the fish will visually swim between tanks in that case (but still belong to a particular tank).
    - **WARNING**: Do not make a 1x1 footprint fish tank! It'll be placable on a table and you will lose all fish if you do that! (Also don't make a 1x1 dresser, same reason).

### TileProperties

(This is same as vanilla `BuildingData.TileProperties`, copied here for reference)

| Property | Type | Default | Notes |
| -------- | ---- | ------- | ----- |
| `Id` | string | _empty_ | Unique string id for this tile property. |
| `Name` | string | _empty_ | Tile property name, e.g. `"mushymato.MMAP_Critter"`. |
| `Value` | string | _null_ | Tile property value, e.g. `"Crab T 1"`. |
| `Layer` | string | _empty_ | Which map layer this tile property belongs to, e.g. `"Back"`. |
| `TileArea` | Rectangle | Rectangle.Empty | Which tiles this tile property should affect, relative to top left corner. |

### ActionTiles

(This is same as vanilla `BuildingData.ActionTiles`, copied here for reference)

Unlike vanilla these are not map tile actions and instead special interaction tied to the furniture.

| Property | Type | Default | Notes |
| -------- | ---- | ------- | ----- |
| `Id` | string | _empty_ | Unique string id for this action. |
| `Tile` | Point | 0,0 | Interaction point. |
| `Action` | string | _null_ | Tile action string, e.g. `Seat 0 -256 -1`. |

You can use these values in `Action`:
- `Seat [xOffset] [yOffset] [direction]`
    - Gives this furniture a seat.
    - The offset and direction affect farmer appearance appears once seated.
    - Having any `Seat` action tiles will override the vanilla seat logic.
    - Not compatible with furniture rotations atm.

### DrawLayers

(This is same as vanilla `BuildingData.BuildingDrawLayers`, copied here for reference)

| Property | Type | Default | Notes |
| -------- | ---- | ------- | ----- |
| `Id` | string | _empty_ | Unique string id for this draw layer. |
| `Texture` | string | _null_ | Texture asset name, if different than the base texture. |
| `SourceRect` | Rectangle | _null_ | Source rectangle of draw layer, when this layer is animated, only provide first frame. |
| `DrawPosition` | Vector2 | _null_ | An offset to apply to the draw position (relative to top left corner of furniture sprite). |
| `DrawInBackground` | bool | false | If this is true, draw with base layer depth = 0. |
| `SortTileOffset` | float | 0f | How much to adjust layer depth by, according to Y tile position based rules similar to buildings. A positive 1 will make this layer draw as if the furniture was placed 1 tile above its actual position, and a negative 1 does the opposite and makes this layer draw as if the furniture was placed 1 tile below its actual position. A very large negative value can achieve a "draw above (almost) everything" effect, and you can use floating point to achieve partial layer depth offsets. |
| `FrameDuration`| int | 90 | Number of miliseconds between animation frames. |
| `FrameCount`| int | 1 | Number of animation frames. |
| `FramesPerRow`| int | -1 | Number frames per row before wrapping arund, -1 for unlimited. |
| `OnlyDrawIfChestHasContents`| string | _empty_ | Unused. |
| `AnimalDoorOffset`| Point | 0,0 | Unused. |

`DrawLayers` are not affected by "Alternative Textures", should you attempt to use them together the MMAP draw layers will simply appear over/under AT's draw.

#### Using DrawLayers with Rotations

To make a particular draw layer only appear for a certain rotation, use an `Id` with this format (case insensitive): `.*_Rotation.(\d+)` where the number after `Rotation.` is the in-game rotation index. What number this will be depends on the kind of rotation but usually it starts counting up from 0. You can check current rotation in game with [lookup anything (datamining fields on)](https://www.nexusmods.com/stardewvalley/mods/541).

## Caveats with Rotations

If your furniture has rotations, the tile property bounds needs to also cover the rotated shape, e.g. use a 2x2 bound for a 2x1 furniture that can rotate to 1x2. The extra tile is not a problem since the furniture's (rotated) bounds are checked first.

Similarily when using collision map with rotations, you must specify the collision map for all rotations at once

Example for a 3x1 furniture that rotates to 1x3, and should be impassable only in the middle tile:
```
OXO
XOO
OOO
```

## Tile Property Support

(This section is mainly for C# modders hoping to implement their own tile properties)

Whether a particular Tile Property is supported depends on usage of `GameLocation.doesTileHaveProperty` which is where `Furniture.DoesTileHaveProperty` gets called.
Most vanilla properties do call this, but they might not actually take effect for other reasons such as tile being occupied by furniture or the overall map lacking a certain property.

### Vanilla Tile Properties Known to Not Work
- `Passable`
- `Diggable`
- `Water` requires the map to generally support water, i.e. `Outdoors` or `indoorWater` map properties.

### Modded Tile Properties
These MMAP tile properties are supported and guarenteed to update as soon as furniture is moved (this is also true of buildings tile properties).
- `mushymato.MMAP_TAS`
- `mushymato.MMAP_Light`
- `mushymato.MMAP_Critter`

These MMAP tile properties **do not** support usage with furniture.
- `mushymato.MMAP_AnimalSpot`: Never checks furniture or building tile properties, this is intentional.
- `mushymato.MMAP_GrassSpread`: Grass cannot spread onto a tile that is blocked by furniture, regardless of property.

Other modded tile properties depends on their implemenations, in particular whether they implement any sort of furniture changed check. For example, custom companion tile properties will only update when player leave and re-enter the map and thus do not work with this.

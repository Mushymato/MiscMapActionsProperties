# Furniture Properties

To give furniture some properties, add an entry to custom asset `mushymato.MMAP/FurnitureProperties`, where the key is an **unqualified item id** of a furniture item. See examples in [furniture_tile_property.json](../[CP]%20MMAP%20Examples/furniture_tile_property.json)

This asset is secretly reusing [buildings data](https://stardewvalleywiki.com/Modding:Buildings) but only a few fields are actually read. More may be added later, if they make sense.

### Fields Actually in Use

| Property | Type | Default | Notes |
| -------- | ---- | ------- | ----- |
| `TileProperties` | string | _empty_ | List of tile properties to apply on the furniture. |
| `AdditionalTilePropertyRadius` | int | 0 | Extra tile property radius, needed if any tile property should apply in a bound larger than the furniture's own bounding box. For example, having 1 in this field on a 1x1 furniture means the actual bounds checked is 3x3 starting with tile that's 1 left and 1 up from the furniture's placement tile. |
| `CollisionMap` | string | _empty_ | Collision map string, e.g. `"XOX"` where `X` is impassable and `O` is passable. |

#### TileProperties

(This is same as vanilla `BuildingData.TileProperties`, copied here for reference)

| Property | Type | Default | Notes |
| -------- | ---- | ------- | ----- |
| `Id` | string | _empty_ | Unique string id for this tile property. |
| `Name` | string | _empty_ | Tile property name, e.g. `"mushymato.MMAP_Critter"`. |
| `Value` | string | _null_ | Tile property value, e.g. `"Crab T 1"`. |
| `Layer` | string | _empty_ | Which map layer this tile property belongs to, e.g. `"Back"`. |
| `TileArea` | Rectangle | Rectangle.Empty | Which tiles this tile property should affect, relative to top left corner. |

### Caveats with Rotations

If your furniture has rotations, the tile property bounds needs to also cover the rotated shape, e.g. use a 2x2 bound for a 2x1 furniture that can rotate to 1x2. The extra tile is not a problem since the furniture's (rotated) bounds are checked first.

Similarily when using collision map with rotations, you must specify the collision map for all rotations at once

Example for a 3x1 furniture that rotates to 1x3, and should be impassable only in the middle tile:
```
OXO
XOO
OOO
```

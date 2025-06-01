# Furniture Properties

To give furniture some properties, add an entry to custom asset `mushymato.MMAP/FurnitureProperties`, where the key is an **unqualified item id** of a furniture item. See examples in [furniture_tile_property.json](../[CP]%20MMAP%20Examples/furniture_properties.json)

This asset is secretly reusing [buildings data](https://stardewvalleywiki.com/Modding:Buildings) but only a few fields are actually read. More may be added later, if they make sense.

### Fields Actually in Use

| Property | Type | Default | Notes |
| -------- | ---- | ------- | ----- |
| `TileProperties` | BuildingTileProperty | _empty_ | List of tile properties to apply on the furniture. |
| `AdditionalTilePropertyRadius` | int | 0 | Extra tile property radius, needed if any tile property should apply in a bound larger than the furniture's own bounding box. For example, having 1 in this field on a 1x1 furniture means the actual bounds checked is 3x3 starting with tile that's 1 left and 1 up from the furniture's placement tile. |
| `CollisionMap` | string | _empty_ | Collision map string, e.g. `"XOX"` where `X` is impassable and `O` is passable. Rugs ignore this property. |
| `Description` | string | _empty_ | Overrides the furniture description. |
| `DrawLayers` | List\<BuildingDrawLayers\> | _empty_ | List of draw layers to show for this furniture, works very similar to building draw layers, but the fields `OnlyDrawIfChestHasContents` and `AnimalDoorOffset` are not used. |
| `DrawShadow` | bool | false | Because furniture do not have draw shadows in the first place, this field is repurposed to mean "when there are draw layers, do not draw the base furniture sprite". |
| `CustomFields` | Dictionary\<string, string\> | _empty_ | MMAP's [building draw layer extension feature](../README.md#drawlayerext) can be used here too. |

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

### Tile Property Support

(This section is mainly for C# modders hoping to implement their own tile properties)

Whether a particular Tile Property is supported depends on usage of `GameLocation.doesTileHaveProperty` which is where `Furniture.DoesTileHaveProperty` gets called.
Most vanilla properties do call this, but they might not actually take effect for other reasons such as tile being occupied by furniture or the overall map lacking a certain property.

#### Vanilla Tile Properties known to Not Work
- `Passable`
- `Diggable`
- `Water` requires the map to generally support water, i.e. `Outdoors` or `indoorWater` map properties.

#### Modded Tile Properties
These MMAP tile properties are supported and guarenteed to update as soon as furniture is moved (this is also true of buildings tile properties).
- `mushymato.MMAP_TAS`
- `mushymato.MMAP_Light`
- `mushymato.MMAP_Critter`

This MMAP tile property **does not** support usage with furniture.
- `mushymato.MMAP_AnimalSpot`: Never checks furniture or building tile properties, this is intentional.
- `mushymato.MMAP_GrassSpread`: Grass cannot spread onto a tile that is blocked by furniture, regardless of property.

Other modded tile properties depends on their implemenations, in particular whether they implement any sort of furniture changed check. For example, custom companion tile properties will only update when player leave and re-enter the map.

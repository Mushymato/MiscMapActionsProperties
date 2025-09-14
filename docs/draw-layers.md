### Draw Layers

You can extend `DrawLayers` found on `Data/Buildings` and in `mushymato.MMAP/FurnitureProperties` with some special `Metadata` fields.
For info on building draw layers in vanilla see [wiki documentation](https://stardewvalleywiki.com/Modding:Buildings#Exterior_appearance).

### mushymato.MMAP/DrawLayer.{DrawLayerId}.{override} <a name="drawlayerext"></a>

Various draw layer overriding fields, can be used with regular draw layer things.
These also work with draw layers added via [furniture properties](furniture-properties.md).
At the moment, these Metadata fields do not work for building skins (mainly because draw layers are also not settable per building skins).

#### string values

- `mushymato.MMAP/DrawLayer.{DrawLayerId}.condition`: A [Game State Query](https://stardewvalleywiki.com/Modding:Game_state_queries) to determine if this layer should draw. Rechecked on time changed (e.g. 10 in game minutes).
- `mushymato.MMAP/DrawLayer.{DrawLayerId}.color`: Draw color for the layer, can use hex or [named color](https://docs.monogame.net/api/Microsoft.Xna.Framework.Color.html), white by default.

#### float values

These are all support passing either `0.1` float, or `"0.1 0.4"` for random value between first and second.

- `mushymato.MMAP/DrawLayer.{DrawLayerId}.alpha`: transparency (0 to 1)
- `mushymato.MMAP/DrawLayer.{DrawLayerId}.rotate`: rotation (0 to 6.28318), around the origin.
- `mushymato.MMAP/DrawLayer.{DrawLayerId}.rotateRate`: rotation change per second, positive is clockwise, negative is counter clockwise.
- `mushymato.MMAP/DrawLayer.{DrawLayerId}.scale`: draw scale (default 4f)
- `mushymato.MMAP/DrawLayer.{DrawLayerId}.shake`: collision shake amount when player walks through (default 0f). Does not sync across multiplayer. This radius is affected by `AdditionalTilePropertyRadius` on building and furniture property data.

#### Vector2 values

These are Vector2 coordinates, takes 2 integers like `"0 0"`.

- `mushymato.MMAP/DrawLayer.{DrawLayerId}.origin`: defines the origin of the sprite. Mainly relevant if you also use rotate, but can act as a secondary offset for the draw layer.

#### enum values

These take specific strings, listed for each option.

<a name="openAnim"></a>

- `mushymato.MMAP/DrawLayer.{DrawLayerId}.effect`: sprite effects for flipping the sprite
    - `"None"`: no effect
    - `"FlipHorizontally"`: flip sprite horizontally
    - `"FlipVertically"`: flip sprite vertically
- `mushymato.MMAP/DrawLayer.{DrawLayerId}.openAnim`: control draw layer animation cycle,
    - `"None"`: no effect (draw layer animation plays automatically on loop)
    - `"Auto"`: draw layer anim proceeds or reverses depending on player proximity. For backwards compatibility, `"true"` is also accepted and will resolve to `"Auto"`.
    - `"Manual"`: draw layer anim proceeds depending on player interaction with tile/touch actions

##### Details about openAnim

When using openAnim, the animation will progress across 4 states, closed -> opening -> opened -> closing (and then return to closed).

If the draw layer has `FrameCount=4` (and thus has frames 0 1 2 3):
- Closed: frame 0, held steady
- Opening: frames 0 1 2 3
- Opened: frame 3, held steady
- Closing: frames 3 2 1 0

`"Auto"`: The radius of player proximity is controlled by `AdditionalTilePropertyRadius` (on building data and [furniture properties](furniture-properties.md))
`"Manual"`: The opening and closing states are toggled by tile/touch action.
- Buildings: `mushymato.MMAP_BuildingDrawLayerToggle <furnitureId> <drawLayerIdPrefix>`
- Furniture: `mushymato.MMAP_FurnitureDrawLayerToggle <furnitureId> <drawLayerIdPrefix>`

### mushymato.MMAP/ChestLight.{ChestName}: [radius] [color] [type|texture] [offsetX] [offsetY]

- Buildings only.
- Add a light source at the building's tileX/tileY position, only lights up if corresponding building chest has content.
- Radius controls size of light, 2 by default.
- Can use hex or [named color](https://docs.monogame.net/api/Microsoft.Xna.Framework.Color.html), white by default
- Colors are inverted before being passed to light, so that "Red" will give red light.
- `type|texture` is either a light id (1-10 except for 3) or a texture (must be loaded).
- Use `offsetX` and `offsetY` to further adjust the position of the light.

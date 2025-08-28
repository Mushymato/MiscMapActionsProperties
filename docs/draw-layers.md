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

#### SpriteEffects values

SpriteEffects, one of "None", "FlipHorizontally", "FlipVertically"

- `mushymato.MMAP/DrawLayer.{DrawLayerId}.effect`: sprite effects for flipping the sprite

#### Bool values

These are simply `true` or `false`.

- `mushymato.MMAP/DrawLayer.{DrawLayerId}.openAnim`: When true, instead of always playing this layer's animation, control it  with collision with the building/furniture bounding box to achieve automatically opening doors. Does not sync across multiplayer.

### mushymato.MMAP/ChestLight.{ChestName}: [radius] [color] [type|texture] [offsetX] [offsetY]

- Buildings only.
- Add a light source at the building's tileX/tileY position, only lights up if corresponding building chest has content.
- Radius controls size of light, 2 by default.
- Can use hex or [named color](https://docs.monogame.net/api/Microsoft.Xna.Framework.Color.html), white by default
- Colors are inverted before being passed to light, so that "Red" will give red light.
- `type|texture` is either a light id (1-10 except for 3) or a texture (must be loaded).
- Use `offsetX` and `offsetY` to further adjust the position of the light.

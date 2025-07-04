# Panorama

Panorama defines what goes behind a map in a way independent of map tiles, there are some hardcoded in C# examples of this vanilla (summet, island north, submarine).

MMAP panoramas can be used by map property or custom field `mushymato.MMAP_Panorama <panoramaId>`

There are some some panoramas provided by MMAP that can be used out of the box.
    - `MMAP_MountainView`: shows seasonal sky with some animated clouds, mountains, sunset, and stars at night.
    - `MMAP_ClearSky`: like `MMAP_MountainView` but without mountains and clouds.
    - `MMAP_IslandHorizon`: shows the island ocean horizon with clouds background.

### Structure

| Property | Type | Default | Notes |
| -------- | ---- | ------- | ----- |
| `BackingDay` | List<BackingData> | _null_ | List of [backing](panorama.md#backing) data for daytime. |
| `BackingSunset` | List<BackingData> | _null_ | List of [backing](panorama.md#backing) data for sunset. |
| `BackingNight` | List<BackingData> | _null_ | List of [backing](panorama.md#backing) data for night. |
| `ParallaxLayers` | List<ParallaxLayerData> | _null_ | List of [parallax](panorama.md#parallax) data. |
| `BackingNight` | List<BackingData> | _null_ | List of [parallax](panorama.md#parallax) data. |
| `OnetimeTAS` | List<MapWideTAS> | _null_ | List of [parallax](panorama.md#tas) data. |
| `BackingNight` | List<BackingData> | _null_ | List of [parallax](panorama.md#tas) data. |
| `BasedOn` | string | _null_ | If this is set, replace any null values in this panorama with the values from another panorama. Can only do 1 layer, i.e. the BasedOn may not have BasedOn. To clear a specific value, set them to empty list (`[]`). |

MMAP's panorama system is separated into 3 main layers, which will be explained in each section.

## Backing

This is the bottom layer, usually used to draw plain color or 1 static texture that stretches for the whole viewport. It does not move with the player.

It is further divided into 3 sections:
1. `BackingDay`
2. `BackingSunset`
3. `BackingNight`

Day starts fading into Night at 1800 (usually) and finishes at 2000, while sunset fades in during 1800-1900 and fades out during 1900-2000.
The timing can be set with [mushymato.MMAP_NightTime*](../README.md#mushymato.MMAP_NightTime)

Only 1 backing can be active for day sunset and night respectively. This is decided by `Condition` and the first entry in the list with null or truthy `Condition` is used, and `Condition` is checked when the player enters a map with panorama defined.

If `BackingDay` is the only defined entry, time of day changes will not be applied, and the day background is displayed at all times.

See [here](sunset.mp4) for example of the day to night transition.

### Structure

| Property | Type | Default | Notes |
| -------- | ---- | ------- | ----- |
| `Id` | string | **required** | Unique string id |
| `Condition` | string | _null_ | Game state query, the first backing with a null or truthy Condition will be chosen. |
| `Texture` | string | _null_ | Texture name, drawn stretched across entire viewport. |
| `SourceRect` | `Rectangle` | _null_ | Portion of texture to draw. |
| `Color` | `Color` | _null_ | If texture is null, draw a pure color, otherwise draw the texture with this color. |

## Parallax

This is the middle layers, parallax refers to how the background moves as player moves. You can have multiple parallax layers, and they will always be drawn back to front (i.e. the first parallax entry displays beneath all other layers). All layers with null or truthy `Condition` is shown, and `Condition` is checked when the player enters a map with panorama defined.

### Structure

| Property | Type | Default | Notes |
| -------- | ---- | ------- | ----- |
| `Id` | string | **required** | Unique string id |
| `Condition` | string | _null_ | Game state query, all matching parallax layers will display. |
| `Texture` | string | _null_ | Texture name, drawn stretched across entire viewport. |
| `SourceRect` | `Rectangle` | _null_ | Portion of texture to draw. |
| `Color` | `Color` | _null_ | Color overlay to apply when drawing texture. |
| `Scale` | float | 4 | Scale to draw at. |
| `Alpha` | float | 1 | A number to multiply color by, 0.8 would make it 20% transparent. |
| `AlignX` | ParallaxAlignMode | `"Start"` | Forces the layer to align to the left of the viewport (`"Start"`), or right of viewport (`"End"`). |
| `AlignY` | ParallaxAlignMode | `"End"` | Forces the layer to align to the top of the viewport (`"Start"`), or bottom of viewport (`"End"`). Default `"End"`. |
| `DrawOffset` | Vector2 | 0,0 | Flat numeric draw offset from the aligned position. |
| `DrawPercentOffset` | Vector2 | 0,0 | Offset based on a percent of the viewport, i.e. `0.1,-0.05` on a 1280x720 screen translates to `128,-36` draw offset. |
| `DrawViewportOffset` | Vector2 | 0,0 | Offset based on the viewport position, this is what implements parallax movement. |
| `RepeatX` | bool | false | If true, repeat the texture horizontally. |
| `RepeatY` | bool | false |  If true, repeat the texture vertically. |
| `Velocity` | Vector2 | 0,0 | Moves the layer by this many pixels every tick to create a scrolling effect, best used with `RepeatX` or `RepeatY`. |
| `ShowDuring` | ShowDuringMode | `"Any"` | Makes this parallax layer only show during some time of day, and fade out according to same rules as the backing layers. Valid values are `"Day"`, `"Sunset"`, `"Night"`, and `"Any"` (show all day). |

Note: When using `ShowDuring`, it's recommended to arrange your layers in the order of `"Any"`, `"Day"`, `"Night"`, `"Sunset"`, this will ensure the sunset layer is drawn over both day and night, otherwise it might be hidden.

## Temporary Animated Sprite <a name="tas"></a>

Temporary animated sprites (TAS) are used for active elements of the background, such as clouds. See [temporary animated sprites docs](docs/temporary-animated-sprites.md) for details on defining a temporary animated sprite.

For panorama, there are 2 lists of TAS, onetime and respawning. You can put temporary animated sprite with interval in onetime without having them respawn since the list kind takes priority.

The initial position where TAS spawn at is defined relative to the size of the map's Back layer. This is defined by 4 floats that represent percentage: `XStart`, `XEnd`, `YStart`, `YEnd`.

For example if TAS is defined to be allowed to spawn from `XStart` 0.25 to `XEnd` 0.75 while `YStart` and `YEnd` are left at their default values of 0 and 1, that means the TAS can appear between 25% and 75% of the map horizontally, and anywhere on the vertical axis.

### Structure

| Property | Type | Default | Notes |
| -------- | ---- | ------- | ----- |
| `Id` | string | **required** | Unique string id |
| `Condition` | string | _null_ | Game state query, all matching parallax layers will display. |
| `TAS` | List<string> | _null_ | List of string TAS ids. |
| `Count` | int | 1 | Number of times to spawn each sprite in the list. |
| `XStart` | float | 0f | Percent of X axis where TAS can begin to spawn. |
| `XEnd` | float | 1f | Percent of X axis where TAS can no longer spawn. |
| `YStart` | float | 0f | Percent of Y axis where TAS can no begin to spawn. |
| `YEnd` | float | 1f | Percent of Y axis where TAS can no longer spawn. |

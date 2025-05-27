# Temporary Animated Sprites

Temporary sprites defined here can be used with tile property [mushymato.MMAP_TAS](../README.md#mushymato.MMAP_TAS) and [panorama](docs/panorama.md).

To add your own temporary animated sprite, edit `mushymato.MMAP/TAS`. See examples in [tas_spot.json](../[CP]%20MMAP%20Examples/tas_spot.json)

Some temporary animated sprites are provided by default by this mod:
- `MMAP_Cloud1`, `MMAP_Cloud2`, and `MMAP_Cloud3`: clouds for the mountain view panorama.

## Structure

| Property | Type | Default | Notes |
| -------- | ---- | ------- | ----- |
| `Id` | string | **required** | Unique string id |
| `Condition` | string | _null_ | Game state query, show TAS if true (or null). |
| `Texture` | string | **required** | Texture asset name. |
| `SourceRect` | Rectangle | **required** | Area of texture to draw. |
| `Interval` | float | 100 | Time between frames, in miliseconds. |
| `Frames` | int | 1 | Length of the animation. |
| `Loops` | int | 0 | Number of times to repeat the animation. |
| `PositionOffset` | Vector2 | Vector2.Zero | Offset added to position during draw. |
| `Flicker` | bool | false | Skips drawing every other frame. |
| `Flip` | bool | false | Horizontally flip the sprite during draw. |
| `SortOffset` | float | 0f | Offset on layer depth, for determining whether this sprite appear over or under other sprites. |
| `AlphaFade` | float | 0f | Amount of additional transparency every frame. Set this to make the sprite fade away over time. |
| `Scale` | float | 1f | Draw scale, applied on top of the default 4x scale. |
| `ScaleChange` | float | 0f | Amount of additional scale every frame. Set this to make sprite enlarge/shrink over time. |
| `Rotation` | float | 0f | Amount of rotation on the sprite. |
| `RotationChange` | float | 0f | Amount of additional rotation every frame. Set this to make the sprite spin. |
| `Color` | string | _null_ | Color to apply on draw, for use with grayscale sprites.<br>Aside from RGB and hex values, monogame accepts [named colors](https://docs.monogame.net/api/Microsoft.Xna.Framework.Color.html). |
| `ScaleChangeChange` | float | 0 | A change upon `ScaleChange`, i.e. acceleration but for scale. |
| `Motion` | Vector2 | 0,0 | Amount of movement in pixels to do on X and Y axis each tick. |
| `Acceleration` | Vector2 | 0,0 | Amount of increase in motion on X and Y axis each tick. |
| `AccelerationChange` | Vector2 | 0,0 | Amount of increase in acceleration on X and Y axis each tick. |
| `LayerDepth` | Vector2 | 0,0 | Absolute layer depth, but layer depth from position may still apply. |
| `Alpha` | float | 1f | Multiplier on Color, i.e. 0 is transparent. |
| `PingPong` | bool | false | Makes animation frames go 0 1 2 3 2 1 0 instead of 0 1 2 3 0 1 2 3. |
| `SpawnInterval` | double | -1 | How many miliseconds before a new copy of this TAS should be spawned, ideally used with some randomization. |
| `SpawnDelay` | double | -1 | How many miliseconds before the first copy of this TAS should be spawned, `SpawnInterval` begins counting after `SpawnDelay` elapses. |
| `RandMin` | TASExtRand | _null_ | Model used for randomizing certain fields. |
| `RandMax` | TASExtRand | _null_ | Model used for randomizing certain fields. |

## Randomization

Some of the above fields can be randomized with an offset chosen from a range. These must be defined via `RandMin` and `RandMax`.

For example, this causes the `Motion` of the temporary animated sprite to have a random value between `-3,-3` and `5,5` every time it (re)spawns.
```js
// Content Patcher "Changes" entry
{
    "Action": "EditData",
    "Target": "mushymato.MMAP/TAS",
    "Entries": {
        "{{ModId}}_Example": {
            // Non-random fields omitted for brevity
            ...
            // Random is most useful when the TAS spawns more than once, which SpawnInterval will do
            "SpawnInterval": 100,
            // Have this amount of base motion
            "Motion": "1,1"
            // Randomize amount of motion of this TAS
            "RandMin": {
                "Motion": "-4,-4"
            },
            "RandMax": {
                "Motion": "4,4"
            }
        }
    }
}
```

You can randomize these fields:

| Property | Default |
| -------- | ------- |
| `SortOffset` | 0 |
| `Alpha` | 0 |
| `AlphaFade` | 0 |
| `Scale` | 0 |
| `ScaleChange` | 0 |
| `ScaleChangeChange` | 0 |
| `Rotation` | 0 |
| `RotationChange` | 0 |
| `Motion` | 0,0 |
| `Acceleration` | 0,0 |
| `AccelerationChange` | 0,0 |
| `PositionOffset` | 0,0 |
| `SpawnInterval` | 0 |
| `SpawnDelay` | 0 |

# Map Overrides

This feature allows mods to make map changes that immediately take effect via trigger actions. These map changes are sync'd in multiplayer, and persists until it is removed.

## Data Format

To define map overrides, edit `mushymato.MMAP/MapOverrides` and add data like this:

```json
{
  "Action": "EditData",
  // Custom asset defining map overrides
  "Target": "mushymato.MMAP/MapOverrides",
  "Entries": {
    // Your map override id
    "{{ModId}}_MyOverride": {
      // REQUIRED
      // Id must be same as key
      "Id": "{{ModId}}_MyOverride",
      // This is a loaded map asset
      "SourceMap": "Maps/{{ModId}}_MyOverridePatch",
      // OPTIONAL
      // The source area to patch from
      // If not provided, then the entire source map is used
      "SourceRect": {
        "X": 0,
        "Y": 0,
        "Width": 3,
        "Height": 4
      },
      // The target area to patch to
      // If not provided, then the patch will be applied to 0,0
      // Currently the Width and Height are optional, they are always equal to the source rect
      "TargetRect": {
        "X": 10,
        "Y": 10,
        "Width": 3,
        "Height": 4
      },
      // Value used to order the map patches, lower precedence apply first and get covered by higher precedence maps
      // If this is less than 0, the patch applies BEFORE vanilla map modifications in MakeMapModifications
      // When 2 map patches have same precedence, Id is used as tiebreaker
      "Precedence": 0,
      // If this is true, the area is cleared before the map patch is applied
      // Any objects/furniture there will be sent to lost and found
      "ClearTargetRectOnApply": false
    },
  }
}
```

## Usage via mushymato.MMAP_UpdateMapOverride <location> [[mode] [mapOverrideId]]

Can be used as one of `Action`, `TouchAction`, or `TriggerAction`.
Applies the given map overrides.

The arguments are
- `location`: Either `Here` or a location name.
- `mode`: Either `+` for adding an override, or `-` for removing an override.
- `mapOverrideId`: The map override id set in the data asset.

You can give an arbitrary number of `mode` and `mapOverride` pairs. It is recommended to use this action one time and apply all the map override changes you want, instead of calling this action many times.

The applied overrides are tracked per location on the mod data, and persist until they are removed.

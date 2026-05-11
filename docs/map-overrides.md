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
      // If this is true, calculate the target from a reference tile
      // - for tile actions, this would be the tile the action lives on
      // - for trigger actions, this would be the player's current tile
      "TargetRectIsRelative": false,
      // optional, list of tiles to remove before applying this map override
      // this will respect the relative TargetRect if TargetRectIsRelative=true
      "TileRemoveRects": [
        {
          // This field supports regex
          // ".*" - remove all layer
          // "Back\\d*" - remove layers whose name starts with Back
          "Layer": "Back",
          "TileArea": {
            "X": 7,
            "Y": 7,
            "Width": 3,
            "Height": 4
          }
        }
      ],
      // Value used to order the map patches, lower precedence apply first and get covered by higher precedence maps
      // If this is less than 0, the patch applies BEFORE vanilla map modifications in MakeMapModifications
      // When 2 map patches have same precedence, Id is used as tiebreaker
      "Precedence": 0,
      // If this is true, the area is cleared before the map patch is applied
      // Any objects/furniture there will be sent to lost and found
      "ClearTargetRectOnApply": false,
      // If this is true, expand the map right and down needed when applying this map override.
      "ResizeMapIfNeeded": false,
      // Instead of adding new tilesheet as needed, this forces your map override to use a
      // pre-existing tileset with the same name, even if the assets are different
      "ForceTilesheetMatch": false,
      // Renovation Model, null if this map override is not meant for renovations
      "Renovation" {
        // game state query for checking if the location can have this map override, e.g.
        // "mushymato.MMAP_MAP_NAME Here Maps/Shed" checks if the location's map asset is "Maps/Shed"
        // "LOCATION_NAME Here Shed" checks if the location name is Shed
        "TargetLocationCondition": "mushymato.MMAP_MAP_NAME Here Maps/Shed",
        // cost
        "Price": 0,
        // game state query required for adding this override
        "AddCondition": null,
        // game state query required for removing this override, FALSE means this can never be removed
        "RemoveCondition": null,
        // optional list of rectangles that this renovation is expected to change
        // when not given, the TargetRect is used
        "TargetRectGroup": null,
        // whether to check for objects in the target rect before allowing renovation
        "CheckForObstructions": true,
        // These are display strings for the shop entry
        // Adding:
        "AddDisplayName": "Add Chimkins",
        "AddDescription": "Add the Chimkins",
        "AddPlacementText": "Chimkin goes here",
        // Removal:
        "RemoveDisplayName": "Remove Chimkins",
        "RemoveDescription": "Remove the Chimkins",
        "RemovePlacementText": "No more chimkins here (or is it???)",
      }
    }
  },
}
```

## Usage via mushymato.MMAP_UpdateMapOverride \<location\> [[mode] [mapOverrideId]]

Can be used as one of `Action`, `TouchAction`, or `TriggerAction`.
Applies the given map overrides.

The arguments are
- `location`: Either `Here` or a location name.
- `mode`: Either `+` for adding an override, or `-` for removing an override.
- `mapOverrideId`: The map override id set in the data asset.

Special case: if the first mode argument is `RemoveAll`, then every applied map override is removed.

You can give an arbitrary number of `mode` and `mapOverride` pairs. It is recommended to use this action one time and apply all the map override changes you want, instead of calling this action many times.

The applied overrides are tracked per location on the mod data, and persist until they are removed.

## Game State Query: mushymato.MMAP_HAS_MAP_OVERRIDE \<location\> [mapOverrideId]+

Checks if this location has a given map override applied.

## Renovations

You can use a MMAP map override as a renovation, i.e. something the player can access through a menu.

To do this, you need to provide the `"Renovation"` data which will define what location(s) can have this renovation and what text it'll display.
Unlike vanilla renovations, 1 MMAP map override automatically covers both adding and removing entries.

To open the renovation menu, use tile/touch/trigger action `mushymato.MMAP_ShowRenovations <Here|locationName>`.

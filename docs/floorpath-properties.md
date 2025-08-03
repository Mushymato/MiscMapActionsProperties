### Floor Path Properties

You can give paths (`Data/FloorsAndPaths`) tile properties (including action and tile actions) using this feature. This would let you use all the previous tile properties/actions/touch actions listed with furniture, plus various vanilla properties and actions.

To give floor and path some properties, add an entry to custom asset `mushymato.MMAP/FloorPathProperties`. Example:
```js
{
  "Action": "EditData",
  "Target": "mushymato.MMAP/FloorPathProperties",
  "Entries": {
    // unqualified Data/FloorsAndPaths Id
    "{{ModId}}_FloorTerrain": {
      // Layer
      "Back": {
        // property: value
        "mushymato.MMAP_Light": "1 White 4",
        "mushymato.MMAP_LightCond": "mushymato.MMAP_TIME_IS_LIGHTS_OFF"
      },
      "Buildings": {
        "Action": "Message Test"
      }
    }
  }
}
```

Your flooring/paths still needs to be [added to `Data/FloorsAndPaths`](https://stardewvalleywiki.com/Modding:Floors_and_Paths) (and make a corresponding path object item) first to make them a floor/path item, before any `mushymato.MMAP/FloorPathProperties` can take effect.

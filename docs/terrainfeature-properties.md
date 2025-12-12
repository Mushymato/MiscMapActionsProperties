## Terrain Feature Properties

This page covers various terrain features that can be given tile properties.

### Floors and Paths

You can give paths ([`Data/FloorsAndPaths`](https://stardewvalleywiki.com/Modding:Floors_and_Paths)) tile properties (including action and tile actions) using this feature.

Your flooring/paths still needs to be [added to `Data/FloorsAndPaths`](https://stardewvalleywiki.com/Modding:Floors_and_Paths) (and make a corresponding path object item) first to make them a floor/path item, before any `mushymato.MMAP/FloorPathProperties` can take effect.

To give floor and path some properties, add an entry to custom asset `mushymato.MMAP/FloorPathProperties`. Example:
```js
{
  "Action": "EditData",
  "Target": "mushymato.MMAP/FloorPathProperties",
  "Entries": {
    // unqualified Data/FloorsAndPaths Id
    // if you are editing a non vanilla path, it's best to initialize the entries first in separate edit.
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

### Wild Trees

You can give wild trees ([`Data/WildTrees`](https://stardewvalleywiki.com/Modding:Wild_trees)) tile properties (including action and tile actions) using this feature.

The key has this format: `<treeId> <growthStage> <flipped>`
- `treeId`: the exact ID in `Data/WildTrees`
- `growthStage`: growth stage, from 0 to 5
- `flipped`: trees are randomly flipped on planting, `T` targets not flipped, `F` targets flipped

You can omit `growthStage` and `flipped` if you want to target all trees.

Example, including initialization best practices.

```js
{
  "Action": "EditData",
  "Target": "mushymato.MMAP/FloorPathProperties",
  "Entries": {
    {
      "Action": "EditData",
      "Target": "mushymato.MMAP/WildTreeProperties",
      "Entries": {
        // format is space separated: <treeid> <growthstage> <T|F where F means flipped and T means not flipped>
        // can use quotes for treeid if it already contains space
        // can omit growthstage and flipped/not flipped if not important
        // for vanilla trees, initialize it like this first, in case of multiple mods editing
        "3 5 T": {},
        "3 5 F": {}
      },
      "Priority": "Early"
    },
    {
      "Action": "EditData",
      "Target": "mushymato.MMAP/WildTreeProperties",
      "Fields": {
        // for vanilla trees, initialize it like this first, in case of multiple mods editing
        "3 5 T": {
          "Back": {}
        },
        "3 5 F": {
          "Back": {}
        }
      },
      "Priority": "Early"
    },
    {
      "Action": "EditData",
      "Target": "mushymato.MMAP/WildTreeProperties",
      "TargetField": [
        "3 5 T",
        "Back"
      ],
      "Entries": {
        // property: value
        "mushymato.MMAP_Light": "1 White {{ModId}}/treelight 0 -160",
        "mushymato.MMAP_LightCond": "mushymato.MMAP_TIME_IS_LIGHTS_OFF"
      },
      "Buildings": {
        "Action": "Message Test"
      }
    },
  }
}
```

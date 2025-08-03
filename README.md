# Misc Map Actions & Properties

Adds a few map related features, no strong design theme just whatever I happen to want.

**This is a framework mod for other mods to use, installing it by itself does nothing for players.**

## Documentation

### General

These pages document the "entry points" to various features.

- [Actions](docs/actions.md)
- [Map Properties](docs/map-properties.md)
- [Tile Properties](docs/tile-properties.md)

### Specialized

These pages document custom assets needed by some features.

- [Furniture Properties](docs/furniture-properties.md)
- [Floor Path Properties](docs/floorpath-properties.md)
- [Draw Layers](docs/draw-layers.md)
- [Panorama](docs/panorama.md)
- [Question Dialogue](docs/question-dialogue.md)
- [Temporary Animated Sprites](docs/temporary-animated-sprites.md)

## Usage Samples (with Content Patcher)

Much of this framework depend on sourcing data from various aspects of map and other custom assets, for convenience here are some examples using [content patcher](https://www.nexusmods.com/stardewvalley/mods/1915).

You can also consult [\[CP\] MMAP Examples](%5BCP%5D%20MMAP%20Examples) for examples involving specific features.

<details>

<summary><b>Click to Expand</b></summary>

### EditMap

You may do this type of EditMap add prop to MapTiles in the tmx directly too.

#### Adding a tile action

```json
{
  // 
  "Action": "EditMap",
  "Target": "Maps/<YOUR MAP HERE>",
  "MapTiles": [
    {
      // put your X/Y coord here
      "Position": { "X": 99, "Y": 99 },
      "Layer": "Buildings",
      "SetProperties": {
        "Action": "ACTIONNAME ARG1 ARG2"
      },
    }
  ]
},
```

#### Adding a touch action

```json
{
  "Action": "EditMap",
  "Target": "Maps/<YOUR MAP HERE>",
  "MapTiles": [
    {
      // put your X/Y coord here
      "Position": { "X": 99, "Y": 99 },
      "Layer": "Back",
      "SetProperties": {
        "TouchAction": "ACTIONNAME ARG1 ARG2"
      },
    }
  ]
},
```

#### Adding map property

```json
{
  "Action": "EditMap",
  "Target": "Maps/<YOUR MAP HERE>",
  "MapProperties": {
    "MAPPROPERTYNAME": "ARGUMENTS",
  }
},
```

### EditData

### EditData + [Data/Buildings](https://stardewvalleywiki.com/Modding:Buildings)

```json
{
  "Action": "EditData",
  "Target": "Data/Buildings",
  "Entries": {
    "{{ModId}}_YOUR_BUILDING": {
      // other building stuff
      "TileProperties": [
        {
          "Id": "{{ModId}}_ACTIONNAME",
          "Layer": "Buildings", // or Back
          "Name": "Action", // or TouchAction
          "Value": "ACTIONNAME ARG1 ARG2",
          // put your X/Y coord here, relative to the building's top left bound
          // setting width and height to greater than 1 will set property for multiple tiles
          "TileArea": {
            "X": 0,
            "Y": 0,
            "Width": 1,
            "Height": 1
          }
        }
      ],
    }
  },
}
```

#### EditData + [MMAP Furniture Properties](furniture-properties.md)

```json
{
  "Action": "EditData",
  "Target": "mushymato.MMAP/FurnitureProperties",
  "Entries": {
    "{{ModId}}_YOUR_FURNITURE": {
      "TileProperties": [
        {
          "Id": "{{ModId}}_ACTIONNAME",
          "Layer": "Buildings", // or Back
          "Name": "Action", // or TouchAction
          "Value": "ACTIONNAME ARG1 ARG2",
          // put your X/Y coord here, relative to the furniture's top left bound
          // setting width and height to greater than 1 will set property for multiple tiles
          "TileArea": {
            "X": 0,
            "Y": 0,
            "Width": 1,
            "Height": 1
          }
        }
      ],
    }
  },
}
```

#### EditData + [MMAP Furniture Properties](floorpath-properties.md)

```json
{
  "Action": "EditData",
  "Target": "mushymato.MMAP/FloorPathProperties",
  "Entries": {
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
  },
}
```

</details>


## Updates

Because this is a framework mod, you as a mod user don't generally need to update it unless you are installing a new mod that depends on a feature only implemented in newer version of MMAP. Bugs do occur as I develop features, and generally it is not end of the world. Please [report your issue with a log](report your problem with a log), then roll back to a previous version of mod while I fix things.

I have gotten reports of performance problems and worked on improving that from versions 1.9.3 to 1.9.5, if you experiance slowness do try to update first.

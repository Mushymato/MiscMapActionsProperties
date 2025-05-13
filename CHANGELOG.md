# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.7.0]

### Changed
- Some implementation details around tile data cache
- mushymato.MMAP_ShiftContents now runs on the host to avoid ghost location issues
    - "Cellar" will now pick the player's own Cellar, though assignments don't seem to quite work in multiplayer.

### Added
- New tile action/trigger action mushymato.MMAP_ShowBag, opens a global inventory.
- New furniture properties system that allows (some) tile properties to take effect for furniture.

## [1.6.0]

### Removed
- mushymato.MMAP_HoeDirt is removed due to strange visual bugs.

### Changed
- Panorama implementation adjusted to use DrawPercentOffset and DrawViewportOffset instead of the somewhat oblique formula from previous
- ShowDuring=Day also fades out according to day night cycle.

### Added
- mushymato.MMAP_Critter now supports Butterflies

### Fixed
- mushymato.MMAP_SteamOverlay position method changed to account for event issues

## [1.5.4]

## Fixed

- Fail with more information on the reverse transpilers

## [1.5.3]

### Added

- mushymato.MMAP_Critter now supports Birdies

### Fixed

- Let question dialogue work when nested
- Changed some draws to use Render events

## [1.5.2]

### Added

- mushymato.MMAP_Critter, spawns Fireflies Seagull or Crab

### Fixed

- Wrong register for mushymato.MMAP_ShowConstructForCurrent

## [1.5.1]

### Fixed

- Check location is not null when getting map prop.

## [1.5.0]

### Added

- Map Prop mushymato.MMAP_WoodsLighting: change ambiant lighting of current map.
- Map Prop mushymato.MMAP_LightRays: add god rays to current map.
- Map Prop mushymato.MMAP_SteamOverlay: add a repeating texture overlay to map.
- Map Prop mushymato.MMAP_Panorama: add background via new custom asset mushymato.MMAP/Panorama.
- Map Prop mushymato.MMAP_CribPosition: farmhouse only, change position of the crib.
- Map Prop mushymato.MMAP_FridgePosition: farmhouse only, change position of the fridge independent of the tilesheet.
- mushymato.MMAP_FridgeDoorSprite: farmhouse only, change the open door sprite.
- Tile Data mushymato.MMAP_TAS and custom asset mushymato.MMAP/TAS to show a TAS on the map.

### Changed

- QuestionDialogue now return if there are no valid entries, or if Cancel is the only entry.
- Map props and custom properties from this mod are interchangable unless otherwise noted.

## [1.4.4]

### Added

- Add stuff to make custom farmhouse upgrading less of a pain
    - special mailflag mushymato.MMAP_SkipMoveObjectsForHouseUpgrade
    - action mushymato.MMAP_ShiftContents
    - trigger mushymato.MMAP_MoveObjectsForHouseUpgrade

## [1.4.3]

### Added

- Add Condition for entire QuestionDialogue model

### Fixed

- Bug in draw layer not stopping draw when condition false

## [1.4.2]

### Fixed

- Fix incompatibility with flip buildings

## [1.4.0]

### Fixed

- New DrawLayerExt features

## [1.3.1]

### Fixed

- Remove bad GC call in ChestLight

## [1.3.0]

### Added

- Add new map property mushymato.MMAP_QuestionDialogue, and new custom asset mushymato.MMAP/QuestionDialogue

## [1.2.0]

### Added

- Add new map property mushymato.MMAP_FruitTreeCosmeticSeason

## [1.1.1]

### Added

- Fix issue where this mod crashes the game when there are error buildings.

## [1.1.0]

### Added

- Add HoleWarp and Lights, relax caching on AnimalSpot

## [1.0.0]

### Added

- Initial Release

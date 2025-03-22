# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.5.0] WIP

### Added

- Map Prop mushymato.MMAP_WoodsLighting change ambiant lighting of current map
- Map Prop mushymato.MMAP_LightRays add god rays to current map
- Map Prop mushymato.MMAP_SteamOverlay add a repeating texture overlay to map
- Map Prop mushymato.MMAP_Background add a repeating texture overlay to map
- Map Prop mushymato.MMAP_Background/TAS add a repeating texture overlay to map
- Tile Data mushymato.MMAP_TAS and custom asset mushymato.MMAP/TAS to show a TAS on the map

### Changed

- QuestionDialogue now return if there are no valid entries, or if Cancel is the only entry
- HoeDirt.texture is now also a map property

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

# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.12.4]

### Fixed
- TAS is now cleaned up forcifully on map change and day ending to workaround farm always active issues.
- Improved invalidation handling on wild tree props.

## [1.12.3]

### Fixed
- TAS Condition is now rechecked every time change for onetime (no respawn interval) TAS.

## [1.12.2]

### Added
- `BuildMenuDrawOffset` now has meaning for furniture. It is used to adjust the position of their menu icons. Most useful if the draw layers result in a highly offset icon.
- You can now grant tile properties to wild trees via `mushymato.MMAP/WildTreeProperties`.

## [1.12.1]

### Fixed
- Add check for null location name before trying to get data.

## [1.12.0]

### Fixed
- Question Dialogue lacking a Name, causing incompat with portraiture.

### Added
- Map property mushymato.MMAP_WoodsBaubles, adds sparkly woods particles.
- New feature map overrides, apply a small map override dynamically.

## [1.11.1]

### Fixed
- Removed unneeded logging in dispose

## [1.11.0]

### Added
- 2 new critters Squirrel and Opossum
- More options for flipping on Frog and Rabbit critters
- Water draw texture override for more control over how water looks
- Manual mode for draw layer open anim

### Fixed
- Critter microsyntax GSQ causing early return

## [1.10.3]

### Fixed
- DrawInBackground layers not rendering correctly in menu
- Fish tank decoration update loop crash for 1 tile wide tanks

## [1.10.2]

### Added
- New action mushymato.MMAP_SetPanorama for actively setting the panorama of current location.
- New panorama field FullView which controls whether panorama is visible past the bounds of the map, default true.

### Fixed
- Panorama conflicting with SVE Summit sigh, revert to resetLocalState timing
- Make Panorama look decent enough in map screenshots to enable DrawInMapScreenshot=true all the time

### Changed
- Removed IndoorPot draw fix for connected textures since Raised Garden Beds is back(!) and MMAP doesn't have means of making custom IndoorPot anyways

## [1.10.1]

### Fixed
- Panorama not applying.

## [1.10.0]

### Added
- New feature connected textures, which allows for creation of modular objects.
- Furniture CustomFields FishTank to define custom sizes and bounds.
- mushymato.MMAP_Panorama now accept special value SUMMIT which will directly use the summit panorama, instead of custom.
- New action mushymato.MMAP_ShowShipping, shows the shipping bin
- mushymato.MMAP_ShowConstruct is now available for traction
- Draw layers can now be colored.

### Fixed
- small caching improvements

## [1.9.7]

### Added
- Micro-syntax for GSQ critters `Crab:FALSE:1:3`.
- Special -1000 -1000 coord for "at this tile" TAS

### Fixed
- Fixed woods debris not actually working oops.
- The draw layers are fighting again

## [1.9.6]

### Changed
- Use bounding box location instead of tile location for collision checks and seats, which improves compat with precise furniture.

## [1.9.5]

### Added
- Draw layers can now have open animation behavior, kind of like shipping bin lid
- New map property `mushymato.MMAP_WoodsDebris`, spawn leaves like secret woods
- Added a more significant `GameLocation.doesTileHaveProperty` optimization, this can be disabled via config.json in case of compatibility problems
- New action `mushymato.MMAP_AddItemToBag` adds an item to MMAP global inventory
- New gsq `mushymato.MMAP_RAINING_HERE` checks current location for raining-ness
q
### Fixed
- more small perf improvements (it never ends)

## [1.9.4]

### Added
- New GSQ mushymato.MMAP_WINDOW_LIGHTS
- New traction mushymato.MMAP_CritterRandom, spawn critter at random Back tiles across the map
- Furniture action tiles for seats
- Furniture CustomFields TV

### Fixed
- more small perf improvements

## [1.9.3]

### Fixed
- small perf improvements
- fix furniture description not working

## [1.9.2]

### Added
- mushymato.MMAP_SetFlooring [flooringId] [floorId] to set flooring for farmhouse
- mushymato.MMAP_SetWallpaper [wallpaperId] [wallId] to set wallpaper for farmhouse
- mushymato.MMAP_FarmHouseUpgrade now optionally accepts a day until upgrade, default 1

### Fixed
- BuildingWrp and WrpOut were not functional

## [1.9.1]

### Fixed
- Drawlayer rotation check was inverted

## [1.9.0]

### Added
- New map property mushymato.MMAP_WaterColor that allows setting the water color for a location.
- Panorama now has BasedOn field, which will allow reuse of the top level fields from a different panorama. This only goes 1 layer deep and will refuse to resolve if the BasedOn panorama itself has a BasedOn.
- New map property mushymato.MMAP_FarmHouseFurnitureAdd and mushymato.MMAP_FarmHouseFurnitureRemove for dealing with starting furniture
- New traction mushymato.MMAP_FarmHouseUpgrade to upgrade farmhouse without going through robin
- Draw layers on furniture and buildings can now shake when player passes through their collision. Does not sync in multiplayer.
- [ExtendedTAS] add AlphaFadeFade and DrawAboveAlwaysFront

## [1.8.9]

### Fixed
- Fix a draw loop crash that happens sometimes(?)

## [1.8.8]

### Added
- New tile/touch action mushymato.MMAP_PoolEntry which combines ChangeIntoSwimsuit/ChangeOutOfSwimsuit/PoolEntrance into a single action.
- New tile/touch action mushymato.MMAP_WarpHere which warps player within 1 location without requiring a target location name
- Critter now supports Rabbit

### Changed
- [BREAKING CHANGE] Renamed all warp actions to 'Wrp' to avoid participating in vanilla door warp logic

### Fixed
- Hopefully fix NRE on save deserializer for real this time (sigh)

## [1.8.7]

### Added
- Support BedFurniture for tile properties and draw layers.

### Changed
- Disable SortTileOffset on base furniture as it was buggy.

## [1.8.6]

### Added
- Add new EndActions and ApplyEndActionsOnForceRemove fields to allow tractions to be used on TAS ending.

### Fixed
- Fix z-fighting with furniture layerdepth somewhat.

## [1.8.5]

### Changed
- Make furniture properties SortTileOffset work for base furniture sprite.
- Put furniture properties draw layer DrawPosition in 4x pixel scale.
- Furniture draw layers now work in the menu.

## [1.8.4]

### Changed
- Limit tile data cache updates to current location

## [1.8.3]

### Fixed
- Split screen NRE problems
- Panorama not drawing completely on small maps

## [1.8.2]

### Added
- Can now set a night time color for mushymato.MMAP_WoodsLighting and suppress maplight off behavior

### Fixed
- Fix wood lightning not applying bug

## [1.8.1]

### Added
- Furniture properties now support `SeasonOffset` and `DrawLayers`.
- QuestionDialogue can now be automatically paginated.

## [1.8.0]

### Added
- New tile/touch action mushymato.MMAP_HoleWarpBuilding [X Y], warp that will put player into the building on this tile, for use with buildings.
- New tile/touch action mushymato.MMAP_WarpBuildingOut [X Y], mushymato.MMAP_MagicWarpBuildingOut [X Y], mushymato.MMAP_HoleWarpBuildingOut [X Y], to add additional nonstandard exits from building interior.
- New tile/touch action mushymato.MMAP_If GSQ ## if-case ## else-case
- New tile/traction mushymato.MMAP_ToggleTAS X Y \<tasId\>+
- New touch action mushymato.MMAP_CountactTAS X Y \<tasId\>+
- TAS LayerDepth calculations reworked
- New feature mushymato.MMAP/FloorPathProperties which allows paths to get tile properties.
- New tile data mushymato.MMAP_Paddy for making a tile valid for paddy.
- mushymato.MMAP_Critter now supports Frog and LeaperFrog

### Changed
- `mushymato.MMAP/DrawLayerRotate.` keys are renamed to `mushymato.MMAP/DrawLayer.`

### Fixed
- Respawning temporary animated sprites now immediately spawn.
- Light spawning at top left of tile instead of center.

## [1.7.3]

### Added
- New tile/touch action mushymato.MMAP_WarpBuilding [X Y], warp that will put player into the building on this tile, for use with buildings.
- New tile/touch action mushymato.MMAP_MagicWarpBuilding [X Y], magic warp that will put player into the building on this tile, for use with buildings.

### Fixed
- When Grass Growth (`bcmpinc.GrassGrowth`) is installed, `mushymato.MMAP_GrassSpread` is disabled.
- mushymato.MMAP_ProtectTree/mushymato.MMAP_ProtectFruitTree plays axchop sound.

## [1.7.2]

### Fixed
- NRE on map reload.

## [1.7.1]

### Changed
- Some more implementation details around tile data cache.
- mushymato.MMAP_Light's 3rd argument (texture) now uses "4" by default, this is same as path light.

### Added
- GSQs for checking special times of the day.
- Triggers for special times of day.
- New DialogueBefore field in question dialogue, which displays a dialogue box before raising the question.

## [1.7.0]

### Changed
- Some implementation details around tile data cache.
- mushymato.MMAP_ShiftContents now runs on the host to avoid ghost location issues
    - "Cellar" will now pick the player's own Cellar, though assignments don't seem to quite work in multiplayer.

### Added
- New tile action/traction mushymato.MMAP_ShowBag, opens a global inventory.
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

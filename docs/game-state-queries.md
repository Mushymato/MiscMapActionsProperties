# Game State Queries

All `location` args accepts `Here` (current location), `Target` (context location), and specific location name.

When `location` is optional, it defaults to `Here` (current location).

## Time of Day

- `mushymato.MMAP_TIME_IS_DAY` [location]: true when time of day is less than night starting time.
- `mushymato.MMAP_TIME_IS_SUNSET` [location]: true when time of day is during night starting and truly time.
- `mushymato.MMAP_TIME_IS_LIGHTS_OFF` [location]: true when time of day is after window lights turn off and lamp lights turn on.
- `mushymato.MMAP_TIME_IS_NIGHT` [location]: true when time of day is later than night truly time.
- `mushymato.MMAP_WINDOW_LIGHTS` [location]: true when window lights should be on (e.g. `!mushymato.MMAP_TIME_IS_LIGHTS_OFF` and not raining).
- `mushymato.MMAP_RAINING_HERE` [location]: true when current location is raining.

## Map Override

- `mushymato.MMAP_HAS_MAP_OVERRIDE` \<location\> [mapOverrideId]+

## Mines

- `LOCATION_MINE_DIFFICULTY` \<location\> \<minDifficulty\> [maxDifficulty]: Checks the current mine location difficulty (0 for normal 1 for hard), backport from 1.6.16.
- `LOCATION_MINE_LEVEL` \<location\> \<minFloor\> [maxFloor]: Floor number of current mine location, backport from 1.6.16.
- `mushymato.MMAP_MINE_AREA_TYPE` \<location\> [minetype]+: Check if current mine location has `"SLIME"` or `"DINO"` or `"QUARRY"` area types.

## Maps

- `mushymato.MMAP_MAP_NAME` \<location\> \<map asset name\>: Checks the location's map asset name.
- `mushymato.MMAP_TILESHEET_NAME` \<location\> \<tilesheet id\> \<tilesheet asset name\>: Checks the location's tilesheet asset name.

# Game State Queries

## Time of Day

- `mushymato.MMAP_TIME_IS_DAY`: true when time of day is less than night starting time.
- `mushymato.MMAP_TIME_IS_SUNSET`: true when time of day is during night starting and truly time.
- `mushymato.MMAP_TIME_IS_LIGHTS_OFF`: true when time of day is after window lights turn off and lamp lights turn on.
- `mushymato.MMAP_TIME_IS_NIGHT`: true when time of day is later than night truly time.
- `mushymato.MMAP_WINDOW_LIGHTS`: true when window lights should be on (e.g. `!mushymato.MMAP_TIME_IS_LIGHTS_OFF` and not raining).
- `mushymato.MMAP_RAINING_HERE`: true when current location is raining.

## Mines

- `mushymato.MMAP_TIME_IS_DAY`: true when time of day is less than night starting time.
- `mushymato.MMAP_TIME_IS_SUNSET`: true when time of day is during night starting and truly time.
- `mushymato.MMAP_TIME_IS_LIGHTS_OFF`: true when time of day is after window lights turn off and lamp lights turn on.
- `mushymato.MMAP_TIME_IS_NIGHT`: true when time of day is later than night truly time.
- `mushymato.MMAP_WINDOW_LIGHTS`: true when window lights should be on (e.g. `!mushymato.MMAP_TIME_IS_LIGHTS_OFF` and not raining).
- `mushymato.MMAP_RAINING_HERE`: true when current location is raining.

## Map Override

- `mushymato.MMAP_HAS_MAP_OVERRIDE` \<location\> [mapOverrideId]+

## Mines

- `LOCATION_MINE_DIFFICULTY` \<location\> \<minDifficulty\> [maxDifficulty]: Checks the current mine location difficulty (0 for normal 1 for hard), backport from 1.6.16
- `LOCATION_MINE_LEVEL` \<location\> \<minFloor\> [maxFloor]: Floor number of current mine location.
- `mushymato.MMAP_MINE_AREA_TYPE` \<location\> [minetype]+: Check if current mine location has `"SLIME"` or `"DINO"` or `"QUARRY"` area types.

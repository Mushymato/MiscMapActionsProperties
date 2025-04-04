# Panorama

Panorama defines what goes behind a map in a way independent of map tiles, there are some hardcoded in C# examples of this vanilla (summet, island north, submarine).

MMAP's panorama system is separated into 3 main layers, which will be explained in each section.

## Backing

This is the bottom layer, usually used to draw plain color or 1 static texture that stretches for the whole viewport.

It is further divided into 3 sections:
1. `BackingDay`
2. `BackingSunset`
3. `BackingNight`

In most cases, `BackingDay` lasts from 0600 to 1800, after which `BackingSunset` begins and reach it's height at 1900, then completely transitions to `BackingNight` at 2000. `BackingDay` and `BackingSunset` are both optional, if `BackingNight` is null, then `BackingDay` will be used for all times of day.
# Panorama

Panorama defines what goes behind a map in a way independent of map tiles, there are some hardcoded in C# examples of this vanilla (summet, island north, submarine).

MMAP's panorama system is separated into 3 main layers, which will be explained in each section.

## Backing

This is the bottom layer, usually used to draw plain color or 1 static texture that stretches for the whole viewport.

It is further divided into 3 sections:
1. `BackingDay`
2. `BackingSunset`
3. `BackingNight`

Day starts fading into Night at 1800 (usually) and finishes at 2000, while sunset fades in during 1800-1900 and fades out during 1900-2000.
The timing can be set with 
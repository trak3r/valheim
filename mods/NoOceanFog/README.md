# Teflon Ted's No Ocean Fog

<img src="thunderstore/icon.png" width="128" alt="Teflon Ted's No Ocean Fog" />

> **One mod, one job** — no kitchen-sink configs or feature creep.

<a href="https://buymeacoffee.com/teflonted"><img src="https://img.buymeacoffee.com/button-api/?text=Buy%20me%20a%20coffee&slug=teflonted&button_colour=FFDD00&font_colour=000000&font_family=Lato&outline_colour=000000&coffee_colour=ffffff" width="174" alt="Buy me a coffee" /></a>

Developer notes for this mod. The Thunderstore / player-facing page lives in [`thunderstore/README.md`](thunderstore/README.md).

Removes **Misty** whiteout fog weather everywhere it can roll — Ocean, Plains, and any other biome that lists it. Does **not** remove Mistlands mist.

## What “Misty” is

| Concept | Detail |
|---------|--------|
| Weather id | `Misty` (dense white fog) |
| Common biomes | Ocean, Plains (also injectable elsewhere) |
| Not the same as | Mistlands volumetric mist / `ParticleMist` |

## Behavior

| Hook | Purpose |
|------|---------|
| `EnvMan.Awake` | Purge Misty from every biome’s weather pool |
| `EnvMan.AppendBiomeSetup` | Catch late ZoneSystem merges that re-add Misty |
| `EnvMan.GetAvailableEnvironments` | Filter Misty out of selection |
| `EnvMan.UpdateEnvironment` | If Misty is already playing, force `Clear` |

## What stays / what goes

| Effect | Removed? |
|--------|----------|
| Ocean / Plains Misty whiteout | Yes |
| Rain, thunder, clear skies | No |
| Mistlands mist | No |
| Ashlands ash / other env FX | No |

## What it does **not** do

- No density sliders or per-biome toggles
- Does not disable Mistlands gameplay mist
- Does not change wind or wave height by itself (only the Misty weather entry)

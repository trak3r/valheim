# Teflon Ted's Farm Grid

> **One mod, one job** â€” no kitchen-sink configs or feature creep.

Developer notes for this mod. The Thunderstore / player-facing page lives in [`thunderstore/README.md`](thunderstore/README.md).

Snaps cultivator planting to a local grid so crops land in clean, optimally spaced rows â€” no more guessing â€œneeds room to grow.â€

Modeled on [Venture Farm Grid](https://github.com/OrianaVenture/VentureValheim/tree/master/FarmGrid) (Sarcenâ€™s Farm Grid workflow).

## Behavior

| Aspect | Detail |
|--------|--------|
| Tool | Cultivator placement ghost |
| Cell size | `2 Ã— max(growRadius, collider extent) + 0.1 m` per crop |
| Snap origin | Plant root (same point vanilla uses for grow-space checks) |
| Visual | Green grid lines while placing near existing crops |
| Config | None |

## Workflow

| Step | What you do | What the grid does |
|------|-------------|--------------------|
| 1 | Plant any crop | Green grid appears; orientation follows the cursor around that root |
| 2 | Plant a second crop on a snapped cell | Direction locks to that row |
| 3 | Keep planting | Ghost snaps to empty cells on the square field |

Walk away from plants (or put the cultivator away) and the grid hides; start a new patch anytime with a fresh first plant.

## Spacing

| Rule | Why |
|------|-----|
| Cell = 2 Ã— clearance + 0.1 m | Clearance is `max(m_growRadius, widest horizontal collider)` so fat colliders (e.g. barley) are not packed inside vanillaâ€™s grow check |
| Per crop type | Flax, barley, carrots, etc. each measure their own plant + colliders |
| Root position, not child collider centers | Off-center colliders were shrinking rows and browning plants |

## Examples

| Youâ€™re planting | Nearby plants | Result |
|-----------------|---------------|--------|
| Flax | One flax | Ghost snaps from that root using flax clearance; grid pivots freely |
| Barley | Two barley already on a row | Orientation locks; cells use barley collider-aware clearance |
| Carrot next to flax | Mixed radii | Cell uses the larger of ghost vs neighbor clearance |
| Cultivator, no plants nearby | â€” | No grid; vanilla free placement |

## What it does **not** do

| Feature (other farm mods often add) | Here? |
|-------------------------------------|-------|
| Config toggles / custom spacing | No |
| Auto-plant / fill a whole field | No |
| Harvest / replant helpers | No |
| Force-grow or ignore biome rules | No |
| Snap when cultivator is not selected | No |

## Tips

- Replant any already-brown crops after updating â€” tight rows from older snaps wonâ€™t heal themselves.
- Lock the row with the **second** plant carefully; that sets the field angle for everything after.
- Works for vanilla crops and anything else that uses Valheimâ€™s `Plant` â€” spacing respects both grow radius and collider size.

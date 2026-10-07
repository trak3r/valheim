# Teflon Ted's Auto Eat

<img src="thunderstore/icon.png" width="128" alt="Teflon Ted's Auto Eat" />

> **One mod, one job** — no kitchen-sink configs or feature creep.

<a href="https://buymeacoffee.com/teflonted"><img src="https://img.buymeacoffee.com/button-api/?text=Buy%20me%20a%20coffee&slug=teflonted&button_colour=FFDD00&font_colour=000000&font_family=Lato&outline_colour=000000&coffee_colour=ffffff" width="174" alt="Buy me a coffee" /></a>

Developer notes for this mod. The Thunderstore / player-facing page lives in [`thunderstore/README.md`](thunderstore/README.md).

When a food buff’s timer hits zero, automatically eat **the same food** again if it is still in your inventory.

## Behavior

| Aspect | Detail |
|--------|--------|
| Trigger | Natural food tick (`Player.UpdateFood`, ~1 s food timer) |
| Match | Exact same shared item name as the buff that expired |
| Source | Player inventory only (not chests), including hotbar |
| Guards | Skips if dead, can’t eat, or nested re-entry from eating |

## Examples

| Active buffs | Inventory | When honey expires |
|--------------|-----------|--------------------|
| Honey, cooked meat, raspberry | 5× honey | Honey is re-eaten; others untouched |
| Honey only | No honey left | Buff ends; nothing eaten |
| Three foods | Stacks of each | Each refills independently as it expires |

## What it does **not** do

| Feature (kitchen-sink mods often add) | Here? |
|---------------------------------------|-------|
| Eat “best” food into empty slots | No |
| Re-eat before expiry / at 50% flash | No — only when fully expired |
| Harvest berries / mushrooms | No |
| Drink meads automatically | No |
| Pull food from chests | No |
| Config toggles / hotkeys | No |

Inspired by simpler auto-eat ideas in mods like Hunger Pangs / HungerPangsPlus, stripped to this one behavior.

## Tips

- Keep stacks of the foods you actually run (e.g. your usual three) in the bag or on the hotbar — both work. The original stack you ate from is preferred.
- Manually clearing a food icon does **not** trigger a refill (only timer expiry does).

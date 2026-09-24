# Teflon Ted's Craft From Chests

Craft and build using materials in nearby chests — not only your backpack.

## Behavior

| Aspect | Detail |
|--------|--------|
| When | Crafting, building, and consuming recipe materials |
| Where | Containers within **~20 m** of the player |
| What | Any item a recipe/piece requires |
| Order | Player inventory first, then chests |
| Access | Only chests you can open (guards / ownership respected) |

```mermaid
flowchart LR
  Recipe[Recipe needs 10 Wood]
  Inv[Player inventory]
  ChestA[Chest A]
  ChestB[Chest B]
  Recipe --> Inv
  Inv -->|not enough| ChestA
  ChestA -->|still short| ChestB
```

## Which containers count?

Any player-accessible `Container` in range, including:

| Container | Notes |
|-----------|--------|
| Wood chest | `piece_chest_wood` |
| Reinforced chest | `piece_chest` |
| Black metal chest | `piece_chest_blackmetal` |
| Personal chest | If you have access |
| Cart / longship storage | If in range and accessible |
| Modded chests | If they use Valheim's `Container` |

Locked / ward-blocked chests you cannot open are skipped.

## What it applies to

| Action | Uses nearby chests? |
|--------|---------------------|
| Crafting station recipes | Yes |
| Building with the hammer | Yes |
| UI “have / need” counts | Yes (while requirements are evaluated) |
| Repair costs | Only if those paths check inventory the same way |

## What it does **not** do

- Does not pull from chests farther than ~20 m
- Does not open chests for you or move leftovers into storage
- Does not change recipes or unlock stations

## Stack with other mods

Works well with [Sort Into Chests](../SortIntoChests) (stash) and [Fuel From Chests](../FuelFromChests) (processing). Same ~20 m idea as sort; fuel mods use a much tighter ~4 m radius on purpose.

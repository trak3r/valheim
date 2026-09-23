I want to create a suite of single-purpose Valheim mods.

# Design rules

- each mod does one thing only
- minimal tuning options (preferably none)

# Mods

- craft from nearby chests. when using a workstation, items in nearby chests are eligible ingredients, not just player's inventory
- sort into nearby chests. when pressing the "`" hotkey, all unequipped and non-hotkey items in the player's invetory will be moved into nearby chests already containing the same items
- no ocean fog. dense fog is a weather event in the ocean biome and it's annoying as fuck not fun gameplay. disable it.
- eternal lights. lighting build items (torches, sconces, braziers, etc.) do not require refueling
- auto repair. when opening a workstation, automatically repair all equipped items pertinent to that workstation.
- everything floats. items dropped in the water don't sink.
- kilns, smelters, furnaces fuel from nearby chests. example, kilns need wood. if there's a chest *very* close by containing wood, it will fuel itself. ditto smeltes and furnaces with coal. stress the chest needs to be *real* close so general storage isn't inadvertenly consumed.
- kilns, smelters, furnaces fuel from nearby ground. example, if i drop a stack of wood in from of a kiln, it should suck it up and start producing coal. this should allow a sort of assembly line set-up where a kiln spits out coal near a smelter which then pulls in the coal, etc.

# Plan

- create each mod in s subfolder under this "valheim" folder

# Open items

- should these be a single github reposotory or individual? seems a single repository would facilitate code sharing and syncing. but individual would be better for publishing and versioning. but i'm not sure i'm going to publish them - the market is saturated and these are just to appease myself.
- i don't know anything about valheim mod development other than BepinEx seems to be a common prerequisite to most of them. i'm assuming you'll be able to figure it out from nexus mods, thunderstore, and github.
- even though each mode will be independent, they should all share the same common branding like "Teflon Ted's X Valheim Mod"

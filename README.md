# Teflon Ted's Valheim Mod Suite

Personal suite of single-purpose Valheim mods. Each mod does one thing. Minimal (usually zero) config.

| Mod | What it does |
|-----|----------------|
| [Craft From Chests](mods/CraftFromChests) | Workstation crafts/builds can use materials in chests within ~20m |
| [Sort Into Chests](mods/SortIntoChests) | `` ` `` quick-stacks unequipped, non-hotbar items into nearby chests that already hold that item |
| [No Ocean Fog](mods/NoOceanFog) | Removes Misty weather from the Ocean biome |
| [Eternal Lights](mods/EternalLights) | Torches / sconces / braziers never need fuel |
| [Auto Repair](mods/AutoRepair) | Opening a workstation repairs inventory items that station can repair |
| [Everything Floats](mods/EverythingFloats) | Dropped items float on water instead of sinking |
| [Fuel From Chests](mods/FuelFromChests) | Kilns / smelters / furnaces / windmills pull from chests within ~2.5m (auto + manual E) |
| [Fuel From Ground](mods/FuelFromGround) | Same machines suck matching item-drops within ~2.5m (assembly lines) |
| [Auto Eat](mods/AutoEat) | When a food buff fully expires, re-eat the same food from inventory if you still have it |

Shared helpers live in [`common/TeflonTed.Common`](common/TeflonTed.Common).

## Requirements

- Valheim (Windows)
- [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) (BepInEx 5)

## Build (Windows)

1. Install the .NET SDK (6+ is fine; projects target `net472`) and a C# IDE if you want one.
2. Install BepInEx into your Valheim folder.
3. Copy `Environment.props.example` → `Environment.props` and set `VALHEIM_INSTALL` to your game path, e.g.

   ```xml
   <VALHEIM_INSTALL>C:\Program Files (x86)\Steam\steamapps\common\Valheim</VALHEIM_INSTALL>
   ```

4. From this repo root:

   ```bat
   dotnet restore TeflonTed.Valheim.sln
   dotnet build TeflonTed.Valheim.sln -c Release
   ```

5. On a successful build, each plugin DLL is copied to:

   `VALHEIM_INSTALL\BepInEx\plugins\TeflonTed.<ModName>\`

   Mods that use the shared library also get `TeflonTed.Common.dll` in the same folder.

## Launch from this folder

Double-click [`Launch-Valheim.bat`](Launch-Valheim.bat) (Windows). It:

1. Reads `VALHEIM_INSTALL` from `Environment.props`
2. Builds + deploys all mods (`Release`)
3. Starts `valheim.exe` from that install (BepInEx Doorstop loads whatever is in `plugins`)

Skip the build when you just want to play:

```bat
Launch-Valheim.bat -SkipBuild
```

You can also pin a Windows shortcut to `Launch-Valheim.bat` on the taskbar/desktop; keep the shortcut’s “Start in” as this repo folder (the `.bat` already `cd`s to itself).

`BepInEx.AssemblyPublicizer.MSBuild` publicizes `assembly_valheim` (and related) at compile time so patches can reach normally-private game members. You do not need to run a separate publicizer tool.

## Install without building

Drop each `TeflonTed.*.dll` (and `TeflonTed.Common.dll` beside any chest/fuel mod) into `BepInEx\plugins\` — one folder per mod is fine.

## Notes

- These are client-side quality-of-life mods aimed at single-player / trusted friends.
- Game updates can rename Harmony targets; rebuild against the current `assembly_valheim.dll` if a mod stops loading.
- Branding: display names are `Teflon Ted's …`; GUIDs are `com.teflonted.valheim.<mod>`.

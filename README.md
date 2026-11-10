# Storing What — Oxygen Not Included mod

> **Disclaimer:** This mod was developed by Claude, an AI model made by Anthropic.

Hover over a storage building and its info card lists what it holds:

```
Refrigerator
  ⓘ Mush Bar: 4,800 kcal
  ⓘ Berry Sludge: 2,400 kcal
Liquid Reservoir
  ⓘ Polluted Water: 3,512.4 kg
```

- Food is shown in kcal, countable things (eggs, seeds, critters, ...) in units, everything else in mass.
- Item types are sorted by mass, largest first. Past the line limit you get "+N more item types".
- Empty storage buildings say "Stored: nothing".
- Lines update right away when something goes in or out, and once a second otherwise (tanks fill
  in place).

## Better Info Cards

Compatible, and they work together. The lines are ordinary status items, which Better Info
Cards already handles. Storing What also registers a Better Info Cards converter for each item
type, so cards that Better Info Cards merges show the total:

```
Storage Bin x 3
  ⓘ Sandstone: 42,000 kg (Σ)
```

Bins only merge when they hold the same item types. That's Better Info Cards' rule: every line
on the merged card has to exist on each card. Without Better Info Cards nothing changes. The
converters are registered only if it's installed.

## Options

Open them with the mod's **Options** button in the main-menu **Mods** screen. They're read each
time a save loads.

| Option          | Default           |                                                                    |
|-----------------|-------------------|--------------------------------------------------------------------|
| Show on         | Storage buildings | or *Every building with storage* (generators, Electrolyzers, fabricators...) |
| Max item lines  | 6                 | item types listed per building                                     |
| Show "Empty"    | On                | storage buildings with nothing inside say so                       |

"Storage buildings" means buildings with one of these components: `StorageLocker`,
`StorageLockerSmart`, `Refrigerator`, `RationBox`, `Reservoir`, `SmartReservoir`,
`ObjectDispenser`, `StorageTile`, `CargoBay`, `CargoBayCluster`, `ModularLaunchpadPort`,
`TreeFilterable`. Storage buildings from other mods are usually covered, because they tend to
use one of these components.

## Build in Visual Studio (Windows)

Same setup as Smart Power Management: `netstandard2.1`, paths in `Directory.Build.props`,
installs to `mods\Local\DEV_StoringWhat`, PLib merged in with ILRepack.

1. Check `GameFolder` (and `ModFolder` if Documents is in OneDrive) in **`Directory.Build.props`**.
2. Open **`StoringWhat.sln`** in Visual Studio 2022 and **Build → Rebuild Solution**.
3. Start the game, open **Mods**, enable **Storing What**, restart.

Build with the game closed (it locks the DLL while running).
Log: `%USERPROFILE%\AppData\LocalLow\Klei\Oxygen Not Included\Player.log` (lines start with `[StoringWhat]`).

## How it works

- Harmony postfix on `BuildingComplete.OnSpawn` adds a `StoredItemsDisplay` component to
  qualifying buildings that have a `Storage` (nothing is saved, so removing the mod is safe).
- Each refresh adds up `Storage.items` by `KPrefabID.PrefabTag` across the building's storages
  (only those with `showInUI`, unless none have it). Amounts come from `Edible.Calories`,
  `PrimaryElement.Units` (if `Assets.IsTagCountable`) or `PrimaryElement.Mass`.
- One `StatusItem` per item type (ID `StoringWhat_<tag>`, no world icon) is added to the
  `KSelectable`. Its text comes from `resolveStringCallback`, so amount changes don't need the
  item re-added. Status items are only re-added when the set or order of item types changes.
- Better Info Cards: `BetterInfoCards.ConverterManager.AddConverterReflect(id, Func<object,float>,
  Func<string,List<float>,string>, null)`, called by reflection.

## Files

```
StoringWhat.sln              open this in Visual Studio
Directory.Build.props        YOUR paths (GameFolder / ModFolder)
Directory.Build.targets      merges PLib.dll into the mod DLL
mod.yaml / mod_info.yaml     title, description, version, DLC support (all)
src/Mod.cs                   UserMod2 entry, PLib registration, Harmony patches
src/StoringWhatOptions.cs    Options screen fields (config.json)
src/StoredItemsDisplay.cs    the component that adds the status-item lines
src/BetterInfoCardsCompat.cs Better Info Cards converters (sums on merged cards)
```

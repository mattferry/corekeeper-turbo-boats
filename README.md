# Turbo Boats — faster Speeders for Core Keeper

Four craftable boats at the Boat Workbench. Each tier multiplies the vanilla Speeder's speed
multiplier — **and its crafting cost** — by the same number. Pay 3x, sail 3x.

**Get it on mod.io: https://mod.io/g/corekeeper/m/turboboats**

| Item | Speed (vs basic Boat) | Recipe (Boat Workbench) | Name colour |
|---|---|---|---|
| Turbo Speeder (2x) | 2.6x | 200 Coral Plank + 160 Octarine Bar + 60 Gold Bar + 30 Mechanical Part | Rare (blue) |
| Turbo Speeder (3x) | 3.9x | 300 Coral Plank + 240 Octarine Bar + 90 Gold Bar + 45 Mechanical Part | Epic (purple) |
| Turbo Speeder (4x) | 5.2x | 400 Coral Plank + 320 Octarine Bar + 120 Gold Bar + 60 Mechanical Part | Legendary (orange) |
| Turbo Speeder (5x) | 6.5x | 500 Coral Plank + 400 Octarine Bar + 150 Gold Bar + 75 Mechanical Part | Legendary (orange) |

(Vanilla Speeder on 1.3.0: `speedMultiplier` 1.3, 100 Coral Plank + 80 Octarine Bar + 30 Gold Bar +
15 Mechanical Part — the game rebalanced the Speeder in 1.3.0; the mod always scales whatever the
game's current recipe is, so these columns follow it.) Works on Core Keeper 1.3.0 (1.2.0+, tested on 1.3.0.6; 1.1.0 was the last
build for 1.2.1.5). No CoreLib, no dependencies, no Unity project — the game compiles these
sources itself at boot (official PugMod system). Land is a suggestion. Docking is a skill.

## Multiplayer

Boat movement is client-predicted, so this is `requiredOn: 3` — the mod must be installed on
the **server and every client** (the join screen offers the subscription automatically).
Dedicated servers: add `turboboats` to the mods list.

## How it works

In `IMod.EarlyInit` the mod creates four empty runtime `EntityAuthoringDataBlock`s through
`API.DataBlocks`. Then, in a Harmony prefix on `ECSManager.Init`, it finds the game's own
Speeder authoring prefab among `ScriptableData.GetDataBlocks<EntityAuthoringDataBlock>()`,
clones it four times, scales the `speedMultiplier`, recipe and sell value per tier, attaches
each clone to its data block, and appends the four recipes to the Boat Workbench's
`CanCraftObjectsBuffer` when it converts. (Core Keeper 1.3.0 builds both the entity conversion
and the object database only from data blocks — the mod API's `RegisterAuthoringGameObject`
list is no longer converted; see the Minecart Turbo README for the full story.)

### Pinned ObjectIDs

Unlike sequentially-assigned mod objects (fragile: adding another entity mod later can shift
IDs and orphan items), the boats **pin** their ObjectIDs at 40001–40004 by pre-seeding
`Manager.mod.Authoring.ObjectIDLookup` before conversion — `ObjectAuthoring`'s
`TryGetPreferredObjectIndex` honours the seed and the sequential counter never advances for
them. Boot-time assertions verify the pins landed and that no foreign object sits on those IDs.
If you use this pattern, never seed an id another mod already took, and never seed names of
objects that already exist sequentially in live worlds (that would strand the old ID).

### Engine traps (inherited from the companion Minecart Turbo mod)

- Runtime-cloned authoring objects must be `Prefab`-tagged in `OnObjectTypeAdded`, or clients
  Burst-crash on world load ("Could not find ghost type in the collection").
- I2 localization rewrites `:` to `_` on lookup but not on CSV import — loc keys must use the
  underscore form (`Items/TurboBoats_Mk1`).

Full write-ups of both traps:
[Minecart Turbo](https://github.com/mattferry/corekeeper-minecart-turbo)'s README.

## Developing

Nothing to build — edit the `.cs` files and the game compiles them at launch. Type-check
locally with Roslyn's net472 `csc.exe` against the game's `CoreKeeper_Data/Managed/` DLLs.
`publish/modio_publish.py` uploads a release to mod.io (reads `MODIO_TOKEN` and
`MODIO_API_URL` from the environment).

## Credits

Written by Claude (Anthropic), piloted by Matt Ferry, for the Ferry family server. MIT
licensed; see [LICENSE](LICENSE).

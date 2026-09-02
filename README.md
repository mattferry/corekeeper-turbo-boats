# Turbo Boats — faster Speeders for Core Keeper

Three craftable boats at the Boat Workbench. Each tier multiplies the vanilla Speeder's speed
multiplier — **and its crafting cost** — by the same number. Pay 3x, sail 3x.

**Get it on mod.io: https://mod.io/g/corekeeper/m/turboboats**

| Item | Speed (vs basic Boat) | Recipe (Boat Workbench) | Name colour |
|---|---|---|---|
| Turbo Speeder (2x) | 2.6x | 400 Coral Plank + 240 Octarine Bar + 60 Gold Bar + 60 Mechanical Part | Rare (blue) |
| Turbo Speeder (3x) | 3.9x | 600 Coral Plank + 360 Octarine Bar + 90 Gold Bar + 90 Mechanical Part | Epic (purple) |
| Turbo Speeder (4x) | 5.2x | 800 Coral Plank + 480 Octarine Bar + 120 Gold Bar + 120 Mechanical Part | Legendary (orange) |

(Vanilla Speeder: `speedMultiplier` 1.3, 200 Coral Plank + 120 Octarine Bar + 30 Gold Bar +
30 Mechanical Part.) Works on Core Keeper 1.2.1.5. No CoreLib, no dependencies, no Unity
project — the game compiles these sources itself at boot (official PugMod system). Land is a
suggestion. Docking is a skill.

## Multiplayer

Boat movement is client-predicted, so this is `requiredOn: 3` — the mod must be installed on
the **server and every client** (the join screen offers the subscription automatically).
Dedicated servers: add `turboboats` to the mods list.

## How it works

At boot the mod clones the game's own Speeder authoring prefab three times, scales the
`speedMultiplier`, recipe and sell value per tier, registers the clones through
`API.Authoring.RegisterAuthoringGameObject`, and appends the three recipes to the Boat
Workbench's `CanCraftObjectsBuffer` when it converts.

### Pinned ObjectIDs

Unlike sequentially-assigned mod objects (fragile: adding another entity mod later can shift
IDs and orphan items), the boats **pin** their ObjectIDs at 40001–40003 by pre-seeding
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

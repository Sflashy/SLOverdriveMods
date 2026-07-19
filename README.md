# Solo Leveling: ARISE OVERDRIVE — Mods

BepInEx mods for **Solo Leveling: ARISE OVERDRIVE**, built for offline single-player play.

| Mod | Description | Status |
|---|---|---|
| [Drop Multiplier](src/SLOverdrive.DropMultiplier) | Multiplies item drop quantities from dungeon runs | Working |
| [Table Dumper](src/SLOverdrive.TableDumper) | Dumps the game's decrypted GameData tables to CSV | Diagnostic tool |

## How loot works in this game

Understanding the three layers makes the settings obvious. A dungeon's loot passes through
each in turn:

```
ContentsDrop table          Does this drop slot fire at all?
  prob 100  common          → [DropChance] multiplies this
  prob   1  rare
        ↓
RewardGroup table           How many entries does the slot hand out?
  mode  = Random|Rate|Fix
  rate  = 4000              → [RewardGroup] RateMultiplier
  count = 1                 → [RewardGroup] PickCount
        ↓
Reward table (by group)     Which entries are they?
  weight 10000  Material    → [RewardWeight] multiplies this
  weight  1000  Artifact
        ↓
Drop packet                 How many of each?
  itemID, stack             → [Quantity] multiplies this
```

**`PickCount` is the important one.** The game ships every one of its 10,482 reward groups
with a pick count of `1`, so a drop slot only ever hands out a single item no matter how the
weights are tuned. Raising it is what makes a dungeon give you several things at once, and it
behaves differently depending on the group:

- **Multi-item groups** → you get variety. Cosmetics and equipment that were being crowded
  out by common materials start appearing.
- **Single-item groups** → you get quantity. The same item is picked repeatedly and the game
  merges it into one larger stack.

Equipment is `Artifact` in this game, and that is where the grind lives: 440 artifact entries
sit below 1% of their group's weight. `[RewardWeight]` biases the group toward equipment, but
it cannot help in groups that are *entirely* artifacts — scaling every entry equally leaves
the odds unchanged. 210 groups are like that, which is why `PickCount` matters more.

**Scope.** Items, materials and equipment are all covered. Gold is affected only when it
drops as a world pickup (gates, field dungeons), not when awarded on the result screen. EXP
never passes through the packet and is never touched.

Drops are rolled when you **select the dungeon on the map**, not when you enter it — the
packet carries a `randomSeed`. Select the dungeon fresh for each run; re-entering through a
"retry" button may not produce a new packet.

---

## ⚠️ Read this first

**Do not use these mods online.** The game syncs your save to Netmarble's servers when
connected, and the Netmarble anti-cheat (`NMSSW`) ships with the client. A modified save
reaching the servers can get your account banned. Offline mode only.

**Back up your save** before installing: `%LOCALAPPDATA%\SoloLevelingArise`.

These mods do not touch, disable or bypass the anti-cheat. They modify the game's own data
in memory while it runs offline.

Uninstalling is clean: delete the `BepInEx` folder, `winhttp.dll` and `doorstop_config.ini`.

---

## Installation

1. Install **BepInEx 6** — bleeding edge, **Unity.IL2CPP**, **win-x64**
   ([builds.bepinex.dev](https://builds.bepinex.dev/projects/bepinex_be)).
   BepInEx 5 does not support IL2CPP and will not work.

   Extract into the game folder next to `Solo_Leveling_ARISE_OVERDRIVE.exe`, not into a
   nested subfolder.

2. Run the game once and close it. The first launch takes several minutes while BepInEx
   generates interop assemblies.

   > **404 while downloading Unity base libraries?** The exact Unity version is not hosted.
   > In `BepInEx/config/BepInEx.cfg` set:
   > ```ini
   > UnityBaseLibrariesSource = https://unity.bepinex.dev/libraries/2021.3.0.zip
   > ```
   > These are only compile references, so a nearby patch version of the same minor
   > release works.

3. Copy the contents of a release archive into `BepInEx/plugins/`.

4. Each mod ships with `DryRun = true`. Verify the logged values against your actual
   rewards before turning it off.

---

## Building

Requires the **.NET 6 SDK**.

```bash
git clone https://github.com/sflashy/SLOverdriveMods.git
cd SLOverdriveMods
dotnet build -c Release
```

Build output is staged into `artifacts/plugins/`, ready to copy into `BepInEx/plugins/`.

---

## Repository layout

```
SLOverdriveMods/
├── SLOverdriveMods.sln
├── Directory.Build.props          shared build settings and BepInEx reference
├── data/
│   └── item_names.json            item ID → display name, for readable logs
├── docs/                          reverse engineering notes
└── src/
    ├── SLOverdrive.Core/          shared helpers, no game logic
    │   ├── Il2Cpp/
    │   │   ├── TypeResolver.cs    find types and methods by signature
    │   │   └── Il2CppReflect.cs   read/write Il2Cpp objects and lists
    │   └── Data/
    │       └── ItemDatabase.cs    item name lookup
    └── SLOverdrive.DropMultiplier/
        ├── Plugin.cs              entry point, patch wiring
        ├── DropConfig.cs          settings
        └── DropPacketPatch.cs     the actual patch
```

`SLOverdrive.Core` holds everything that is not specific to a single mod. New mods reference
it and add their own project under `src/`.

---

## Adding a new mod

1. Create `src/SLOverdrive.<Name>/` with a `.csproj` referencing `SLOverdrive.Core`
2. Add it to `SLOverdriveMods.sln`
3. Follow the existing shape: `Plugin.cs` wires things up, a config class caches settings,
   patch classes stay static and receive their dependencies through an `Initialize` call

`Directory.Build.props` already supplies the target framework and BepInEx reference, so a new
project file only needs its assembly name and a project reference.

---

## Patch points

| Setting | Hooked method | Runs |
|---|---|---|
| `[DropChance]` | `AJIHKOOLMFK.LoadDataSheet` | Once per table row, at startup |
| `[RewardGroup]` | `PEGMFCJCGFA.LoadDataSheet` | Once per table row, at startup |
| `[RewardWeight]` | `COABIFJBJBJ.LoadDataSheet` | Once per table row, at startup |
| `[Quantity]` | `ELAHJNHCOHO.NFNNBJJLIKF(GAME_BATTLE_STAGE_DROP_NFY)` | Once per dungeon |
| `[DropRate]` | `NLib.GKDropCommon.GetDropInfoCommons` | Once per dungeon |

The three tables are linked by ID, which is how a drop in the packet traces back to its items:

```
dropID  →  ContentsDrop.id
           ContentsDrop.ACPKFBEIJBG  →  RewardGroup.BFMDEGIDKLN
                                        RewardGroup.BLLMJIAPNFP  →  Reward.BLLMJIAPNFP
                                                                    → the item entries
```

The two table patches edit the decrypted rows in memory as the game loads them. Nothing is
written to disk, so removing the mod restores the original behaviour immediately.

`[DropRate]` is named after the game's own `dropRate` parameter, but that value multiplies how
many drop objects each row spawns rather than gating them — it behaves as a quantity
multiplier, not a rarity one. Off by default.

---

## Updating after a game patch

Obfuscated names change with every game build. All of them live in the `[Advanced]` config
section, and the Table Dumper is how you find the new ones.

1. Run the Table Dumper with `Types = *`, with this mod disabled so the values are original
2. Find `ContentsDrop_*.csv` and `Reward_*.csv` — the filename suffix is the new class name
3. In `ContentsDrop`, the probability column is the one with very few distinct values, all
   between 1 and 999
4. In `Reward`, find the column containing `Artifact`, `Material`, `Gold`; the weight is the
   numeric column beside it
5. Put the new names into `[Advanced]`

`DropManagerType` can be left empty — the plugin then locates the drop manager by searching
for a method that takes `GAME_BATTLE_STAGE_DROP_NFY`.

---

## Working with this game

Notes that will save you time if you write your own patches.

**Names are obfuscated, signatures are not.** Roughly a quarter of the game's classes
(5,011 of 19,341) have names like `ELAHJNHCOHO`. Searching by name is close to useless.
Search by type signature instead — `BattleDropInfo_Network`, `EDropTargetType` and the
`GAME_*` packet types are all intact. `TypeResolver.FindTypesDeclaringParameter` does this.

**Not everything that looks hookable is.** In the interop assembly:

| Backing field | Meaning | Hookable |
|---|---|---|
| `NativeFieldInfoPtr_X` | A raw field; Il2CppInterop invented the property | **No** — the game writes memory directly and never calls it |
| `NativeMethodInfoPtr_get_X` | A real native property accessor | **Yes** |

Constructors are also not hookable — Il2CppInterop cannot install a native detour for them,
and the patch silently never fires.

**Tooling.** Il2CppDumper fails in auto mode on this game, because the il2cpp code lives in a
custom `il2cpp` PE section instead of `.text`. Cpp2IL 2022.1+ handles it. The metadata is not
encrypted (il2cpp v31, magic `0xFAB11BAF`).

Tested against a build running Unity 2021.3.57f2.

---

## License

MIT — see [LICENSE](LICENSE).

Not affiliated with Netmarble, Netmarble Neo, or the Solo Leveling rights holders.

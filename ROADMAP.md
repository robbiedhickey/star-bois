# star-bois Roadmap

A fork of upstream SS14 being adapted into a 4-player PvE co-op game inspired by FTL. The core loop: small crew manages a ship under attack, jumps between locations on a star map, fights AI enemy ships.

---

## Phase 1 — Fork & Setup ✅ Complete

- Forked upstream SS14 (v277, .NET 10, RobustToolbox v277)
- Renamed to `star-bois`, created branch `ftl-port`
- Confirmed clean build: 0 errors, 779 warnings (all pre-existing SS14 warnings)
- Set up workspace structure with `Makefile`, `WORKSPACE.md`, fork references

---

## Phase 2 — Port _FTL C# Code ✅ Complete

97 files from Ekrixi's `_FTL` namespace ported to SS14 v277. Ekrixi targets .NET 8 / SS14 v225. All API migrations resolved:

| Fix | What Changed |
|---|---|
| `PowerChangedEvent` missing using | Added `Content.Shared.Power` |
| `DeviceNetworkPacketEvent` missing using | Added `Content.Shared.DeviceNetwork.Events` |
| `DamageableSystem` namespace | Moved to `Content.Shared.Damage.Systems` |
| `MapLoaderSystem` namespace | Moved to `Robust.Shared.EntitySerialization.Systems` |
| `GunSystem.AttemptShoot` signature | Now takes `Entity<GunComponent>` |
| `AfterActivatableUIOpenEvent.Actor` | Renamed to `.User` |
| `GunSystem.CycleCartridge` removed | Stubbed — autofire handles cycling |
| `StationDataComponent` namespace | Moved to `Content.Shared.Station.Components` |
| `GetLargestGrid` signature | Now takes `Entity<StationDataComponent?>` |
| `MindSystem.TryGetSession` removed | Use `IPlayerManager.TryGetSessionByEntity` |
| `DoorVisuals.Powered` removed | Defaulted to `true` |
| `DatasetPrototype` → `LocalizedDatasetPrototype` | Type renamed |
| `SharedSalvageSystem.GetFTLName` not static | Injected system instance |
| `MapLoaderSystem.TryLoad` renamed | Now `TryLoadMap` |
| `TriggerEvent` by-ref | Must use `ref` keyword when raising |
| `FtlPointPrototype` not partial | Added `partial`, added setters |
| Worldgen effects (3 files deleted) | Depend on Frontier worldgen we don't use |
| Missing CCVars | Created `Content.Shared/_FTL/CCVar/CCVarsFTL.cs` |

---

## Phase 3 — Port YAML Content ✅ Complete

Server reaches `Ready` with all `_FTL` prototypes loading. Map format 6 auto-migrated by engine.

| Fix | What Changed |
|---|---|
| `BaseMobHuman` → `MobHuman` | Parent renamed upstream |
| `BaseMobSpeciesOrganic` → `BaseSpeciesMobOrganic` | Parent renamed |
| `ClothingHeadHardsuitWithLightBase` → `ClothingHeadHardsuitBase` | Base renamed |
| `HumanoidAppearance` → `HumanoidProfile` | Component renamed |
| `Advertise` component removed | Was vending machine ad system — removed from NPC |
| `Hypospray` → `Injector` | Full component replacement with `HyposprayInjectMode` |
| `BodyPart` component removed | IPC species rewritten to organ-based body system |
| `Sharp` component removed | Now `MeleeWeapon.damage.types.Slash` |
| Worldgen YAML deleted | `weather.yml`, `World/` — depended on removed worldgen code |

**IPC Species:** Old `BodyPart` entities with `partType/symmetry` → `OrganIPCX` entities inheriting from `OrganBaseX`, with `InitialBody` slot mapping. Same pattern as Arachnid species in current upstream.

---

## Phase 4 — Runtime Verification 🔄 In Progress

Server boots, reaches Ready. Client not yet connected.

- [ ] Star map opens and displays warp points
- [ ] Warp drive charges and jump executes
- [ ] Arrival effects trigger (map spawns, station spawns)
- [ ] AI enemy ships spawn in sector
- [ ] AI transitions Cruising → Hostile on contact
- [ ] AI fires weapons at player ship
- [ ] Gunner console opens, weapon rotates, fires
- [ ] Heat-seeking projectiles track targets
- [ ] TriggerOnEnterGrid fires on boarding
- [ ] Power control UI works
- [ ] IPC species selectable in character creator

---

## Phase 5 — Game Loop Adaptation ⏳ Pending

Replace station-centric round with ship-centric session:

- Replace starting map (station) with a player ship
- Win/lose: ship destroyed or all crew dead
- Remove irrelevant round-end triggers (nuke, syndicate objectives)
- Disable station antag systems (traitor, revolutionary, etc.)
- Configure 4-player max
- Dev tooling for easy testing

---

## Phase 6 — Save / Session Persistence ⏳ Pending

Make world state survive between sessions:

- Ship state (hull damage, systems, cargo)
- Player inventories and character state
- Campaign progress (sectors visited, resources)
- Sector map state (locations cleared)

**Options to decide:**
1. Serialize ship grid to YAML on session end, reload next session
2. Extend existing SQLite DB to store session state
3. Admin command checkpoint (simplest to build first)

---

## Deferred

- **Economy**: `EconomySystem` partially stubbed. `StationBankAccountComponent` was Frontier-specific — needs own design.
- **Worldgen**: Removed. Hand-crafted location maps preferred for FTL-style game.
- **Session model**: "Save file friends join" needs architectural design — host concept vs. always-on server.

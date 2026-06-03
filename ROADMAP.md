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

**Imported YAML cleanup:** a few Ekrixi-era prototypes still need normalization after the main port. The remaining work is slot compatibility for `lowerClothing`, removal of stale duplicate `_FTL` prototypes that still trigger load errors, and the two FTL point effect stubs needed by `_FTL/WarpPoints/points.yml`.

---

## Phase 4a — MCP Game Server ✅ Complete

An MCP server embedded in `Content.Server` that exposes game state and actions as tools, enabling agents (and automated tests) to drive the game without a human client. Built as a pair of EntitySystems (`McpServerSystem` on server, `ClientAgentSystem` on client) that spin up HTTP/SSE endpoints alongside the game.

**Transport:** HTTP SSE — game server on `mcp.port` (default 27050), client agent on `mcp.client_port` (default 27051). Disabled by default (`mcp.enabled false`). Claude Code connects via MCP settings.

**Server tools (`Content.Server/_StarBois/MCP/`):**

| Tool | What it does |
|---|---|
| `world_get_entities_near` | Query entities + components within radius of a map position |
| `world_get_map_info` | List loaded maps and grids with entity counts |
| `world_get_entity_info` | Full component dump for a specific entity UID |
| `admin_execute_command` | Run any server console command |
| `admin_spawn_entity` | Spawn a prototype at map coordinates |
| `test_list_players` | List connected sessions and their attached entities |
| `test_get_player` | Get the first (or named) in-game player entity |
| `test_teleport_entity` | Move any entity to map coordinates |

**Client tools (`Content.Client/_StarBois/MCP/`):**

| Tool | What it does |
|---|---|
| `client_screenshot` | Capture the current client frame as PNG |
| `client_click_control` | Click a UI control by `AgentId` |
| `client_set_control_value` | Set value on a named input control |
| `client_get_control_tree` | Dump all visible UI controls with their `AgentId`s |

**Test harness (`Tools/`):** Four Python scripts — smoke test (connection + tool list), contract test (schema validation), registration test (MCP discovery), and full scenario test (arrange/act/assert).

---

## Phase 4b — Runtime Verification ⏳ Pending

Driven by the MCP server. Server boots, reaches Ready.

- [ ] Star map generates warp points on round start
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

**Context:** Ekrixi already made the fundamental shift — `StandardIndependentShip` inherits `BaseStation + BaseStationShuttles`, so the ship IS the station. The star map, warp drive, and `EndOnShipDestruction` game rule form a working ship-centric loop. What remains is peeling away station-centric SS14 cruft that was inherited alongside it.

**Steps in priority order:**

### 5a — Fix ship selection and crew cap (YAML only) ✅ Complete
- `FTLMapPool` locked to Cestoda only; Myrmeleon removed, disabled ships already commented out
- Cestoda `minPlayers 1` (solo testing), `maxPlayers 4`
- Crew slots trimmed to 4: `Pilot[1,1]`, `StationEngineer[1,1]`, `MedicalDoctor[1,1]`, `Captain[0,1]`; SecurityOfficer and Passenger removed
- Pilot playtime whitelist requirement removed (nobody has accrued time; re-add later if needed)

### 5b — Strip station-centric systems ✅ Complete
- Removed `BaseStationAlertLevels`, `BaseStationExpeditions`, `BaseStationAllEventsEligible` from `StandardIndependentStation`
- `Exploration` preset was already clean — only `EndOnShipDestruction` and `GeneratePoints`; no antag rules needed stripping
- Note: `BaseStationCargo` intentionally kept — cargo bay / resource acquisition fits FTL-style play

### 5c — Simplify crew spawning (C#)
- Replace the full `StationJobs` lobby flow with a direct "spawn on ship as crew" path
- Target four ship roles: **Pilot, Gunner, Engineer, Crew** (generic multi-role)
- No department selection screen; players pick a role and spawn aboard

### 5d — Round model alignment
- `EndOnShipDestruction` calls `RoundEndSystem.EndRound()` → round restarts
- This is already correct: **round = run**, ship destroyed = run over, restart = new run — the same loop as FTL
- Only needed: a "ship destroyed" end screen with run summary before returning to lobby
- Mid-run save/resume is Phase 6, not here

### 5e — Dev tooling
- Console command to skip lobby and spawn a full test crew instantly
- CVar to start with warp drive pre-charged
- MCP test scenario covering the full Phase 5 loop (spawn → warp → combat → destruction)

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

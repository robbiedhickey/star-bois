# star-bois Workspace

## What This Is

A fork of [Space Station 14](https://github.com/space-wizards/space-station-14) being adapted into a small-crew PvE co-op game inspired by FTL: Faster Than Light. 4-player, self-hosted, ship-based. Not a station game.

The upstream SS14 engine (RobustToolbox) already has FTL jump mechanics, ship physics, atmos, power grids, hull breaches, and room-to-room combat — exactly what FTL's gameplay loop needs. The gap was a star map navigation system and AI enemy ships. We ported both from [Ekrixi](https://github.com/ekrixi-14/ekrixi), the only SS14 fork purpose-built for PvE ship combat.

## Key Decisions Made

**Why not Frontier or Wayfarer?** Frontier modified 3,020 upstream core files for an MMO-style persistent economy. We don't want economy, we want co-op sessions. Frontier's ship infrastructure is valuable but comes with massive baggage. We port selectively instead.

**Why not fork Ekrixi directly?** Ekrixi targets .NET 8 / SS14 v225, two years behind upstream (v277). Porting the `_FTL` namespace to current upstream took 2-3 days and gives us a clean foundation.

**Why upstream SS14 as the base?** It already has `ShuttleSystem.FasterThanLight.cs` (1,010 lines) — a full FTL jump API used for evacuation shuttles. All ship physics, atmos, power, and combat are built-in. We add Ekrixi's ship combat layer on top.

**What we are not building:** A round-based station game. An MMO. Anything with a persistent economy or many-player assumptions.

## Repository Structure

```
star-bois/
├── WORKSPACE.md              ← you are here
├── ROADMAP.md                ← phase-by-phase plan with progress
├── ss14-fork-analysis.md     ← research on all major SS14 forks
├── Makefile                  ← make forks / make server / make client
├── forks/                    ← gitignored, run `make forks` to populate
│   ├── ekrixi/               ← PvE ship combat fork (primary reference)
│   ├── frontier-station-14/  ← ship/economy fork (infrastructure reference)
│   ├── wayfarer-14/          ← Frontier-based RP fork
│   ├── deltav/               ← antag/RP fork
│   ├── goobstation/          ← chaos/content fork
│   └── einstein-engines/     ← broad content fork
├── Content.Server/_FTL/      ← our ship combat systems (server)
├── Content.Shared/_FTL/      ← our ship combat systems (shared)
├── Content.Client/_FTL/      ← our ship combat systems (client)
├── Resources/Prototypes/_FTL/← ship entity prototypes, NPC definitions
├── Resources/Maps/_FTL/      ← player ships + AI ship maps
└── ... (upstream SS14 content, untouched)
```

## Getting Started

```bash
# Get reference forks (optional, for research)
make forks

# Run the server
make server

# Run the client (separate terminal)
make client

# Connect: Direct Connect → localhost
```

Requirements: .NET 10 (`brew install dotnet`), Python 3.

## Active Branch

`ftl-port` — all star-bois work lives here. `master` tracks upstream SS14.

## Current State

See `ROADMAP.md` for full detail. Short version:
- Phases 1–3 complete: `_FTL` C# systems compile clean against SS14 v277, all YAML loads, server reaches Ready
- Phase 4 in progress: runtime verification of star map, AI ships, gunner console
- Phases 5–6 pending: game loop adaptation (ship-centric sessions) and save/restore

## What the _FTL Systems Do

- **FTLPoints** — star map UI, warp drive, jump to procedurally-typed destinations
- **AutomatedShip** — AI enemy ships with neutral/hostile states, combat AI, weapon firing
- **ShipTracker** — tracks all ship grids in sector, fires events on destruction
- **ShipWeapons** — manned gunner console: crew member aims and fires ship weapons
- **HeatSeeking** — heat-seeking projectiles
- **TriggerOnEnterGrid** — boarding/collision events
- **ContantDamage** — continuous damage zones (radiation, hostile space)
- **Economy** — lightweight credit system and ATMs (partially stubbed pending own economy design)
- **IPC species** — robotic playable species, ported to current SS14 body/organ system

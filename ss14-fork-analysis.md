# SS14 Fork Analysis

All forks run on the same **RobustToolbox** engine. Differences are entirely in the content layer (`Content.Server/Client/Shared`). Each fork uses prefixed namespaces (`_NF`, `_FTL`, etc.) for their additions, and most forks cherry-pick from each other freely.

**Upstream SS14 baseline: 7,037 .cs files**

---

## Frontier (`_NF`) — 1,267 unique files vs upstream

**GitHub:** [new-frontiers-14/frontier-station-14](https://github.com/new-frontiers-14/frontier-station-14) · `forks/frontier-station-14` · **Web:** [frontierstation14.com](https://frontierstation14.com)

> Everyone gets their own ship. You buy it, you fly it, you make money with it — trading, mining, salvaging, or pirating. There's no single station to man; the whole sector is the map. Frontier is the closest thing to an open-world space sandbox in the SS14 ecosystem and serves as the ship/economy infrastructure that most other forks borrow from.

The ship and economy backbone that most other forks pull from.

**Ships & Navigation**
- Shipyard: buy/sell ships with in-game currency, ship deeds (ownership), shipyard consoles
- ShuttleRecords: persistent log of ship activity
- Extended Shuttle systems: IFF overrides, autopilot, forced anchoring
- PublicTransit: scheduled shuttle routes between points
- Radar: extended radar console UI

**Economy**
- Bank: persistent wallet that survives round resets
- Market: player-driven commodity pricing
- Trade, BountyContracts, CrateMachine
- Cargo expansion, Manufacturing

**Space Content**
- Worldgen (35 files): procedural generation of asteroids, ruins, and derelict ships in open space
- Pirate NPCs (6 server / 7 shared files): player antagonist roles (captain, first mate, crew) auto-assigned via the antag system — not AI ships. Players become pirates and prey on others in the sector.
- Salvage expeditions: structured loot-run missions
- Smuggling system: contraband detection and penalties

**Crew & Station**
- CryoSleep: persistence between sessions via cryo pod
- SectorServices: sector-wide service layer
- PacifiedZones: areas where combat is disabled
- Extended Roles, SizeAttribute, Contraband tracking

**Other**
- Atmos extensions, Power extensions, Speech system, additional Species

---

## Ekrixi (`_FTL`) — 980 unique files vs upstream

**GitHub:** [ekrixi-14/ekrixi](https://github.com/ekrixi-14/ekrixi) · `forks/ekrixi` · **Web:** none (wiki offline)

> Small crew, hostile space, enemy ships that shoot back. Ekrixi is the only fork purpose-built for PvE ship combat — you fly a vessel, encounter AI-controlled enemy ships, fight or flee, and visit locations to trade and restock. The dev described it as "like Barotrauma." It's the smallest and least active fork but has the clearest conceptual overlap with an FTL-style game.

> **Note:** Ekrixi is based on SS14 targeting .NET 8.0 — roughly two years behind current upstream (.NET 10). The large apparent gap vs upstream (2,384 missing files) is almost entirely **staleness**, not intentional removal. Features like CCVar, Silicons, CartridgeLoader, Procedural, and Mapping were added to upstream after Ekrixi forked.

**Ships & Navigation**
- FTLPoints: the core navigation system — a star map UI (`StarmapConsoleComponent`) showing jump destinations, each defined as a prototype with a type tag (planet, dungeon, station, etc.) and a list of spawn effects that trigger on arrival. A physical `WarpDriveComponent` must charge before a jump executes. New sectors generate procedurally over time.
- ShipTracker: tracks all ships in the sector by grid, fires events on ship destruction, and drives the map display
- ShipRename: rename your ship

**Combat**
- AutomatedShip: AI-controlled enemy ships with two states — Cruising (neutral) and Hostile. Combat AI targets crew by a priority system and fires using the same gunner console system that player weapons use. Ship destruction can be configured to end the round.
- ShipWeapons: a manned gunner console — a crew member sits at the station, rotates weapon mounts via a UI, and fires via action commands. Not automated; requires a dedicated gunner.
- HeatSeeking projectiles
- TriggerOnEnterGrid: fires events when an entity enters a ship's grid (boarding, collision damage)
- ContantDamage (sic): zones that apply continuous damage — radiation fields, fire, hostile environments

**Economy**
- Lightweight Economy: simplified currency with fewer moving parts than Frontier's full market
- Pager: inter-ship communication device

**Environment**
- Worldgen (32 files): procedural space content (inherited from an older Frontier version)
- AmbientHeater: environmental heat sources
- ExplodeOnInit: entities that detonate on spawn
- NoAnchor: prevents grids from being anchored (keeps ships mobile)
- Areas: named zone system for ship interiors
- FilmGrain (client): visual effect

**Known gaps vs other forks (due to staleness, not intentional removal)**
- Missing Holopad, Delivery, Changeling, CartridgeLoader, Silicons updates, and ~600 other files added to upstream after Ekrixi's fork point

---

## DeltaV (`_DV`) — 1,599 unique files vs upstream

**GitHub:** [DeltaV-Station/Delta-v](https://github.com/DeltaV-Station/Delta-v) · `forks/deltav` · **Web:** [deltav.gay](https://deltav.gay)

> Classic SS13 chaos with the new engine — DeltaV leans into SS14's core loop but deepens it with psionics, a massive new antagonist faction (CosmicCult), expanded surgery, and character systems like mood, language, and chronic pain. The pitch is "more SS13 weirdness, more mechanical depth, same station format." It's the most upstream-compatible fork and the easiest to pull individual systems from.

Heavily RP and antagonist focused. Closest to upstream of all forks (only 318 upstream files missing, and only 1 confirmed intentional removal: Kitchen).

**Psionics** (79 shared / 13 server / 12 client files)
- Full psionic power system: telekinesis, telepathy, metapsionic abilities
- Psionic records, feedback, and anti-psionic mechanics

**CosmicCult** (48 server / 48 shared / 18 client files)
- Round-ending cult antagonist. Cultists build a Monument to summon a Cosmic God, draw damaging glyphs on the station, recruit/convert crew, and can mindwipe those who resist. Tied to shuttle departure timing — the cult must complete their ritual before or after evacuation. Cultists can be deconverted. Includes polymorph (transformation) mechanics.

**Medical**
- Surgery system
- Chronic pain
- Medical records expansion

**Crew Systems**
- Carrying: physically pick up and carry other players
- Grappling
- Footprints: characters leave visible footprints
- NanoChat: in-game messaging/chat device
- TapeRecorder

**Combat**
- Xenoarchaeology
- Weapon expansions
- Fishing (yes, fishing)

**Other**
- Mood system: persistent emotional state affecting gameplay
- Language system: characters can speak/understand different languages
- Harpy, Feroxi species
- Silicons expansion
- Shipyard and Shuttles (carried from Frontier)

**Removals**
- Kitchen: 1 file removed intentionally; replaced in `_DV/Kitchen`
- Delivery system: not present (may be replaced by own cargo flow)

---

## Goob (`_Shitcode` + `_Shitmed`) — 2,090 unique files vs upstream

**GitHub:** [Goob-Station/Goob-Station](https://github.com/Goob-Station/Goob-Station) · `forks/goobstation` · **Web:** [goobstation.com](https://goobstation.com)

> More antags, more chaos, more stuff. Goob is a kitchen-sink fork that pulls content from across the ecosystem and adds its own — wizard magic, heretic ritual mechanics, a deep surgery overhaul, and a full Lavaland biome. The pitch is maximalist: if another fork built something cool, Goob probably has it too. It self-describes as "super chud SS14 fork. By chuds, for chuds."

The largest additions of any fork. Chaos and fun focused, with two primary namespaces.

**Wizard** (`_Shitcode`, 93 shared / 25 client files)
- Classic SS13 wizard antagonist: a single player spawns as a wizard with a staff, robes, and a spell kit. Spells include teleportation, jaunt/blink, scrying, animal transformation (crew speak in animal accents when transformed), and projectile combat. Wizard death triggers configurable round consequences.

**Heretic** (`_Shitcode`, 120 shared / 36 client files)
- Heretic antagonist with multiple ascension paths, each with a different ritual magic theme. Players perform rituals to progress, collecting reagents and completing steps to reach final ascension. The largest single system in any fork by file count.

**_Shitmed** (348 files total)
- Heavily expanded surgery and medical system

**_Lavaland** (136 shared / 32 server files)
- Lavaland biome: hostile volcanic surface with unique fauna and loot

**Pulled from other forks**
- `_EinsteinEngines`: Shadowling antagonist, Supermatter reactor (137 files)
- `_DV`: Psionics, CosmicCult, crew systems (217 files)
- `_White`: Content from a Russian SS14 server (99 files)
- `_NF`: Frontier ships/economy layer (27 files)

**Removals**
- Bible system (4 files): chaplain bible mechanics removed
- FixedPoint utility (1 file): removed

---

## Einstein Engines (`_EE`) — 2,649 unique files vs upstream

**GitHub:** [Simple-Station/Einstein-Engines](https://github.com/Simple-Station/Einstein-Engines) · `forks/einstein-engines` · **Web:** none

> A contributor platform more than a game. Einstein Engines positions itself as a staging ground for new content — it pulls from nearly every other fork and adds its own unique antagonists (Shadowling, Nightmare, Contractors) and an enhanced Supermatter reactor. The largest unique file count of any fork. More useful as a content library to mine than as a base to ship from; it has the broadest surface area but the least coherent identity.

Broad scope: new antagonists, medical overhaul, crew abilities, and pulls from nearly every other fork.

**Shadowling** (`_EE`, 41 shared / 31 server / 3 client files)
- Shadow-based antagonist with unique powers, thralls, and ascension mechanic

**Supermatter** (`_EE`, 6 shared / 3 server / 4 client files)
- Enhanced supermatter crystal reactor with more detailed simulation

**Contractors** (`_EE`, 9 shared / 3 server files)
- Mercenary/contractor player role

**Nightmare** (`_EE`)
- Dark entity antagonist

**WhiteDream** (95 files)
- Large content block from an external contributor/server

**Pulled from other forks**
- `_Goobstation` (275 files): Heretic, Wizard, Shitmed, Lavaland
- `_Shitmed` (207 files)
- `_Lavaland` (102 files)
- `DeltaV` (118 files): Psionics, CosmicCult
- `_NF` (16 files): ship infrastructure

**Other additions**
- Psionics (57 server files): full psionic system (from DeltaV lineage)
- Language system
- Flight ability
- InteractionVerbs (28 files): expanded interaction options
- Shipyard (13 files): ship purchasing UI
- Mood system
- DiscordAuth, JoinQueue: server management tools
- Shower system (yes, a hygiene system — 2 shared files)
- SelfExtinguisher, TelescopicBaton, Whetstone: misc items

**Removals / Replacements**
- Preferences system (10 files): replaced with own version
- Traits (9 files): replaced with own
- MapText (7 files): removed
- Lobby (5 files): replaced
- Minor: Zombies (1), Guidebook (1), Cloning (1) files removed

---

## Wayfarer (`_WF`) — 1,638 unique files vs upstream

**GitHub:** [project-wayfarer/wayfarer-14](https://github.com/project-wayfarer/wayfarer-14) · `forks/wayfarer-14` · **Web:** [wayfarer14.com](https://www.wayfarer14.com)

> Frontier's ship gameplay with an RP and community layer on top. You still captain your own ship and explore open space, but Wayfarer wraps it in persistent character identity — player-owned corporations, collaborative server goals, items that survive between rounds, and a leveling system tied to roleplay. The most actively developed fork (multiple commits daily), and the most conservative with upstream — it removes nothing. The pitch is "Frontier, but with more reason to care about your character."

Built on Frontier. Wayfarer's own additions (`_WF`, 105 files) sit on top of the full Frontier layer.

**Corporations** (6 server / 8 client files)
- Player-owned company system with shared resources and hierarchy

**CommunityGoals** (5 server / 4 client files)
- Server-wide collaborative objective system that shapes the station's direction

**SafetyDepositBox** (7 shared / 1 server / 2 client files)
- Persistent item storage: items survive between rounds

**RoleplayLeveling** (1 server / 3 shared / 1 client file)
- XP/level system tied to roleplay interactions

**Shuttles** (4 server / 1 shared / 3 client files)
- Autopilot extensions on top of Frontier's navigation

**Consent** (3 server / 4 shared / 5 client files)
- Player opt-in system for certain interactions

**Other**
- Clown/Outlaws faction content
- Corporations admin EUI
- Pulled from: `_CS` (44 files), `_FarHorizons` (32 files), `_EinsteinEngines` (31 files), `_EE` (22 files), `_Floof` (10 files), and others

**Removals**
- None confirmed. Wayfarer is the most conservative fork — 0 upstream files missing that all other forks have.

---

## Cross-Fork Borrowing

Forks pull from each other extensively. The namespace prefixes make the lineage traceable.

| System | Origin | Appears in |
|---|---|---|
| `_NF` ships/economy/worldgen | Frontier | Wayfarer (full), DeltaV (partial), Einstein (partial), Goob (partial) |
| `_DV` psionics + CosmicCult | DeltaV | Einstein, Goob, Wayfarer |
| `_EE` Shadowling + Supermatter | Einstein | Goob, Wayfarer |
| `_Lavaland` biome | Goob | Einstein, DeltaV |
| `_Shitmed` surgery | Goob | Einstein, Wayfarer |
| `_Goobstation` Heretic/Wizard | Goob | Einstein |
| `Worldgen` procedural space | Frontier (origin) | All forks |

---

## What Upstream SS14 Already Has (No Fork Required)

Relevant systems in vanilla SS14 before any fork:

| System | File | Size |
|---|---|---|
| FTL jump system | `ShuttleSystem.FasterThanLight.cs` | 1,010 lines |
| FTL console | `ShuttleConsoleSystem.FTL.cs` | 164 lines |
| IFF (friend/foe) | `ShuttleSystem.IFF.cs` | 130 lines |
| Collision damage | `ShuttleSystem.Impact.cs` | 460 lines |
| Automated docking | `DockingSystem.Shuttle.cs` | 371 lines |
| Thruster simulation | `ThrusterSystem.cs` | 592 lines |
| Radar console | `RadarConsoleSystem.cs` | 61 lines |

The FTL system exposes: `FTLToCoordinates()`, `FTLToDock()`, `TryFTLDock()`, `CanFTL()`, `TryAddFTLDestination()` — a complete jump API with startup/travel/arriving/cooldown state machine, audio, and mass limits. It is used for the emergency evacuation shuttle but is fully general-purpose.

Full atmos simulation, power grid, medical, AI/NPC, combat, and all standard SS14 game systems are also in upstream and inherited by every fork.

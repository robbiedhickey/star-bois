---
description: Search the Space Station 14 / RobustToolbox official docs for codebase conventions, patterns, ECS architecture, netcode, serialization, and engine APIs
---

# SS14 Docs Search Skill

The official Space Station 14 and RobustToolbox documentation lives at `forks/docs/src/en/`. Use this skill to look up codebase conventions, architecture patterns, engine APIs, and design guidelines before writing code or making architectural decisions.

Run `make forks` if `forks/docs/` doesn't exist yet.

## Directory Map

```
forks/docs/src/en/
├── robust-toolbox/          # Engine internals — ECS, IoC, netcode, serialization, rendering
│   ├── ecs.md               # Entity-Component-System architecture (READ THIS FIRST)
│   ├── ioc.md               # Dependency injection container
│   ├── serialization.md     # YAML data definition, DataField, TypeSerializers
│   ├── coordinate-systems.md# Map coords, grid coords, world coords
│   ├── netcode/             # Networking, component states, PVS
│   └── rendering/           # Sprite layers, RSI, shaders
├── space-station-14/        # SS14-specific patterns
│   ├── core-tech/           # Systems architecture, component conventions
│   ├── character-species/   # Species/body system
│   ├── combat/              # Damage, weapons, melee
│   ├── core-design/         # Design philosophy
│   └── departments/         # Job/department system
├── general-development/     # Contributing conventions
│   ├── codebase-info/       # Code style, conventions, PR guidelines
│   ├── setup/               # Dev environment
│   └── tips/                # Common patterns and gotchas
└── ss14-by-example/         # Worked examples (best for learning patterns)
```

## How to Search

**For a specific topic, grep first:**
```bash
grep -r "your topic" forks/docs/src/en/ --include="*.md" -l
```

**Then read the relevant file:**
```bash
cat forks/docs/src/en/robust-toolbox/ecs.md
```

**For code conventions:**
```bash
cat forks/docs/src/en/general-development/codebase-info/
ls forks/docs/src/en/general-development/codebase-info/
```

**For a worked example of implementing something:**
```bash
ls forks/docs/src/en/ss14-by-example/
```

## Key Docs by Task

| Task | Doc |
|---|---|
| Adding a component | `robust-toolbox/ecs.md` |
| YAML DataField / prototype | `robust-toolbox/serialization.md` |
| Networking a component state | `robust-toolbox/netcode/` |
| Coordinate transforms | `robust-toolbox/coordinate-systems.md` |
| Sprite layers / RSI format | `robust-toolbox/rendering/` |
| IoC dependency injection | `robust-toolbox/ioc.md` |
| Species / body system | `space-station-14/character-species/` |
| Damage / combat | `space-station-14/combat/` |
| Code style / PR conventions | `general-development/codebase-info/` |
| End-to-end worked example | `ss14-by-example/` |

## Workflow

1. Grep for the topic to find relevant files
2. Read those files fully — docs are concise
3. Cross-reference with actual code in the repo to see how patterns are applied in practice
4. If docs and code disagree, trust the code (docs may lag behind)

## Keeping Docs Current

```bash
make update-forks   # pulls latest for all forks including docs
```

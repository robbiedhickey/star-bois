FORKS_DIR := forks
# Game preset for server-dev. Options:
#   Exploration  — FTL game mode (EndOnShipDestruction + GeneratePoints). Default.
#   Sandbox      — No rules, free build/test. Good for inspecting ship layout or
#                  testing individual mechanics without a running game loop.
PRESET ?= Exploration

.PHONY: forks update-forks clean-forks help

help:
	@echo "star-bois dev targets:"
	@echo "  make forks              Clone all reference forks into forks/"
	@echo "  make update-forks       Pull latest for each fork"
	@echo "  make clean-forks        Remove forks/ entirely"
	@echo "  make server             Run the game server (no MCP)"
	@echo "  make client             Run the game client"
	@echo "  make server-dev         Run server with MCP on localhost:9222 (PRESET=Exploration)"
	@echo "  make server-dev PRESET=Sandbox   Same but start in Sandbox mode"
	@echo "  make client-dev         Run client connected to localhost:1212 with agent API on localhost:9223"
	@echo "  make mcp-register       Validate repo-local MCP registration"
	@echo "  make mcp-contract       Validate the MCP tool taxonomy"
	@echo "  make mcp-smoke          Smoke-test MCP connection and tool list"
	@echo "  make mcp-scenario       Run an arrange-act-assert MCP scenario"

forks:
	@mkdir -p $(FORKS_DIR)
	@test -d $(FORKS_DIR)/ekrixi || git clone --depth=1 https://github.com/ekrixi-14/ekrixi.git $(FORKS_DIR)/ekrixi
	@test -d $(FORKS_DIR)/frontier-station-14 || git clone --depth=1 https://github.com/new-frontiers-14/frontier-station-14.git $(FORKS_DIR)/frontier-station-14
	@test -d $(FORKS_DIR)/docs || git clone --depth=1 https://github.com/space-wizards/docs.git $(FORKS_DIR)/docs
	@test -d $(FORKS_DIR)/wayfarer-14 || git clone --depth=1 https://github.com/project-wayfarer/wayfarer-14.git $(FORKS_DIR)/wayfarer-14
	@test -d $(FORKS_DIR)/deltav || git clone --depth=1 https://github.com/DeltaV-Station/Delta-v.git $(FORKS_DIR)/deltav
	@test -d $(FORKS_DIR)/goobstation || git clone --depth=1 https://github.com/Goob-Station/Goob-Station.git $(FORKS_DIR)/goobstation
	@test -d $(FORKS_DIR)/einstein-engines || git clone --depth=1 https://github.com/Simple-Station/Einstein-Engines.git $(FORKS_DIR)/einstein-engines
	@echo "All forks ready in $(FORKS_DIR)/"

update-forks:
	@for fork in $(FORKS_DIR)/*/; do \
		echo "Updating $$fork..."; \
		git -C $$fork pull --depth=1 origin HEAD; \
	done

clean-forks:
	rm -rf $(FORKS_DIR)

server:
	dotnet run --project Content.Server

client:
	dotnet run --project Content.Client

server-dev:
	dotnet run --project Content.Server -- --cvar mcp.enabled=true --cvar mcp.port=9222 --cvar mcp.client_url=http://localhost:9223 --cvar game.defaultpreset=$(PRESET) --cvar game.map=Cestoda --cvar game.lobbyenabled=true --cvar starmap.generate_roundstart=true

client-dev:
	dotnet run --project Content.Client -- --connect --connect-address localhost:1212 \
		--cvar mcp.enabled=true --cvar mcp.client_port=9223

mcp-register:
	python3 Tools/starbois_mcp_registration.py

mcp-smoke:
	python3 Tools/starbois_mcp_smoke.py

mcp-contract:
	python3 Tools/starbois_mcp_contract.py

mcp-scenario:
	python3 Tools/starbois_mcp_test_scenario.py

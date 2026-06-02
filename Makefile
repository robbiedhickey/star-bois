FORKS_DIR := forks

.PHONY: forks update-forks clean-forks help

help:
	@echo "star-bois dev targets:"
	@echo "  make forks         Clone all reference forks into forks/"
	@echo "  make update-forks  Pull latest for each fork"
	@echo "  make clean-forks   Remove forks/ entirely"
	@echo "  make server        Run the game server"
	@echo "  make client        Run the game client"

forks:
	@mkdir -p $(FORKS_DIR)
	@test -d $(FORKS_DIR)/ekrixi || git clone --depth=1 https://github.com/ekrixi-14/ekrixi.git $(FORKS_DIR)/ekrixi
	@test -d $(FORKS_DIR)/frontier-station-14 || git clone --depth=1 https://github.com/new-frontiers-14/frontier-station-14.git $(FORKS_DIR)/frontier-station-14
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

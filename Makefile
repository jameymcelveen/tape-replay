# TapeReplay — Electron + React + .NET
# Single-app bundle: self-contained .NET backend + Vite frontend + Electron shell

.PHONY: help install clean dev dev-ui demo demo-install demo-data-pull demo-overview demo-strategy demo-chart demo-chart-heatmap build build-frontend publish-backend stage bundle run test verify-backend installer-mac installer-win installers build-patch cdn-dist deploy publish-data record record-overnight verify-data

TICKERS   ?= EDHL,CCHH,CAST,VSME,JRSH
DATE_FROM ?= 2026-06-11
DATE_TO   ?= 2026-06-17

ROOT_DIR        := $(abspath $(dir $(lastword $(MAKEFILE_LIST))))
BACKEND_PROJECT := backend/TapeReplay.Api.csproj
TEST_PROJECT    := backend/tests/TapeReplay.Api.Tests/TapeReplay.Api.Tests.csproj
ARTIFACTS_DIR   := artifacts
BACKEND_OUT     := $(ARTIFACTS_DIR)/backend
FRONTEND_DIR    := frontend
RELEASE_DIR     := release
CDN_DIST_DIR    := dist
SURGE_DOMAIN    ?= tapereplay.surge.sh
CONFIGURATION   ?= Release
INSTALLER_SCRIPT := ./scripts/build-installer.sh

OS   := $(shell uname -s)
ARCH := $(shell uname -m)

ifndef DOTNET_RID
ifeq ($(OS),Darwin)
  ifeq ($(ARCH),arm64)
    DOTNET_RID := osx-arm64
  else
    DOTNET_RID := osx-x64
  endif
else ifeq ($(OS),Linux)
  DOTNET_RID := linux-x64
else
  DOTNET_RID := win-x64
endif
endif

help: ## Show available targets
	@echo "TapeReplay Makefile"
	@echo ""
	@echo "Targets:"
	@grep -E '^[a-zA-Z0-9_-]+:.*##' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*## "}; {printf "  %-18s %s\n", $$1, $$2}'
	@echo ""
	@echo "Detected platform RID: $(DOTNET_RID) (override with DOTNET_RID=...)"

install: ## Install npm and dotnet dependencies
	npm install
	npm install --prefix $(FRONTEND_DIR)
	dotnet restore TapeReplay.sln

clean: ## Remove build artifacts and release output (repo-local paths only)
	@test -n "$(ROOT_DIR)" && test "$(ROOT_DIR)" != "/"
	@test -f "$(ROOT_DIR)/Makefile" || (echo "clean: refusing — ROOT_DIR is not the repo: $(ROOT_DIR)" && exit 1)
	@test -n "$(ARTIFACTS_DIR)" && test -n "$(RELEASE_DIR)" && test -n "$(CDN_DIST_DIR)"
	@test "$(ARTIFACTS_DIR)" != "/" && test "$(CDN_DIST_DIR)" != "/"
	rm -rf "$(ROOT_DIR)/$(ARTIFACTS_DIR)"
	rm -rf "$(ROOT_DIR)/$(RELEASE_DIR)"
	rm -rf "$(ROOT_DIR)/$(CDN_DIST_DIR)"
	rm -rf "$(ROOT_DIR)/$(FRONTEND_DIR)/dist"
	rm -rf "$(ROOT_DIR)/backend/bin" "$(ROOT_DIR)/backend/obj"
	@echo "Clean complete."

dev: ## Run backend, Vite, and Electron in development mode
	npm run dev

dev-ui: ## Run backend + Vite only (for browser demos at http://localhost:5173)
	npm run dev:ui

demo-install: ## Install Playwright Chromium for showcase scripts
	npm run demo:install

demo-preflight: ## Verify :5173 and :5180 are up before running a demo
	chmod +x scripts/demo-preflight.sh
	./scripts/demo-preflight.sh

demo-data-pull: demo-preflight ## Headed demo: Data pull view (pre-filled DB, human pace)
	npm run demo:data-pull

demo-overview: demo-preflight ## Headed demo: Exploratory overview heatmap
	npm run demo:overview

demo-strategy: demo-preflight ## Headed demo: Strategy lab exploratory run
	npm run demo:strategy

demo-chart: demo-preflight ## Headed demo: Chart backtest candlesticks
	npm run demo:chart

demo-chart-heatmap: demo-preflight ## Headed demo: Chart heatmap coverage/performance
	npm run demo:chart-heatmap

build-frontend: ## Build React frontend for production
	npm run build:frontend

publish-backend: ## Publish self-contained .NET backend for this OS/arch
	@mkdir -p $(BACKEND_OUT)
	dotnet publish $(BACKEND_PROJECT) \
		-c $(CONFIGURATION) \
		-r $(DOTNET_RID) \
		--self-contained true \
		-o $(BACKEND_OUT) \
		/p:PublishSingleFile=false \
		/p:IncludeNativeLibrariesForSelfExtract=true

stage: build-frontend publish-backend ## Stage frontend dist and backend publish for bundling
	@test -f $(BACKEND_OUT)/TapeReplay.Api$(if $(filter win-x64,$(DOTNET_RID)),.exe,) \
		|| (echo "Backend executable missing in $(BACKEND_OUT)" && exit 1)
	@test -f $(FRONTEND_DIR)/dist/index.html \
		|| (echo "Frontend dist missing. Run make build-frontend." && exit 1)
	@echo "Stage ready: $(FRONTEND_DIR)/dist + $(BACKEND_OUT)"

build: stage ## Build all production artifacts without packaging Electron
	@echo "Build complete (not bundled)."

bundle: stage ## Package a single installable Electron app (DMG/ZIP on macOS)
	CSC_IDENTITY_AUTO_DISCOVERY=false npm run bundle
	@echo ""
	@echo "Bundle complete. Installers are in $(RELEASE_DIR)/"

installer-mac: ## Build Mac DMG + ZIP for current CPU (arm64 or x64)
	chmod +x $(INSTALLER_SCRIPT)
	$(INSTALLER_SCRIPT) mac $(ARCH)

installer-mac-arm64: ## Build Mac DMG + ZIP for Apple Silicon
	chmod +x $(INSTALLER_SCRIPT)
	$(INSTALLER_SCRIPT) mac arm64

installer-mac-x64: ## Build Mac DMG + ZIP for Intel Macs
	chmod +x $(INSTALLER_SCRIPT)
	$(INSTALLER_SCRIPT) mac x64

installer-win: ## Build Windows installer (NSIS on Windows; zip/portable on macOS)
	chmod +x $(INSTALLER_SCRIPT)
	$(INSTALLER_SCRIPT) win x64

installers: installer-mac ## Build installer for the current OS (use CI for cross-platform)
	@echo "For the other platform, run on Windows/macOS or tag with a minor/major version bump."

build-patch: cdn-dist ## Alias for cdn-dist (usage: make build-patch PATCH=0.1.1)

cdn-dist: ## Build dist/ for surge deploy (PATCH=0.1.1 SURGE_DOMAIN=foo.surge.sh INCLUDE_INSTALLERS=1)
	chmod +x scripts/build-cdn-dist.sh scripts/build-patch.sh scripts/generate-cdn-index.mjs
	SURGE_DOMAIN="$(SURGE_DOMAIN)" INCLUDE_INSTALLERS="$(INCLUDE_INSTALLERS)" PATCH="$(PATCH)" \
		./scripts/build-cdn-dist.sh $(PATCH)

deploy: cdn-dist ## Build dist/ and publish to Surge (PATCH=... SURGE_DOMAIN=... INCLUDE_INSTALLERS=1)
	@test -n "$(SURGE_DOMAIN)"
	@test "$(SURGE_DOMAIN)" != "/"
	@test -f "$(ROOT_DIR)/$(CDN_DIST_DIR)/index.html" || (echo "deploy: missing $(CDN_DIST_DIR)/index.html" && exit 1)
	@test -f "$(ROOT_DIR)/$(CDN_DIST_DIR)/manifest.json" || (echo "deploy: missing $(CDN_DIST_DIR)/manifest.json" && exit 1)
	cd "$(ROOT_DIR)/$(CDN_DIST_DIR)" && npx --yes surge . "$(SURGE_DOMAIN)"
	@echo "Deployed to https://$(SURGE_DOMAIN)/"

publish-data: ## Export data partitions to publish/data/ (backend must be running)
	curl -sf -X POST http://localhost:5180/api/data/publish | (command -v jq >/dev/null && jq . || cat)

record: ## Pull minute bars from Polygon (backend must be running; TICKERS=... DATE_FROM=... DATE_TO=...)
	chmod +x scripts/record.sh
	./scripts/record.sh "$(TICKERS)" "$(DATE_FROM)" "$(DATE_TO)"

record-overnight: ## Same as record with progress rounds (matt-five defaults; RECORD_DATE_FROM/TO override)
	chmod +x scripts/overnight-record.sh
	./scripts/overnight-record.sh

verify-data: ## Show bar counts for EDHL,CCHH,CAST,VSME,JRSH in local SQLite
	chmod +x scripts/verify-data.sh
	./scripts/verify-data.sh backend/tapereplay.db

run: stage ## Run staged Electron app (built frontend/dist; use make dev for hot reload)
	NODE_ENV=production npm start

VERIFY_BACKEND_PORT ?= 5199

verify-backend: publish-backend ## Smoke-test the published backend binary
	@ASPNETCORE_URLS=http://127.0.0.1:$(VERIFY_BACKEND_PORT) $(BACKEND_OUT)/TapeReplay.Api & \
		BACKEND_PID=$$!; \
		sleep 3; \
		curl -sf http://127.0.0.1:$(VERIFY_BACKEND_PORT)/api/health > /dev/null; \
		STATUS=$$?; \
		kill $$BACKEND_PID 2>/dev/null || true; \
		if [ $$STATUS -eq 0 ]; then echo "Backend health check passed."; else echo "Backend health check failed."; exit 1; fi

test: ## Run unit tests and backend health smoke test
	dotnet test $(TEST_PROJECT) -c $(CONFIGURATION)
	npm run test --prefix $(FRONTEND_DIR)
	dotnet build $(BACKEND_PROJECT) -c $(CONFIGURATION)
	$(MAKE) verify-backend

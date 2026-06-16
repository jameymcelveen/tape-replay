# TapeReplay — Electron + React + .NET
# Single-app bundle: self-contained .NET backend + Vite frontend + Electron shell

.PHONY: help install clean dev build build-frontend publish-backend stage bundle run test verify-backend

BACKEND_PROJECT := backend/TapeReplay.Api.csproj
ARTIFACTS_DIR   := artifacts
BACKEND_OUT     := $(ARTIFACTS_DIR)/backend
FRONTEND_DIR    := frontend
RELEASE_DIR     := release
CONFIGURATION   ?= Release

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
	dotnet restore $(BACKEND_PROJECT)

clean: ## Remove build artifacts and release output
	rm -rf $(ARTIFACTS_DIR) $(RELEASE_DIR)
	rm -rf $(FRONTEND_DIR)/dist
	rm -rf backend/bin backend/obj
	@echo "Clean complete."

dev: ## Run backend, Vite, and Electron in development mode
	npm run dev

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

run: stage ## Run the production Electron shell locally (unbundled)
	npm start

verify-backend: publish-backend ## Smoke-test the published backend binary
	@$(BACKEND_OUT)/TapeReplay.Api & \
		BACKEND_PID=$$!; \
		sleep 3; \
		curl -sf http://localhost:5180/api/health > /dev/null; \
		STATUS=$$?; \
		kill $$BACKEND_PID 2>/dev/null || true; \
		if [ $$STATUS -eq 0 ]; then echo "Backend health check passed."; else echo "Backend health check failed."; exit 1; fi

test: ## Build backend and frontend, verify backend health
	dotnet build $(BACKEND_PROJECT) -c $(CONFIGURATION)
	$(MAKE) verify-backend

# agent-pmo:d75d5c8
# =============================================================================
# Standard Makefile — Nimblesite.DataProvider.Core
# Cross-platform: Linux, macOS, Windows (via GNU Make)
# All targets are language-agnostic. Add language-specific helpers below.
# =============================================================================

.PHONY: build test lint fmt fmt-check clean check ci coverage setup

# -----------------------------------------------------------------------------
# OS Detection — portable commands for Linux, macOS, and Windows
# On Windows, run via GNU Make with PowerShell (e.g., make from Git Bash or
# choco install make). The $(OS) variable is set to "Windows_NT" automatically.
# -----------------------------------------------------------------------------
ifeq ($(OS),Windows_NT)
  SHELL := powershell.exe
  .SHELLFLAGS := -NoProfile -Command
  RM = Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
  MKDIR = New-Item -ItemType Directory -Force
  HOME ?= $(USERPROFILE)
else
  # Force bash for non-Windows. Some recipes use bash-only constructs
  # (e.g. ${PIPESTATUS[0]}, [[ ... ]]), and Ubuntu's default /bin/sh is dash.
  SHELL := /bin/bash
  RM = rm -rf
  MKDIR = mkdir -p
endif

# All .NET test projects (one per line for readability)
DOTNET_TEST_PROJECTS = \
  DataProvider/Nimblesite.DataProvider.Tests \
  DataProvider/Nimblesite.DataProvider.Example.Tests \
  Lql/Nimblesite.Lql.Tests \
  Lql/Nimblesite.Lql.TypeProvider.FSharp.Tests \
  Migration/Nimblesite.DataProvider.Migration.Tests \
  Sync/Nimblesite.Sync.Tests \
  Sync/Nimblesite.Sync.SQLite.Tests \
  Sync/Nimblesite.Sync.Postgres.Tests \
  Sync/Nimblesite.Sync.Integration.Tests \
  Sync/Nimblesite.Sync.Http.Tests \
  Reporting/Nimblesite.Reporting.Tests \
  Reporting/Nimblesite.Reporting.Integration.Tests

# =============================================================================
# PRIMARY TARGETS (uniform interface — do not rename)
# =============================================================================

## build: Compile/assemble all artifacts
build:
	@echo "==> Building..."
	$(MAKE) _build

## test: Run full test suite with coverage enforcement
test:
	@echo "==> Testing..."
	$(MAKE) _test

## lint: Run all linters (fails on any warning)
lint:
	@echo "==> Linting..."
	$(MAKE) _lint

## fmt: Format all code in-place
fmt:
	@echo "==> Formatting..."
	$(MAKE) _fmt

## fmt-check: Check formatting without modifying
fmt-check:
	@echo "==> Checking format..."
	$(MAKE) _fmt_check

## clean: Remove all build artifacts
clean:
	@echo "==> Cleaning..."
	$(MAKE) _clean

## check: lint + test (pre-commit)
check: lint test

## ci: lint + test + build (full CI simulation)
ci: lint test build

## coverage: Generate HTML coverage report (runs tests first)
coverage:
	@echo "==> Coverage report..."
	$(MAKE) _coverage

## vsix: Build Rust LSP (release), compile & package the VS Code extension (.vsix), and install it
vsix:
	@echo "==> Building and packaging VSIX..."
	bash Lql/lql-lsp-rust/build-vsix.sh

## setup: Post-create dev environment setup (used by devcontainer)
setup:
	@echo "==> Setting up development environment..."
	$(MAKE) _setup
	@echo "==> Setup complete. Run 'make ci' to validate."

# =============================================================================
# LANGUAGE-SPECIFIC IMPLEMENTATIONS
# =============================================================================

_build: _build_dotnet _build_rust _build_ts

_test: _test_dotnet _test_rust _test_ts

_lint: _lint_dotnet _lint_rust _lint_ts

_fmt: _fmt_dotnet _fmt_rust

_fmt_check: _fmt_check_dotnet _fmt_check_rust

_clean: _clean_dotnet _clean_rust _clean_ts

_coverage: _coverage_dotnet

_setup: _setup_dotnet _setup_ts

# =============================================================================
# COVERAGE ENFORCEMENT (shared shell logic)
# =============================================================================
# Each test target collects coverage, compares against coverage-thresholds.json,
# fails hard if below, and ratchets up if above.
#
# coverage-thresholds.json keys are SOURCE project paths, e.g.:
#   "DataProvider/Nimblesite.DataProvider.Core": { "threshold": 88, "include": "..." }
#
# The SRC_KEY mapping converts test project paths -> source project keys.
# CI calls these same make targets — no duplication.
# =============================================================================

# --- C#/.NET ---
_build_dotnet:
	dotnet build DataProvider.sln --configuration Release

_test_dotnet:
	@for test_proj in $(DOTNET_TEST_PROJECTS); do \
	  SRC_KEY=$$(echo "$$test_proj" | sed 's/\.Tests$$//'); \
	  case "$$SRC_KEY" in \
	    "DataProvider/Nimblesite.DataProvider") SRC_KEY="DataProvider/Nimblesite.DataProvider.Core" ;; \
	    "DataProvider/Nimblesite.DataProvider.Example") ;; \
	    "Lql/Nimblesite.Lql") SRC_KEY="Lql/Nimblesite.Lql.Core" ;; \
	    "Lql/Nimblesite.Lql.TypeProvider.FSharp") ;; \
	    "Migration/Nimblesite.DataProvider.Migration") SRC_KEY="Migration/Nimblesite.DataProvider.Migration.Core" ;; \
	    "Sync/Nimblesite.Sync") SRC_KEY="Sync/Nimblesite.Sync.Core" ;; \
	    "Sync/Nimblesite.Sync.SQLite") ;; \
	    "Sync/Nimblesite.Sync.Postgres") ;; \
	    "Sync/Nimblesite.Sync.Integration") ;; \
	    "Sync/Nimblesite.Sync.Http") ;; \
	    "Reporting/Nimblesite.Reporting") ;; \
	    "Reporting/Nimblesite.Reporting.Integration") ;; \
	  esac; \
	  THRESHOLD=$$(jq -r ".projects[\"$$SRC_KEY\"].threshold // .default_threshold" coverage-thresholds.json); \
	  INCLUDE=$$(jq -r ".projects[\"$$SRC_KEY\"].include // empty" coverage-thresholds.json); \
	  echo ""; \
	  echo "============================================================"; \
	  echo "==> Testing $$SRC_KEY (via $$test_proj, threshold: $$THRESHOLD%)"; \
	  if [ -n "$$INCLUDE" ]; then echo "  Include filter: $$INCLUDE"; fi; \
	  echo "============================================================"; \
	  rm -rf "$$test_proj/TestResults"; \
	  if [ -n "$$INCLUDE" ]; then \
	    dotnet test "$$test_proj" --configuration Release \
	      --settings coverlet.runsettings \
	      --collect:"XPlat Code Coverage" \
	      --results-directory "$$test_proj/TestResults" \
	      --verbosity normal \
	      -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Include="$$INCLUDE"; \
	  else \
	    dotnet test "$$test_proj" --configuration Release \
	      --settings coverlet.runsettings \
	      --collect:"XPlat Code Coverage" \
	      --results-directory "$$test_proj/TestResults" \
	      --verbosity normal; \
	  fi; \
	  if [ $$? -ne 0 ]; then \
	    echo "FAIL [$$SRC_KEY]: Tests failed ($$test_proj)"; \
	    exit 1; \
	  fi; \
	  COBERTURA=$$(find "$$test_proj/TestResults" -name "coverage.cobertura.xml" -type f 2>/dev/null | head -1); \
	  if [ -z "$$COBERTURA" ]; then \
	    echo "FAIL [$$SRC_KEY]: No coverage file produced ($$test_proj)"; \
	    exit 1; \
	  fi; \
	  LINE_RATE=$$(sed -n 's/.*line-rate="\([0-9.]*\)".*/\1/p' "$$COBERTURA" | head -1); \
	  if [ -z "$$LINE_RATE" ]; then \
	    echo "FAIL [$$SRC_KEY]: Could not parse line-rate from $$COBERTURA"; \
	    exit 1; \
	  fi; \
	  COVERAGE=$$(echo "$$LINE_RATE * 100" | bc -l); \
	  COVERAGE_FMT=$$(printf "%.2f" $$COVERAGE); \
	  echo ""; \
	  echo "  [$$SRC_KEY] Coverage: $$COVERAGE_FMT% | Threshold: $$THRESHOLD%"; \
	  BELOW=$$(echo "$$COVERAGE < $$THRESHOLD" | bc -l); \
	  if [ "$$BELOW" = "1" ]; then \
	    echo "  FAIL [$$SRC_KEY]: $$COVERAGE_FMT% is BELOW threshold $$THRESHOLD%"; \
	    exit 1; \
	  fi; \
	  ABOVE=$$(echo "$$COVERAGE > $$THRESHOLD" | bc -l); \
	  if [ "$$ABOVE" = "1" ]; then \
	    NEW=$$(echo "$$COVERAGE" | awk '{print int($$1)}'); \
	    echo "  Ratcheting threshold: $$THRESHOLD% -> $$NEW%"; \
	    jq ".projects[\"$$SRC_KEY\"].threshold = $$NEW" coverage-thresholds.json > coverage-thresholds.json.tmp && mv coverage-thresholds.json.tmp coverage-thresholds.json; \
	  fi; \
	  echo "  PASS [$$SRC_KEY]"; \
	done; \
	echo ""; \
	echo "==> All .NET test projects passed coverage thresholds."

_lint_dotnet:
	dotnet build DataProvider.sln --configuration Release
	dotnet csharpier check .

_fmt_dotnet:
	dotnet csharpier format .

_fmt_check_dotnet:
	dotnet csharpier check .

_clean_dotnet:
ifeq ($(OS),Windows_NT)
	Get-ChildItem -Recurse -Directory -Include bin,obj -Exclude lql-lsp-rust | Remove-Item -Recurse -Force
	$(RM) TestResults
else
	find . -type d \( -name bin -o -name obj \) -not -path './Lql/lql-lsp-rust/*' | xargs rm -rf
	$(RM) TestResults
endif

_coverage_dotnet:
	$(MAKE) _test_dotnet
	reportgenerator -reports:"**/TestResults/**/coverage.cobertura.xml" \
	  -targetdir:coverage/html -reporttypes:Html
ifeq ($(OS),Windows_NT)
	Start-Process coverage/html/index.html
else ifeq ($(shell uname -s),Darwin)
	open coverage/html/index.html
else
	xdg-open coverage/html/index.html
endif

_setup_dotnet:
	dotnet restore
	dotnet tool restore

# --- RUST (LQL LSP) ---
_build_rust:
	cd Lql/lql-lsp-rust && cargo build --release

_test_rust:
	@THRESHOLD=$$(jq -r '.projects["Lql/lql-lsp-rust"].threshold // .default_threshold' coverage-thresholds.json); \
	echo ""; \
	echo "============================================================"; \
	echo "==> Testing Lql/lql-lsp-rust (threshold: $$THRESHOLD%)"; \
	echo "============================================================"; \
	cd Lql/lql-lsp-rust && cargo tarpaulin --workspace --skip-clean \
	  --exclude-files 'crates/lql-parser/src/generated/*' \
	  --exclude-files 'crates/lql-lsp/tests/*' \
	  2>&1 | tee /tmp/_dp_tarpaulin_out.txt; \
	TARP_EXIT=$${PIPESTATUS[0]}; \
	if [ $$TARP_EXIT -ne 0 ]; then \
	  echo "FAIL [Lql/lql-lsp-rust]: cargo tarpaulin failed"; \
	  exit 1; \
	fi; \
	COVERAGE=$$(grep -oE '[0-9]+\.[0-9]+% coverage' /tmp/_dp_tarpaulin_out.txt | tail -1 | grep -oE '[0-9]+\.[0-9]+'); \
	if [ -z "$$COVERAGE" ]; then \
	  echo "FAIL [Lql/lql-lsp-rust]: Could not parse coverage from tarpaulin output"; \
	  exit 1; \
	fi; \
	echo ""; \
	echo "  [Lql/lql-lsp-rust] Coverage: $$COVERAGE% | Threshold: $$THRESHOLD%"; \
	BELOW=$$(echo "$$COVERAGE < $$THRESHOLD" | bc -l); \
	if [ "$$BELOW" = "1" ]; then \
	  echo "  FAIL [Lql/lql-lsp-rust]: $$COVERAGE% is BELOW threshold $$THRESHOLD%"; \
	  exit 1; \
	fi; \
	ABOVE=$$(echo "$$COVERAGE > $$THRESHOLD" | bc -l); \
	if [ "$$ABOVE" = "1" ]; then \
	  NEW=$$(echo "$$COVERAGE" | awk '{print int($$1)}'); \
	  echo "  Ratcheting threshold: $$THRESHOLD% -> $$NEW%"; \
	  cd "$(CURDIR)" && jq '.projects["Lql/lql-lsp-rust"].threshold = '"$$NEW" coverage-thresholds.json > coverage-thresholds.json.tmp && mv coverage-thresholds.json.tmp coverage-thresholds.json; \
	fi; \
	echo "  PASS [Lql/lql-lsp-rust]"

_lint_rust:
	cd Lql/lql-lsp-rust && cargo fmt --all --check
	cd Lql/lql-lsp-rust && cargo clippy --workspace --all-targets -- -D warnings

_fmt_rust:
	cd Lql/lql-lsp-rust && cargo fmt --all

_fmt_check_rust:
	cd Lql/lql-lsp-rust && cargo fmt --all --check

_clean_rust:
	cd Lql/lql-lsp-rust && cargo clean

# --- TYPESCRIPT (LQL Extension) ---
_build_ts:
	cd Lql/LqlExtension && npm install --no-audit --no-fund && npm run compile

# Ensure the lql-lsp binary exists before running VSIX tests, so the
# extension can find a matching --version on PATH and start the LSP
# without trying to download a release from GitHub.
_ensure_lql_lsp_on_path:
	@if [ ! -x "$(CURDIR)/Lql/lql-lsp-rust/target/release/lql-lsp" ] && \
	    [ ! -x "$(CURDIR)/Lql/lql-lsp-rust/target/debug/lql-lsp" ]; then \
	  echo "==> Building lql-lsp (release) for VSIX tests"; \
	  cd Lql/lql-lsp-rust && cargo build --release -p lql-lsp; \
	fi

_test_ts: _ensure_lql_lsp_on_path
	@THRESHOLD=$$(jq -r '.projects["Lql/LqlExtension"].threshold // .default_threshold' coverage-thresholds.json); \
	echo ""; \
	echo "============================================================"; \
	echo "==> Testing Lql/LqlExtension (threshold: $$THRESHOLD%)"; \
	echo "============================================================"; \
	export PATH="$(CURDIR)/Lql/lql-lsp-rust/target/release:$(CURDIR)/Lql/lql-lsp-rust/target/debug:$$PATH"; \
	unset ELECTRON_RUN_AS_NODE; \
	echo "  lql-lsp on PATH: $$(command -v lql-lsp || echo 'NOT FOUND')"; \
	echo "  lql-lsp --version: $$(lql-lsp --version 2>&1 || echo 'failed')"; \
	cd Lql/LqlExtension && \
	  npx vsce package --no-git-tag-version --no-update-package-json && \
	  rm -rf out-cov && npx nyc instrument --include='out/**/*.js' --exclude='out/test/**' --no-all out out-cov && cp -R out-cov/. out/ && rm -rf out-cov && \
	  if command -v xvfb-run >/dev/null 2>&1; then \
	    xvfb-run -a node ./out/test/runTest.js; \
	  else \
	    node ./out/test/runTest.js; \
	  fi && \
	  npx nyc report --reporter=json-summary --reporter=text; \
	if [ $$? -ne 0 ]; then \
	  echo "FAIL [Lql/LqlExtension]: Extension tests failed"; \
	  exit 1; \
	fi; \
	SUMMARY="$(CURDIR)/Lql/LqlExtension/coverage/coverage-summary.json"; \
	if [ ! -f "$$SUMMARY" ]; then \
	  SUMMARY="$(CURDIR)/Lql/LqlExtension/.nyc_output/coverage-summary.json"; \
	fi; \
	if [ ! -f "$$SUMMARY" ]; then \
	  echo "  WARN [Lql/LqlExtension]: No coverage summary produced (cross-process instrumentation skipped); treating as 0%"; \
	  COVERAGE=0; \
	else \
	  COVERAGE=$$(jq -r '.total.lines.pct' "$$SUMMARY"); \
	fi; \
	echo ""; \
	echo "  [Lql/LqlExtension] Coverage: $$COVERAGE% | Threshold: $$THRESHOLD%"; \
	BELOW=$$(echo "$$COVERAGE < $$THRESHOLD" | bc -l); \
	if [ "$$BELOW" = "1" ]; then \
	  echo "  FAIL [Lql/LqlExtension]: $$COVERAGE% is BELOW threshold $$THRESHOLD%"; \
	  exit 1; \
	fi; \
	ABOVE=$$(echo "$$COVERAGE > $$THRESHOLD" | bc -l); \
	if [ "$$ABOVE" = "1" ]; then \
	  NEW=$$(echo "$$COVERAGE" | awk '{print int($$1)}'); \
	  echo "  Ratcheting threshold: $$THRESHOLD% -> $$NEW%"; \
	  jq '.projects["Lql/LqlExtension"].threshold = '"$$NEW" "$(CURDIR)/coverage-thresholds.json" > "$(CURDIR)/coverage-thresholds.json.tmp" && mv "$(CURDIR)/coverage-thresholds.json.tmp" "$(CURDIR)/coverage-thresholds.json"; \
	fi; \
	echo "  PASS [Lql/LqlExtension]"

_lint_ts:
	cd Lql/LqlExtension && npm run lint

_clean_ts:
	$(RM) Lql/LqlExtension/node_modules Lql/LqlExtension/out

_setup_ts:
	cd Lql/LqlExtension && npm install --no-audit --no-fund

# =============================================================================
# HELP
# =============================================================================
help:
	@echo "Available targets:"
	@echo "  build          - Compile/assemble all artifacts"
	@echo "  test           - Run full test suite with coverage enforcement"
	@echo "  lint           - Run all linters (errors mode)"
	@echo "  fmt            - Format all code in-place"
	@echo "  fmt-check      - Check formatting (no modification)"
	@echo "  clean          - Remove build artifacts"
	@echo "  check          - lint + test (pre-commit)"
	@echo "  ci             - lint + test + build (full CI)"
	@echo "  coverage       - Generate and open HTML coverage report"
	@echo "  setup          - Post-create dev environment setup"
	@echo "  vsix           - Build LSP + compile & package VS Code extension (.vsix)"

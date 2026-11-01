.DEFAULT_GOAL := build
.DELETE_ON_ERROR:

DOTNET ?= dotnet
NIX ?= nix

SOLUTION := interval.slnx
SRC_PROJECTS := $(wildcard src/*/*.fsproj)
EXAMPLE_PROJECTS := $(wildcard examples/*/*.fsproj)

NUGET_SOURCE := https://api.nuget.org/v3/index.json
NUGET_PACKAGES_DIR := out/nix-lock
NUGET_TO_JSON ?= nixpkgs\#nuget-to-json
PACKAGE_ID := Interval.Fs
DOCS_OUTPUT := out/docs
PACKAGE_OUTPUT := out/packages
COVERAGE_DIR := $(CURDIR)/coverage

# Directory.Build.props declares the version, which is also what the flake reads, so a local
# pack and a Nix build agree on what they are building.
declared := $(shell sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props | head -n 1)
VERSION ?= $(declared)

PROJECT_FILES := $(wildcard src/*/*.fsproj tests/*/*.fsproj tools/*/*.fsproj examples/*/*.fsproj)
RESTORE_INPUTS := Makefile $(SOLUTION) global.json nuget.config $(PROJECT_FILES) \
	$(wildcard Directory.Build.* Directory.Packages.*)
PACKAGE_INPUTS := $(SRC_PROJECTS) $(wildcard src/*/*.fs) Directory.Build.props global.json README.md
PACKAGES := $(PACKAGE_OUTPUT)/$(PACKAGE_ID).$(VERSION).nupkg $(PACKAGE_OUTPUT)/$(PACKAGE_ID).$(VERSION).snupkg

.PHONY: build
build:
	$(DOTNET) build $(SOLUTION)

.PHONY: test
test:
	$(DOTNET) test $(SOLUTION)

.PHONY: coverage
coverage:
	$(RM) -r $(COVERAGE_DIR)
	$(DOTNET) tool restore
	$(DOTNET) test $(SOLUTION) \
		/p:CollectCoverage=true \
		/p:CoverletOutputFormat=cobertura \
		/p:CoverletOutput=$(COVERAGE_DIR)/
	$(DOTNET) reportgenerator \
		-reports:"$(COVERAGE_DIR)/coverage.cobertura.xml" \
		-targetdir:"$(COVERAGE_DIR)/html" \
		-reporttypes:Html
	@echo "HTML coverage report: $(COVERAGE_DIR)/html/index.html"

.PHONY: fmt
fmt:
	$(NIX) fmt

.PHONY: docs
docs: $(DOCS_OUTPUT)/index.html

$(DOCS_OUTPUT)/index.html: docfx.json dotnet-tools.json $(wildcard docs/*)
	$(DOTNET) tool restore
	$(RM) -r $(DOCS_OUTPUT)
	$(DOTNET) docfx docfx.json

# SOURCE:
# https://github.com/NixOS/nixpkgs/blob/master/doc/languages-frameworks/dotnet.section.md#generating-and-updating-nuget-dependencies-generating-and-updating-nuget-dependencies
.PHONY: nix-lock
nix-lock: deps.json

deps.json: $(RESTORE_INPUTS)
	$(RM) -r $(NUGET_PACKAGES_DIR)
	$(DOTNET) restore $(SOLUTION) --packages $(NUGET_PACKAGES_DIR) -m:1
	@for project in $(EXAMPLE_PROJECTS); do \
		$(DOTNET) restore "$$project" --packages $(NUGET_PACKAGES_DIR) -m:1 || exit 1; \
	done
	$(NIX) run '$(NUGET_TO_JSON)' -- $(NUGET_PACKAGES_DIR) > $@.tmp
	mv $@.tmp $@

# Fails when a release tag and the declared version disagree.
.PHONY: check-version
check-version:
	@test -n "$(TAG)" || { echo "TAG must be set, for example TAG=v$(declared)." >&2; exit 1; }
	@test "$(TAG)" = "v$(declared)" || { echo "Tag $(TAG) does not match declared version $(declared)." >&2; exit 1; }

# Packages VERSION, defaulting to the version Directory.Build.props declares.
.PHONY: pack
pack: $(PACKAGES)

$(PACKAGES) &: $(PACKAGE_INPUTS)
	@echo "PACKING RELEASE: $(VERSION)"
	$(RM) -r $(PACKAGE_OUTPUT)
	@for project in $(SRC_PROJECTS); do \
		$(DOTNET) pack "$$project" -c Release /p:Version=$(VERSION) /p:PackageOutputPath=$(CURDIR)/$(PACKAGE_OUTPUT) || exit 1; \
	done

# Pushes the packed release to NuGet. The key is expanded by the shell, so the echoed recipe
# never prints it.
.PHONY: push
push:
	@test -n "$$NUGET_API_KEY" || { echo "NUGET_API_KEY must be set." >&2; exit 1; }
	@echo "Pushing release '$(VERSION)' to $(NUGET_SOURCE)"
	$(DOTNET) nuget push '$(PACKAGE_OUTPUT)/*.nupkg' -k "$$NUGET_API_KEY" -s $(NUGET_SOURCE) --skip-duplicate
	$(DOTNET) nuget push '$(PACKAGE_OUTPUT)/*.snupkg' -k "$$NUGET_API_KEY" -s $(NUGET_SOURCE) --skip-duplicate

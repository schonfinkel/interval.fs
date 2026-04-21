NUGET_SOURCE := https://api.nuget.org/v3/index.json
RELEASE := $(shell git tag -l --sort=-creatordate | head -n 1)
VERSION := $(patsubst v%,%,$(RELEASE))

REPORT_GENERATOR_VERSION := 5.3.11
COVERAGE_DIR := $(CURDIR)/coverage

.PHONY: coverage gen-deps pack push

coverage:
	rm -rf $(COVERAGE_DIR)
	dotnet tool update dotnet-reportgenerator-globaltool \
	  --tool-path .tools \
	  --version $(REPORT_GENERATOR_VERSION)
	dotnet test \
	  /p:CollectCoverage=true \
	  /p:CoverletOutputFormat=cobertura \
	  /p:CoverletOutput=$(COVERAGE_DIR)/
	.tools/reportgenerator \
	  -reports:$(COVERAGE_DIR)/coverage.cobertura.xml \
	  -targetdir:$(COVERAGE_DIR)/html \
	  -reporttypes:Html
	@echo "HTML coverage report: $(COVERAGE_DIR)/html/index.html"

gen-deps:
	dotnet restore --packages out
	nix run nixpkgs#nuget-to-json -- out > deps.json

pack:
	@echo "PACKING RELEASE: $(RELEASE)"
	rm -f src/Interval/bin/Release/*.nupkg
	dotnet pack -c Release /p:Version=$(VERSION)

push:
	@echo "Pushing release '$(RELEASE)' to $(NUGET_SOURCE)"
	dotnet nuget push src/Interval/bin/Release/*.nupkg -k "$(NUGET_API_KEY)" -s $(NUGET_SOURCE) --skip-duplicate

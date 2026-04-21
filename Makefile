NUGET_SOURCE := https://api.nuget.org/v3/index.json
RELEASE := $(shell git tag -l --sort=-creatordate | head -n 1)
VERSION := $(patsubst v%,%,$(RELEASE))

.PHONY: gen-deps pack push

gen-deps:
	dotnet restore --packages out
	nix run nixpkgs#nuget-to-json -- out > deps.json

pack:
	@echo "PACKING RELEASE: $(RELEASE)"
	rm -f */bin/Release/*.nupkg
	dotnet pack -c Release /p:Version=$(VERSION)

push:
	@echo "Pushing release '$(RELEASE)' to $(NUGET_SOURCE)"
	dotnet nuget push */bin/Release/*.nupkg -k "$(NUGET_API_KEY)" -s $(NUGET_SOURCE) --skip-duplicate

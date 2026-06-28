SOLUTION = Ciderfy.slnx
APP = src/Ciderfy/Ciderfy.csproj
CONFIGURATION ?= Debug
COVERAGE_RESULTS ?= ./coverage

.PHONY: install
install:
	dotnet restore $(SOLUTION)
	dotnet tool restore

.PHONY: build
build:
	dotnet build $(SOLUTION) --no-restore --configuration $(CONFIGURATION)

.PHONY: run
run:
	dotnet run --project $(APP)

.PHONY: test
test:
	dotnet test $(SOLUTION) --configuration $(CONFIGURATION)

.PHONY: test-unit
test-unit:
	dotnet test $(SOLUTION) --configuration $(CONFIGURATION) --filter "Category!=Integration"

.PHONY: test-unit-coverage
test-unit-coverage:
	dotnet test $(SOLUTION) --configuration $(CONFIGURATION) --filter "Category!=Integration" --collect:"XPlat Code Coverage" --results-directory $(COVERAGE_RESULTS) --settings coverage.runsettings

.PHONY: format
format:
	dotnet tool run csharpier format .

.PHONY: lint
lint:
	dotnet tool run csharpier check .
	dotnet tool run slopwatch analyze --fail-on warning
	dotnet build $(SOLUTION) --no-restore --configuration $(CONFIGURATION)

.PHONY: outdated
outdated:
	dotnet tool run dotnet-outdated

SOLUTION = Ciderfy.slnx
APP = src/Ciderfy/Ciderfy.csproj

.PHONY: install
install:
	dotnet restore $(SOLUTION)
	dotnet tool restore

.PHONY: build
build:
	dotnet build $(SOLUTION) --no-restore

.PHONY: run
run:
	dotnet run --project $(APP)

.PHONY: test
test:
	dotnet test $(SOLUTION)

.PHONY: test-unit
test-unit:
	dotnet test $(SOLUTION) --filter "Category!=Integration"

.PHONY: format
format:
	dotnet tool run csharpier format .

.PHONY: lint
lint:
	dotnet tool run csharpier check .
	dotnet tool run slopwatch analyze --fail-on warning
	dotnet build $(SOLUTION) --no-restore

.PHONY: check
check: lint

.PHONY: fix
fix:
	dotnet tool run csharpier format .

.PHONY: outdated
outdated:
	dotnet tool run dotnet-outdated

.PHONY: pack
pack:
	dotnet pack $(APP) -c Release -o ./artifacts

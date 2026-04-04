PROJECTS = src/Ciderfy/Ciderfy.csproj tests/Ciderfy.Tests/Ciderfy.Tests.csproj

build:
	dotnet build

run:
	dotnet run --project src/Ciderfy

test:
	dotnet test

test-unit:
	dotnet test --filter "Category!=Integration"

format:
	dotnet csharpier format .

check:
	dotnet csharpier check .
	dotnet roslynator analyze $(PROJECTS)

fix:
	dotnet csharpier format .
	dotnet roslynator fix $(PROJECTS)

outdated:
	dotnet dotnet-outdated

pack:
	dotnet pack src/Ciderfy -c Release -o ./artifacts

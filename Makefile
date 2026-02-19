build:
	dotnet build

run:
	dotnet run --project src/Ciderfy

test:
	dotnet test

test-unit:
	dotnet test --filter "Category!=Integration"

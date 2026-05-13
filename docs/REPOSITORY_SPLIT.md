# Repository split with Git submodules

GamerTodo is moving from one repository to three independently cloneable repositories:

- `GamerTodo.Shared`
- `GamerTodo.Server`
- `GamerTodo.Client`

`GamerTodo.Server` and `GamerTodo.Client` consume `GamerTodo.Shared` as a Git submodule at `shared/GamerTodo.Shared`.

## Repository layout

Server repository:

```text
GamerTodo.Server/
├── deploy/
├── shared/
│   └── GamerTodo.Shared/    Git submodule: GamerTodo.Shared
└── src/
    └── GamerTodo.Server/
```

Client repository:

```text
GamerTodo.Client/
├── shared/
│   └── GamerTodo.Shared/    Git submodule: GamerTodo.Shared
└── src/
    └── GamerTodo.Client/
```

The project references must point at the submodule path:

```xml
<ProjectReference Include="..\..\shared\GamerTodo.Shared\GamerTodo.Shared.csproj" />
```

## Clone

Clone with submodules:

```bash
git clone --recurse-submodules https://github.com/HHHHCHYK/GamerTodo.Server.git
git clone --recurse-submodules https://github.com/HHHHCHYK/GamerTodo.Client.git
```

If the repository was cloned without submodules:

```bash
git submodule update --init --recursive
```

## Add the submodule in a split repository

Run this once in each split repository:

```bash
git submodule add https://github.com/HHHHCHYK/GamerTodo.Shared.git shared/GamerTodo.Shared
git commit -m "Add shared submodule"
```

## Update Shared

To move Server or Client to a newer Shared commit:

```bash
cd shared/GamerTodo.Shared
git pull origin main
cd ../..
git add shared/GamerTodo.Shared
git commit -m "Update shared submodule"
```

## Build

Shared:

```bash
dotnet restore shared/GamerTodo.Shared/GamerTodo.Shared.csproj
dotnet build shared/GamerTodo.Shared/GamerTodo.Shared.csproj
```

Server:

```bash
dotnet restore src/GamerTodo.Server/GamerTodo.Server.csproj
dotnet build src/GamerTodo.Server/GamerTodo.Server.csproj
```

Client:

```bash
dotnet restore src/GamerTodo.Client/GamerTodo.Client.csproj
dotnet build src/GamerTodo.Client/GamerTodo.Client.csproj
```

## Docker

Server Docker builds require the Shared submodule to be initialized before building:

```bash
git submodule update --init --recursive
docker compose -f deploy/docker-compose.yml build server
```

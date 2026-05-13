#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")" && pwd)"
ROOT="$SCRIPT_DIR"
COMPOSE_FILE="$ROOT/deploy/docker-compose.yml"
SOLUTION_FILE="$ROOT/HeyeTodo.sln"
SERVER_DIR="$ROOT/src/HeyeTodo.Server"
CLIENT_DIR="$ROOT/src/HeyeTodo.Client"
POSTGRES_PORT=0427
SERVER_PORT=5254
AVALONIA_BUILD_DIR="$HOME/Library/Application Support/AvaloniaUI/BuildServices"
LOG_DIR="$ROOT/.logs/test-env"
START_POSTGRES=true
START_SERVER=true
START_CLIENT=true
FORCE_RESTORE=false
FORCE_BUILD=false

print_step() {
    echo ""
    echo "[$1/$TOTAL_STEPS] $2"
}

fail() {
    echo "Error: $1"
    exit 1
}

usage() {
    cat <<EOF
Usage: ./start-test-env.sh [options]

Options:
  --postgres-only    Start PostgreSQL only.
  --server-only      Start PostgreSQL and Server, skip Client.
  --no-server        Start PostgreSQL only, skip Server and Client.
  --no-client        Start PostgreSQL and Server, skip Client.
  --restore          Force dotnet restore before building.
  --build            Force dotnet build before launching.
  -h, --help         Show this help.
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --postgres-only)
            START_SERVER=false
            START_CLIENT=false
            ;;
        --server-only|--no-client)
            START_CLIENT=false
            ;;
        --no-server)
            START_SERVER=false
            START_CLIENT=false
            ;;
        --restore)
            FORCE_RESTORE=true
            FORCE_BUILD=true
            ;;
        --build)
            FORCE_BUILD=true
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            fail "Unknown option: $1"
            ;;
    esac
    shift
done

TOTAL_STEPS=7
if [[ "$START_SERVER" == "false" ]]; then
    TOTAL_STEPS=5
elif [[ "$START_CLIENT" == "false" ]]; then
    TOTAL_STEPS=6
fi

is_port_listening() {
    lsof -nP -iTCP:"$1" -sTCP:LISTEN -t &>/dev/null
}

list_port_owners() {
    lsof -nP -iTCP:"$1" -sTCP:LISTEN 2>/dev/null || true
}

is_client_running() {
    ps -axo comm=,args= | awk '$1 ~ /(^|\/)dotnet$/ && $0 ~ /HeyeTodo\.Client/ { found = 1 } END { exit found ? 0 : 1 }'
}

open_terminal_command() {
    local title="$1"
    local dir="$2"
    local cmd="$3"
    local safe_title="${title//[^a-zA-Z0-9_-]/_}"
    local command_file="$LOG_DIR/$safe_title.command"

    cat >"$command_file" <<EOF
#!/usr/bin/env bash
set -euo pipefail
printf '\\033]0;$title\\007'
cd "$dir"
$cmd
echo ""
echo "$title exited. Press Ctrl-D or close this window."
exec bash -l
EOF
    chmod +x "$command_file"
    open -a Terminal "$command_file" >/dev/null
}

ensure_command_exists() {
    command -v "$1" &>/dev/null || fail "$1 was not found. Install it and ensure it is available in PATH."
}

wait_for_docker() {
    for _ in {1..60}; do
        if docker info &>/dev/null; then
            return 0
        fi
        sleep 1
    done
    return 1
}

wait_for_postgres() {
    for _ in {1..45}; do
        if docker compose -f "$COMPOSE_FILE" exec -T postgres pg_isready -U heyetodo -d heyetodo &>/dev/null; then
            echo "PostgreSQL is ready."
            return 0
        fi
        sleep 1
    done
    return 1
}

wait_for_server() {
    for _ in {1..60}; do
        if curl -fsS "http://localhost:$SERVER_PORT/" &>/dev/null; then
            echo "Server is healthy at http://localhost:$SERVER_PORT."
            return 0
        fi
        sleep 1
    done
    return 1
}

server_is_healthy() {
    curl -fsS "http://localhost:$SERVER_PORT/" &>/dev/null
}

ensure_file_exists() {
    [[ -f "$1" ]] || fail "$2 not found: $1"
}

assets_missing() {
    [[ ! -f "$SERVER_DIR/obj/project.assets.json" || ! -f "$CLIENT_DIR/obj/project.assets.json" ]]
}

build_outputs_missing() {
    [[ ! -f "$SERVER_DIR/bin/Debug/net10.0/HeyeTodo.Server.dll" || ! -f "$CLIENT_DIR/bin/Debug/net10.0/HeyeTodo.Client.dll" ]]
}

print_step 1 "Checking project files"
ensure_file_exists "$COMPOSE_FILE" "Compose file"
ensure_file_exists "$SOLUTION_FILE" "Solution file"
ensure_file_exists "$SERVER_DIR/HeyeTodo.Server.csproj" "Server project"
ensure_file_exists "$CLIENT_DIR/HeyeTodo.Client.csproj" "Client project"

print_step 2 "Checking required tools"
ensure_command_exists docker
ensure_command_exists dotnet
ensure_command_exists curl
ensure_command_exists lsof

if ! docker info &>/dev/null; then
    if [[ -d "/Applications/Docker.app" ]]; then
        echo "Docker daemon is not running. Opening Docker Desktop..."
        open -a Docker
        wait_for_docker || fail "Docker Desktop did not become ready in time."
    else
        fail "Docker daemon is not running. Start Docker Desktop first."
    fi
fi

print_step 3 "Preparing local directories"
mkdir -p "$AVALONIA_BUILD_DIR"
chmod 755 "$AVALONIA_BUILD_DIR" || true
mkdir -p "$LOG_DIR"

print_step 4 "Starting PostgreSQL"
POSTGRES_CONTAINER=$(docker compose -f "$COMPOSE_FILE" ps -q postgres 2>/dev/null || true)
POSTGRES_RUNNING=""
if [[ -n "$POSTGRES_CONTAINER" ]]; then
    POSTGRES_RUNNING=$(docker inspect -f "{{.State.Running}}" "$POSTGRES_CONTAINER" 2>/dev/null || true)
fi

if [[ "$POSTGRES_RUNNING" == "true" ]]; then
    echo "PostgreSQL container is already running."
elif is_port_listening "$POSTGRES_PORT"; then
    echo "Port $POSTGRES_PORT is already in use:"
    list_port_owners "$POSTGRES_PORT"
    fail "PostgreSQL port $POSTGRES_PORT is occupied. Stop that process or change deploy/docker-compose.yml."
else
    docker compose -f "$COMPOSE_FILE" up -d postgres || fail "Failed to start PostgreSQL."
fi

print_step 5 "Waiting for PostgreSQL"
wait_for_postgres || fail "PostgreSQL did not become ready in time."

if [[ "$START_SERVER" == "false" ]]; then
    echo ""
    echo "HeyeTodo test environment partially started."
    echo "- PostgreSQL: localhost:$POSTGRES_PORT via Docker Compose service 'postgres'"
    echo ""
    echo "To stop PostgreSQL: docker compose -f '$COMPOSE_FILE' stop postgres"
    exit 0
fi

print_step 6 "Building and starting Server"
if [[ "$FORCE_RESTORE" == "true" ]] || assets_missing; then
    echo "Restoring NuGet packages..."
    dotnet restore "$SOLUTION_FILE" --ignore-failed-sources || fail "dotnet restore failed."
fi

if [[ "$FORCE_BUILD" == "true" ]] || build_outputs_missing; then
    echo "Building solution..."
    dotnet build "$SOLUTION_FILE" --no-restore || fail "dotnet build failed."
fi

if server_is_healthy; then
    echo "Server is already healthy at http://localhost:$SERVER_PORT."
elif is_port_listening "$SERVER_PORT"; then
    echo "Port $SERVER_PORT is already in use, but HeyeTodo Server health check failed:"
    list_port_owners "$SERVER_PORT"
    fail "Stop the process on port $SERVER_PORT before starting HeyeTodo Server."
else
    SERVER_LOG="$LOG_DIR/server.log"
    SERVER_CMD="dotnet run --no-restore --no-build --launch-profile http 2>&1 | tee '$SERVER_LOG'"
    echo "Starting Server in a new Terminal window. Logs: $SERVER_LOG"
    open_terminal_command "HeyeTodo Server" "$SERVER_DIR" "$SERVER_CMD"
    wait_for_server || fail "Server did not start listening on port $SERVER_PORT in time."
fi

if [[ "$START_CLIENT" == "false" ]]; then
    echo ""
    echo "HeyeTodo test environment started."
    echo "- PostgreSQL: localhost:$POSTGRES_PORT via Docker Compose service 'postgres'"
    echo "- Server:      http://localhost:$SERVER_PORT"
    echo "- Client:      skipped"
    echo ""
    echo "To stop PostgreSQL: docker compose -f '$COMPOSE_FILE' stop postgres"
    exit 0
fi

print_step 7 "Starting Client"
if is_client_running; then
    echo "HeyeTodo Client is already running. Skipping Client startup."
else
    CLIENT_LOG="$LOG_DIR/client.log"
    CLIENT_CMD="dotnet run --no-restore --no-build --project '$CLIENT_DIR/HeyeTodo.Client.csproj' 2>&1 | tee '$CLIENT_LOG'"
    echo "Starting Client in a new Terminal window. Logs: $CLIENT_LOG"
    open_terminal_command "HeyeTodo Client" "$CLIENT_DIR" "$CLIENT_CMD"
fi

echo ""
echo "HeyeTodo macOS quick start completed."
echo "- PostgreSQL: localhost:$POSTGRES_PORT via Docker Compose service 'postgres'"
echo "- Server:      http://localhost:$SERVER_PORT"
echo "- Client:      launched or already running"
echo "- Logs:        $LOG_DIR"
echo ""
echo "To stop PostgreSQL: docker compose -f '$COMPOSE_FILE' stop postgres"

#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")" && pwd)"
ROOT="$SCRIPT_DIR"
COMPOSE_FILE="$ROOT/deploy/docker-compose.yml"
SERVER_DIR="$ROOT/src/HeyeTodo.Server"
CLIENT_DIR="$ROOT/src/HeyeTodo.Client"
POSTGRES_PORT=55432
SERVER_PORT=5254
AVALONIA_BUILD_DIR="$HOME/Library/Application Support/AvaloniaUI/BuildServices"

print_step() {
    echo ""
    echo "[$1/7] $2"
}

fail() {
    echo "Error: $1"
    exit 1
}

is_port_listening() {
    lsof -nP -iTCP:"$1" -sTCP:LISTEN -t &>/dev/null
}

is_client_running() {
    pgrep -f "dotnet.*HeyeTodo.Client" &>/dev/null || pgrep -f "HeyeTodo.Client.dll" &>/dev/null
}

open_terminal_command() {
    local title="$1"
    local dir="$2"
    local cmd="$3"
    local escaped_dir="${dir//\'/\'\\\'\'}"
    local escaped_cmd="${cmd//\'/\'\\\'\'}"
    osascript -e "tell application \"Terminal\" to do script \"printf '\\\\e]0;$title\\\\a'; cd '$escaped_dir' && $escaped_cmd\"" >/dev/null
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
    for _ in {1..45}; do
        if curl -fsS "http://localhost:$SERVER_PORT" &>/dev/null || is_port_listening "$SERVER_PORT"; then
            echo "Server port $SERVER_PORT is listening."
            return 0
        fi
        sleep 1
    done
    return 1
}

ensure_file_exists() {
    [[ -f "$1" ]] || fail "$2 not found: $1"
}

print_step 1 "Checking project files"
ensure_file_exists "$COMPOSE_FILE" "Compose file"
ensure_file_exists "$SERVER_DIR/HeyeTodo.Server.csproj" "Server project"
ensure_file_exists "$CLIENT_DIR/HeyeTodo.Client.csproj" "Client project"

print_step 2 "Checking Docker Desktop"
if ! command -v docker &>/dev/null; then
    fail "Docker CLI was not found. Install Docker Desktop and ensure docker is available in PATH."
fi

if ! docker info &>/dev/null; then
    if [[ -d "/Applications/Docker.app" ]]; then
        echo "Docker daemon is not running. Opening Docker Desktop..."
        open -a Docker
        wait_for_docker || fail "Docker Desktop did not become ready in time."
    else
        fail "Docker daemon is not running. Start Docker Desktop first."
    fi
fi

print_step 3 "Preparing Avalonia build directory"
mkdir -p "$AVALONIA_BUILD_DIR"
chmod 755 "$AVALONIA_BUILD_DIR" || true

print_step 4 "Starting PostgreSQL"
POSTGRES_CONTAINER=$(docker compose -f "$COMPOSE_FILE" ps -q postgres 2>/dev/null || true)
POSTGRES_RUNNING=""
if [[ -n "$POSTGRES_CONTAINER" ]]; then
    POSTGRES_RUNNING=$(docker inspect -f "{{.State.Running}}" "$POSTGRES_CONTAINER" 2>/dev/null || true)
fi

if [[ "$POSTGRES_RUNNING" == "true" ]]; then
    echo "PostgreSQL container is already running."
elif is_port_listening "$POSTGRES_PORT"; then
    fail "PostgreSQL port $POSTGRES_PORT is already in use by another process."
else
    docker compose -f "$COMPOSE_FILE" up -d postgres || fail "Failed to start PostgreSQL."
fi

print_step 5 "Waiting for PostgreSQL"
wait_for_postgres || fail "PostgreSQL did not become ready in time."

print_step 6 "Starting Server"
if is_port_listening "$SERVER_PORT"; then
    echo "Server port $SERVER_PORT is already listening. Skipping Server startup."
else
    open_terminal_command "HeyeTodo Server" "$SERVER_DIR" "dotnet run --launch-profile http"
    wait_for_server || fail "Server did not start listening on port $SERVER_PORT in time."
fi

print_step 7 "Starting Client"
if is_client_running; then
    echo "HeyeTodo Client is already running. Skipping Client startup."
else
    open_terminal_command "HeyeTodo Client" "$CLIENT_DIR" "dotnet run"
fi

echo ""
echo "HeyeTodo macOS quick start completed."
echo "- PostgreSQL: localhost:$POSTGRES_PORT via Docker Compose service 'postgres'"
echo "- Server:      http://localhost:$SERVER_PORT"
echo "- Client:      launched or already running"
echo ""
echo "To stop PostgreSQL: docker compose -f '$COMPOSE_FILE' down"

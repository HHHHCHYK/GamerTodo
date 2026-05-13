#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")" && pwd)"
ROOT="$SCRIPT_DIR"
COMPOSE_FILE="$ROOT/deploy/docker-compose.yml"
SERVER_PORT=5254
STOP_POSTGRES=true
STOP_SERVER=true
STOP_CLIENT=true

usage() {
    cat <<EOF
Usage: ./stop-test-env.sh [options]

Options:
  --server-only      Stop Server only.
  --client-only      Stop Client only.
  --postgres-only    Stop PostgreSQL only.
  --keep-postgres    Stop Server and Client, keep PostgreSQL running.
  -h, --help         Show this help.
EOF
}

fail() {
    echo "Error: $1"
    exit 1
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --server-only)
            STOP_CLIENT=false
            STOP_POSTGRES=false
            ;;
        --client-only)
            STOP_SERVER=false
            STOP_POSTGRES=false
            ;;
        --postgres-only)
            STOP_SERVER=false
            STOP_CLIENT=false
            ;;
        --keep-postgres)
            STOP_POSTGRES=false
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

print_step() {
    echo ""
    echo "[$1/$TOTAL_STEPS] $2"
}

TOTAL_STEPS=0
[[ "$STOP_SERVER" == "true" ]] && TOTAL_STEPS=$((TOTAL_STEPS + 1))
[[ "$STOP_CLIENT" == "true" ]] && TOTAL_STEPS=$((TOTAL_STEPS + 1))
[[ "$STOP_POSTGRES" == "true" ]] && TOTAL_STEPS=$((TOTAL_STEPS + 1))
[[ "$TOTAL_STEPS" -gt 0 ]] || fail "Nothing selected to stop."

find_port_pids() {
    lsof -nP -iTCP:"$1" -sTCP:LISTEN -t 2>/dev/null | sort -u || true
}

stop_pids() {
    local label="$1"
    shift
    local pids=("$@")

    if [[ "${#pids[@]}" -eq 0 ]]; then
        echo "$label is not running."
        return 0
    fi

    echo "Stopping $label: ${pids[*]}"
    kill "${pids[@]}" 2>/dev/null || true
    sleep 2

    local still_running=()
    for pid in "${pids[@]}"; do
        if kill -0 "$pid" 2>/dev/null; then
            still_running+=("$pid")
        fi
    done

    if [[ "${#still_running[@]}" -gt 0 ]]; then
        echo "Force stopping $label: ${still_running[*]}"
        kill -9 "${still_running[@]}" 2>/dev/null || true
    fi
}

find_client_pids() {
    ps -axo pid=,comm=,args= 2>/dev/null \
        | awk '$2 ~ /(^|\/)dotnet$/ && $0 ~ /HeyeTodo\.Client/ { print $1 }'
}

read_lines_into_array() {
    local array_name="$1"
    local line
    eval "$array_name=()"
    while IFS= read -r line; do
        [[ -n "$line" ]] || continue
        eval "$array_name+=(\"\$line\")"
    done
}

step=1

if [[ "$STOP_SERVER" == "true" ]]; then
    print_step "$step" "Stopping Server on port $SERVER_PORT"
    read_lines_into_array server_pids < <(find_port_pids "$SERVER_PORT")
    stop_pids "HeyeTodo Server" "${server_pids[@]}"
    step=$((step + 1))
fi

if [[ "$STOP_CLIENT" == "true" ]]; then
    print_step "$step" "Stopping Client"
    read_lines_into_array client_pids < <(find_client_pids)
    stop_pids "HeyeTodo Client" "${client_pids[@]}"
    step=$((step + 1))
fi

if [[ "$STOP_POSTGRES" == "true" ]]; then
    print_step "$step" "Stopping PostgreSQL container"
    if ! command -v docker &>/dev/null; then
        echo "Docker CLI was not found. Skipping PostgreSQL shutdown."
    elif ! docker info &>/dev/null; then
        echo "Docker daemon is not running. Skipping PostgreSQL shutdown."
    elif [[ ! -f "$COMPOSE_FILE" ]]; then
        echo "Compose file not found: $COMPOSE_FILE"
    else
        docker compose -f "$COMPOSE_FILE" stop postgres
    fi
fi

echo ""
echo "HeyeTodo test environment stopped."

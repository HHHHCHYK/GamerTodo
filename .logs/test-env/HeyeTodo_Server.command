#!/usr/bin/env bash
set -euo pipefail
printf '\033]0;HeyeTodo Server\007'
cd "/Users/hykal311/Project/Other/GamerTodo/src/HeyeTodo.Server"
dotnet run --no-restore --no-build --launch-profile http 2>&1 | tee '/Users/hykal311/Project/Other/GamerTodo/.logs/test-env/server.log'
echo ""
echo "HeyeTodo Server exited. Press Ctrl-D or close this window."
exec bash -l

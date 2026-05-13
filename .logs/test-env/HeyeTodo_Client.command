#!/usr/bin/env bash
set -euo pipefail
printf '\033]0;HeyeTodo Client\007'
cd "/Users/hykal311/Project/Other/GamerTodo/src/HeyeTodo.Client"
dotnet run --no-restore --no-build --project '/Users/hykal311/Project/Other/GamerTodo/src/HeyeTodo.Client/HeyeTodo.Client.csproj' 2>&1 | tee '/Users/hykal311/Project/Other/GamerTodo/.logs/test-env/client.log'
echo ""
echo "HeyeTodo Client exited. Press Ctrl-D or close this window."
exec bash -l

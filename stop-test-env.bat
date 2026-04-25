@echo off
setlocal EnableExtensions

set "ROOT=%~dp0"
set "COMPOSE_FILE=%ROOT%deploy\docker-compose.yml"
set "SERVER_PORT=5254"

echo [1/3] Stopping local Server processes on port %SERVER_PORT%...
call :StopPortProcess %SERVER_PORT%

echo [2/3] Stopping local Client processes...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -match 'HeyeTodo.Client' -and $_.CommandLine -match 'dotnet' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }"
if errorlevel 1 (
    echo Failed to stop one or more Client processes.
    exit /b 1
)

echo [3/3] Stopping PostgreSQL container...
where docker >nul 2>nul
if errorlevel 1 (
    echo Docker CLI was not found. Skipping PostgreSQL shutdown.
    goto :Done
)

docker info >nul 2>nul
if errorlevel 1 (
    echo Docker daemon is not running. Skipping PostgreSQL shutdown.
    goto :Done
)

if not exist "%COMPOSE_FILE%" (
    echo Compose file not found: "%COMPOSE_FILE%"
    goto :Done
)

docker compose -f "%COMPOSE_FILE%" stop postgres
if errorlevel 1 (
    echo Failed to stop PostgreSQL container.
    exit /b 1
)

:Done
echo.
echo Test environment stopped.
endlocal
exit /b 0

:StopPortProcess
powershell -NoProfile -ExecutionPolicy Bypass -Command "$connections = Get-NetTCPConnection -LocalPort %~1 -State Listen -ErrorAction SilentlyContinue; if (-not $connections) { exit 0 }; $connections | Select-Object -ExpandProperty OwningProcess -Unique | ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }"
exit /b %errorlevel%

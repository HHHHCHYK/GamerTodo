@echo off
setlocal EnableExtensions

set "ROOT=%~dp0"
set "COMPOSE_FILE=%ROOT%deploy\docker-compose.yml"
set "SERVER_DIR=%ROOT%src\GamerTodo.Server"
set "CLIENT_DIR=%ROOT%src\GamerTodo.Client"
set "SHARED_DIR=%ROOT%shared\GamerTodo.Shared"
set "POSTGRES_PORT=0427"
set "SERVER_PORT=5254"

where docker >nul 2>nul
if errorlevel 1 (
    echo Docker CLI was not found. Please install Docker Desktop and ensure 'docker' is available in PATH.
    exit /b 1
)

docker info >nul 2>nul
if errorlevel 1 (
    echo Docker daemon is not running. Please start Docker Desktop first.
    exit /b 1
)

if not exist "%COMPOSE_FILE%" (
    echo Compose file not found: "%COMPOSE_FILE%"
    exit /b 1
)

if not exist "%SERVER_DIR%\GamerTodo.Server.csproj" (
    echo Server project not found: "%SERVER_DIR%"
    exit /b 1
)

if not exist "%CLIENT_DIR%\GamerTodo.Client.csproj" (
    echo Client project not found: "%CLIENT_DIR%"
    exit /b 1
)

if not exist "%SHARED_DIR%\GamerTodo.Shared.csproj" (
    echo Shared submodule project not found: "%SHARED_DIR%"
    echo Run: git submodule update --init --recursive
    exit /b 1
)

echo [1/5] Checking PostgreSQL container...
for /f "tokens=*" %%i in ('docker compose -f "%COMPOSE_FILE%" ps -q postgres 2^>nul') do set "POSTGRES_CONTAINER=%%i"
if defined POSTGRES_CONTAINER (
    for /f "tokens=*" %%i in ('docker inspect -f "{{.State.Running}}" "%POSTGRES_CONTAINER%" 2^>nul') do set "POSTGRES_RUNNING=%%i"
)

if /i "%POSTGRES_RUNNING%"=="true" (
    echo PostgreSQL container is already running.
) else (
    call :EnsurePortFree %POSTGRES_PORT% PostgreSQL
    if errorlevel 1 exit /b 1

    echo [2/5] Starting PostgreSQL container...
    docker compose -f "%COMPOSE_FILE%" up -d postgres
    if errorlevel 1 (
        echo Failed to start PostgreSQL.
        exit /b 1
    )
)

echo [3/5] Waiting for PostgreSQL to become healthy...
call :WaitForPostgres
if errorlevel 1 exit /b 1

echo [4/5] Checking Server port...
call :IsPortListening %SERVER_PORT%
if errorlevel 1 (
    echo Server port %SERVER_PORT% is already listening. Skipping Server startup.
) else (
    echo Starting Server...
    start "GamerTodo Server" cmd /k cd /d "%SERVER_DIR%" ^&^& dotnet run
)

echo [5/5] Checking Client process...
call :IsClientRunning
if errorlevel 1 (
    echo GamerTodo Client is already running. Skipping Client startup.
) else (
    echo Starting Client...
    start "GamerTodo Client" cmd /k cd /d "%CLIENT_DIR%" ^&^& dotnet run --project "%CLIENT_DIR%\GamerTodo.Client.csproj"
)

echo.
echo Test environment started.
echo - PostgreSQL: localhost:%POSTGRES_PORT% via docker compose service 'postgres'
echo - Server:      http://localhost:%SERVER_PORT%
echo - Client:      launched or already running
echo.
echo Use stop-test-env.bat to stop the test environment.

endlocal
exit /b 0

:IsPortListening
powershell -NoProfile -ExecutionPolicy Bypass -Command "if (Get-NetTCPConnection -LocalPort %~1 -State Listen -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }"
exit /b %errorlevel%

:IsClientRunning
powershell -NoProfile -ExecutionPolicy Bypass -Command "$client = Get-CimInstance Win32_Process | Where-Object { $_.Name -in @('dotnet.exe', 'GamerTodo.Client.exe') -and ($_.CommandLine -match 'GamerTodo\.Client' -or $_.CommandLine -match [regex]::Escape('%CLIENT_DIR%')) }; if ($client) { exit 1 } else { exit 0 }"
exit /b %errorlevel%

:EnsurePortFree
call :IsPortListening %~1
if errorlevel 1 (
    echo %~2 port %~1 is already in use by another process.
    echo Stop that process or change the configured port before running this script again.
    exit /b 1
)
exit /b 0

:WaitForPostgres
for /l %%i in (1,1,30) do (
    docker compose -f "%COMPOSE_FILE%" exec -T postgres pg_isready -U gamertodo -d gamertodo >nul 2>nul
    if not errorlevel 1 (
        echo PostgreSQL is ready.
        exit /b 0
    )
    timeout /t 1 /nobreak >nul
)
echo PostgreSQL did not become ready in time.
exit /b 1

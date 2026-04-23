@echo off
setlocal

set "ROOT=%~dp0"

echo [1/3] Starting PostgreSQL container...
docker compose -f "%ROOT%deploy\docker-compose.yml" up -d postgres
if errorlevel 1 (
    echo Failed to start PostgreSQL.
    exit /b 1
)

echo [2/3] Starting Server...
start "HeyeTodo Server" cmd /k "cd /d "%ROOT%src\HeyeTodo.Server" && dotnet run"

echo [3/3] Starting Client...
start "HeyeTodo Client" cmd /k "cd /d "%ROOT%src\HeyeTodo.Client" && dotnet run"

echo.
echo Test environment started.
echo - PostgreSQL: docker compose service 'postgres'
echo - Server:      http://localhost:5254
echo - Client:      launched in a separate window

endlocal

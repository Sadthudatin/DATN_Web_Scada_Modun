@echo off
chcp 65001 >nul
setlocal EnableExtensions
cd /d "%~dp0"

set "DOCKER_DESKTOP=%LOCALAPPDATA%\Programs\DockerDesktop\Docker Desktop.exe"
if not exist "%DOCKER_DESKTOP%" set "DOCKER_DESKTOP=%ProgramFiles%\Docker\Docker\Docker Desktop.exe"

set "TS_IP="
if exist "%ProgramFiles%\Tailscale\tailscale.exe" (
    for /f "delims=" %%i in ('"%ProgramFiles%\Tailscale\tailscale.exe" ip -4 2^>nul') do (
        if not defined TS_IP set "TS_IP=%%i"
    )
)
if not defined TS_IP set "TS_IP=100.68.150.98"

if /i "%~1"=="start"    goto do_start
if /i "%~1"=="stop"     goto do_stop
if /i "%~1"=="restart"  goto do_restart
if /i "%~1"=="status"   goto do_status
if /i "%~1"=="stopall"  goto do_stopall

:menu
cls
echo ============================================================
echo   DATN SCADA  -  DVP24 IoT Gateway
echo   Thu muc: %CD%
echo ============================================================
echo    [1] Bat he thong            (start)
echo    [2] Tat he thong            (stop)
echo    [3] Khoi dong lai           (restart)
echo    [4] Trang thai + kiem tra   (status)
echo    [5] Tat hoan toan           (stop + thoat Docker Desktop)
echo    [0] Thoat
echo ============================================================
set "chon="
set /p "chon=Chon [0-5]: "
if "%chon%"=="1" goto do_start
if "%chon%"=="2" goto do_stop
if "%chon%"=="3" goto do_restart
if "%chon%"=="4" goto do_status
if "%chon%"=="5" goto do_stopall
if "%chon%"=="0" exit /b 0
goto menu

:do_start
echo.
echo [1/3] Kiem tra Docker Desktop...
call :ensure_docker
if errorlevel 1 (
    echo       LOI: Docker khong san sang. Mo Docker Desktop thu cong roi thu lai.
    pause
    exit /b 1
)
echo [2/3] Khoi dong cac dich vu...
docker compose up -d
if errorlevel 1 (
    echo       LOI: docker compose up that bai. Xem log ben duoi.
    docker compose logs --tail 20
    pause
    exit /b 1
)
echo [3/3] Cho dich vu san sang...
timeout /t 8 /nobreak >nul
docker compose ps
echo.
echo ------------------------------------------------------------
echo   Web UI    : http://%TS_IP%:8088
echo   Node-RED  : http://%TS_IP%:1880
echo   Grafana   : http://%TS_IP%:3000
echo   InfluxDB  : http://%TS_IP%:8086
echo   MQTT      : %TS_IP%:1883
echo   (cuc bo)  : http://localhost:8088
echo ------------------------------------------------------------
start "" http://%TS_IP%:8088
pause
exit /b 0

:do_stop
echo.
echo Dang tat he thong (giu nguyen du lieu)...
docker compose down
if errorlevel 1 (
    echo       LOI khi tat. Kiem tra Docker Desktop.
    pause
    exit /b 1
)
echo Da tat toan bo container va network.
pause
exit /b 0

:do_restart
call :do_stop_silent
call :do_start_silent
goto do_start_report

:do_stop_silent
docker compose down >nul 2>&1
exit /b 0

:do_start_silent
call :ensure_docker
docker compose up -d >nul 2>&1
exit /b 0

:do_start_report
timeout /t 8 /nobreak >nul
docker compose ps
echo.
echo Da khoi dong lai he thong.
start "" http://%TS_IP%:8088
pause
exit /b 0

:do_status
echo.
docker compose ps
echo.
if exist "scripts\healthcheck.ps1" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "scripts\healthcheck.ps1"
) else (
    echo [i] Khong tim thay scripts\healthcheck.ps1
)
echo.
pause
exit /b 0

:do_stopall
echo.
echo Dang tat he thong...
docker compose down
echo Dang thoat Docker Desktop...
taskkill /IM "Docker Desktop.exe" /F >nul 2>&1
taskkill /IM "com.docker.backend.exe" /F >nul 2>&1
echo Da tat hoan toan: container, network va Docker Desktop.
pause
exit /b 0

:ensure_docker
docker info >nul 2>&1
if not errorlevel 1 exit /b 0
echo       Docker chua chay, dang khoi dong Docker Desktop...
if not exist "%DOCKER_DESKTOP%" (
    echo       Khong tim thay: %DOCKER_DESKTOP%
    exit /b 1
)
start "" "%DOCKER_DESKTOP%"
set /a tries=0
:wait_docker
timeout /t 3 /nobreak >nul
docker info >nul 2>&1
if not errorlevel 1 exit /b 0
set /a tries+=1
if %tries% lss 40 goto wait_docker
exit /b 1

<#
.SYNOPSIS
    Kiểm tra sức khỏe cụm SCADA Delta DVP-24ES2.
.DESCRIPTION
    Kiểm tra Docker, trạng thái 6 container, các dịch vụ HTTP, luồng MQTT plc/+/telemetry
    và file log CSV mới nhất.
    In PASS/FAIL từng mục, tổng kết và trả exit code 1 nếu có mục FAIL.
    Chỉ dùng PowerShell thuần, không cần quyền admin.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Continue'
$ProgressPreference = 'SilentlyContinue'

$script:PassCount = 0
$script:FailCount = 0

function Write-Item {
    param(
        [string]$Name,
        [bool]$Ok,
        [string]$Detail = ''
    )

    $safeDetail = ($Detail -replace '\s+', ' ').Trim()
    if ($safeDetail.Length -gt 160) {
        $safeDetail = $safeDetail.Substring(0, 157) + '...'
    }

    if ($Ok) {
        $script:PassCount++
        $line = "[PASS] $Name"
    }
    else {
        $script:FailCount++
        $line = "[FAIL] $Name"
    }
    if ($safeDetail) { $line = "$line - $safeDetail" }
    Write-Host $line
}

function Test-HttpEndpoint {
    param(
        [string]$Name,
        [string]$Url
    )

    $code = 0
    try {
        $raw = & curl.exe -s -o NUL -w '%{http_code}' --max-time 6 $Url 2>$null
        if ($LASTEXITCODE -eq 0 -and $raw) {
            $code = [int]$raw
        }
    }
    catch {
        $code = 0
    }

    if ($code -ge 200 -and $code -lt 400) {
        Write-Item -Name $Name -Ok $true -Detail "HTTP $code ($Url)"
    }
    elseif ($code -eq 0) {
        Write-Item -Name $Name -Ok $false -Detail "$Url - khong phan hoi"
    }
    else {
        Write-Item -Name $Name -Ok $false -Detail "HTTP $code ($Url)"
    }
}

Write-Host '=== Kiểm tra cụm SCADA Delta DVP-24ES2 ==='
Write-Host ''

# ---------------------------------------------------------------- Docker
Write-Host '--- Docker ---'
$dockerReady = $false
try {
    $dockerInfo = & docker info 2>&1
    $dockerReady = ($LASTEXITCODE -eq 0)
    if ($dockerReady) {
        Write-Item -Name 'docker info' -Ok $true -Detail 'Docker engine sẵn sàng'
    }
    else {
        $firstError = ($dockerInfo | Where-Object { $_ } | Select-Object -First 1 | Out-String).Trim()
        Write-Item -Name 'docker info' -Ok $false -Detail $firstError
    }
}
catch {
    Write-Item -Name 'docker info' -Ok $false -Detail $_.Exception.Message
}

# ------------------------------------------------------------ Containers
$expectedContainers = @('mosquitto', 'web', 'nodered', 'influxdb', 'telegraf', 'grafana')
$containerState = @{}

if ($dockerReady) {
    try {
        $rows = & docker ps -a --format '{{.Names}}|{{.State}}|{{.Status}}' 2>&1
        if ($LASTEXITCODE -eq 0) {
            foreach ($row in $rows) {
                $parts = "$row".Split([char]'|', 3)
                if ($parts.Count -ge 2) {
                    $containerState[$parts[0]] = @{
                        State  = $parts[1]
                        Status = if ($parts.Count -ge 3) { $parts[2] } else { '' }
                    }
                }
            }
        }
        else {
            Write-Host 'Không đọc được danh sách container.'
        }
    }
    catch {
        Write-Host "Không đọc được danh sách container: $($_.Exception.Message)"
    }
}

foreach ($name in $expectedContainers) {
    if ($containerState.ContainsKey($name)) {
        $state = $containerState[$name].State
        $status = $containerState[$name].Status
        Write-Item -Name "container $name" -Ok ($state -eq 'running') -Detail "$state ($status)"
    }
    else {
        Write-Item -Name "container $name" -Ok $false -Detail 'không tìm thấy container'
    }
}

# ----------------------------------------------------------------- HTTP
Write-Host ''
Write-Host '--- Dịch vụ HTTP ---'
Test-HttpEndpoint -Name 'Web UI (8088)' -Url 'http://localhost:8088'
Test-HttpEndpoint -Name 'Node-RED (1880)' -Url 'http://localhost:1880'
Test-HttpEndpoint -Name 'Grafana (3000)' -Url 'http://localhost:3000'
Test-HttpEndpoint -Name 'InfluxDB health (8086)' -Url 'http://localhost:8086/health'

# ----------------------------------------------------------------- MQTT
Write-Host ''
Write-Host '--- MQTT ---'
try {
    $mqttOutput = & docker exec mosquitto mosquitto_sub -h 127.0.0.1 -u web -P web-pass-2026 -t "plc/+/telemetry" -C 1 -W 5 2>&1
    $mqttCode = $LASTEXITCODE
    $payload = ($mqttOutput | Out-String).Trim()

    if ($mqttCode -eq 0 -and $payload -and $payload -match '"device"') {
        Write-Item -Name 'MQTT plc/+/telemetry' -Ok $true -Detail $payload
    }
    elseif ($mqttCode -eq 0) {
        Write-Item -Name 'MQTT plc/+/telemetry' -Ok $false -Detail 'không nhận được payload hợp lệ'
    }
    else {
        Write-Item -Name 'MQTT plc/+/telemetry' -Ok $false -Detail "mosquitto_sub exit $mqttCode - $payload"
    }
}
catch {
    Write-Item -Name 'MQTT plc/+/telemetry' -Ok $false -Detail $_.Exception.Message
}

# ---------------------------------------------------------------- CSV log
Write-Host ''
Write-Host '--- CSV log ---'
try {
    $logDir = Join-Path (Split-Path -Parent $PSScriptRoot) 'logs'
    $csv = Get-ChildItem -Path $logDir -Filter '*.csv' -ErrorAction Stop |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if (-not $csv) {
        Write-Item -Name 'CSV telemetry' -Ok $false -Detail "không thấy file trong $logDir"
    }
    elseif (((Get-Date) - $csv.LastWriteTime).TotalSeconds -lt 120) {
        Write-Item -Name 'CSV telemetry' -Ok $true -Detail ("{0} ({1:N0} bytes)" -f $csv.Name, $csv.Length)
    }
    else {
        Write-Item -Name 'CSV telemetry' -Ok $false -Detail ("file cũ: {0} lúc {1:HH:mm:ss}" -f $csv.Name, $csv.LastWriteTime)
    }
}
catch {
    Write-Item -Name 'CSV telemetry' -Ok $false -Detail $_.Exception.Message
}

# -------------------------------------------------------------- Summary
Write-Host ''
Write-Host ('Tổng kết: {0} PASS, {1} FAIL' -f $script:PassCount, $script:FailCount)

if ($script:FailCount -gt 0) {
    exit 1
}
exit 0

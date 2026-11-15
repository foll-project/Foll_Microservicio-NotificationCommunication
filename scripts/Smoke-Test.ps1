param([int]$Port = 15257, [switch]$CheckDatabase)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskDll = Join-Path $taskRoot 'bin/Debug/net10.0/Foll-NotificationCommunication.dll'
if (-not (Test-Path -LiteralPath $taskDll)) { throw 'Ejecute dotnet build primero.' }
$taskKey = 'smoke-test-only-key-with-at-least-32-bytes'
$taskArgs = @('"' + $taskDll + '"', '--urls', "http://127.0.0.1:$Port", "--JwtSettings:Secret=$taskKey", '--Inbox:Enabled=false', '--Telemetry:Enabled=false', '--IntegrationConsumers:Enabled=false', '--ApplyMigrationsOnStartup=false', '--Swagger:Enabled=true')
$taskProcess = Start-Process -FilePath 'dotnet' -ArgumentList $taskArgs -WorkingDirectory $taskRoot -WindowStyle Hidden -PassThru
try {
    $taskBase = "http://127.0.0.1:$Port"
    $taskReady = $false
    for ($taskAttempt = 0; $taskAttempt -lt 40; $taskAttempt++) {
        if ($taskProcess.HasExited) { throw 'El servicio terminó antes de iniciar.' }
        try { Invoke-WebRequest -UseBasicParsing "$taskBase/health/live" -TimeoutSec 1 | Out-Null; $taskReady = $true; break } catch { Start-Sleep -Milliseconds 250 }
    }
    if (-not $taskReady) { throw 'No respondió health/live.' }
    if ($CheckDatabase) {
        $taskDatabaseHealth = Invoke-RestMethod "$taskBase/health/ready" -TimeoutSec 30
        if ($taskDatabaseHealth.status -ne 'ready') { throw 'La base de Notifications no está preparada.' }
        Write-Output 'PASS: health/ready consulta las cinco tablas de Notifications.'
    }
    foreach ($taskPath in @('/api/notifications', '/hubs/notifications/negotiate?negotiateVersion=1')) {
        $taskCode = 0
        try { $taskMethod = if ($taskPath.StartsWith('/hubs')) { 'Post' } else { 'Get' }; $taskResult = Invoke-WebRequest -UseBasicParsing "$taskBase$taskPath" -Method $taskMethod -TimeoutSec 3; $taskCode = [int]$taskResult.StatusCode }
        catch { if ($_.Exception.Response) { $taskCode = [int]$_.Exception.Response.StatusCode } else { throw } }
        if ($taskCode -ne 401) { throw "Se esperaba 401 en $taskPath; recibido $taskCode" }
    }
    $taskSwagger = Invoke-RestMethod "$taskBase/swagger/v1/swagger.json" -TimeoutSec 5
    if (-not $taskSwagger.paths.'/api/notifications') { throw 'Faltan contratos en Swagger.' }
    if ($taskSwagger.paths.'/internal/notifications/emergency-events') { throw 'La recepción de eventos debe realizarse por RabbitMQ.' }
    if ($taskSwagger.paths.PSObject.Properties.Name -match 'account-deleted|patients-deleted') { throw 'Hay una ruta excluida.' }
    # Synthetic local JWT; no real user credentials or providers are used.
    function ConvertTo-TaskBase64Url([byte[]]$Value) { [Convert]::ToBase64String($Value).TrimEnd('=').Replace('+','-').Replace('/','_') }
    $taskHeader = ConvertTo-TaskBase64Url ([Text.Encoding]::UTF8.GetBytes('{"alg":"HS256","typ":"JWT"}'))
    $taskPayload = ConvertTo-TaskBase64Url ([Text.Encoding]::UTF8.GetBytes(('{"userId":"7","exp":' + [DateTimeOffset]::UtcNow.AddMinutes(5).ToUnixTimeSeconds() + '}')))
    $taskHmac = [Security.Cryptography.HMACSHA256]::new([Text.Encoding]::ASCII.GetBytes($taskKey))
    try { $taskSignature = ConvertTo-TaskBase64Url ($taskHmac.ComputeHash([Text.Encoding]::ASCII.GetBytes("$taskHeader.$taskPayload"))) } finally { $taskHmac.Dispose() }
    $taskJwt = "$taskHeader.$taskPayload.$taskSignature"
    $taskSocket = [Net.WebSockets.ClientWebSocket]::new()
    $taskCancel = [Threading.CancellationTokenSource]::new(5000)
    try {
        $taskSocket.ConnectAsync([Uri]"ws://127.0.0.1:$Port/hubs/notifications?access_token=$taskJwt", $taskCancel.Token).GetAwaiter().GetResult() | Out-Null
        $taskHandshake = [Text.Encoding]::UTF8.GetBytes('{"protocol":"json","version":1}' + [char]30)
        $taskSocket.SendAsync([ArraySegment[byte]]::new($taskHandshake), [Net.WebSockets.WebSocketMessageType]::Text, $true, $taskCancel.Token).GetAwaiter().GetResult() | Out-Null
        $taskBuffer = [byte[]]::new(1024)
        $taskRead = $taskSocket.ReceiveAsync([ArraySegment[byte]]::new($taskBuffer), $taskCancel.Token).GetAwaiter().GetResult()
        $taskText = [Text.Encoding]::UTF8.GetString($taskBuffer,0,$taskRead.Count)
        if (-not $taskText.StartsWith('{}')) { throw 'SignalR no aceptó el handshake.' }
    } finally { $taskSocket.Abort(); $taskSocket.Dispose(); $taskCancel.Dispose() }
    Write-Output 'PASS: arranque real, health, autenticación, Swagger y WebSocket SignalR con JWT sintético; sin workers ni migraciones.'
} finally {
    if (-not $taskProcess.HasExited) { Stop-Process -Id $taskProcess.Id -Force }
    $taskProcess.Dispose()
}

param(
    [Parameter(Mandatory = $true)][ValidateSet('Export', 'Import', 'Verify')][string]$Mode,
    [Parameter(Mandatory = $true)][string]$PgService,
    [Parameter(Mandatory = $true)][string]$BackupDirectory
)
$ErrorActionPreference = 'Stop'
if ($PgService -notmatch '^[A-Za-z0-9_-]+$') { throw 'PgService debe ser un nombre de servicio libpq, no una cadena de conexión.' }
$taskPsql = Get-Command psql -ErrorAction Stop
$taskDirectory = [System.IO.Path]::GetFullPath($BackupDirectory)
if ($Mode -eq 'Export') {
    New-Item -ItemType Directory -Path $taskDirectory -Force | Out-Null
    foreach ($taskFile in @('notification_logs.csv', 'sms_notification_logs.csv', 'user_push_tokens.csv', 'emergency_location_access_links.csv', 'integration_receipts.csv', 'manifest.json')) {
        if (Test-Path -LiteralPath (Join-Path $taskDirectory $taskFile)) { throw "Ya existe $taskFile; use otra carpeta para preservar el respaldo." }
    }
} elseif (-not (Test-Path -LiteralPath $taskDirectory -PathType Container)) {
    throw 'No existe la carpeta de respaldo.'
}
$taskScript = switch ($Mode) { 'Export' { 'export-data.sql' } 'Import' { 'import-data.sql' } 'Verify' { 'verify-data.sql' } }
$taskSqlPath = Join-Path $PSScriptRoot $taskScript
if ($Mode -eq 'Import') {
    $taskManifest = Get-Content -LiteralPath (Join-Path $taskDirectory 'manifest.json') -Raw | ConvertFrom-Json
    foreach ($taskEntry in $taskManifest) {
        if ($taskEntry.Name -notin @('notification_logs.csv', 'sms_notification_logs.csv', 'user_push_tokens.csv', 'emergency_location_access_links.csv', 'integration_receipts.csv')) { throw 'Nombre inesperado en manifiesto.' }
        $taskHash = (Get-FileHash -LiteralPath (Join-Path $taskDirectory $taskEntry.Name) -Algorithm SHA256).Hash
        if ($taskHash -ne $taskEntry.Hash) { throw "Checksum inválido: $($taskEntry.Name)" }
    }
    if (@($taskManifest).Count -ne 5) { throw 'Manifiesto incompleto.' }
}
Push-Location -LiteralPath $taskDirectory
try {
    $taskArgs = @("service=$PgService", '--no-psqlrc', '--set', 'ON_ERROR_STOP=1', '--file', $taskSqlPath)
    if ($Mode -eq 'Import') { $taskArgs += '--single-transaction' }
    & $taskPsql.Source @taskArgs
    if ($LASTEXITCODE -ne 0) { throw "psql falló con código $LASTEXITCODE; revise el error anterior." }
    if ($Mode -eq 'Export') {
        $taskManifest = foreach ($taskFile in @('notification_logs.csv', 'sms_notification_logs.csv', 'user_push_tokens.csv', 'emergency_location_access_links.csv', 'integration_receipts.csv')) {
            [pscustomobject]@{ Name = $taskFile; Hash = (Get-FileHash -LiteralPath $taskFile -Algorithm SHA256).Hash }
        }
        $taskManifest | ConvertTo-Json | Set-Content -LiteralPath 'manifest.json' -Encoding UTF8
    }
} finally { Pop-Location }

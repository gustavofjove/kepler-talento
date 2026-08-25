param(
    [string]$OutputRoot = "./backups",
    [ValidateRange(1, 3650)][int]$RetentionDays = 30
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$resolvedOutput = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputRoot))
if (-not $resolvedOutput.StartsWith($repositoryRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Backup output must stay inside the repository workspace."
}

$recoveryId = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ")
$recoveryPath = Join-Path $resolvedOutput $recoveryId
New-Item -ItemType Directory -Path $recoveryPath -Force | Out-Null
$env:KTL_BACKUP_ROOT = $resolvedOutput

try {
    docker compose --profile operations run --rm db-tools pg_dump --host postgres --username ktl_migrator --dbname kepler_talento --format custom --file "/backups/$recoveryId/database.dump"
    if ($LASTEXITCODE -ne 0) { throw "PostgreSQL backup failed." }
    docker compose --profile operations run --rm file-tools tar -czf "/backups/$recoveryId/documents.tar.gz" -C /documents .
    if ($LASTEXITCODE -ne 0) { throw "Document backup failed." }
    $migration = docker compose --profile operations run --rm db-tools psql --host postgres --username ktl_migrator --dbname kepler_talento --tuples-only --no-align --command='SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\" DESC LIMIT 1;'
    if ($LASTEXITCODE -ne 0) { throw "Migration version lookup failed." }

    $artifacts = Get-ChildItem -LiteralPath $recoveryPath -File | ForEach-Object {
        [ordered]@{ name = $_.Name; bytes = $_.Length; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
    }
    $manifest = [ordered]@{
        recoveryId = $recoveryId
        createdAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        status = "complete"
        migration = ($migration | Select-Object -Last 1).Trim()
        database = "kepler_talento"
        artifacts = @($artifacts)
        rpoHours = 24
        rtoHours = 4
    }
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $recoveryPath "manifest.json") -Encoding utf8
}
catch {
    [ordered]@{ recoveryId = $recoveryId; createdAtUtc = (Get-Date).ToUniversalTime().ToString("o"); status = "incomplete"; error = "backup.failed" } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $recoveryPath "manifest.json") -Encoding utf8
    throw
}

$cutoff = (Get-Date).ToUniversalTime().AddDays(-$RetentionDays)
$validSets = Get-ChildItem -LiteralPath $resolvedOutput -Directory | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName "manifest.json") } | Sort-Object Name -Descending
$latestValid = $validSets | Where-Object { (Get-Content -Raw -LiteralPath (Join-Path $_.FullName "manifest.json") | ConvertFrom-Json).status -eq "complete" } | Select-Object -First 1
foreach ($set in $validSets) {
    if ($set.FullName -ne $latestValid.FullName -and $set.LastWriteTimeUtc -lt $cutoff) {
        Remove-Item -LiteralPath $set.FullName -Recurse -Force
    }
}

Write-Output $recoveryPath

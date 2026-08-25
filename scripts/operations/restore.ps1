param(
    [Parameter(Mandatory)][string]$RecoveryId,
    [string]$BackupRoot = "./backups",
    [switch]$ConfirmNonProduction
)

$ErrorActionPreference = "Stop"
if (-not $ConfirmNonProduction) { throw "Restore is restricted to an explicitly confirmed non-production validation stack." }
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$resolvedBackupRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $BackupRoot))
if (-not $resolvedBackupRoot.StartsWith($repositoryRoot, [System.StringComparison]::OrdinalIgnoreCase)) { throw "Backup root must stay inside the repository workspace." }
if ($RecoveryId -notmatch '^[0-9]{8}T[0-9]{6}Z$') { throw "Invalid recovery identifier." }
$recoveryPath = Join-Path $resolvedBackupRoot $RecoveryId
$manifestPath = Join-Path $recoveryPath "manifest.json"
if (-not (Test-Path -LiteralPath $manifestPath)) { throw "Recovery manifest not found." }
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
if ($manifest.status -ne "complete") { throw "Incomplete recovery sets cannot be restored." }
foreach ($artifact in $manifest.artifacts) {
    $artifactPath = Join-Path $recoveryPath $artifact.name
    if (-not (Test-Path -LiteralPath $artifactPath)) { throw "Recovery artifact missing." }
    if ((Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $artifact.sha256) { throw "Recovery artifact hash mismatch." }
}

$env:KTL_BACKUP_ROOT = $resolvedBackupRoot
docker compose --profile operations run --rm db-tools pg_restore --host postgres --username ktl_migrator --dbname kepler_talento --clean --if-exists "/backups/$RecoveryId/database.dump"
if ($LASTEXITCODE -ne 0) { throw "PostgreSQL restore failed." }
docker compose --profile operations run --rm file-tools sh -eu -c "find /documents -mindepth 1 -delete; tar -xzf /backups/$RecoveryId/documents.tar.gz -C /documents"
if ($LASTEXITCODE -ne 0) { throw "Document restore failed." }
docker compose run --rm migrator --migrate
if ($LASTEXITCODE -ne 0) { throw "Restored schema validation failed." }

Write-Output "Restore completed; run reconcile.ps1 before accepting the validation result."

$ErrorActionPreference = "Stop"

docker compose run --rm migrator --reconcile
if ($LASTEXITCODE -ne 0) { throw "Document reconciliation found recovery errors. Review document.reconciliation audit events." }
Write-Output "Document reconciliation passed with no missing, mismatched, orphaned, or stale-quarantined objects."

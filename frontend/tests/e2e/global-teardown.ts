import { execFileSync } from 'node:child_process';
import path from 'node:path';
import { repoRoot } from '../repo-root';

// Removes everything the suite created (see scripts/e2e-cleanup.sql for how test data is
// recognised). Set KTL_E2E_KEEP_DATA=1 to keep it for debugging a failed run.
export default function globalTeardown(): void {
  if (process.env['KTL_E2E_KEEP_DATA']) return;
  execFileSync('node', [path.join(repoRoot, 'scripts', 'e2e-cleanup.js')], { stdio: 'inherit' });
}

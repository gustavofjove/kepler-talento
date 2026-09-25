#!/usr/bin/env node
// Purges Playwright-created data from the local Compose stack: rows via e2e-cleanup.sql,
// then the private binaries those rows pointed at. Runs as the e2e global teardown and can
// be run by hand (`node scripts/e2e-cleanup.js`) after an interrupted suite.
const { execFileSync } = require('node:child_process');
const { readFileSync } = require('node:fs');
const path = require('node:path');

const repoRoot = path.resolve(__dirname, '..');
const compose = (args, input) =>
  execFileSync('docker', ['compose', ...args], { cwd: repoRoot, input, encoding: 'utf8' });

const output = compose(
  ['exec', '-T', 'postgres', 'sh', '-c', 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At'],
  readFileSync(path.join(__dirname, 'e2e-cleanup.sql'), 'utf8'),
);

// Only opaque keys of the documented shape reach the shell.
const keys = output
  .split(/\r?\n/)
  .filter((line) => line.startsWith('KEY '))
  .map((line) => line.slice(4))
  .filter((key) => /^(candidates|imports)\/[0-9a-f]+(\/[0-9a-f]+)?\/content$/.test(key));

if (keys.length > 0) {
  compose(
    [
      'exec',
      '-T',
      '-u',
      '0',
      'api',
      'sh',
      '-c',
      'cd /var/lib/kepler-talento/documents && while read k; do rm -f "$k"; rmdir -p "$(dirname "$k")" 2>/dev/null; done; true',
    ],
    keys.join('\n') + '\n',
  );
}
console.log(`e2e cleanup: purged test data and ${keys.length} stored file(s).`);

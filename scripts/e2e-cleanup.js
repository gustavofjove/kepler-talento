#!/usr/bin/env node
// Purges Playwright-created data from the local Compose stack: rows via e2e-cleanup.sql,
// then the private binaries those rows pointed at. Runs as the e2e global teardown and can
// be run by hand (`node scripts/e2e-cleanup.js`) after an interrupted suite.
const { execFileSync } = require('node:child_process');
const { createDecipheriv } = require('node:crypto');
const { readFileSync } = require('node:fs');
const path = require('node:path');

const repoRoot = path.resolve(__dirname, '..');
const compose = (args, input) =>
  execFileSync('docker', ['compose', ...args], { cwd: repoRoot, input, encoding: 'utf8' });
const psql = (sql) =>
  compose(
    [
      'exec',
      '-T',
      'postgres',
      'sh',
      '-c',
      'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At -F "\t"',
    ],
    sql,
  );

// KTL-33: candidate names and e-mails are stored encrypted. Test candidates are still recognised
// by the Date.now() marker, so they are decrypted here with the development key file — the same
// file the Compose api mounts. Nothing decrypted is printed.
const keyFile = path.resolve(
  repoRoot,
  process.env.KTL_FIELD_KEYS_FILE || path.join('secrets', 'field-keys.json'),
);
const encryptionKeys = JSON.parse(readFileSync(keyFile, 'utf8')).encryption.keys;

const decrypt = (envelope, column) => {
  const [prefix, keyId, payload] = envelope.split('.');
  if (prefix !== 'ktl1' || !encryptionKeys[keyId] || !payload) return '';
  const bytes = Buffer.from(payload, 'base64url');
  const decipher = createDecipheriv(
    'aes-256-gcm',
    Buffer.from(encryptionKeys[keyId], 'base64'),
    bytes.subarray(0, 12),
  );
  decipher.setAAD(Buffer.from(`CND_Candidates.${column}`, 'utf8'));
  decipher.setAuthTag(bytes.subarray(bytes.length - 16));
  return Buffer.concat([
    decipher.update(bytes.subarray(12, bytes.length - 16)),
    decipher.final(),
  ]).toString('utf8');
};

const marker = /\d{13}/;
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
const candidateIds = psql('select "Id", "FirstName", "LastName", "Email" from "CND_Candidates";')
  .split(/\r?\n/)
  .filter((line) => line.length > 0)
  .map((line) => line.split('\t'))
  .filter(
    ([, first, last, email]) =>
      marker.test(decrypt(first, 'FirstName')) ||
      marker.test(decrypt(last, 'LastName')) ||
      marker.test(decrypt(email, 'Email')),
  )
  .map(([id]) => id)
  .filter((id) => uuid.test(id));

const sql = readFileSync(path.join(__dirname, 'e2e-cleanup.sql'), 'utf8').replace(
  '/*E2E_CANDIDATE_IDS*/',
  candidateIds.length > 0 ? candidateIds.map((id) => `'${id}'::uuid`).join(', ') : 'NULL::uuid',
);
const output = psql(sql);

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
console.log(
  `e2e cleanup: purged ${candidateIds.length} test candidate(s), their data and ${keys.length} stored file(s).`,
);

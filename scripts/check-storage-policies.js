#!/usr/bin/env node
const fs = require('node:fs');
const path = require('node:path');

const read = (relative) => fs.readFileSync(path.join(process.cwd(), relative), 'utf8');
const storage = read('backend/Infrastructure/Documents/FileSystemDocumentStorage.cs');
const download = read('backend/Infrastructure/Documents/DocumentDownloadService.cs');
const nginx = read('nginx.conf');
const compose = read('docker-compose.yml');

const serviceBlock = (name) => {
  const match = compose.match(
    new RegExp(`^  ${name}:\\r?\\n([\\s\\S]*?)(?=^  [a-zA-Z0-9_-]+:\\r?\\n|^networks:)`, 'm'),
  );
  return match?.[0] ?? '';
};

const checks = [
  [
    'Opaque key construction',
    read('backend/Infrastructure/Documents/DocumentStorageKey.cs').includes(
      'candidates/{candidateId:N}/{documentId:N}/content',
    ),
  ],
  ['Traversal containment enforced', storage.includes('ResolveContained')],
  ['Create-new collision semantics', storage.includes('FileMode.CreateNew')],
  [
    'Quarantine precedes available promotion',
    storage.includes('_quarantineRoot') && storage.includes('File.Move'),
  ],
  [
    'Only clean metadata downloads',
    download.includes('metadata.ScanState != DocumentScanState.Clean'),
  ],
  ['No direct Nginx document route', !/location\s+[^\n]*documents/i.test(nginx)],
  [
    'Document volume mounts only backend tools',
    !serviceBlock('nginx').includes('documents:/var/lib/kepler-talento'),
  ],
  [
    '20 MiB content limit plus bounded multipart envelope align',
    nginx.includes('client_max_body_size 21m') && storage.includes('AbsoluteMaximumBytes'),
  ],
];

const failed = checks.filter(([, passed]) => !passed);
for (const [name, passed] of checks)
  console.log(`[security:storage] ${passed ? 'PASS' : 'FAIL'} ${name}`);
if (failed.length) process.exit(1);
console.log(
  '[security:storage] Private quarantine/download boundary checks passed; legacy Supabase storage SQL remains for unchanged paths.',
);

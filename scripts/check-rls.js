#!/usr/bin/env node
const fs = require('node:fs');
const path = require('node:path');

const read = (relative) => fs.readFileSync(path.join(process.cwd(), relative), 'utf8');
const compose = read('docker-compose.yml');
const migration = fs
  .readdirSync(path.join(process.cwd(), 'backend/Infrastructure/Persistence/Migrations'))
  .filter((name) => name.endsWith('_InitialInfrastructure.cs'))
  .map((name) => read(`backend/Infrastructure/Persistence/Migrations/${name}`))
  .join('\n');
const program = read('backend/Web/Program.cs');
const frontendConfig = read('public/env.template.js');

const serviceBlock = (name) => {
  const match = compose.match(
    new RegExp(`^  ${name}:\\r?\\n([\\s\\S]*?)(?=^  [a-zA-Z0-9_-]+:\\r?\\n|^networks:)`, 'm'),
  );
  return match?.[0] ?? '';
};

const checks = [
  ['PostgreSQL has no published host port', !serviceBlock('postgres').includes('\n    ports:')],
  [
    'Runtime identity is distinct',
    compose.includes('Username=ktl_runtime') && compose.includes('Username=ktl_migrator'),
  ],
  ['Runtime DML grants are explicit', migration.includes('GRANT SELECT, INSERT, UPDATE, DELETE')],
  [
    'Runtime schema creation is revoked',
    migration.includes('REVOKE CREATE ON SCHEMA public FROM ktl_runtime'),
  ],
  [
    'Production development actor is rejected',
    program.includes('DevelopmentActor cannot be enabled in Production'),
  ],
  [
    'Production reference routes are not registered',
    program.includes('IsEnvironment("Testing")') &&
      program.includes('MapReferenceCandidateEndpoints'),
  ],
  [
    'Frontend has no PostgreSQL credential',
    !/POSTGRES|ConnectionStrings|ktl_runtime/i.test(frontendConfig),
  ],
];

const failed = checks.filter(([, passed]) => !passed);
for (const [name, passed] of checks)
  console.log(`[security:database] ${passed ? 'PASS' : 'FAIL'} ${name}`);
if (failed.length) process.exit(1);
console.log(
  '[security:database] Replacement boundary checks passed; legacy RLS SQL remains for unchanged Supabase paths.',
);

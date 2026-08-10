#!/usr/bin/env node
const { execSync } = require('node:child_process');

const commands = [
  'npm run build',
  'npm test -- --runInBand',
  'npm run test:integration',
  'npm run security:rls',
  'npm run security:storage',
];

if (process.env.RUN_E2E === 'true') {
  commands.push('npm run e2e');
}

try {
  for (const command of commands) {
    console.log(`[release:gate] Running: ${command}`);
    execSync(command, { stdio: 'inherit' });
  }
  console.log('[release:gate] All gates passed');
} catch (error) {
  console.error('[release:gate] Failed');
  process.exit(1);
}

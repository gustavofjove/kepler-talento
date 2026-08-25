#!/usr/bin/env node
const required = ['STAGING_BASE_URL'];
const missing = required.filter((key) => !process.env[key]);

console.log('[smoke:staging] Starting staging smoke checklist');
for (const key of required) {
  console.log(`- ${key}: ${process.env[key] ? 'present' : 'missing'}`);
}

if (missing.length) {
  console.log(`[smoke:staging] Missing env vars: ${missing.join(', ')}`);
  if (process.argv.includes('--strict')) {
    process.exit(1);
  }
}

console.log('[smoke:staging] Checklist completed');
if (process.env.SUPABASE_URL) console.log('- SUPABASE_URL: present for an unchanged legacy path');

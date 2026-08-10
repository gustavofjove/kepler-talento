#!/usr/bin/env node
const fs = require('node:fs');
const path = require('node:path');

const filePath = path.join(process.cwd(), 'tests/security/rls-auth.sql');
const sql = fs.readFileSync(filePath, 'utf-8');

const requiredPatterns = [
	'roles_select',
	'profiles_self_select',
	'candidates_select',
	'candidates_insert',
	'candidates_update',
	'trg_profiles_last_admin_guardrail',
];
const missing = requiredPatterns.filter((pattern) => !sql.includes(pattern));

if (missing.length) {
	console.error(`[security:rls] Missing required checks: ${missing.join(', ')}`);
	process.exit(1);
}

console.log('[security:rls] SQL guardrail checks are defined in tests/security/rls-auth.sql');
process.exit(0);

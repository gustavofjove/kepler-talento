#!/usr/bin/env node
const fs = require('node:fs');
const path = require('node:path');

const filePath = path.join(process.cwd(), 'tests/security/storage-candidate-cvs.sql');
const sql = fs.readFileSync(filePath, 'utf-8');

const requiredPatterns = [
	'candidate-cvs',
	'candidate_cvs_read',
	'candidate_cvs_insert',
	'can_access_candidate_cv_path',
];
const missing = requiredPatterns.filter((pattern) => !sql.includes(pattern));

if (missing.length) {
	console.error(`[security:storage] Missing required checks: ${missing.join(', ')}`);
	process.exit(1);
}

console.log('[security:storage] Storage checks are defined in tests/security/storage-candidate-cvs.sql');
process.exit(0);

import { resolve } from 'node:path';

/**
 * Absolute path of the repository root. Vitest runs from `frontend/`, but several specs
 * read contracts that live beside it (`backend/`, `supabase/`, `docker-compose.yml`).
 */
export const repoRoot = resolve(__dirname, '../..');

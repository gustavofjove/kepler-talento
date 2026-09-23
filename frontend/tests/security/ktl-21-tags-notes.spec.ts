import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { repoRoot } from '../repo-root';

const read = (relative: string) => readFileSync(join(repoRoot, relative), 'utf8');
const migration = read(
  'backend/Infrastructure/Persistence/Migrations/20260917115642_AddCandidateTagsAndNotes.cs',
);

describe('KTL-21 tags and notes security boundary', () => {
  it('ships the exact runtime grants for tags and notes', () => {
    expect(migration).toContain(
      'GRANT SELECT, INSERT, UPDATE, DELETE ON "CND_CandidateTags" TO ktl_runtime',
    );
    expect(migration).toContain(
      'GRANT SELECT, INSERT, UPDATE ON "CND_CandidateNotes" TO ktl_runtime',
    );
    expect(migration).toContain('REVOKE DELETE, TRUNCATE ON "CND_CandidateNotes" FROM ktl_runtime');
    expect(migration).toContain('REVOKE TRUNCATE ON "CND_CandidateTags" FROM ktl_runtime');
  });

  it('exposes no physical note deletion endpoint and redacts note and tag payload fields', () => {
    const endpoints = read('backend/Web/Features/Candidates/CandidateEndpoints.cs');
    const redaction = read('backend/Web/Observability/PersonalDataRedaction.cs');
    expect(endpoints).not.toMatch(/MapDelete\([^\n]*notes/i);
    expect(redaction).toContain('"body"');
    expect(redaction).toContain('"tagCriteria"');
  });

  it('keeps note bodies out of audit event storage', () => {
    const audit = read('backend/Domain/Auditing/AuditEvent.cs');
    expect(audit).not.toMatch(/Body|NoteBody|TagName/);
  });
});

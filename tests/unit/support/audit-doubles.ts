import type { AuditEvent, AuditPage } from '../../../src/app/features/admin/audit/audit.logic';

/** An audit event as the API returns it: identifiers and codes only. */
export const auditEvent = (overrides: Partial<AuditEvent> = {}): AuditEvent => ({
  id: 'e-1',
  eventType: 'candidate.read',
  subjectId: '0192aaaa0000700080000000000000c1',
  actorKind: 'user',
  actorUserId: 'u-1',
  outcomeCode: 'served',
  createdAt: '2026-09-17T08:30:00Z',
  ...overrides,
});

export const auditPage = (overrides: Partial<AuditPage> = {}): AuditPage => ({
  items: [],
  page: 1,
  pageSize: 25,
  totalCount: 0,
  ...overrides,
});

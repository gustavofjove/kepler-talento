import type { TFunction } from 'i18next';

export type AuditActorKind = 'user' | 'system' | 'unknown';

export interface AuditEvent {
  id: string;
  eventType: string;
  subjectId: string;
  actorKind: AuditActorKind;
  actorUserId: string | null;
  outcomeCode: string | null;
  createdAt: string;
}

export interface AuditPage {
  items: AuditEvent[];
  page: number;
  pageSize: number;
  totalCount: number;
}

/** The filters as the form holds them. Empty strings are not sent. */
export interface AuditFilters {
  /** `YYYY-MM-DD`, the start of that local day. */
  from: string;
  /** `YYYY-MM-DD`, the end of that local day. */
  to: string;
  eventType: string;
  actor: string;
  subject: string;
}

export const EMPTY_AUDIT_FILTERS: AuditFilters = {
  from: '',
  to: '',
  eventType: '',
  actor: '',
  subject: '',
};

export const AUDIT_PAGE_SIZE = 25;

/**
 * The closed event-type catalogue, mirroring `AuditEventTypes.All` in the API. The filter offers
 * exactly these; the API rejects anything else.
 */
export const AUDIT_EVENT_TYPES = [
  'candidate.created',
  'candidate.updated',
  'candidate.status_changed',
  'candidate.removed',
  'candidate.restored',
  'candidate.relations_changed',
  'candidate.tags_changed',
  'candidate.note_added',
  'candidate.note_updated',
  'candidate.note_retired',
  'candidate.documents_changed',
  'candidate.read',
  'catalog.created',
  'catalog.updated',
  'catalog.reordered',
  'catalog.activation_changed',
  'document.upload.accepted',
  'document.scan',
  'document.downloaded',
  'document.primary.changed',
  'document.removed',
  'document.reconciliation',
] as const;

/** Builds the query string for `GET /api/audit/events`. */
export function auditQuery(
  filters: AuditFilters,
  page: number,
  pageSize = AUDIT_PAGE_SIZE,
): string {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (filters.from) {
    query.set('from', new Date(`${filters.from}T00:00:00`).toISOString());
  }
  if (filters.to) {
    query.set('to', new Date(`${filters.to}T23:59:59.999`).toISOString());
  }
  for (const key of ['eventType', 'actor', 'subject'] as const) {
    const value = filters[key].trim();
    if (value) {
      query.set(key, value);
    }
  }
  return query.toString();
}

/** The distinct user ids on a page, the only ones worth resolving. */
export function actorIds(events: AuditEvent[]): string[] {
  return [
    ...new Set(
      events.flatMap((event) =>
        event.actorKind === 'user' && event.actorUserId ? [event.actorUserId] : [],
      ),
    ),
  ];
}

/**
 * What the Actor column shows. Never empty: an unknown actor and the system are distinct explicit
 * labels, because a blank cell reads as "nobody", which is a different claim. A user whose name
 * the reader may not resolve is shown by internal id.
 */
export function actorLabel(event: AuditEvent, names: Record<string, string>, t: TFunction): string {
  if (event.actorKind === 'system') {
    return t('admin.audit.actor.system');
  }
  if (event.actorKind === 'user' && event.actorUserId) {
    return names[event.actorUserId] ?? event.actorUserId;
  }
  return t('admin.audit.actor.unknown');
}

export function pageCount(page: AuditPage): number {
  return Math.max(1, Math.ceil(page.totalCount / page.pageSize));
}

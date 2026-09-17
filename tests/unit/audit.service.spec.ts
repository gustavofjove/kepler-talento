import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { i18n } from '../../src/app/core/i18n/i18n';
import {
  actorIds,
  actorLabel,
  AUDIT_EVENT_TYPES,
  auditQuery,
  EMPTY_AUDIT_FILTERS,
  pageCount,
  type AuditEvent,
} from '../../src/app/features/admin/audit/audit.logic';
import { AuditService } from '../../src/app/features/admin/audit/audit.service';
import { AppError } from '../../src/app/shared/models/error.models';
import { auditEvent, auditPage } from './support/audit-doubles';

const t = i18n.t.bind(i18n);

describe('AuditService', () => {
  it('requests the first page with the default size and no empty filters', async () => {
    const api = { request: vi.fn().mockResolvedValue(auditPage()) };
    const service = new AuditService(api as never);

    await service.load(EMPTY_AUDIT_FILTERS);

    expect(api.request).toHaveBeenCalledWith('/audit/events?page=1&pageSize=25');
  });

  it('sends every filter it is given and publishes the page with those filters', async () => {
    const page = auditPage({ page: 3, totalCount: 80 });
    const api = { request: vi.fn().mockResolvedValue(page) };
    const service = new AuditService(api as never);
    const filters = {
      from: '2026-09-01',
      to: '2026-09-02',
      eventType: 'candidate.read',
      actor: ' system ',
      subject: 'abc',
    };

    await service.load(filters, 3);

    const [path] = api.request.mock.calls[0] as [string];
    const query = new URLSearchParams(path.split('?')[1]);
    expect(query.get('page')).toBe('3');
    expect(query.get('eventType')).toBe('candidate.read');
    expect(query.get('actor')).toBe('system');
    expect(query.get('subject')).toBe('abc');
    expect(new Date(query.get('from')!).getTime()).toBe(new Date('2026-09-01T00:00:00').getTime());
    expect(new Date(query.get('to')!).getTime()).toBe(
      new Date('2026-09-02T23:59:59.999').getTime(),
    );
    expect(service.state()).toEqual({ filters, page });
  });

  it('propagates an API refusal and leaves the published page untouched', async () => {
    const refusal = new AppError('FORBIDDEN', 'x', undefined, undefined, 'authorization.denied');
    const api = { request: vi.fn().mockRejectedValue(refusal) };
    const service = new AuditService(api as never);
    const before = service.state();

    await expect(service.load(EMPTY_AUDIT_FILTERS)).rejects.toBe(refusal);
    expect(service.state()).toBe(before);
  });

  it('resolves only the requested actor names, in one call to the users endpoint', async () => {
    const api = {
      request: vi.fn().mockResolvedValue({
        items: [
          { id: 'u-1', displayName: 'Ana' },
          { id: 'u-2', displayName: 'Bea' },
        ],
      }),
    };
    const service = new AuditService(api as never);

    const names = await service.resolveActorNames(['u-1', 'u-9']);

    expect(names).toEqual({ 'u-1': 'Ana' });
    expect(api.request).toHaveBeenCalledOnce();
    expect(api.request.mock.calls[0][0]).toBe(
      '/admin/users?includeInactive=true&page=1&pageSize=100',
    );
  });

  it('makes no users request when there is nothing to resolve', async () => {
    const api = { request: vi.fn() };
    const service = new AuditService(api as never);

    expect(await service.resolveActorNames([])).toEqual({});
    expect(api.request).not.toHaveBeenCalled();
  });

  it('keeps nothing in browser storage', async () => {
    localStorage.clear();
    const api = { request: vi.fn().mockResolvedValue(auditPage()) };

    await new AuditService(api as never).load(EMPTY_AUDIT_FILTERS);

    expect(localStorage.length).toBe(0);
  });
});

describe('audit page logic', () => {
  it('collects each user actor once and ignores system and unknown actors', () => {
    const events: AuditEvent[] = [
      auditEvent({ id: 'a', actorUserId: 'u-1' }),
      auditEvent({ id: 'b', actorUserId: 'u-1' }),
      auditEvent({ id: 'c', actorKind: 'system', actorUserId: null }),
      auditEvent({ id: 'd', actorKind: 'unknown', actorUserId: null }),
    ];

    expect(actorIds(events)).toEqual(['u-1']);
  });

  it('labels system and unknown actors distinctly and never with an empty value', () => {
    const system = actorLabel(auditEvent({ actorKind: 'system', actorUserId: null }), {}, t);
    const unknown = actorLabel(auditEvent({ actorKind: 'unknown', actorUserId: null }), {}, t);

    expect(system).toBe('Sistema');
    expect(unknown).toBe('Actor desconocido');
    expect(system).not.toBe(unknown);
  });

  it('shows a resolved name, or the internal id when the name is not available', () => {
    const event = auditEvent({ actorUserId: 'u-1' });

    expect(actorLabel(event, { 'u-1': 'Ana' }, t)).toBe('Ana');
    expect(actorLabel(event, {}, t)).toBe('u-1');
  });

  it('counts pages from the total, with at least one', () => {
    expect(pageCount(auditPage({ totalCount: 0 }))).toBe(1);
    expect(pageCount(auditPage({ totalCount: 51 }))).toBe(3);
  });

  it('builds the query without the filters left empty', () => {
    expect(auditQuery({ ...EMPTY_AUDIT_FILTERS, subject: '  ' }, 2)).toBe('page=2&pageSize=25');
  });

  it('offers exactly the event-type catalogue the API defines, each with Spanish copy', () => {
    const domain = join(process.cwd(), 'backend/Domain');
    const sources = [
      'Candidates/CandidateAuditEvents.cs',
      'Catalogs/CatalogAuditEvents.cs',
      'Documents/DocumentAuditEvents.cs',
    ].map((file) => readFileSync(join(domain, file), 'utf8'));
    const codes = sources.flatMap((source) =>
      [...source.matchAll(/public const string \w+ = "([^"]+)";/g)].map((match) => match[1]),
    );

    expect([...AUDIT_EVENT_TYPES].sort()).toEqual([...codes].sort());
    for (const type of AUDIT_EVENT_TYPES) {
      expect(i18n.exists(`admin.audit.eventType.${type}`)).toBe(true);
    }
  });
});

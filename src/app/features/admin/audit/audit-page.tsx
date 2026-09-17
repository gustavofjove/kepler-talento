import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useAudit, usePermission, useServices } from '../../../core/di/services-context';
import { formatDate, formatNumber } from '../../../core/i18n/format';
import { useErrorToast } from '../../../core/services/use-error-toast';
import {
  actorIds,
  actorLabel,
  AUDIT_EVENT_TYPES,
  EMPTY_AUDIT_FILTERS,
  pageCount,
  type AuditFilters,
} from './audit.logic';

/**
 * Auditoría (KTL-19). Lists the trail with its filters and paging. The API sends identifiers and
 * codes only; actor names are resolved here, per page, for readers entitled to the user
 * directory, and everyone else sees the internal id.
 */
export function AuditPage() {
  const { t } = useTranslation();
  const { auditService } = useServices();
  const { filters: applied, page } = useAudit();
  const canResolveNames = usePermission('users.manage');
  const notifyError = useErrorToast();
  const [draft, setDraft] = useState<AuditFilters>(applied);
  const [names, setNames] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);

  const load = useCallback(
    async (filters: AuditFilters, pageNumber: number) => {
      setBusy(true);
      try {
        await auditService.load(filters, pageNumber);
      } catch (error) {
        notifyError(error, t('admin.audit.errors.load'));
      } finally {
        setBusy(false);
      }
    },
    [auditService, notifyError, t],
  );

  useEffect(() => {
    void load(auditService.state().filters, 1);
  }, [auditService, load]);

  const ids = useMemo(() => actorIds(page.items), [page.items]);
  useEffect(() => {
    if (!canResolveNames || ids.length === 0) {
      return;
    }
    let cancelled = false;
    auditService
      .resolveActorNames(ids)
      .then((resolved) => {
        if (!cancelled) setNames((current) => ({ ...current, ...resolved }));
      })
      // Unresolved names fall back to the id; that is not worth interrupting the reader for.
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, [auditService, canResolveNames, ids]);

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    void load(draft, 1);
  };

  const set = (key: keyof AuditFilters) => (value: string) =>
    setDraft((current) => ({ ...current, [key]: value }));

  const pages = pageCount(page);

  return (
    <section className="page">
      <div className="page-header">
        <h1>{t('admin.audit.title')}</h1>
        <p className="muted">{t('admin.audit.subtitle')}</p>
      </div>

      <form className="panel grid two" onSubmit={submit} noValidate data-testid="audit-filters">
        <div className="field">
          <label htmlFor="audit-from">{t('admin.audit.filter.from')}</label>
          <input
            id="audit-from"
            name="from"
            type="date"
            data-testid="audit-filter-from"
            value={draft.from}
            onChange={(event) => set('from')(event.target.value)}
          />
        </div>
        <div className="field">
          <label htmlFor="audit-to">{t('admin.audit.filter.to')}</label>
          <input
            id="audit-to"
            name="to"
            type="date"
            data-testid="audit-filter-to"
            value={draft.to}
            onChange={(event) => set('to')(event.target.value)}
          />
        </div>
        <div className="field">
          <label htmlFor="audit-event-type">{t('admin.audit.filter.eventType')}</label>
          <select
            id="audit-event-type"
            name="eventType"
            data-testid="audit-filter-event-type"
            value={draft.eventType}
            onChange={(event) => set('eventType')(event.target.value)}
          >
            <option value="">{t('admin.audit.filter.anyEventType')}</option>
            {AUDIT_EVENT_TYPES.map((type) => (
              <option key={type} value={type}>
                {t(`admin.audit.eventType.${type}`)}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="audit-actor">{t('admin.audit.filter.actor')}</label>
          <input
            id="audit-actor"
            name="actor"
            data-testid="audit-filter-actor"
            placeholder={t('admin.audit.filter.actorPlaceholder')}
            value={draft.actor}
            onChange={(event) => set('actor')(event.target.value)}
          />
        </div>
        <div className="field span-all">
          <label htmlFor="audit-subject">{t('admin.audit.filter.subject')}</label>
          <input
            id="audit-subject"
            name="subject"
            data-testid="audit-filter-subject"
            placeholder={t('admin.audit.filter.subjectPlaceholder')}
            value={draft.subject}
            onChange={(event) => set('subject')(event.target.value)}
          />
        </div>
        <div className="form-actions span-all">
          <button
            className="button secondary"
            type="button"
            name="clearFilters"
            data-testid="audit-clear"
            disabled={busy}
            onClick={() => {
              setDraft(EMPTY_AUDIT_FILTERS);
              void load(EMPTY_AUDIT_FILTERS, 1);
            }}
          >
            {t('admin.audit.clear')}
          </button>
          <button className="button" type="submit" data-testid="audit-apply" disabled={busy}>
            {t('admin.audit.apply')}
          </button>
        </div>
      </form>

      <div className="panel stack">
        <p className="muted" data-testid="audit-total">
          {t('admin.audit.total', { total: formatNumber(page.totalCount) })}
        </p>
        {page.items.length ? (
          <div className="table-wrap">
            <table data-testid="audit-table">
              <thead>
                <tr>
                  <th>{t('admin.audit.column.date')}</th>
                  <th>{t('admin.audit.column.eventType')}</th>
                  <th>{t('admin.audit.column.subject')}</th>
                  <th>{t('admin.audit.column.actor')}</th>
                  <th>{t('admin.audit.column.outcome')}</th>
                </tr>
              </thead>
              <tbody>
                {page.items.map((event) => (
                  <tr
                    key={event.id}
                    data-testid={`audit-row-${event.id}`}
                    data-event-type={event.eventType}
                    data-actor-kind={event.actorKind}
                  >
                    <td>
                      {formatDate(event.createdAt, { dateStyle: 'short', timeStyle: 'medium' })}
                    </td>
                    <td>
                      {t(`admin.audit.eventType.${event.eventType}`, {
                        defaultValue: event.eventType,
                      })}
                    </td>
                    <td>
                      <code>{event.subjectId}</code>
                    </td>
                    <td data-testid="audit-actor">
                      <span className={event.actorKind === 'user' ? undefined : 'badge'}>
                        {actorLabel(event, names, t)}
                      </span>
                    </td>
                    <td>{event.outcomeCode ?? t('admin.audit.noOutcome')}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <p className="empty-state" data-testid="audit-empty">
            {t('admin.audit.empty')}
          </p>
        )}
        {pages > 1 ? (
          <div className="toolbar">
            <button
              className="button secondary"
              type="button"
              name="previousPage"
              data-testid="audit-previous"
              disabled={busy || page.page <= 1}
              onClick={() => void load(applied, page.page - 1)}
            >
              {t('admin.audit.previous')}
            </button>
            <span className="muted" data-testid="audit-page">
              {t('admin.audit.pageOf', {
                page: formatNumber(page.page),
                pages: formatNumber(pages),
              })}
            </span>
            <button
              className="button secondary"
              type="button"
              name="nextPage"
              data-testid="audit-next"
              disabled={busy || page.page >= pages}
              onClick={() => void load(applied, page.page + 1)}
            >
              {t('admin.audit.next')}
            </button>
          </div>
        ) : null}
      </div>
    </section>
  );
}

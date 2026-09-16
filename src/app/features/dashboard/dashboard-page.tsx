import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { usePermission, useServices } from '../../core/di/services-context';
import { formatNumber } from '../../core/i18n/format';
import type { CandidateListQuery } from '../candidates/models/candidate.models';

interface Counts {
  active: number;
  withoutCv: number;
  withPrimaryCv: number;
  /** Absent for actors who may not see removed candidates. */
  inactive?: number;
}

const COUNT_QUERY: CandidateListQuery = {
  page: 1,
  pageSize: 1,
  sortField: 'updatedAt',
  sortDirection: 'desc',
  text: '',
  status: '',
  hasCv: '',
  includeInactive: false,
};

/**
 * Operational counts, taken from the server's totals (KTL-18).
 *
 * Each figure is the `totalCount` of a one-row search, so no candidate list reaches the
 * browser to be counted. "Pendientes de revisión" and "Recibidos este mes" were removed: the
 * search contract has no review-date or received-date filter, and computing them used to
 * mean downloading every candidate's retention metadata.
 */
export function DashboardPage() {
  const { candidateService } = useServices();
  const { t } = useTranslation();
  const canSeeRemoved = usePermission('candidates.delete');
  const [counts, setCounts] = useState<Counts | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    const total = (patch: Partial<CandidateListQuery>) =>
      candidateService
        .listPage({ ...COUNT_QUERY, ...patch }, controller.signal)
        .then((page) => page.totalCount);
    Promise.all([
      total({}),
      total({ hasCv: 'no' }),
      total({ hasCv: 'yes' }),
      canSeeRemoved ? total({ includeInactive: true }) : Promise.resolve(undefined),
    ])
      .then(([active, withoutCv, withPrimaryCv, everyone]) => {
        if (!controller.signal.aborted) {
          setCounts({
            active,
            withoutCv,
            withPrimaryCv,
            inactive: everyone === undefined ? undefined : everyone - active,
          });
        }
      })
      .catch(() => {
        if (!controller.signal.aborted) {
          setFailed(true);
        }
      });
    return () => controller.abort();
  }, [candidateService, canSeeRemoved]);

  const value = (count: number | undefined) =>
    count === undefined ? (failed ? '—' : '…') : formatNumber(count);

  return (
    <section className="page">
      <div className="toolbar">
        <div className="page-header">
          <h1>{t('dashboard.title')}</h1>
          <p className="muted">{t('dashboard.subtitle')}</p>
        </div>
        <Link className="button" to="/app/candidates/new">
          {t('candidate.new')}
        </Link>
      </div>
      {failed ? (
        <p className="empty-state" data-testid="dashboard-error">
          {t('dashboard.error')}
        </p>
      ) : null}
      <div className="grid three">
        <article className="panel kpi-card" data-testid="kpi-active">
          <span className="kpi-label">{t('dashboard.kpi.active')}</span>
          <span className="kpi-value">{value(counts?.active)}</span>
        </article>
        <article className="panel kpi-card" data-testid="kpi-without-cv">
          <span className="kpi-label">{t('dashboard.kpi.withoutCv')}</span>
          <span className="kpi-value">{value(counts?.withoutCv)}</span>
        </article>
        <article className="panel kpi-card" data-testid="kpi-with-primary-cv">
          <span className="kpi-label">{t('dashboard.kpi.withPrimaryCv')}</span>
          <span className="kpi-value">{value(counts?.withPrimaryCv)}</span>
        </article>
      </div>

      <div className="grid three">
        <article className="panel stack">
          <h2>{t('dashboard.shortcuts.title')}</h2>
          <p className="muted">{t('dashboard.shortcuts.subtitle')}</p>
          <Link className="button secondary" to="/app/candidates">
            {t('dashboard.shortcuts.list')}
          </Link>
          <Link className="button secondary" to="/app/search">
            {t('candidate.search')}
          </Link>
        </article>
        {canSeeRemoved ? (
          <article className="panel kpi-card" data-testid="kpi-inactive">
            <span className="kpi-label">{t('dashboard.kpi.inactive')}</span>
            <span className="kpi-value">{value(counts?.inactive)}</span>
          </article>
        ) : null}
      </div>
    </section>
  );
}

import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';
import { usePermission, useServices } from '../../core/di/services-context';
import { formatDate } from '../../core/i18n/format';
import { useErrorToast } from '../../core/services/use-error-toast';
import { SearchCriteriaSummary } from '../search/components/search-criteria-summary';
import { SearchResults } from '../search/components/search-results';
import type { SearchResultPage } from '../search/models/search.models';
import { PositionDescription } from './components/position-description-editor';
import type { Position } from './position.models';
import './positions.css';

const EMPTY_RESULTS: SearchResultPage = { items: [], page: 1, pageSize: 25, totalCount: 0 };
export function PositionDetailPage() {
  const { id } = useParams();
  const { t } = useTranslation();
  const { positionService, candidateSearchService } = useServices();
  const notifyError = useErrorToast();
  const canManage = usePermission('positions.manage');
  const canReadCandidates = usePermission('candidates.read');
  const [position, setPosition] = useState<Position>();
  const [results, setResults] = useState(EMPTY_RESULTS);
  const [loading, setLoading] = useState(false);
  const [failed, setFailed] = useState(false);
  useEffect(() => {
    if (!id) return;
    void positionService
      .get(id)
      .then(setPosition)
      .catch((error) => notifyError(error, t('positions.detail.loadError')));
  }, [id, notifyError, positionService, t]);
  // One search in flight at a time: a newer page request aborts the older one, so a slow
  // earlier response can never overwrite the page the user asked for last.
  const inFlight = useRef<AbortController | null>(null);
  const search = useCallback(
    (page = 1) => {
      if (!position || !canReadCandidates) return () => undefined;
      inFlight.current?.abort();
      const controller = new AbortController();
      inFlight.current = controller;
      setLoading(true);
      setFailed(false);
      void candidateSearchService
        .search(position.requirements, { page, signal: controller.signal })
        .then((next) => {
          if (!controller.signal.aborted) setResults(next);
        })
        .catch((error) => {
          if (!controller.signal.aborted) {
            setFailed(true);
            notifyError(error, t('positions.matches.error'));
          }
        })
        .finally(() => {
          if (!controller.signal.aborted) setLoading(false);
        });
      return () => controller.abort();
    },
    [canReadCandidates, candidateSearchService, notifyError, position, t],
  );
  useEffect(() => search(1), [search]);
  useEffect(() => () => inFlight.current?.abort(), []);
  if (!position) return <p role="status">{t('positions.detail.loading')}</p>;
  return (
    <section className="page position-detail">
      <div className="toolbar position-detail-toolbar">
        <div className="page-header position-detail-title">
          <h1>{position.title}</h1>
          <span className="badge">{t(`positions.status.${position.status}`)}</span>
        </div>
        {canManage ? (
          <Link className="button primary" to={`/app/positions/${position.id}/edit`}>
            {t('positions.actions.edit')}
          </Link>
        ) : null}
      </div>
      <div className="panel position-section">
        <dl className="position-metadata">
          <div>
            <dt>{t('positions.detail.location')}</dt>
            <dd>{position.location || t('positions.detail.notSpecified')}</dd>
          </div>
          <div>
            <dt>{t('positions.detail.createdAt')}</dt>
            <dd>{formatDate(position.createdAtUtc, { dateStyle: 'medium' })}</dd>
          </div>
          <div>
            <dt>{t('positions.detail.updatedAt')}</dt>
            <dd>{formatDate(position.updatedAtUtc, { dateStyle: 'medium' })}</dd>
          </div>
        </dl>
        <PositionDescription html={position.description} />
      </div>
      <div className="panel position-section">
        <h2>{t('positions.requirements.title')}</h2>
        <SearchCriteriaSummary filters={position.requirements} />
      </div>
      <div className="panel position-section">
        <h2>{t('positions.matches.title')}</h2>
        {canReadCandidates ? (
          <SearchResults
            results={results}
            loading={loading}
            failed={failed}
            lastPage={Math.max(1, Math.ceil(results.totalCount / results.pageSize))}
            onPageChange={(page) => search(page)}
          />
        ) : (
          <p>{t('positions.matches.forbidden')}</p>
        )}
      </div>
    </section>
  );
}

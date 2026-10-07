import { useCallback, useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { usePermission, useSearchPresets, useServices } from '../../core/di/services-context';
import { formatNumber } from '../../core/i18n/format';
import type { CandidateListQuery } from '../candidates/models/candidate.models';
import { CandidateSummary, CountTile } from './components/candidate-summary';
import { DashboardActions } from './components/dashboard-actions';
import { OpenPositionsPanel } from './components/open-positions-panel';
import { RecentCandidatesPanel } from './components/recent-candidates-panel';
import { SavedSearchesPanel } from './components/saved-searches-panel';
import {
  CANDIDATE_QUERIES,
  DASHBOARD_HREFS,
  OPEN_POSITIONS_QUERY,
  availabilitySplit,
  inactiveCount,
} from './dashboard.logic';
import { combinePanels, usePanelData, type PanelState } from './use-panel-data';
import './dashboard.css';

/**
 * «Inicio» (KTL-40): candidates and their availability, open positions and their stages, recent
 * candidates and shared saved searches, each figure leading to where the user acts on it.
 *
 * Every permission is read here once and passed down. A panel the actor may not see is not
 * rendered and sends no request; that is courtesy, not the control - the API refuses anyway.
 * At most seven requests, in parallel: five candidate searches, one position list, one preset
 * list (design D1).
 */
export function DashboardPage() {
  const { candidateService, positionService, searchPresetsService } = useServices();
  const { t } = useTranslation();
  const canReadCandidates = usePermission('candidates.read');
  const canSeeRemoved = usePermission('candidates.delete');
  const canCreateCandidate = usePermission('candidates.create');
  const canImport = usePermission('candidates.import');
  const canReadPositions = usePermission('positions.read');
  const canManagePositions = usePermission('positions.manage');
  const canManagePresets = usePermission('presets.manage');

  const search = useCallback(
    (query: CandidateListQuery) => (signal: AbortSignal) =>
      candidateService.listPage(query, signal),
    [candidateService],
  );
  const count = useCallback(
    (query: CandidateListQuery) => (signal: AbortSignal) =>
      candidateService.listPage(query, signal).then((page) => page.totalCount),
    [candidateService],
  );
  const loaders = useMemo(
    () => ({
      recentAvailable: search(CANDIDATE_QUERIES.recentAvailable),
      recentAdded: search(CANDIDATE_QUERIES.recentAdded),
      unavailable: count(CANDIDATE_QUERIES.unavailableCount),
      withoutCv: count(CANDIDATE_QUERIES.withoutCvCount),
      everyone: count(CANDIDATE_QUERIES.everyoneCount),
      positions: (signal: AbortSignal) => positionService.search(OPEN_POSITIONS_QUERY, signal),
    }),
    [count, positionService, search],
  );

  const recentAvailable = usePanelData(loaders.recentAvailable, canReadCandidates);
  const recentAdded = usePanelData(loaders.recentAdded, canReadCandidates);
  const unavailable = usePanelData(loaders.unavailable, canReadCandidates);
  const withoutCv = usePanelData(loaders.withoutCv, canReadCandidates);
  const everyone = usePanelData(loaders.everyone, canReadCandidates && canSeeRemoved);
  const positions = usePanelData(loaders.positions, canReadPositions);
  const presets = useSearchPresets();

  useEffect(() => {
    if (!canReadCandidates) return;
    // The panel shows the failure itself, from the presets state.
    searchPresetsService.load().catch(() => undefined);
  }, [canReadCandidates, searchPresetsService]);

  const split = useMemo<PanelState<ReturnType<typeof availabilitySplit>>>(() => {
    const parts = combinePanels(recentAdded, recentAvailable, unavailable);
    if (parts.status !== 'ready') return parts;
    const [added, available, unavailableCount] = parts.data;
    return {
      status: 'ready',
      data: availabilitySplit(added.totalCount, available.totalCount, unavailableCount),
    };
  }, [recentAdded, recentAvailable, unavailable]);

  const inactive = useMemo<PanelState<number>>(() => {
    const parts = combinePanels(everyone, recentAdded);
    if (parts.status !== 'ready') return parts;
    return { status: 'ready', data: inactiveCount(parts.data[0], parts.data[1].totalCount) };
  }, [everyone, recentAdded]);

  const openPositions = useMemo<PanelState<number>>(
    () =>
      positions.status === 'ready'
        ? { status: 'ready', data: positions.data.totalCount }
        : positions,
    [positions],
  );

  return (
    <section className="page dashboard">
      <div className="toolbar">
        <div className="page-header">
          <h1>{t('dashboard.title')}</h1>
          <p className="muted">{t('dashboard.subtitle')}</p>
        </div>
        <DashboardActions
          canCreateCandidate={canCreateCandidate}
          canManagePositions={canManagePositions}
          canImport={canImport}
        />
      </div>

      {!canReadCandidates && !canReadPositions ? (
        <p className="empty-state" data-testid="dashboard-no-access">
          {t('dashboard.noAccess')}
        </p>
      ) : null}

      {canReadCandidates || canReadPositions ? (
        <div className="dashboard-summary">
          {canReadCandidates ? (
            <CandidateSummary split={split} inactive={canSeeRemoved ? inactive : undefined} />
          ) : null}
          {canReadPositions ? (
            <CountTile
              title={t('dashboard.kpi.openPositions')}
              testId="kpi-open-positions"
              href={DASHBOARD_HREFS.positions}
              count={openPositions}
            />
          ) : null}
          {canReadCandidates ? (
            <CountTile
              title={t('dashboard.kpi.withoutCv')}
              testId="kpi-without-cv"
              href={DASHBOARD_HREFS.withoutCv}
              count={withoutCv}
            />
          ) : null}
        </div>
      ) : null}

      {canReadPositions ? (
        <OpenPositionsPanel positions={positions} canManagePositions={canManagePositions} />
      ) : null}

      {canReadCandidates ? (
        <>
          <div className="grid two">
            <RecentCandidatesPanel
              title={t('dashboard.recentAvailable.title')}
              testId="dashboard-recent-available"
              emptyText={t('dashboard.recentAvailable.empty')}
              candidates={recentAvailable}
              viewAll={(page) => (
                <Link to={DASHBOARD_HREFS.available} data-testid="dashboard-recent-available-all">
                  {t('dashboard.viewAllCount', { value: formatNumber(page.totalCount) })}
                </Link>
              )}
            />
            <RecentCandidatesPanel
              title={t('dashboard.recentAdded.title')}
              testId="dashboard-recent-added"
              emptyText={t('dashboard.recentAdded.empty')}
              candidates={recentAdded}
              flagMissingCv
              viewAll={() => (
                <Link to={DASHBOARD_HREFS.recentAdded} data-testid="dashboard-recent-added-all">
                  {t('dashboard.viewAll')}
                </Link>
              )}
            />
          </div>
          <div className="grid two">
            <SavedSearchesPanel presets={presets} canManagePresets={canManagePresets} />
          </div>
        </>
      ) : null}
    </section>
  );
}

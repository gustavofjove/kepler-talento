import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { StatusChip } from '../../../shared/components/status-chip';
import { AvailabilityCell } from '../../candidates/components/availability-cell';
import type { CandidateListPage } from '../../candidates/models/candidate.models';
import { candidateHref } from '../dashboard.logic';
import type { PanelState } from '../use-panel-data';
import { DashboardPanel } from './dashboard-panel';

interface RecentCandidatesPanelProps {
  title: string;
  testId: string;
  emptyText: string;
  candidates: PanelState<CandidateListPage>;
  /** «Ver todos», given the page so it can show the total. */
  viewAll: (page: CandidateListPage) => ReactNode;
  /** «Últimos añadidos» also flags candidates without a primary CV. */
  flagMissingCv?: boolean;
}

/**
 * «Últimos disponibles» and «Últimos añadidos» (KTL-40). A row shows the candidate's name and
 * availability only - no e-mail, phone or CV action; the detail is one click away.
 */
export function RecentCandidatesPanel({
  title,
  testId,
  emptyText,
  candidates,
  viewAll,
  flagMissingCv = false,
}: RecentCandidatesPanelProps) {
  const { t } = useTranslation();
  const state =
    candidates.status !== 'ready'
      ? candidates.status
      : candidates.data.items.length === 0
        ? 'empty'
        : 'ready';

  return (
    <DashboardPanel
      title={title}
      testId={testId}
      state={state}
      emptyText={emptyText}
      action={candidates.status === 'ready' ? viewAll(candidates.data) : null}
    >
      {candidates.status === 'ready' ? (
        <ul className="dashboard-list">
          {candidates.data.items.map((item) => (
            <li
              key={item.candidateId}
              className="dashboard-list__row"
              data-testid={`${testId}-row`}
            >
              <Link className="dashboard-list__name" to={candidateHref(item.candidateId)}>
                {`${item.firstName} ${item.lastName}`.trim()}
              </Link>
              <span className="dashboard-list__meta">
                <AvailabilityCell
                  state={item.availabilityState}
                  checkedOn={item.availabilityCheckedOn}
                  testIdPrefix={`${testId}-availability`}
                />
                {flagMissingCv && !item.hasPrimaryCv ? (
                  <StatusChip tone="neutral" testId={`${testId}-no-cv`}>
                    {t('dashboard.noCv')}
                  </StatusChip>
                ) : null}
              </span>
            </li>
          ))}
        </ul>
      ) : null}
    </DashboardPanel>
  );
}

import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { formatNumber } from '../../../core/i18n/format';
import { useRowLink } from '../../../shared/components/row-link';
import '../../../shared/components/data-table.css';
import '../../../shared/components/row-link.css';
import { POSITION_CANDIDATE_STAGES, type PositionPage } from '../../positions/position.models';
import { DASHBOARD_HREFS, positionHref } from '../dashboard.logic';
import type { PanelState } from '../use-panel-data';
import { DashboardPanel } from './dashboard-panel';

interface OpenPositionsPanelProps {
  positions: PanelState<PositionPage>;
  canManagePositions: boolean;
}

/**
 * The most recently updated open positions with how many candidates sit at each stage (KTL-40).
 * The counts name no one, so `positions.read` alone shows them. As in the position list, the
 * whole row opens the position and the title is its keyboard link.
 */
export function OpenPositionsPanel({ positions, canManagePositions }: OpenPositionsPanelProps) {
  const { t } = useTranslation();
  const rowLink = useRowLink();
  const state =
    positions.status !== 'ready'
      ? positions.status
      : positions.data.items.length === 0
        ? 'empty'
        : 'ready';

  return (
    <DashboardPanel
      title={t('dashboard.positions.title')}
      testId="dashboard-positions"
      state={state}
      emptyText={t('dashboard.positions.empty')}
      emptyAction={
        canManagePositions ? (
          <Link className="button secondary" to="/app/positions/new">
            {t('positions.actions.create')}
          </Link>
        ) : null
      }
      action={
        positions.status === 'ready' ? (
          <Link to={DASHBOARD_HREFS.positions} data-testid="dashboard-positions-all">
            {t('dashboard.viewAllPositions', {
              value: formatNumber(positions.data.totalCount),
            })}
          </Link>
        ) : null
      }
    >
      {positions.status === 'ready' ? (
        <div className="table-wrap">
          <table className="data-table dashboard-positions-table">
            <thead>
              <tr>
                <th scope="col">{t('dashboard.positions.position')}</th>
                {POSITION_CANDIDATE_STAGES.map((stage) => (
                  <th key={stage} scope="col" className="numeric">
                    {t(`positions.stage.${stage}`)}
                  </th>
                ))}
                <th scope="col" className="numeric">
                  {t('positions.list.candidates')}
                </th>
              </tr>
            </thead>
            <tbody>
              {positions.data.items.map((item) => (
                <tr
                  key={item.id}
                  className="row-link-row"
                  data-testid="dashboard-position-row"
                  onClick={rowLink(positionHref(item.id))}
                  onAuxClick={rowLink(positionHref(item.id))}
                >
                  <td>
                    <Link className="row-link" to={positionHref(item.id)}>
                      {item.title}
                    </Link>
                  </td>
                  {POSITION_CANDIDATE_STAGES.map((stage) => (
                    <td key={stage} className="numeric" data-testid={`stage-count-${stage}`}>
                      {formatNumber(item.stageCounts?.[stage] ?? 0)}
                    </td>
                  ))}
                  <td className="numeric" data-testid="stage-count-total">
                    {formatNumber(item.candidateCount)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
    </DashboardPanel>
  );
}

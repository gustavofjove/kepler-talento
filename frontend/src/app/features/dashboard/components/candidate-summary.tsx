import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { formatNumber } from '../../../core/i18n/format';
import { DASHBOARD_HREFS, barSegments, type AvailabilitySplit } from '../dashboard.logic';
import type { PanelState } from '../use-panel-data';
import { DashboardPanel } from './dashboard-panel';

interface CandidateSummaryProps {
  split: PanelState<AvailabilitySplit>;
  /** Absent for an actor without `candidates.delete`, who is shown no inactive figure. */
  inactive?: PanelState<number>;
}

/**
 * The «Candidatos» card (KTL-40): the active total, the availability split and its bar. Every
 * figure is text and every split figure a link; the bar only repeats them, so it is hidden from
 * assistive technology and never carries meaning by colour alone.
 */
export function CandidateSummary({ split, inactive }: CandidateSummaryProps) {
  const { t } = useTranslation();
  const figure = (key: string, count: number) => t(key, { count, value: formatNumber(count) });

  return (
    <DashboardPanel
      title={t('dashboard.candidates.title')}
      testId="dashboard-candidates"
      className="dashboard-candidates"
      state={split.status === 'ready' ? 'ready' : split.status}
      emptyText=""
    >
      {split.status === 'ready' ? (
        <div className="stack">
          <Link className="dashboard-figure" to={DASHBOARD_HREFS.active} data-testid="kpi-active">
            {figure('dashboard.candidates.active', split.data.total)}
          </Link>
          {split.data.total > 0 ? (
            <div className="availability-bar" aria-hidden="true" data-testid="availability-bar">
              {barSegments(split.data).map((segment) => (
                <span
                  key={segment.tone}
                  className={`availability-bar__segment availability-bar__segment--${segment.tone}`}
                  style={{ flexGrow: segment.share }}
                />
              ))}
            </div>
          ) : null}
          <ul className="dashboard-split">
            <li>
              <Link to={DASHBOARD_HREFS.available} data-testid="kpi-available">
                {figure('dashboard.candidates.available', split.data.available)}
              </Link>
            </li>
            <li>
              <Link to={DASHBOARD_HREFS.unavailable} data-testid="kpi-unavailable">
                {figure('dashboard.candidates.unavailable', split.data.unavailable)}
              </Link>
            </li>
            <li>
              <Link to={DASHBOARD_HREFS.unknown} data-testid="kpi-unknown">
                {figure('dashboard.candidates.unknown', split.data.unknown)}
              </Link>
            </li>
            {inactive?.status === 'ready' ? (
              // Text, not a link: the list's «inactive» view shows active and removed together.
              <li className="muted" data-testid="kpi-inactive">
                {figure('dashboard.candidates.inactive', inactive.data)}
              </li>
            ) : null}
          </ul>
        </div>
      ) : null}
    </DashboardPanel>
  );
}

interface CountTileProps {
  title: string;
  testId: string;
  href: string;
  count: PanelState<number>;
}

/** A single linked figure: «Posiciones abiertas» or «Sin CV principal». */
export function CountTile({ title, testId, href, count }: CountTileProps) {
  const { t } = useTranslation();
  return (
    <DashboardPanel
      title={title}
      testId={`${testId}-tile`}
      className="dashboard-tile"
      tile
      state={count.status === 'ready' ? 'ready' : count.status}
      emptyText=""
    >
      {count.status === 'ready' ? (
        <Link
          className="kpi-value dashboard-tile__value"
          to={href}
          aria-label={t('dashboard.tileLink', { title, value: formatNumber(count.data) })}
          data-testid={testId}
        >
          {formatNumber(count.data)}
        </Link>
      ) : null}
    </DashboardPanel>
  );
}

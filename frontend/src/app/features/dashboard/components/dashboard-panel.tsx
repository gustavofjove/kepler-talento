import { useId, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

interface DashboardPanelProps {
  title: string;
  testId: string;
  /** Loading, failed, empty or ready - the panel's own state, never the page's. */
  state: 'loading' | 'failed' | 'empty' | 'ready';
  emptyText: string;
  /** Shown beside the title once the panel has loaded, e.g. «Ver todos». */
  action?: ReactNode;
  /** Offered in the empty state, e.g. «Nueva posición» for managers. */
  emptyAction?: ReactNode;
  className?: string;
  /**
   * A single figure (a tile) is an `article`, not a region landmark: the «Posiciones abiertas»
   * tile and panel share a title, and two landmarks with one name could not be told apart.
   */
  tile?: boolean;
  children?: ReactNode;
}

/**
 * The shared shell of a home page panel (KTL-40): a labelled region with its heading, and its own
 * loading, failure and empty states, so one panel's failure never blanks another.
 */
export function DashboardPanel({
  title,
  testId,
  state,
  emptyText,
  action,
  emptyAction,
  className,
  tile = false,
  children,
}: DashboardPanelProps) {
  const { t } = useTranslation();
  const headingId = useId();
  const Element = tile ? 'article' : 'section';
  return (
    <Element
      className={`panel dashboard-panel${className ? ` ${className}` : ''}`}
      aria-labelledby={headingId}
      aria-busy={state === 'loading'}
      data-testid={testId}
    >
      {/* A div, not <header>: `.shell header` styles the app bar (navy, sticky). */}
      <div className="dashboard-panel__header">
        <h2 id={headingId}>{title}</h2>
        {state === 'ready' || state === 'empty' ? action : null}
      </div>
      {state === 'loading' ? (
        <p className="muted" role="status">
          {t('dashboard.state.loading')}
        </p>
      ) : null}
      {state === 'failed' ? (
        <p className="empty-state" role="alert" data-testid={`${testId}-error`}>
          {t('dashboard.state.failed')}
        </p>
      ) : null}
      {state === 'empty' ? (
        <div className="dashboard-panel__empty">
          <p className="empty-state" data-testid={`${testId}-empty`}>
            {emptyText}
          </p>
          {emptyAction}
        </div>
      ) : null}
      {state === 'ready' ? children : null}
    </Element>
  );
}

import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

interface DashboardActionsProps {
  canCreateCandidate: boolean;
  canManagePositions: boolean;
  canImport: boolean;
}

/**
 * The create actions, each shown only with its own permission (KTL-40). Hiding one is a
 * convenience: the route guards and the API still refuse an actor without it. Nothing here
 * repeats the primary navigation.
 */
export function DashboardActions({
  canCreateCandidate,
  canManagePositions,
  canImport,
}: DashboardActionsProps) {
  const { t } = useTranslation();
  if (!canCreateCandidate && !canManagePositions && !canImport) return null;
  return (
    <div className="dashboard-actions" data-testid="dashboard-actions">
      {canCreateCandidate ? (
        <Link className="button" to="/app/candidates/new" data-testid="dashboard-new-candidate">
          {t('candidate.new')}
        </Link>
      ) : null}
      {canManagePositions ? (
        <Link
          className="button secondary"
          to="/app/positions/new"
          data-testid="dashboard-new-position"
        >
          {t('positions.actions.create')}
        </Link>
      ) : null}
      {canImport ? (
        <Link className="button secondary" to="/app/admin/import" data-testid="dashboard-import">
          {t('dashboard.actions.import')}
        </Link>
      ) : null}
    </div>
  );
}

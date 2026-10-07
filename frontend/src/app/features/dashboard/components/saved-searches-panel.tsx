import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { formatElapsed, localDay } from '../../../core/i18n/format';
import type { PresetsState } from '../../search/services/search-presets.service';
import { presetSearchHref } from '../../search/pages/advanced-search.logic';
import { DASHBOARD_HREFS, topPresets } from '../dashboard.logic';
import { DashboardPanel } from './dashboard-panel';

interface SavedSearchesPanelProps {
  presets: PresetsState;
  canManagePresets: boolean;
}

/**
 * The shared presets most recently used (KTL-40). Each opens the advanced search with the preset
 * applied; only its id enters the address, never its filters.
 */
export function SavedSearchesPanel({ presets, canManagePresets }: SavedSearchesPanelProps) {
  const { t } = useTranslation();
  const shown = useMemo(() => topPresets(presets.presets), [presets.presets]);
  const state =
    presets.status === 'failed'
      ? 'failed'
      : presets.status !== 'loaded'
        ? 'loading'
        : shown.length === 0
          ? 'empty'
          : 'ready';

  return (
    <DashboardPanel
      title={t('dashboard.savedSearches.title')}
      testId="dashboard-saved-searches"
      state={state}
      emptyText={t('dashboard.savedSearches.empty')}
      action={
        canManagePresets ? (
          <Link to={DASHBOARD_HREFS.presets} data-testid="dashboard-manage-presets">
            {t('dashboard.savedSearches.manage')}
          </Link>
        ) : null
      }
    >
      <ul className="dashboard-list">
        {shown.map((preset) => (
          <li key={preset.id} className="dashboard-list__row" data-testid="dashboard-preset-row">
            <Link className="dashboard-list__name" to={presetSearchHref(preset.id)}>
              {preset.name}
            </Link>
            <span className="dashboard-list__meta muted">
              {preset.lastUsedAt
                ? t('dashboard.savedSearches.usedAgo', {
                    elapsed: formatElapsed(localDay(new Date(preset.lastUsedAt))),
                  })
                : t('dashboard.savedSearches.neverUsed')}
            </span>
          </li>
        ))}
      </ul>
    </DashboardPanel>
  );
}

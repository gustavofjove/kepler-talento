import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useParams } from 'react-router';
import { usePermission, useSearchPresets, useServices } from '../../core/di/services-context';
import { useErrorToast } from '../../core/services/use-error-toast';
import { Breadcrumb, type BreadcrumbItem } from '../../shared/components/breadcrumb';
import { SearchCriteriaForm } from '../search/components/search-criteria-form';
import {
  cloneSearchFilters,
  EMPTY_SEARCH_FILTERS,
  type SearchFilters,
} from '../search/models/search.models';
import { PositionDescriptionEditor } from './components/position-description-editor';
import type { PositionStatus } from './position.models';
import './positions.css';

export function PositionFormPage() {
  const { id } = useParams();
  const editing = Boolean(id);
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { positionService, searchPresetsService } = useServices();
  const notifyError = useErrorToast();
  // Applying a preset is part of searching (the same rule as the search page); creating one
  // is preset management. The two are checked independently.
  const canApplyPresets = usePermission('candidates.read');
  const canManagePresets = usePermission('presets.manage');
  const presets = useSearchPresets();
  const canReadPositions = usePermission('positions.read');
  const [title, setTitle] = useState('');
  // The saved title, for the breadcrumb: `title` is the draft and changes as the user types.
  const [storedTitle, setStoredTitle] = useState('');
  const [description, setDescription] = useState('');
  const [location, setLocation] = useState('');
  const [status, setStatus] = useState<PositionStatus>('open');
  const [requirements, setRequirements] = useState<SearchFilters>(() =>
    cloneSearchFilters(EMPTY_SEARCH_FILTERS),
  );
  const [version, setVersion] = useState(0);
  const [ready, setReady] = useState(!editing);
  const [saving, setSaving] = useState(false);
  const [presetId, setPresetId] = useState('');
  const [presetName, setPresetName] = useState('');
  useEffect(() => {
    if (canApplyPresets && presets.status === 'idle')
      void searchPresetsService
        .load()
        .catch((error) => notifyError(error, t('positions.presets.loadError')));
  }, [canApplyPresets, notifyError, presets.status, searchPresetsService, t]);
  useEffect(() => {
    if (!id) return;
    void positionService
      .get(id)
      .then((position) => {
        setTitle(position.title);
        setStoredTitle(position.title);
        setDescription(position.description);
        setLocation(position.location);
        setStatus(position.status);
        setRequirements(cloneSearchFilters(position.requirements));
        setVersion(position.version);
        setReady(true);
      })
      .catch((error) => notifyError(error, t('positions.form.loadError')));
  }, [id, notifyError, positionService, t]);
  const save = async () => {
    setSaving(true);
    try {
      const draft = { title, description, location, status, requirements };
      const saved = editing
        ? await positionService.update(id!, draft, version)
        : await positionService.create({ title, description, location, requirements });
      navigate(`/app/positions/${saved.id}`);
    } catch (error) {
      notifyError(error, t('positions.form.saveError'));
    } finally {
      setSaving(false);
    }
  };
  const trail: BreadcrumbItem[] = [
    {
      label: t('breadcrumb.positions'),
      to: canReadPositions ? '/app/positions' : undefined,
      testId: 'breadcrumb-positions',
    },
  ];
  if (!editing) trail.push({ label: t('breadcrumb.newPosition'), current: true });
  else if (ready)
    trail.push(
      {
        label: storedTitle,
        to: canReadPositions ? `/app/positions/${id}` : undefined,
        testId: 'breadcrumb-position',
      },
      { label: t('breadcrumb.edit'), current: true },
    );
  if (!ready)
    return (
      <>
        <Breadcrumb items={trail} />
        <p role="status">{t('positions.detail.loading')}</p>
      </>
    );
  return (
    <section className="page position-form">
      <Breadcrumb items={trail} />
      <div className="page-header">
        <h1>{t(editing ? 'positions.form.editTitle' : 'positions.form.createTitle')}</h1>
      </div>
      <div className="panel position-main-fields">
        <div className="field">
          <label htmlFor="position-title">{t('positions.form.title')}</label>
          <input
            id="position-title"
            name="title"
            data-testid="position-title"
            maxLength={200}
            required
            value={title}
            onChange={(event) => setTitle(event.target.value)}
          />
        </div>
        <div className="field">
          <label htmlFor="position-location">{t('positions.form.location')}</label>
          <input
            id="position-location"
            name="location"
            data-testid="position-location"
            maxLength={200}
            value={location}
            onChange={(event) => setLocation(event.target.value)}
          />
        </div>
        {editing ? (
          <div className="field">
            <label htmlFor="position-status">{t('positions.form.status')}</label>
            <select
              id="position-status"
              name="status"
              data-testid="position-status"
              value={status}
              onChange={(event) => setStatus(event.target.value as PositionStatus)}
            >
              <option value="open">{t('positions.status.open')}</option>
              <option value="closed">{t('positions.status.closed')}</option>
            </select>
          </div>
        ) : null}
      </div>
      <div className="panel position-section">
        <h2>{t('positions.form.description')}</h2>
        <PositionDescriptionEditor value={description} onChange={setDescription} />
      </div>
      {canApplyPresets || canManagePresets ? (
        <div className="panel position-presets">
          <h2>{t('positions.presets.title')}</h2>
          <div className="position-actions">
            {canApplyPresets ? (
              <>
                <div className="field">
                  <label htmlFor="position-preset">{t('positions.presets.select')}</label>
                  <select
                    id="position-preset"
                    name="preset"
                    data-testid="position-preset"
                    value={presetId}
                    onChange={(event) => setPresetId(event.target.value)}
                  >
                    <option value="">{t('positions.presets.none')}</option>
                    {presets.presets.map((preset) => (
                      <option key={preset.id} value={preset.id}>
                        {preset.name}
                      </option>
                    ))}
                  </select>
                </div>
                <button
                  type="button"
                  className="button secondary"
                  disabled={!presetId}
                  onClick={() =>
                    void searchPresetsService
                      .applyPreset(presetId)
                      .then((filters) => setRequirements(cloneSearchFilters(filters)))
                      .catch((error) => notifyError(error, t('positions.presets.applyError')))
                  }
                >
                  {t('positions.presets.apply')}
                </button>
              </>
            ) : null}
            {canManagePresets ? (
              <>
                <div className="field">
                  <label htmlFor="position-preset-name">{t('positions.presets.name')}</label>
                  <input
                    id="position-preset-name"
                    name="presetName"
                    value={presetName}
                    onChange={(event) => setPresetName(event.target.value)}
                  />
                </div>
                <button
                  type="button"
                  className="button secondary"
                  disabled={!presetName.trim()}
                  onClick={() =>
                    void searchPresetsService
                      .createPreset(presetName, cloneSearchFilters(requirements))
                      .then(() => setPresetName(''))
                      .catch((error) => notifyError(error, t('positions.presets.saveError')))
                  }
                >
                  {t('positions.presets.save')}
                </button>
              </>
            ) : null}
          </div>
        </div>
      ) : null}
      <div className="panel position-section">
        <SearchCriteriaForm
          filters={requirements}
          onFiltersChange={setRequirements}
          onSubmit={() => void save()}
          actions={
            <div className="position-actions">
              <button
                type="button"
                className="button secondary"
                onClick={() => navigate(editing ? `/app/positions/${id}` : '/app/positions')}
              >
                {t('positions.actions.cancel')}
              </button>
              <button type="submit" className="button primary" disabled={saving}>
                {t(saving ? 'positions.actions.saving' : 'positions.actions.save')}
              </button>
            </div>
          }
        />
      </div>
    </section>
  );
}

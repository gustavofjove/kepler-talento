import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate, useParams } from 'react-router';
import { useServices } from '../../../core/di/services-context';
import { errorText } from '../../../core/i18n/translatable-error';
import { useErrorToast } from '../../../core/services/use-error-toast';
import { SearchCriteriaForm } from '../../search/components/search-criteria-form';
import type { SearchFilters } from '../../search/models/search.models';
import { isPresetNotFound, presetErrorKey } from './preset-errors';
import { PRESETS_ROUTE } from './preset-list.logic';
import './preset-list-page.css';

/** Create (`/new`) and edit (`/:id/edit`) share one page: the same name field, the same editor. */
export function PresetEditPage() {
  const { id } = useParams();
  const isEdit = id !== undefined;
  const { searchPresetsService, toastService } = useServices();
  const notifyError = useErrorToast();
  const navigate = useNavigate();
  const { t } = useTranslation();

  const [name, setName] = useState('');
  const [filters, setFilters] = useState<SearchFilters>(() => searchPresetsService.emptyFilters());
  const [version, setVersion] = useState<number | null>(null);
  const [loading, setLoading] = useState(isEdit);
  const [loadFailed, setLoadFailed] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    if (id === undefined) {
      return;
    }
    let active = true;
    searchPresetsService
      .get(id)
      .then((preset) => {
        if (!active) {
          return;
        }
        setName(preset.name);
        setFilters(preset.filters);
        setVersion(preset.version);
        setLoading(false);
      })
      .catch((err: unknown) => {
        if (!active) {
          return;
        }
        if (isPresetNotFound(err)) {
          toastService.show(t('presets.errors.notFound'), 'warning');
          navigate(PRESETS_ROUTE, { replace: true });
          return;
        }
        setLoading(false);
        setLoadFailed(true);
        notifyError(err, t('presets.form.loadFailed'));
      });
    return () => {
      active = false;
    };
  }, [id, navigate, notifyError, searchPresetsService, t, toastService]);

  const save = async (next: SearchFilters): Promise<void> => {
    setError('');
    setSaving(true);
    try {
      let saved;
      if (id !== undefined) {
        if (version === null) {
          return;
        }
        saved = await searchPresetsService.updatePreset(id, name, next, version);
      } else {
        saved = await searchPresetsService.createPreset(name, next);
      }
      toastService.show(t('presets.form.saved', { name: saved.name }), 'success');
      // Back to the library: there is no separate read-only page, the list's criteria dialog is
      // where a preset is viewed.
      navigate(PRESETS_ROUTE);
    } catch (err) {
      // Shown in place, with the entered name and criteria kept, so a conflict costs a rename
      // or a reload rather than retyping the whole preset.
      const key = presetErrorKey(err);
      setError(key ? t(key) : errorText(err, t));
    } finally {
      setSaving(false);
    }
  };

  const describedBy = error ? 'preset-privacy-hint preset-form-error' : 'preset-privacy-hint';

  return (
    <section className="page">
      <div className="toolbar">
        <div className="page-header">
          <h1>{t(isEdit ? 'presets.form.editTitle' : 'presets.form.newTitle')}</h1>
        </div>
      </div>

      {loading ? (
        <p className="empty-state">{t('presets.form.loading')}</p>
      ) : loadFailed ? (
        <p className="empty-state" data-testid="preset-load-error">
          {t('presets.form.loadFailed')}
        </p>
      ) : (
        <div className="panel">
          <SearchCriteriaForm
            filters={filters}
            onFiltersChange={setFilters}
            onSubmit={(next) => void save(next)}
            leading={
              <div className="field span-all">
                <label htmlFor="presetName">{t('presets.form.name')}</label>
                <input
                  id="presetName"
                  name="presetName"
                  data-testid="preset-name"
                  value={name}
                  maxLength={120}
                  required
                  aria-invalid={error ? true : undefined}
                  aria-describedby={describedBy}
                  onChange={(event) => setName(event.target.value)}
                />
                <p id="preset-privacy-hint" className="muted">
                  {t('presets.form.privacyHint')}
                </p>
                {error ? (
                  <p
                    id="preset-form-error"
                    className="field-error"
                    role="alert"
                    data-testid="preset-form-error"
                  >
                    {error}
                  </p>
                ) : null}
              </div>
            }
            actions={
              <>
                <button
                  className="button"
                  type="submit"
                  data-testid="preset-save"
                  disabled={saving}
                >
                  {t('presets.form.save')}
                </button>
                <Link className="button secondary" to={PRESETS_ROUTE} data-testid="preset-cancel">
                  {t('presets.form.cancel')}
                </Link>
              </>
            }
          />
        </div>
      )}
    </section>
  );
}

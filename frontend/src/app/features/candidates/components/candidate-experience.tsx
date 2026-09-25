import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useServices } from '../../../core/di/services-context';
import { errorText } from '../../../core/i18n/translatable-error';
import { useCatalogs } from '../../catalogs/use-catalogs';
import { CatalogStatusNotice } from '../../catalogs/components/catalog-status';
import { useCatalogStatus } from '../../catalogs/components/use-catalog-status';
import type { Candidate } from '../models/candidate.models';
import {
  normalizeExperience,
  validateExperienceEntry,
} from '../services/candidate-relations.service';
import { CandidatePanel } from './candidate-panel';
import type { PanelControl } from './candidate-panel.logic';
import { usePanelDraft } from './use-panel-draft';

interface Draft {
  company: string;
  position: string;
  sector: string;
  startDate: string;
  endDate: string;
  yearsExperience: number | undefined;
  isCurrent: boolean;
}

const EMPTY: Draft = {
  company: '',
  position: '',
  sector: '',
  startDate: '',
  endDate: '',
  yearsExperience: undefined,
  isCurrent: false,
};

interface Props {
  candidate: Candidate;
  control: PanelControl;
}

/**
 * Experiencia as a candidate page panel (KTL-29): read-only rows, or a staged draft whose rows
 * are added and removed locally and written together with the panel's «Guardar».
 */
export function CandidateExperience({ candidate, control }: Props) {
  const { t } = useTranslation();
  const { candidateRelationsService, toastService } = useServices();
  const catalogs = useCatalogs();
  const [draft, setDraft] = useState<Draft>(EMPTY);
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  const entries = usePanelDraft(candidate.experience, control.editing, control.onDirtyChange);
  const experience = entries.value;
  const canEdit = control.editing;

  const catalogStatus = useCatalogStatus();
  const sectorOptions = catalogs.activeNames('sector');

  const add = (event: FormEvent<HTMLFormElement>): void => {
    event.preventDefault();
    const entry = normalizeExperience({ ...draft, id: crypto.randomUUID() });
    try {
      validateExperienceEntry(entry);
    } catch (err) {
      setError(errorText(err, t));
      return;
    }
    entries.set([...experience, entry]);
    setDraft(EMPTY);
    setError('');
  };

  const remove = (experienceId: string): void => {
    entries.set(experience.filter((item) => item.id !== experienceId));
    setError('');
  };

  const save = async (): Promise<void> => {
    setSaving(true);
    try {
      await candidateRelationsService.saveExperience(candidate.id, experience);
      setError('');
      toastService.show(t('candidate.panel.saved.experience'), 'success');
      control.onClose();
    } catch (err) {
      setError(errorText(err, t));
    } finally {
      setSaving(false);
    }
  };

  const cancel = (): void => {
    entries.reset();
    setDraft(EMPTY);
    setError('');
  };

  return (
    <CandidatePanel
      id="experience"
      title={t('candidate.profile.experience.title')}
      control={control}
      mode="form"
      onSave={() => void save()}
      onCancel={cancel}
      saving={saving}
    >
      <section className="section-block" data-testid="candidate-experience">
        {!experience.length ? (
          <p className="empty-state">{t('candidate.profile.experience.empty')}</p>
        ) : null}
        <div className="item-list">
          {experience.map((item) => (
            <div className="item-row" key={item.id}>
              <p className="item-main">
                <span className="badge">{item.position}</span> {item.company} ({item.sector})
                {item.isCurrent
                  ? ` · ${t('candidate.profile.experience.currentBadge')}`
                  : item.yearsExperience !== undefined
                    ? ` · ${t('candidate.profile.yearsCount', { years: item.yearsExperience })}`
                    : ''}
              </p>
              {canEdit ? (
                <button className="button secondary" type="button" onClick={() => remove(item.id)}>
                  {t('candidate.profile.action.remove')}
                </button>
              ) : null}
            </div>
          ))}
        </div>
        {canEdit ? (
          <form className="section-block" onSubmit={add} noValidate>
            <CatalogStatusNotice status={catalogStatus} />
            <div className="grid two">
              <div className="field">
                <label htmlFor="company">{t('candidate.profile.experience.company')}</label>
                <input
                  id="company"
                  name="company"
                  value={draft.company}
                  onChange={(e) => setDraft({ ...draft, company: e.target.value })}
                  required
                />
              </div>
              <div className="field">
                <label htmlFor="position">{t('candidate.profile.experience.position')}</label>
                <input
                  id="position"
                  name="position"
                  value={draft.position}
                  onChange={(e) => setDraft({ ...draft, position: e.target.value })}
                  required
                />
              </div>
              <div className="field">
                <label htmlFor="sector">{t('candidate.profile.experience.sector')}</label>
                <select
                  id="sector"
                  name="sector"
                  value={draft.sector}
                  onChange={(e) => setDraft({ ...draft, sector: e.target.value })}
                  required
                >
                  <option value="" disabled>
                    {t('candidate.profile.option.select')}
                  </option>
                  {sectorOptions.map((option) => (
                    <option key={option} value={option}>
                      {option}
                    </option>
                  ))}
                </select>
              </div>
              <div className="field">
                <label htmlFor="startDate">{t('candidate.profile.experience.startDate')}</label>
                <input
                  id="startDate"
                  name="startDate"
                  type="date"
                  value={draft.startDate}
                  onChange={(e) => setDraft({ ...draft, startDate: e.target.value })}
                />
              </div>
              <div className="field">
                <label htmlFor="endDate">{t('candidate.profile.experience.endDate')}</label>
                <input
                  id="endDate"
                  name="endDate"
                  type="date"
                  value={draft.endDate}
                  disabled={draft.isCurrent}
                  onChange={(e) => setDraft({ ...draft, endDate: e.target.value })}
                />
              </div>
              <div className="field">
                <label htmlFor="experience-years">
                  {t('candidate.profile.experience.yearsExperience')}
                </label>
                <input
                  id="experience-years"
                  name="yearsExperience"
                  type="number"
                  min="0"
                  value={draft.yearsExperience ?? ''}
                  onChange={(e) =>
                    setDraft({
                      ...draft,
                      yearsExperience: e.target.value === '' ? undefined : Number(e.target.value),
                    })
                  }
                />
              </div>
              <div className="field">
                <label className="inline-check">
                  <input
                    name="isCurrent"
                    type="checkbox"
                    checked={draft.isCurrent}
                    onChange={(e) => setDraft({ ...draft, isCurrent: e.target.checked })}
                  />
                  {t('candidate.profile.experience.current')}
                </label>
              </div>
            </div>
            {error ? <p className="empty-state">{error}</p> : null}
            <div className="form-actions">
              <button className="button" type="submit" disabled={!!catalogStatus.message}>
                {t('candidate.profile.experience.add')}
              </button>
            </div>
          </form>
        ) : null}
      </section>
    </CandidatePanel>
  );
}

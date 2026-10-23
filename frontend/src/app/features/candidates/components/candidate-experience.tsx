import { useRef, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useServices } from '../../../core/di/services-context';
import { errorText } from '../../../core/i18n/translatable-error';
import { FormError } from '../../../shared/components/form-error';
import { randomId } from '../../../shared/random-id';
import { useCatalogs } from '../../catalogs/use-catalogs';
import { CatalogStatusNotice } from '../../catalogs/components/catalog-status';
import { useCatalogStatus } from '../../catalogs/components/use-catalog-status';
import type { Candidate, CandidateExperience as ExperienceEntry } from '../models/candidate.models';
import {
  normalizeExperience,
  validateExperienceEntry,
} from '../services/candidate-relations.service';
import { applyEntryDraft, optionsWithCurrent, replaceEntry } from './candidate-entry-form.logic';
import { CandidatePanel } from './candidate-panel';
import type { PanelControl } from './candidate-panel.logic';
import { experiencePeriod } from './candidate-period.logic';
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

const toDraft = (entry: ExperienceEntry): Draft => ({
  company: entry.company,
  position: entry.position,
  sector: entry.sector,
  startDate: entry.startDate ?? '',
  endDate: entry.endDate ?? '',
  yearsExperience: entry.yearsExperience,
  isCurrent: entry.isCurrent,
});

interface Props {
  candidate: Candidate;
  control: PanelControl;
}

/**
 * Experiencia as a candidate page panel (KTL-29): read-only rows, or a staged draft whose rows
 * are added, edited and removed locally and written together with the panel's «Guardar».
 * «Editar» on a row loads it into the form, which then updates that row in place.
 */
export function CandidateExperience({ candidate, control }: Props) {
  const { t } = useTranslation();
  const { candidateRelationsService, toastService } = useServices();
  const catalogs = useCatalogs();
  const [draft, setDraft] = useState<Draft>(EMPTY);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  const firstField = useRef<HTMLInputElement>(null);
  const entries = usePanelDraft(candidate.experience, control.editing, control.onDirtyChange);
  const experience = entries.value;
  const canEdit = control.editing;
  const editing = canEdit ? experience.find((item) => item.id === editingId) : undefined;
  // The years column appears once any row has a year, and then on every row, to keep alignment.
  const showPeriods = experience.some((item) => experiencePeriod(item, t) !== undefined);

  const catalogStatus = useCatalogStatus();
  const sectorOptions = optionsWithCurrent(catalogs.activeNames('sector'), draft.sector);

  const clearForm = (): void => {
    setDraft(EMPTY);
    setEditingId(null);
    setError('');
  };

  const startEdit = (entry: ExperienceEntry): void => {
    setDraft(toDraft(entry));
    setEditingId(entry.id);
    setError('');
    firstField.current?.focus();
  };

  const submit = (event: FormEvent<HTMLFormElement>): void => {
    event.preventDefault();
    const entry = normalizeExperience(
      editing ? applyEntryDraft(editing, draft) : { ...draft, id: randomId() },
    );
    try {
      validateExperienceEntry(entry);
    } catch (err) {
      setError(errorText(err, t));
      return;
    }
    entries.set(editing ? replaceEntry(experience, entry) : [...experience, entry]);
    clearForm();
  };

  const remove = (experienceId: string): void => {
    entries.set(experience.filter((item) => item.id !== experienceId));
    if (experienceId === editingId) clearForm();
    setError('');
  };

  const save = async (): Promise<void> => {
    setSaving(true);
    try {
      await candidateRelationsService.saveExperience(candidate.id, experience);
      clearForm();
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
    clearForm();
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
          {experience.map((item) => {
            const period = experiencePeriod(item, t);
            return (
              <div
                className={item.id === editing?.id ? 'item-row is-editing' : 'item-row'}
                key={item.id}
              >
                <p className="item-main">
                  {showPeriods ? (
                    <span className="item-period" data-testid="experience-period">
                      {period}
                    </span>
                  ) : null}
                  <span className="badge">{item.position}</span> {item.company} ({item.sector})
                  {/* A current role with a period already reads «2019–actualidad». */}
                  {item.isCurrent
                    ? period
                      ? ''
                      : ` · ${t('candidate.profile.experience.currentBadge')}`
                    : item.yearsExperience !== undefined
                      ? ` · ${t('candidate.profile.yearsCount', { years: item.yearsExperience })}`
                      : ''}
                </p>
                {canEdit ? (
                  <div className="item-actions">
                    <button
                      className="button secondary"
                      type="button"
                      data-testid="experience-edit"
                      aria-label={t('candidate.profile.experience.editLabel', {
                        position: item.position,
                        company: item.company,
                      })}
                      onClick={() => startEdit(item)}
                    >
                      {t('candidate.profile.action.edit')}
                    </button>
                    <button
                      className="button secondary"
                      type="button"
                      onClick={() => remove(item.id)}
                    >
                      {t('candidate.profile.action.remove')}
                    </button>
                  </div>
                ) : null}
              </div>
            );
          })}
        </div>
        {canEdit ? (
          <form
            className="section-block candidate-add-form"
            aria-labelledby="experience-add-title"
            data-testid="experience-add-form"
            onSubmit={submit}
            noValidate
          >
            <h3 id="experience-add-title">
              {editing
                ? t('candidate.profile.experience.editTitle')
                : t('candidate.profile.experience.newTitle')}
            </h3>
            <FormError message={error} />
            <CatalogStatusNotice status={catalogStatus} />
            <div className="grid two">
              <div className="field">
                <label htmlFor="company">{t('candidate.profile.experience.company')}</label>
                <input
                  ref={firstField}
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
              {/* Above the dates it governs: a current role has no end date. */}
              <div className="field span-all">
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
            </div>
            <div className="form-actions">
              <button className="button" type="submit" disabled={!!catalogStatus.message}>
                {editing
                  ? t('candidate.profile.experience.update')
                  : t('candidate.profile.experience.add')}
              </button>
              {editing ? (
                <button className="button secondary" type="button" onClick={clearForm}>
                  {t('candidate.profile.action.cancelEdit')}
                </button>
              ) : null}
            </div>
          </form>
        ) : null}
      </section>
    </CandidatePanel>
  );
}

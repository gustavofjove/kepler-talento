import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useServices } from '../../../core/di/services-context';
import { errorText } from '../../../core/i18n/translatable-error';
import { useCatalogs } from '../../catalogs/use-catalogs';
import { CatalogStatusNotice } from '../../catalogs/components/catalog-status';
import { useCatalogStatus } from '../../catalogs/components/use-catalog-status';
import type { Candidate } from '../models/candidate.models';
import { validateEducationEntry } from '../services/candidate-relations.service';
import { CandidatePanel } from './candidate-panel';
import type { PanelControl } from './candidate-panel.logic';
import { usePanelDraft } from './use-panel-draft';

interface Draft {
  educationType: string;
  degree: string;
  specialty: string;
  institution: string;
  endYear: number | undefined;
  status: string;
}

const EMPTY: Draft = {
  educationType: '',
  degree: '',
  specialty: '',
  institution: '',
  endYear: undefined,
  status: '',
};

interface Props {
  candidate: Candidate;
  control: PanelControl;
}

/**
 * Educación as a candidate page panel (KTL-29): read-only rows, or a staged draft whose rows
 * are added and removed locally and written together with the panel's «Guardar».
 */
export function CandidateEducation({ candidate, control }: Props) {
  const { t } = useTranslation();
  const { candidateRelationsService, toastService } = useServices();
  const catalogs = useCatalogs();
  const [draft, setDraft] = useState<Draft>(EMPTY);
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  const entries = usePanelDraft(candidate.education, control.editing, control.onDirtyChange);
  const education = entries.value;
  const canEdit = control.editing;

  const catalogStatus = useCatalogStatus();
  const typeOptions = catalogs.activeNames('education_type');
  const statusOptions = catalogs.activeNames('education_status');

  const add = (event: FormEvent<HTMLFormElement>): void => {
    event.preventDefault();
    const entry = { ...draft, id: crypto.randomUUID() };
    try {
      validateEducationEntry(entry);
    } catch (err) {
      setError(errorText(err, t));
      return;
    }
    entries.set([...education, entry]);
    setDraft(EMPTY);
    setError('');
  };

  const remove = (educationId: string): void => {
    entries.set(education.filter((item) => item.id !== educationId));
    setError('');
  };

  const save = async (): Promise<void> => {
    setSaving(true);
    try {
      await candidateRelationsService.saveEducation(candidate.id, education);
      setError('');
      toastService.show(t('candidate.panel.saved.education'), 'success');
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
      id="education"
      title={t('candidate.profile.education.title')}
      control={control}
      mode="form"
      onSave={() => void save()}
      onCancel={cancel}
      saving={saving}
    >
      <section className="section-block" data-testid="candidate-education">
        {!education.length ? (
          <p className="empty-state">{t('candidate.profile.education.empty')}</p>
        ) : null}
        <div className="item-list">
          {education.map((item) => (
            <div className="item-row" key={item.id}>
              <p className="item-main">
                <span className="badge">{item.degree}</span> {item.institution} ({item.status})
                {item.endYear ? ` · ${item.endYear}` : ''}
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
                <label htmlFor="educationType">{t('candidate.profile.education.type')}</label>
                <select
                  id="educationType"
                  name="educationType"
                  value={draft.educationType}
                  onChange={(e) => setDraft({ ...draft, educationType: e.target.value })}
                  required
                >
                  <option value="" disabled>
                    {t('candidate.profile.option.select')}
                  </option>
                  {typeOptions.map((option) => (
                    <option key={option} value={option}>
                      {option}
                    </option>
                  ))}
                </select>
              </div>
              <div className="field">
                <label htmlFor="degree">{t('candidate.profile.education.degree')}</label>
                <input
                  id="degree"
                  name="degree"
                  value={draft.degree}
                  onChange={(e) => setDraft({ ...draft, degree: e.target.value })}
                  required
                />
              </div>
              <div className="field">
                <label htmlFor="specialty">{t('candidate.profile.education.specialty')}</label>
                <input
                  id="specialty"
                  name="specialty"
                  value={draft.specialty}
                  onChange={(e) => setDraft({ ...draft, specialty: e.target.value })}
                />
              </div>
              <div className="field">
                <label htmlFor="institution">{t('candidate.profile.education.institution')}</label>
                <input
                  id="institution"
                  name="institution"
                  value={draft.institution}
                  onChange={(e) => setDraft({ ...draft, institution: e.target.value })}
                />
              </div>
              <div className="field">
                <label htmlFor="endYear">{t('candidate.profile.education.endYear')}</label>
                <input
                  id="endYear"
                  name="endYear"
                  type="number"
                  value={draft.endYear ?? ''}
                  onChange={(e) =>
                    setDraft({
                      ...draft,
                      endYear: e.target.value === '' ? undefined : Number(e.target.value),
                    })
                  }
                />
              </div>
              <div className="field">
                <label htmlFor="education-status">{t('candidate.profile.education.status')}</label>
                <select
                  id="education-status"
                  name="status"
                  value={draft.status}
                  onChange={(e) => setDraft({ ...draft, status: e.target.value })}
                  required
                >
                  <option value="" disabled>
                    {t('candidate.profile.option.select')}
                  </option>
                  {statusOptions.map((option) => (
                    <option key={option} value={option}>
                      {option}
                    </option>
                  ))}
                </select>
              </div>
            </div>
            {error ? <p className="empty-state">{error}</p> : null}
            <div className="form-actions">
              <button className="button" type="submit" disabled={!!catalogStatus.message}>
                {t('candidate.profile.education.add')}
              </button>
            </div>
          </form>
        ) : null}
      </section>
    </CandidatePanel>
  );
}

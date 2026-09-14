import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useServices } from '../../../core/di/services-context';
import { errorText } from '../../../core/i18n/translatable-error';
import { useCatalogs } from '../../catalogs/use-catalogs';
import { CatalogStatusNotice } from '../../catalogs/components/catalog-status';
import { useCatalogStatus } from '../../catalogs/components/use-catalog-status';
import type { CandidateExperience as CandidateExperienceModel } from '../models/candidate.models';

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
  candidateId: string;
  experience: CandidateExperienceModel[];
  canEdit: boolean;
}

export function CandidateExperience({ candidateId, experience, canEdit }: Props) {
  const { t } = useTranslation();
  const { candidateRelationsService } = useServices();
  const catalogs = useCatalogs();
  const [draft, setDraft] = useState<Draft>(EMPTY);
  const [error, setError] = useState('');

  const catalogStatus = useCatalogStatus();
  const sectorOptions = catalogs.activeNames('sector');

  // See candidate-languages: these persist through the API and must be awaited.
  const add = async (event: FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();
    try {
      await candidateRelationsService.addExperience(candidateId, { ...draft });
      setDraft(EMPTY);
      setError('');
    } catch (err) {
      setError(errorText(err, t));
    }
  };

  const remove = async (experienceId: string): Promise<void> => {
    try {
      await candidateRelationsService.removeExperience(candidateId, experienceId);
      setError('');
    } catch (err) {
      setError(errorText(err, t));
    }
  };

  return (
    <section className="section-block" data-testid="candidate-experience">
      <h3 className="section-title">{t('candidate.profile.experience.title')}</h3>
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
              <button
                className="button secondary"
                type="button"
                onClick={() => void remove(item.id)}
              >
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
  );
}

import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useServices } from '../../../core/di/services-context';
import { errorText } from '../../../core/i18n/translatable-error';
import { useCatalogs } from '../../catalogs/use-catalogs';
import { CatalogStatusNotice } from '../../catalogs/components/catalog-status';
import { useCatalogStatus } from '../../catalogs/components/use-catalog-status';
import type { CandidateProgram } from '../models/candidate.models';

interface Draft {
  program: string;
  level: string;
  yearsExperience: number | undefined;
}

const EMPTY: Draft = { program: '', level: '', yearsExperience: undefined };

interface Props {
  candidateId: string;
  programs: CandidateProgram[];
  canEdit: boolean;
}

export function CandidatePrograms({ candidateId, programs, canEdit }: Props) {
  const { t } = useTranslation();
  const { candidateRelationsService } = useServices();
  const catalogs = useCatalogs();
  const [draft, setDraft] = useState<Draft>(EMPTY);
  const [error, setError] = useState('');

  const catalogStatus = useCatalogStatus();
  const programOptions = catalogs.activeNames('program');
  const levelOptions = catalogs.activeNames('program_level');

  // See candidate-languages: these persist through the API and must be awaited.
  const add = async (event: FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();
    try {
      await candidateRelationsService.addProgram(candidateId, { ...draft });
      setDraft(EMPTY);
      setError('');
    } catch (err) {
      setError(errorText(err, t));
    }
  };

  const remove = async (programId: string): Promise<void> => {
    try {
      await candidateRelationsService.removeProgram(candidateId, programId);
      setError('');
    } catch (err) {
      setError(errorText(err, t));
    }
  };

  return (
    <section className="section-block" data-testid="candidate-programs">
      <h3 className="section-title">{t('candidate.profile.programs.title')}</h3>
      {!programs.length ? (
        <p className="empty-state">{t('candidate.profile.programs.empty')}</p>
      ) : null}
      <div className="item-list">
        {programs.map((item) => (
          <div className="item-row" key={item.id}>
            <p className="item-main">
              <span className="badge">{item.program}</span> {item.level}
              {item.yearsExperience !== undefined
                ? ` (${t('candidate.profile.yearsCount', { years: item.yearsExperience })})`
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
              <label htmlFor="program">{t('candidate.profile.programs.label')}</label>
              <select
                id="program"
                name="program"
                value={draft.program}
                onChange={(e) => setDraft({ ...draft, program: e.target.value })}
                required
              >
                <option value="" disabled>
                  {t('candidate.profile.option.select')}
                </option>
                {programOptions.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="program-level">{t('candidate.profile.programs.level')}</label>
              <select
                id="program-level"
                name="level"
                value={draft.level}
                onChange={(e) => setDraft({ ...draft, level: e.target.value })}
                required
              >
                <option value="" disabled>
                  {t('candidate.profile.option.select')}
                </option>
                {levelOptions.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="program-years">
                {t('candidate.profile.programs.yearsExperience')}
              </label>
              <input
                id="program-years"
                name="yearsExperience"
                type="number"
                min="0"
                value={draft.yearsExperience ?? ''}
                // ngModel coerced to number; React hands back a string.
                onChange={(e) =>
                  setDraft({
                    ...draft,
                    yearsExperience: e.target.value === '' ? undefined : Number(e.target.value),
                  })
                }
              />
            </div>
          </div>
          {error ? <p className="empty-state">{error}</p> : null}
          <div className="form-actions">
            <button className="button" type="submit" disabled={!!catalogStatus.message}>
              {t('candidate.profile.programs.add')}
            </button>
          </div>
        </form>
      ) : null}
    </section>
  );
}

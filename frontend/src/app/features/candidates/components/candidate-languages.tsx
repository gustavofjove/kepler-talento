import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { usePermission, useServices } from '../../../core/di/services-context';
import { errorText } from '../../../core/i18n/translatable-error';
import { useCatalogs } from '../../catalogs/use-catalogs';
import { CatalogStatusNotice } from '../../catalogs/components/catalog-status';
import { useCatalogStatus } from '../../catalogs/components/use-catalog-status';
import type { CandidateLanguage } from '../models/candidate.models';

const EMPTY = { language: '', level: '', certification: '' };

interface Props {
  candidateId: string;
  languages: CandidateLanguage[];
  /** The detail page renders every section read-only, whatever the viewer may do. */
  readOnly?: boolean;
}

export function CandidateLanguages({ candidateId, languages, readOnly = false }: Props) {
  const { t } = useTranslation();
  const canUpdate = usePermission('candidates.update');
  const canEdit = !readOnly && canUpdate;
  const { candidateRelationsService } = useServices();
  const catalogs = useCatalogs();
  const [draft, setDraft] = useState(EMPTY);
  const [error, setError] = useState('');

  const catalogStatus = useCatalogStatus();
  const languageOptions = catalogs.activeNames('language');
  const levelOptions = catalogs.activeNames('language_level');

  // The relation services persist through the API now, so these must be awaited: a
  // synchronous try/catch around a promise catches nothing.
  const add = async (event: FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();
    try {
      await candidateRelationsService.addLanguage(candidateId, { ...draft });
      setDraft(EMPTY);
      setError('');
    } catch (err) {
      setError(errorText(err, t));
    }
  };

  const remove = async (languageId: string): Promise<void> => {
    try {
      await candidateRelationsService.removeLanguage(candidateId, languageId);
      setError('');
    } catch (err) {
      setError(errorText(err, t));
    }
  };

  return (
    <section className="section-block" data-testid="candidate-languages">
      <h3 className="section-title">{t('candidate.profile.languages.title')}</h3>
      {!languages.length ? (
        <p className="empty-state">{t('candidate.profile.languages.empty')}</p>
      ) : null}
      <div className="item-list">
        {languages.map((item) => (
          <div className="item-row" key={item.id}>
            <p className="item-main">
              <span className="badge">{item.language}</span> {item.level} {item.certification || ''}
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
              <label htmlFor="language">{t('candidate.profile.languages.label')}</label>
              <select
                id="language"
                name="language"
                value={draft.language}
                onChange={(e) => setDraft({ ...draft, language: e.target.value })}
                disabled={!!catalogStatus.message}
                required
              >
                <option value="" disabled>
                  {t('candidate.profile.option.select')}
                </option>
                {languageOptions.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="language-level">{t('candidate.profile.languages.level')}</label>
              <select
                id="language-level"
                name="level"
                value={draft.level}
                onChange={(e) => setDraft({ ...draft, level: e.target.value })}
                disabled={!!catalogStatus.message}
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
              <label htmlFor="certification">
                {t('candidate.profile.languages.certification')}
              </label>
              <input
                id="certification"
                name="certification"
                value={draft.certification}
                onChange={(e) => setDraft({ ...draft, certification: e.target.value })}
              />
            </div>
          </div>
          {error ? <p className="empty-state">{error}</p> : null}
          <div className="form-actions">
            <button className="button" type="submit" disabled={!!catalogStatus.message}>
              {t('candidate.profile.languages.add')}
            </button>
          </div>
        </form>
      ) : null}
    </section>
  );
}

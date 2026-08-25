import { useState, type FormEvent } from 'react';
import { useServices } from '../../../core/di/services-context';
import { useCatalogs } from '../../catalogs/use-catalogs';
import type { CandidateLanguage } from '../models/candidate.models';

const EMPTY = { language: '', level: '', certification: '' };

interface Props {
  candidateId: string;
  languages: CandidateLanguage[];
  canEdit: boolean;
}

export function CandidateLanguages({ candidateId, languages, canEdit }: Props) {
  const { candidateRelationsService } = useServices();
  const catalogs = useCatalogs();
  const [draft, setDraft] = useState(EMPTY);
  const [error, setError] = useState('');

  const languageOptions = catalogs.activeNames('language');
  const levelOptions = catalogs.activeNames('language_level');

  const add = (event: FormEvent<HTMLFormElement>): void => {
    event.preventDefault();
    try {
      candidateRelationsService.addLanguage(candidateId, { ...draft });
      setDraft(EMPTY);
      setError('');
    } catch (err) {
      setError((err as Error).message);
    }
  };

  return (
    <section className="section-block" data-testid="candidate-languages">
      <h3 className="section-title">Idiomas</h3>
      {!languages.length ? <p className="empty-state">Sin idiomas asociados.</p> : null}
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
                onClick={() => candidateRelationsService.removeLanguage(candidateId, item.id)}
              >
                Quitar
              </button>
            ) : null}
          </div>
        ))}
      </div>
      {canEdit ? (
        <form className="section-block" onSubmit={add} noValidate>
          <div className="grid two">
            <div className="field">
              <label htmlFor="language">Idioma</label>
              <select
                id="language"
                name="language"
                value={draft.language}
                onChange={(e) => setDraft({ ...draft, language: e.target.value })}
                required
              >
                <option value="" disabled>
                  Selecciona
                </option>
                {languageOptions.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="language-level">Nivel</label>
              <select
                id="language-level"
                name="level"
                value={draft.level}
                onChange={(e) => setDraft({ ...draft, level: e.target.value })}
                required
              >
                <option value="" disabled>
                  Selecciona
                </option>
                {levelOptions.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="certification">Certificación</label>
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
            <button className="button" type="submit">
              Añadir idioma
            </button>
          </div>
        </form>
      ) : null}
    </section>
  );
}

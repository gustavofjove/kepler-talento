import { useState, type FormEvent } from 'react';
import { useServices } from '../../../core/di/services-context';
import { useCatalogs } from '../../catalogs/use-catalogs';
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
  const { candidateRelationsService } = useServices();
  const catalogs = useCatalogs();
  const [draft, setDraft] = useState<Draft>(EMPTY);
  const [error, setError] = useState('');

  const programOptions = catalogs.activeNames('program');
  const levelOptions = catalogs.activeNames('program_level');

  const add = (event: FormEvent<HTMLFormElement>): void => {
    event.preventDefault();
    try {
      candidateRelationsService.addProgram(candidateId, { ...draft });
      setDraft(EMPTY);
      setError('');
    } catch (err) {
      setError((err as Error).message);
    }
  };

  return (
    <section className="section-block" data-testid="candidate-programs">
      <h3 className="section-title">Programas</h3>
      {!programs.length ? <p className="empty-state">Sin programas asociados.</p> : null}
      <div className="item-list">
        {programs.map((item) => (
          <div className="item-row" key={item.id}>
            <p className="item-main">
              <span className="badge">{item.program}</span> {item.level}
              {item.yearsExperience !== undefined ? ` (${item.yearsExperience} años)` : ''}
            </p>
            {canEdit ? (
              <button
                className="button secondary"
                type="button"
                onClick={() => candidateRelationsService.removeProgram(candidateId, item.id)}
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
              <label htmlFor="program">Programa</label>
              <select
                id="program"
                name="program"
                value={draft.program}
                onChange={(e) => setDraft({ ...draft, program: e.target.value })}
                required
              >
                <option value="" disabled>
                  Selecciona
                </option>
                {programOptions.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="program-level">Nivel</label>
              <select
                id="program-level"
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
              <label htmlFor="program-years">Años de experiencia</label>
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
            <button className="button" type="submit">
              Añadir programa
            </button>
          </div>
        </form>
      ) : null}
    </section>
  );
}

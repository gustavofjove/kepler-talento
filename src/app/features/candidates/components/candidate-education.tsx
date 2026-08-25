import { useState, type FormEvent } from 'react';
import { useServices } from '../../../core/di/services-context';
import { useCatalogs } from '../../catalogs/use-catalogs';
import type { CandidateEducation as CandidateEducationModel } from '../models/candidate.models';

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
  candidateId: string;
  education: CandidateEducationModel[];
  canEdit: boolean;
}

export function CandidateEducation({ candidateId, education, canEdit }: Props) {
  const { candidateRelationsService } = useServices();
  const catalogs = useCatalogs();
  const [draft, setDraft] = useState<Draft>(EMPTY);
  const [error, setError] = useState('');

  const typeOptions = catalogs.activeNames('education_type');
  const statusOptions = catalogs.activeNames('education_status');

  const add = (event: FormEvent<HTMLFormElement>): void => {
    event.preventDefault();
    try {
      candidateRelationsService.addEducation(candidateId, { ...draft });
      setDraft(EMPTY);
      setError('');
    } catch (err) {
      setError((err as Error).message);
    }
  };

  return (
    <section className="section-block" data-testid="candidate-education">
      <h3 className="section-title">Formación</h3>
      {!education.length ? <p className="empty-state">Sin formación registrada.</p> : null}
      <div className="item-list">
        {education.map((item) => (
          <div className="item-row" key={item.id}>
            <p className="item-main">
              <span className="badge">{item.degree}</span> {item.institution} ({item.status})
              {item.endYear ? ` · ${item.endYear}` : ''}
            </p>
            {canEdit ? (
              <button
                className="button secondary"
                type="button"
                onClick={() => candidateRelationsService.removeEducation(candidateId, item.id)}
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
              <label htmlFor="educationType">Tipo</label>
              <select
                id="educationType"
                name="educationType"
                value={draft.educationType}
                onChange={(e) => setDraft({ ...draft, educationType: e.target.value })}
                required
              >
                <option value="" disabled>
                  Selecciona
                </option>
                {typeOptions.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="degree">Titulación</label>
              <input
                id="degree"
                name="degree"
                value={draft.degree}
                onChange={(e) => setDraft({ ...draft, degree: e.target.value })}
                required
              />
            </div>
            <div className="field">
              <label htmlFor="specialty">Especialidad</label>
              <input
                id="specialty"
                name="specialty"
                value={draft.specialty}
                onChange={(e) => setDraft({ ...draft, specialty: e.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="institution">Centro</label>
              <input
                id="institution"
                name="institution"
                value={draft.institution}
                onChange={(e) => setDraft({ ...draft, institution: e.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="endYear">Año de fin</label>
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
              <label htmlFor="education-status">Estado</label>
              <select
                id="education-status"
                name="status"
                value={draft.status}
                onChange={(e) => setDraft({ ...draft, status: e.target.value })}
                required
              >
                <option value="" disabled>
                  Selecciona
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
            <button className="button" type="submit">
              Añadir formación
            </button>
          </div>
        </form>
      ) : null}
    </section>
  );
}

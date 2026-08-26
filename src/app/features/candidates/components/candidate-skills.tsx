import { useState, type FormEvent } from 'react';
import { useServices } from '../../../core/di/services-context';
import { useCatalogs } from '../../catalogs/use-catalogs';
import { CatalogStatusNotice } from '../../catalogs/components/catalog-status';
import { useCatalogStatus } from '../../catalogs/components/use-catalog-status';
import type { CandidateSkill } from '../models/candidate.models';

const EMPTY = { skill: '', level: '' };

interface Props {
  candidateId: string;
  skills: CandidateSkill[];
  canEdit: boolean;
}

export function CandidateSkills({ candidateId, skills, canEdit }: Props) {
  const { candidateRelationsService } = useServices();
  const catalogs = useCatalogs();
  const [draft, setDraft] = useState(EMPTY);
  const [error, setError] = useState('');

  const catalogStatus = useCatalogStatus();
  const skillOptions = catalogs.activeNames('skill');
  const levelOptions = catalogs.activeNames('skill_level');

  const add = (event: FormEvent<HTMLFormElement>): void => {
    event.preventDefault();
    try {
      candidateRelationsService.addSkill(candidateId, { ...draft });
      setDraft(EMPTY);
      setError('');
    } catch (err) {
      setError((err as Error).message);
    }
  };

  return (
    <section className="section-block" data-testid="candidate-skills">
      <h3 className="section-title">Habilidades</h3>
      {!skills.length ? <p className="empty-state">Sin habilidades registradas.</p> : null}
      <div className="item-list">
        {skills.map((item) => (
          <div className="item-row" key={item.id}>
            <p className="item-main">
              <span className="badge">{item.skill}</span> {item.level}
            </p>
            {canEdit ? (
              <button
                className="button secondary"
                type="button"
                onClick={() => candidateRelationsService.removeSkill(candidateId, item.id)}
              >
                Quitar
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
              <label htmlFor="skill">Habilidad</label>
              <select
                id="skill"
                name="skill"
                value={draft.skill}
                onChange={(e) => setDraft({ ...draft, skill: e.target.value })}
                required
              >
                <option value="" disabled>
                  Selecciona
                </option>
                {skillOptions.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="skill-level">Nivel</label>
              <select
                id="skill-level"
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
          </div>
          {error ? <p className="empty-state">{error}</p> : null}
          <div className="form-actions">
            <button className="button" type="submit" disabled={!!catalogStatus.message}>
              Añadir habilidad
            </button>
          </div>
        </form>
      ) : null}
    </section>
  );
}

import { useState, type FormEvent } from 'react';
import { useServices } from '../../../core/di/services-context';
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
  const { candidateRelationsService } = useServices();
  const catalogs = useCatalogs();
  const [draft, setDraft] = useState<Draft>(EMPTY);
  const [error, setError] = useState('');

  const catalogStatus = useCatalogStatus();
  const sectorOptions = catalogs.activeNames('sector');

  const add = (event: FormEvent<HTMLFormElement>): void => {
    event.preventDefault();
    try {
      candidateRelationsService.addExperience(candidateId, { ...draft });
      setDraft(EMPTY);
      setError('');
    } catch (err) {
      setError((err as Error).message);
    }
  };

  return (
    <section className="section-block" data-testid="candidate-experience">
      <h3 className="section-title">Experiencia</h3>
      {!experience.length ? <p className="empty-state">Sin experiencia registrada.</p> : null}
      <div className="item-list">
        {experience.map((item) => (
          <div className="item-row" key={item.id}>
            <p className="item-main">
              <span className="badge">{item.position}</span> {item.company} ({item.sector})
              {item.isCurrent
                ? ' · Actual'
                : item.yearsExperience !== undefined
                  ? ` · ${item.yearsExperience} años`
                  : ''}
            </p>
            {canEdit ? (
              <button
                className="button secondary"
                type="button"
                onClick={() => candidateRelationsService.removeExperience(candidateId, item.id)}
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
              <label htmlFor="company">Empresa</label>
              <input
                id="company"
                name="company"
                value={draft.company}
                onChange={(e) => setDraft({ ...draft, company: e.target.value })}
                required
              />
            </div>
            <div className="field">
              <label htmlFor="position">Puesto</label>
              <input
                id="position"
                name="position"
                value={draft.position}
                onChange={(e) => setDraft({ ...draft, position: e.target.value })}
                required
              />
            </div>
            <div className="field">
              <label htmlFor="sector">Sector</label>
              <select
                id="sector"
                name="sector"
                value={draft.sector}
                onChange={(e) => setDraft({ ...draft, sector: e.target.value })}
                required
              >
                <option value="" disabled>
                  Selecciona
                </option>
                {sectorOptions.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="startDate">Fecha inicio</label>
              <input
                id="startDate"
                name="startDate"
                type="date"
                value={draft.startDate}
                onChange={(e) => setDraft({ ...draft, startDate: e.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="endDate">Fecha fin</label>
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
              <label htmlFor="experience-years">Años de experiencia</label>
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
                Puesto actual
              </label>
            </div>
          </div>
          {error ? <p className="empty-state">{error}</p> : null}
          <div className="form-actions">
            <button className="button" type="submit" disabled={!!catalogStatus.message}>
              Añadir experiencia
            </button>
          </div>
        </form>
      ) : null}
    </section>
  );
}

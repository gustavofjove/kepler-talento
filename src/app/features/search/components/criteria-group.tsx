import type { CriteriaFilter, MultiValueMode } from '../models/search.models';
import type { CriteriaGroupDefinition } from './criteria-group.model';

interface Props {
  group: CriteriaGroupDefinition;
  criteria: CriteriaFilter[];
  mode: MultiValueMode;
  draft: CriteriaFilter;
  valueOptions: string[];
  levelOptions: string[];
  onDraftChange: (draft: CriteriaFilter) => void;
  onAdd: () => void;
  onRemove: (index: number) => void;
  onModeChange: (mode: MultiValueMode) => void;
}

export function CriteriaGroup({
  group,
  criteria,
  mode,
  draft,
  valueOptions,
  levelOptions,
  onDraftChange,
  onAdd,
  onRemove,
  onModeChange,
}: Props) {
  return (
    <fieldset className="criteria-group" data-criteria={group.kind} aria-label={group.label}>
      <div className="criteria-add">
        <div className="field">
          <label htmlFor={`${group.kind}-value`}>{group.label}</label>
          <select
            id={`${group.kind}-value`}
            name={`${group.kind}Draft`}
            value={draft.value}
            onChange={(e) => onDraftChange({ ...draft, value: e.target.value })}
          >
            <option value="">Selecciona una opción</option>
            {valueOptions.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor={`${group.kind}-level`}>{group.levelLabel}</label>
          <select
            id={`${group.kind}-level`}
            name={`${group.kind}LevelDraft`}
            value={draft.level}
            onChange={(e) => onDraftChange({ ...draft, level: e.target.value })}
          >
            <option value="">Cualquier nivel</option>
            {levelOptions.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>
        </div>
        <button
          className="button secondary small"
          type="button"
          disabled={!draft.value}
          data-testid={`add-${group.kind}`}
          onClick={onAdd}
        >
          {group.addLabel}
        </button>
      </div>

      {criteria.length ? (
        <>
          <div className="criteria-mode">
            <label htmlFor={`${group.kind}-mode`}>Coincidencia</label>
            <select
              id={`${group.kind}-mode`}
              name={`${group.kind}Mode`}
              value={mode}
              onChange={(e) => onModeChange(e.target.value === 'ALL' ? 'ALL' : 'ANY')}
            >
              <option value="ANY">Cualquiera</option>
              <option value="ALL">Todos</option>
            </select>
          </div>
          <ul className="criteria-list">
            {criteria.map((criterion, index) => (
              <li key={`${criterion.value}-${index}`}>
                <span>
                  <span className="badge">{criterion.value}</span>{' '}
                  {criterion.level || 'Cualquier nivel'}
                </span>
                <button
                  className="button ghost small"
                  type="button"
                  aria-label={`Quitar ${criterion.value}`}
                  onClick={() => onRemove(index)}
                >
                  ×
                </button>
              </li>
            ))}
          </ul>
        </>
      ) : (
        <p className="empty-state">Sin filtros de {group.label.toLowerCase()}.</p>
      )}
    </fieldset>
  );
}

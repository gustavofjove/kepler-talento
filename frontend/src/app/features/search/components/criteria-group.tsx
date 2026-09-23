import type { CriteriaFilter, MultiValueMode } from '../models/search.models';
import type { CriteriaGroupDefinition } from './criteria-group.model';
import { useTranslation } from 'react-i18next';

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
  const { t } = useTranslation();
  const label = t(group.labelKey);
  return (
    <fieldset className="criteria-group" data-criteria={group.kind} aria-label={label}>
      <div className="criteria-add">
        <div className="field">
          <label htmlFor={`${group.kind}-value`}>{label}</label>
          <select
            id={`${group.kind}-value`}
            name={`${group.kind}Draft`}
            value={draft.value}
            onChange={(e) => onDraftChange({ ...draft, value: e.target.value })}
          >
            <option value="">{t('search.criteria.option.select')}</option>
            {valueOptions.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>
        </div>
        {group.levelFamily ? (
          <div className="field">
            <label htmlFor={`${group.kind}-level`}>{t(group.levelLabelKey!)}</label>
            <select
              id={`${group.kind}-level`}
              name={`${group.kind}LevelDraft`}
              value={draft.level}
              onChange={(e) => onDraftChange({ ...draft, level: e.target.value })}
            >
              <option value="">{t('search.criteria.level.any')}</option>
              {levelOptions.map((option) => (
                <option key={option} value={option}>
                  {option}
                </option>
              ))}
            </select>
          </div>
        ) : null}
        <button
          className="button secondary small"
          type="button"
          disabled={!draft.value}
          data-testid={`add-${group.kind}`}
          onClick={onAdd}
        >
          {t(group.addLabelKey)}
        </button>
      </div>

      {criteria.length ? (
        <>
          <div className="criteria-mode">
            <label htmlFor={`${group.kind}-mode`}>{t('search.criteria.match')}</label>
            <select
              id={`${group.kind}-mode`}
              name={`${group.kind}Mode`}
              value={mode}
              onChange={(e) => onModeChange(e.target.value === 'ALL' ? 'ALL' : 'ANY')}
            >
              <option value="ANY">{t('search.criteria.mode.any')}</option>
              <option value="ALL">{t('search.criteria.mode.all')}</option>
            </select>
          </div>
          <ul className="criteria-list">
            {criteria.map((criterion, index) => (
              <li key={`${criterion.value}-${index}`}>
                <span>
                  <span className="badge">{criterion.value}</span>{' '}
                  {criterion.level || (group.levelFamily ? t('search.criteria.level.any') : '')}
                </span>
                <button
                  className="button ghost small"
                  type="button"
                  aria-label={t('search.criteria.remove', { value: criterion.value })}
                  onClick={() => onRemove(index)}
                >
                  ×
                </button>
              </li>
            ))}
          </ul>
        </>
      ) : (
        <p className="empty-state">
          {t('search.criteria.group.empty', { label: label.toLowerCase() })}
        </p>
      )}
    </fieldset>
  );
}

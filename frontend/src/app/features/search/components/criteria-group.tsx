import { useTranslation } from 'react-i18next';
import { ToggleButton, ToggleButtonGroup } from 'react-aria-components';
import { CatalogValuePicker } from '../../catalogs/components/catalog-value-picker';
import {
  normalizeName,
  pickerTestIds,
  type PickerItem,
} from '../../catalogs/components/catalog-value-picker.logic';
import type { CriteriaFilter, MultiValueMode } from '../models/search.models';
import type { CriteriaGroupDefinition } from './criteria-group.model';

interface Props {
  group: CriteriaGroupDefinition;
  criteria: CriteriaFilter[];
  mode: MultiValueMode;
  valueOptions: string[];
  levelOptions: string[];
  disabled?: boolean;
  onCriteriaChange: (criteria: CriteriaFilter[]) => void;
  onModeChange: (mode: MultiValueMode) => void;
}

const toItem = (criterion: CriteriaFilter): PickerItem => ({
  key: normalizeName(criterion.value),
  value: criterion.value,
  level: criterion.level,
});

const toCriterion = (item: PickerItem): CriteriaFilter => ({
  value: item.value,
  level: item.level,
});

/**
 * One multi-value filter family on the shared catalog value picker. It only maps criteria
 * to picker items and back; the level is optional, so a new criterion means any level until
 * it is set on its chip. The same prefixes serve the search, preset and position pages.
 */
export function CriteriaGroup({
  group,
  criteria,
  mode,
  valueOptions,
  levelOptions,
  disabled = false,
  onCriteriaChange,
  onModeChange,
}: Props) {
  const { t } = useTranslation();
  const idPrefix = `search-${group.kind}`;
  const label = t(group.labelKey);
  const items = criteria.map(toItem);

  // ANY/ALL only means something from two criteria, so it is not shown before then.
  const modeToggle =
    criteria.length < 2 ? null : (
      <ToggleButtonGroup
        aria-label={t('search.criteria.mode.label', { label })}
        className="criteria-mode"
        data-testid={pickerTestIds(idPrefix).mode}
        selectionMode="single"
        disallowEmptySelection
        selectedKeys={[mode]}
        onSelectionChange={(keys) => onModeChange([...keys][0] === 'ALL' ? 'ALL' : 'ANY')}
      >
        <ToggleButton id="ANY" className="criteria-mode-option" data-value="ANY">
          {t('search.criteria.mode.any')}
        </ToggleButton>
        <ToggleButton id="ALL" className="criteria-mode-option" data-value="ALL">
          {t('search.criteria.mode.all')}
        </ToggleButton>
      </ToggleButtonGroup>
    );

  return (
    <div className="criteria-group" data-criteria={group.kind}>
      <CatalogValuePicker
        idPrefix={idPrefix}
        label={label}
        addLabel={t(group.addLabelKey)}
        valueOptions={valueOptions}
        levelOptions={group.levelFamily ? levelOptions : undefined}
        levelMode="optional"
        anyLevelLabel={t('search.criteria.level.any')}
        items={items}
        disabled={disabled}
        headerAction={modeToggle}
        onAdd={(item) => onCriteriaChange([...criteria, toCriterion(item)])}
        onChange={(item) =>
          onCriteriaChange(
            items.map((current) => toCriterion(current.key === item.key ? item : current)),
          )
        }
        onRemove={(item) =>
          onCriteriaChange(items.filter((current) => current.key !== item.key).map(toCriterion))
        }
      />
    </div>
  );
}

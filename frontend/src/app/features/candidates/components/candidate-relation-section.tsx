import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { CatalogValuePicker } from '../../catalogs/components/catalog-value-picker';
import type { PickerItem } from '../../catalogs/components/catalog-value-picker.logic';
import { useCatalogStatus } from '../../catalogs/components/use-catalog-status';
import { useCatalogs } from '../../catalogs/use-catalogs';
import {
  detailText,
  RELATION_DEFINITIONS,
  type RelationKind,
} from './candidate-relation-section.logic';

interface Props {
  kind: RelationKind;
  /** The family's items: the saved entries, or the panel's draft while it is edited. */
  items: PickerItem[];
  editing: boolean;
  /** Proposes a new draft; the panel validates it and may refuse it. */
  onItemsChange: (items: PickerItem[], changed: PickerItem) => void;
}

/**
 * One catalog-backed relation of a candidate (languages, skills, programs or tags): a row of
 * the Competencias panel, on the shared picker. Since KTL-29 the row is controlled: every
 * add, change and removal edits the panel's draft, which the panel writes with «Guardar».
 * Languages, skills and programs are added at their lowest level, changed from the chip.
 * The panel shows the catalog notice and each family's save error.
 */
export function CandidateRelationSection({ kind, items, editing, onItemsChange }: Props) {
  const { t } = useTranslation();
  const definition = RELATION_DEFINITIONS[kind];
  const catalogs = useCatalogs();
  const catalogStatus = useCatalogStatus();

  return (
    <div data-testid={definition.testId}>
      <CatalogValuePicker
        // A fresh picker per mode: react-aria keeps rendered chips while `items` is the same
        // array, so re-entering edit mode would otherwise show them without their remove
        // buttons, and an input left open in edit mode would reappear on the next «Editar».
        key={editing ? 'edit' : 'read'}
        idPrefix={definition.idPrefix}
        label={t(definition.titleKey)}
        addLabel={t(definition.addLabelKey)}
        valueOptions={catalogs.activeNames(definition.valueFamily)}
        levelOptions={
          definition.levelFamily ? catalogs.activeNames(definition.levelFamily) : undefined
        }
        levelMode="required"
        items={items}
        emptyText={t(definition.emptyKey)}
        readOnly={!editing}
        disabled={Boolean(catalogStatus.message)}
        detailText={(item) => detailText(definition.detail, item, t)}
        renderDetails={
          definition.detail === 'certification'
            ? (draft, set) => <CertificationField item={draft} onChange={set} />
            : definition.detail === 'yearsExperience'
              ? (draft, set) => <YearsField item={draft} onChange={set} />
              : undefined
        }
        onAdd={(item) => onItemsChange([...items, item], item)}
        onChange={(item) =>
          onItemsChange(
            items.map((entry) => (entry.key === item.key ? item : entry)),
            item,
          )
        }
        onRemove={(item) =>
          onItemsChange(
            items.filter((entry) => entry.key !== item.key),
            item,
          )
        }
      />
    </div>
  );
}

interface DetailFieldProps {
  item: PickerItem;
  onChange: (next: PickerItem) => void;
}

function CertificationField({ item, onChange }: DetailFieldProps) {
  const { t } = useTranslation();
  const id = useId();
  return (
    <div className="field">
      <label htmlFor={id}>{t('candidate.profile.languages.certification')}</label>
      <input
        id={id}
        name="certification"
        value={String(item.details?.['certification'] ?? '')}
        onChange={(e) =>
          onChange({ ...item, details: { ...item.details, certification: e.target.value } })
        }
      />
    </div>
  );
}

function YearsField({ item, onChange }: DetailFieldProps) {
  const { t } = useTranslation();
  const id = useId();
  return (
    <div className="field">
      <label htmlFor={id}>{t('candidate.profile.programs.yearsExperience')}</label>
      <input
        id={id}
        name="yearsExperience"
        type="number"
        min="0"
        value={item.details?.['yearsExperience'] ?? ''}
        onChange={(e) =>
          onChange({
            ...item,
            details: {
              ...item.details,
              yearsExperience: e.target.value === '' ? undefined : Number(e.target.value),
            },
          })
        }
      />
    </div>
  );
}

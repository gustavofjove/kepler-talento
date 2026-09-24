import { useId, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { usePermission, useServices } from '../../../core/di/services-context';
import { errorText } from '../../../core/i18n/translatable-error';
import { CatalogValuePicker } from '../../catalogs/components/catalog-value-picker';
import type { PickerItem } from '../../catalogs/components/catalog-value-picker.logic';
import { useCatalogStatus } from '../../catalogs/components/use-catalog-status';
import { useCatalogs } from '../../catalogs/use-catalogs';
import type { Candidate } from '../models/candidate.models';
import {
  detailText,
  mergeWrites,
  RELATION_DEFINITIONS,
  type RelationIntent,
  type RelationKind,
  type RelationWrite,
} from './candidate-relation-section.logic';

interface Props {
  kind: RelationKind;
  candidate: Candidate;
  /** The detail page renders every section read-only, whatever the viewer may do. */
  readOnly?: boolean;
}

/**
 * One catalog-backed relation of a candidate (languages, skills, programs or tags): a row of
 * the Competencias panel, on the shared picker. Every add, change and removal persists at
 * once through the relations service; each chip carries its own pending or failed state, so a
 * refused write never disturbs the other entries or the core form. Languages, skills and
 * programs are added at their lowest level, changed from the chip. The panel shows the
 * catalog notice.
 */
export function CandidateRelationSection({ kind, candidate, readOnly = false }: Props) {
  const { t } = useTranslation();
  const definition = RELATION_DEFINITIONS[kind];
  const canUpdate = usePermission('candidates.update');
  const canEdit = !readOnly && canUpdate;
  const { candidateRelationsService } = useServices();
  const catalogs = useCatalogs();
  const catalogStatus = useCatalogStatus();
  const [writes, setWrites] = useState<Record<string, RelationWrite>>({});

  const saved = useMemo(() => definition.items(candidate), [definition, candidate]);
  const items = useMemo(() => mergeWrites(saved, writes), [saved, writes]);

  const settle = (key: string, write?: RelationWrite): void =>
    setWrites((current) => {
      const { [key]: _settled, ...rest } = current;
      return write ? { ...rest, [key]: write } : rest;
    });

  const run = async (intent: RelationIntent): Promise<void> => {
    const { key } = intent.item;
    settle(key, { state: 'pending', intent });
    try {
      if (intent.type === 'add') {
        await definition.add(candidateRelationsService, candidate, intent.item);
      } else if (intent.type === 'change') {
        await definition.update(candidateRelationsService, candidate, intent.item);
      } else {
        await definition.remove(candidateRelationsService, candidate.id, key);
      }
      settle(key);
    } catch (err) {
      settle(key, { state: 'error', error: errorText(err, t), intent });
    }
  };

  const remove = (item: PickerItem): void => {
    // A failed add was never saved: removing its chip just drops it.
    const write = writes[item.key];
    if (write?.intent.type === 'add' && !saved.some((entry) => entry.key === item.key)) {
      settle(item.key);
      return;
    }
    void run({ type: 'remove', item });
  };

  return (
    <div data-testid={definition.testId}>
      <CatalogValuePicker
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
        readOnly={!canEdit}
        disabled={Boolean(catalogStatus.message)}
        detailText={(item) => detailText(definition.detail, item, t)}
        renderDetails={
          definition.detail === 'certification'
            ? (draft, set) => <CertificationField item={draft} onChange={set} />
            : definition.detail === 'yearsExperience'
              ? (draft, set) => <YearsField item={draft} onChange={set} />
              : undefined
        }
        onAdd={(item) => void run({ type: 'add', item })}
        onChange={(item) => void run({ type: 'change', item })}
        onRemove={remove}
        onRetry={(item) => {
          const write = writes[item.key];
          if (write) void run(write.intent);
        }}
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

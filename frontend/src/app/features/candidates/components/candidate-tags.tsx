import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { usePermission, useServices } from '../../../core/di/services-context';
import { errorText } from '../../../core/i18n/translatable-error';
import { useCatalogs } from '../../catalogs/use-catalogs';
import type { CandidateTag } from '../models/candidate.models';
import './candidate-tags.css';

interface Props {
  candidateId: string;
  tags: CandidateTag[];
  /** See candidate-languages. */
  readOnly?: boolean;
}

export function CandidateTags({ candidateId, tags, readOnly = false }: Props) {
  const { t } = useTranslation();
  const { candidateRelationsService } = useServices();
  const canUpdate = usePermission('candidates.update');
  const canEdit = !readOnly && canUpdate;
  const catalogs = useCatalogs();
  const [tag, setTag] = useState('');
  const [error, setError] = useState('');
  const assigned = new Set(tags.map((item) => item.tag));
  const options = catalogs.activeNames('tag').filter((item) => !assigned.has(item));

  const add = async (event: FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();
    if (!tag) return;
    try {
      await candidateRelationsService.addTag(candidateId, { tag });
      setTag('');
      setError('');
    } catch (err) {
      setError(errorText(err, t));
    }
  };

  const remove = async (id: string): Promise<void> => {
    try {
      await candidateRelationsService.removeTag(candidateId, id);
      setError('');
    } catch (err) {
      setError(errorText(err, t));
    }
  };

  return (
    <section className="section-block candidate-tags" data-testid="candidate-tags">
      <h3 className="section-title">{t('candidate.profile.tags.title')}</h3>
      {tags.length ? (
        <div className="candidate-tags-list">
          {tags.map((item) => (
            <span className="chip" key={item.id}>
              {item.tag}
              {canEdit ? (
                <button
                  type="button"
                  className="button ghost small"
                  aria-label={t('candidate.profile.tags.remove', { name: item.tag })}
                  onClick={() => void remove(item.id)}
                >
                  ×
                </button>
              ) : null}
            </span>
          ))}
        </div>
      ) : (
        <p className="empty-state">{t('candidate.profile.tags.empty')}</p>
      )}
      {canEdit ? (
        <form className="candidate-tags-form" onSubmit={add} noValidate>
          <label htmlFor="candidate-tag">{t('candidate.profile.tags.label')}</label>
          <select
            id="candidate-tag"
            name="tag"
            data-testid="candidate-tag-select"
            value={tag}
            onChange={(event) => setTag(event.target.value)}
          >
            <option value="">{t('candidate.profile.option.select')}</option>
            {options.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>
          <button className="button secondary" type="submit" disabled={!tag}>
            {t('candidate.profile.tags.add')}
          </button>
        </form>
      ) : null}
      {error ? <p className="empty-state">{error}</p> : null}
    </section>
  );
}

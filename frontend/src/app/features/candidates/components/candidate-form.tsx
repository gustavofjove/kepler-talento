import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import type { Candidate, CandidateDraft } from '../models/candidate.models';
import { toDraft } from './candidate-form.logic';

const STATUSES = ['new', 'available', 'in_process', 'hired', 'rejected'] as const;

interface CandidateFormProps {
  candidate?: Candidate;
  onSave: (draft: CandidateDraft) => void;
}

/** The core record only; the relation sections live beside it on the edit page. */
export function CandidateForm({ candidate, onSave }: CandidateFormProps) {
  const { t } = useTranslation();
  // Initialiser only. The parent passes `key` so switching candidate remounts
  // and resets the draft, which is what the Angular input setter did.
  const [draft, setDraft] = useState<CandidateDraft>(() => toDraft(candidate));
  const [error, setError] = useState('');

  const set =
    (key: keyof CandidateDraft) =>
    (event: { target: { value: string } }): void =>
      setDraft((current) => ({ ...current, [key]: event.target.value }));

  const submit = (event: FormEvent<HTMLFormElement>): void => {
    event.preventDefault();
    if (!draft.firstName.trim() || !draft.lastName.trim()) {
      setError(t('candidate.form.nameRequired'));
      return;
    }
    setError('');
    onSave({ ...draft });
  };

  return (
    // noValidate: the browser must not block submit, so the Spanish message above is
    // what users (and the e2e spec) see.
    <form className="section-block" onSubmit={submit} noValidate>
      <div className="grid two">
        <div className="field">
          <label htmlFor="firstName">{t('candidate.form.firstName')}</label>
          <input
            id="firstName"
            name="firstName"
            value={draft.firstName}
            onChange={set('firstName')}
            required
          />
        </div>
        <div className="field">
          <label htmlFor="lastName">{t('candidate.form.lastName')}</label>
          <input
            id="lastName"
            name="lastName"
            value={draft.lastName}
            onChange={set('lastName')}
            required
          />
        </div>
        <div className="field">
          <label htmlFor="phone">{t('candidate.form.phone')}</label>
          <input id="phone" name="phone" value={draft.phone} onChange={set('phone')} />
        </div>
        <div className="field">
          <label htmlFor="email">{t('candidate.form.email')}</label>
          <input id="email" name="email" type="email" value={draft.email} onChange={set('email')} />
        </div>
        <div className="field">
          <label htmlFor="location">{t('candidate.form.location')}</label>
          <input id="location" name="location" value={draft.location} onChange={set('location')} />
        </div>
        <div className="field">
          <label htmlFor="province">{t('candidate.form.province')}</label>
          <input id="province" name="province" value={draft.province} onChange={set('province')} />
        </div>
        <div className="field">
          <label htmlFor="availability">{t('candidate.form.availability')}</label>
          <input
            id="availability"
            name="availability"
            value={draft.availability}
            onChange={set('availability')}
          />
        </div>
        <div className="field">
          <label htmlFor="status">{t('candidate.form.status')}</label>
          <select id="status" name="status" value={draft.status} onChange={set('status')}>
            {STATUSES.map((status) => (
              <option key={status} value={status}>
                {t(`candidate.form.statusOption.${status}`)}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="receivedAt">{t('candidate.form.receivedAt')}</label>
          <input
            id="receivedAt"
            name="receivedAt"
            type="date"
            value={draft.receivedAt}
            onChange={set('receivedAt')}
          />
        </div>
        <div className="field">
          <label htmlFor="reviewDueAt">{t('candidate.form.reviewDueAt')}</label>
          <input
            id="reviewDueAt"
            name="reviewDueAt"
            type="date"
            value={draft.reviewDueAt}
            onChange={set('reviewDueAt')}
          />
        </div>
      </div>
      <div className="field">
        <label htmlFor="notes">{t('candidate.form.notes')}</label>
        <textarea id="notes" name="notes" rows={4} value={draft.notes} onChange={set('notes')} />
      </div>
      {error ? <p className="empty-state">{error}</p> : null}
      <div className="form-actions">
        <button className="button" type="submit">
          {t('candidate.form.save')}
        </button>
      </div>
    </form>
  );
}

import { useState, type FormEvent } from 'react';
import type { Candidate, CandidateDraft } from '../models/candidate.models';
import { toDraft } from './candidate-form.logic';

interface CandidateFormProps {
  candidate?: Candidate;
  onSave: (draft: CandidateDraft) => void;
}

export function CandidateForm({ candidate, onSave }: CandidateFormProps) {
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
      setError('Nombre y apellidos son obligatorios.');
      return;
    }
    setError('');
    onSave({ ...draft });
  };

  return (
    // noValidate: the original template-driven form did not let the browser
    // block submit, so the custom Spanish message is what users (and the e2e
    // spec) see.
    <form className="section-block" onSubmit={submit} noValidate>
      <div className="grid two">
        <div className="field">
          <label htmlFor="firstName">Nombre</label>
          <input
            id="firstName"
            name="firstName"
            value={draft.firstName}
            onChange={set('firstName')}
            required
          />
        </div>
        <div className="field">
          <label htmlFor="lastName">Apellidos</label>
          <input
            id="lastName"
            name="lastName"
            value={draft.lastName}
            onChange={set('lastName')}
            required
          />
        </div>
        <div className="field">
          <label htmlFor="phone">Teléfono</label>
          <input id="phone" name="phone" value={draft.phone} onChange={set('phone')} />
        </div>
        <div className="field">
          <label htmlFor="email">Email</label>
          <input id="email" name="email" type="email" value={draft.email} onChange={set('email')} />
        </div>
        <div className="field">
          <label htmlFor="location">Localidad</label>
          <input id="location" name="location" value={draft.location} onChange={set('location')} />
        </div>
        <div className="field">
          <label htmlFor="province">Provincia</label>
          <input id="province" name="province" value={draft.province} onChange={set('province')} />
        </div>
        <div className="field">
          <label htmlFor="availability">Disponibilidad</label>
          <input
            id="availability"
            name="availability"
            value={draft.availability}
            onChange={set('availability')}
          />
        </div>
        <div className="field">
          <label htmlFor="status">Estado</label>
          <select id="status" name="status" value={draft.status} onChange={set('status')}>
            <option value="new">Nuevo</option>
            <option value="available">Disponible</option>
            <option value="in_process">En proceso</option>
            <option value="hired">Contratado</option>
            <option value="rejected">Descartado</option>
          </select>
        </div>
        <div className="field">
          <label htmlFor="receivedAt">Fecha recepción</label>
          <input
            id="receivedAt"
            name="receivedAt"
            type="date"
            value={draft.receivedAt}
            onChange={set('receivedAt')}
          />
        </div>
        <div className="field">
          <label htmlFor="reviewDueAt">Fecha revisión</label>
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
        <label htmlFor="notes">Observaciones internas</label>
        <textarea id="notes" name="notes" rows={4} value={draft.notes} onChange={set('notes')} />
      </div>
      {error ? <p className="empty-state">{error}</p> : null}
      <div className="form-actions">
        <button className="button" type="submit">
          Guardar
        </button>
      </div>
    </form>
  );
}

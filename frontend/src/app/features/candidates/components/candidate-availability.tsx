import { useEffect, useId, useRef, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { usePermission, useServices } from '../../../core/di/services-context';
import { formatDay, formatElapsed, localDay } from '../../../core/i18n/format';
import { errorText, TranslatableError } from '../../../core/i18n/translatable-error';
import { useErrorToast } from '../../../core/services/use-error-toast';
import { FormError } from '../../../shared/components/form-error';
import {
  ALL_AVAILABILITY_STATES,
  type Candidate,
  type CandidateAvailability,
  type CandidateAvailabilityInput,
  type CandidateAvailabilityState,
} from '../models/candidate.models';
import {
  availabilityLabel,
  canReconfirm,
  initialForm,
  isLapsed,
  reconfirmInput,
  toRequest,
  undoInput,
  type AvailabilityForm,
} from './candidate-availability.logic';
import './candidate-availability.css';

interface CandidateAvailabilityProps {
  candidate: Candidate;
}

/**
 * The availability block under the contact line (KTL-36). Every action is an immediate write
 * through its own endpoint, so the block takes no part in the page's per-panel edit mode and
 * never counts as unsaved changes. The service replaces the cached aggregate with each
 * response, so a panel opened before a check saves afterwards with the new version.
 *
 * «Deshacer» holds the previous check in this component's state: it lasts until the next check
 * or until the page is left, and is never removed on a timer.
 */
export function CandidateAvailabilityBlock({ candidate }: CandidateAvailabilityProps) {
  const { t } = useTranslation();
  const { candidateService } = useServices();
  const notifyError = useErrorToast();
  const mayUpdate = usePermission('candidates.update');
  const canAct = mayUpdate && candidate.isActive;
  const check = candidate.availability;
  const today = localDay();
  const lapsed = isLapsed(check, today);

  const [form, setForm] = useState<AvailabilityForm | null>(null);
  const [formError, setFormError] = useState('');
  const [previous, setPrevious] = useState<CandidateAvailability | null>(null);
  const [announcement, setAnnouncement] = useState('');
  const [busy, setBusy] = useState(false);
  const changeButton = useRef<HTMLButtonElement>(null);
  const [refocus, setRefocus] = useState(false);
  const formId = useId();

  // Focus returns to «Cambiar…» once the form has closed and the button is back in the DOM.
  useEffect(() => {
    if (!refocus || form) return;
    changeButton.current?.focus();
    setRefocus(false);
  }, [refocus, form]);

  const record = async (
    input: CandidateAvailabilityInput,
    undoTo: CandidateAvailability | null,
    message: string,
  ): Promise<boolean> => {
    setBusy(true);
    try {
      await candidateService.recordAvailability(candidate.id, input);
      setPrevious(undoTo);
      setAnnouncement(message);
      return true;
    } catch (error) {
      if (error instanceof TranslatableError && form) {
        setFormError(errorText(error, t));
      } else {
        notifyError(error, t('candidate.availability.error.saveFailed'));
      }
      return false;
    } finally {
      setBusy(false);
    }
  };

  const reconfirm = (): void => {
    void record(reconfirmInput(check, today), check, t('candidate.availability.recorded'));
  };

  const undo = (): void => {
    if (!previous) return;
    void record(undoInput(previous), null, t('candidate.availability.undone'));
  };

  const openForm = (): void => {
    setFormError('');
    setForm(initialForm(check, today));
  };

  const closeForm = (): void => {
    setForm(null);
    setFormError('');
    setRefocus(true);
  };

  const submit = async (event: FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();
    if (!form) return;
    setFormError('');
    const saved = await record(toRequest(form), check, t('candidate.availability.recorded'));
    if (saved) closeForm();
  };

  const setState = (state: CandidateAvailabilityState): void =>
    setForm((current) => (current ? { ...current, state } : current));

  const line =
    check.state === 'unavailable' && check.until
      ? t('candidate.availability.unavailableUntil', { date: formatDay(check.until) })
      : availabilityLabel(check.state, t);

  return (
    <div className="candidate-availability" data-testid="candidate-availability">
      <div className="candidate-availability__summary">
        <p className="candidate-availability__line" data-testid="candidate-availability-line">
          {line}
          {lapsed ? (
            <>
              {' '}
              <span className="candidate-availability__lapsed">
                {t('candidate.availability.lapsed')}
              </span>
            </>
          ) : null}
        </p>
        {check.state !== 'unknown' ? (
          <p className="candidate-availability__meta" data-testid="candidate-availability-meta">
            {check.checkedByDisplayName
              ? t('candidate.availability.checkedBy', {
                  date: formatDay(check.checkedOn),
                  name: check.checkedByDisplayName,
                  elapsed: formatElapsed(check.checkedOn),
                })
              : t('candidate.availability.checked', {
                  date: formatDay(check.checkedOn),
                  elapsed: formatElapsed(check.checkedOn),
                })}
          </p>
        ) : null}
      </div>
      {lapsed ? (
        <p
          className="candidate-availability__hint"
          data-testid="candidate-availability-lapsed-hint"
        >
          {t('candidate.availability.lapsedHint')}
        </p>
      ) : null}

      {canAct && !form ? (
        <div className="candidate-availability__actions">
          {canReconfirm(check, today) ? (
            <button
              className="button secondary small"
              type="button"
              name="availabilityReconfirm"
              data-testid="availability-reconfirm"
              aria-label={t('candidate.availability.reconfirmLabel')}
              disabled={busy}
              onClick={reconfirm}
            >
              {t('candidate.availability.reconfirm')}
            </button>
          ) : null}
          <button
            ref={changeButton}
            className="button secondary small"
            type="button"
            name="availabilityChange"
            data-testid="availability-change"
            disabled={busy}
            onClick={openForm}
          >
            {t(
              check.state === 'unknown'
                ? 'candidate.availability.register'
                : 'candidate.availability.change',
            )}
          </button>
          {previous ? (
            <button
              className="button ghost small"
              type="button"
              name="availabilityUndo"
              data-testid="availability-undo"
              disabled={busy}
              onClick={undo}
            >
              {t('candidate.availability.undo')}
            </button>
          ) : null}
        </div>
      ) : null}

      {canAct && form ? (
        <form
          id={formId}
          className="candidate-availability__form"
          data-testid="availability-form"
          noValidate
          onSubmit={(event) => void submit(event)}
        >
          <FormError message={formError} testId="availability-form-error" />
          <fieldset>
            <legend>{t('candidate.availability.formLegend')}</legend>
            {ALL_AVAILABILITY_STATES.map((state) => (
              <label className="inline-check" key={state}>
                <input
                  type="radio"
                  name="availabilityState"
                  value={state}
                  data-testid={`availability-state-${state}`}
                  checked={form.state === state}
                  onChange={() => setState(state)}
                />
                {availabilityLabel(state, t)}
              </label>
            ))}
          </fieldset>
          <div className="candidate-availability__entry-row">
            {form.state !== 'unknown' ? (
              <div className="candidate-availability__dates">
                {form.state === 'unavailable' ? (
                  <div className="field">
                    <label htmlFor={`${formId}-until`}>{t('candidate.availability.until')}</label>
                    <input
                      id={`${formId}-until`}
                      name="availabilityUntil"
                      type="date"
                      data-testid="availability-until"
                      value={form.until}
                      onChange={(event) => setForm({ ...form, until: event.target.value })}
                    />
                  </div>
                ) : null}
                <div className="field">
                  <label htmlFor={`${formId}-checked-on`}>
                    {t('candidate.availability.checkedOn')}
                  </label>
                  <input
                    id={`${formId}-checked-on`}
                    name="availabilityCheckedOn"
                    type="date"
                    data-testid="availability-checked-on"
                    value={form.checkedOn}
                    onChange={(event) => setForm({ ...form, checkedOn: event.target.value })}
                  />
                </div>
              </div>
            ) : null}
            <div className="form-actions">
              <button
                className="button"
                type="submit"
                name="availabilitySave"
                data-testid="availability-save"
                disabled={busy}
              >
                {t('candidate.availability.save')}
              </button>
              <button
                className="button secondary"
                type="button"
                name="availabilityCancel"
                data-testid="availability-cancel"
                disabled={busy}
                onClick={closeForm}
              >
                {t('candidate.availability.cancel')}
              </button>
            </div>
          </div>
        </form>
      ) : null}

      <p
        className="candidate-availability__hint"
        role="status"
        aria-live="polite"
        data-testid="candidate-availability-status"
      >
        {announcement}
      </p>
    </div>
  );
}

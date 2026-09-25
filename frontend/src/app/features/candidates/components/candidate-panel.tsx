import { useEffect, useRef, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import {
  FIRST_CONTROL,
  panelTestIds,
  type PanelControl,
  type PanelId,
} from './candidate-panel.logic';
import './candidate-panel.css';

interface Props {
  id: PanelId;
  title: string;
  control: PanelControl;
  /** `form` panels stage a draft (Guardar/Cancelar); `actions` panels act at once (Hecho). */
  mode: 'form' | 'actions';
  /** For a form panel whose body is a `<form>`: «Guardar» submits it. */
  formId?: string;
  onSave?: () => void;
  onCancel?: () => void;
  saving?: boolean;
  saveDisabled?: boolean;
  testId?: string;
  children: ReactNode;
}

/**
 * The frame of one candidate page panel: its heading, the «Editar» button, and the footer of
 * the edit mode. The body is the panel's own read or edit view, unchanged from the pages it
 * replaces. Focus moves into the body on entering edit mode and back to «Editar» on leaving.
 */
export function CandidatePanel({
  id,
  title,
  control,
  mode,
  formId,
  onSave,
  onCancel,
  saving = false,
  saveDisabled = false,
  testId,
  children,
}: Props) {
  const { t } = useTranslation();
  const ids = panelTestIds(id);
  const panel = useRef<HTMLElement>(null);
  const body = useRef<HTMLDivElement>(null);
  const editButton = useRef<HTMLButtonElement>(null);
  const wasEditing = useRef(control.editing);

  useEffect(() => {
    if (control.editing && !wasEditing.current) {
      body.current?.querySelector<HTMLElement>(FIRST_CONTROL)?.focus();
    } else if (!control.editing && wasEditing.current && !focusMovedElsewhere(panel.current)) {
      // Back to «Editar», unless closing was caused by opening another panel, whose editor
      // has already taken focus (effects of the two panels run in either order).
      editButton.current?.focus();
    }
    wasEditing.current = control.editing;
  }, [control.editing]);

  const cancel = (): void => {
    onCancel?.();
    control.onClose();
  };

  return (
    <article ref={panel} className="panel candidate-panel" data-testid={testId}>
      <div className="candidate-panel__header">
        <h2>{title}</h2>
        {control.canEdit && !control.editing ? (
          <button
            ref={editButton}
            className="button secondary"
            type="button"
            data-testid={ids.edit}
            aria-label={t('candidate.panel.editAria', { section: title })}
            onClick={control.onEdit}
          >
            {t('candidate.panel.edit')}
          </button>
        ) : null}
      </div>
      <div ref={body} className="candidate-panel__body">
        {children}
      </div>
      {control.editing ? (
        <div className="form-actions candidate-panel__footer">
          {mode === 'form' ? (
            <>
              <button
                className="button secondary"
                type="button"
                data-testid={ids.cancel}
                aria-label={t('candidate.panel.cancelAria', { section: title })}
                disabled={saving}
                onClick={cancel}
              >
                {t('candidate.panel.cancel')}
              </button>
              <button
                className="button"
                type={formId ? 'submit' : 'button'}
                form={formId}
                data-testid={ids.save}
                aria-label={t('candidate.panel.saveAria', { section: title })}
                aria-busy={saving}
                disabled={saving || saveDisabled}
                onClick={formId ? undefined : onSave}
              >
                {t('candidate.panel.save')}
              </button>
            </>
          ) : (
            <button
              className="button"
              type="button"
              data-testid={ids.done}
              aria-label={t('candidate.panel.doneAria', { section: title })}
              onClick={control.onClose}
            >
              {t('candidate.panel.done')}
            </button>
          )}
        </div>
      ) : null}
    </article>
  );
}

/** True when focus sits on a real control outside this panel, e.g. another panel's editor. */
function focusMovedElsewhere(panel: HTMLElement | null): boolean {
  const active = document.activeElement;
  return active !== null && active !== document.body && !panel?.contains(active);
}

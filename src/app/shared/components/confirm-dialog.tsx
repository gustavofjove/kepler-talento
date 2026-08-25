import { useEffect } from 'react';
import { useServices } from '../../core/di/services-context';
import { useSignal } from '../../core/state/use-signal';
import './modal.css';

export function ConfirmDialog() {
  const { confirmDialogService } = useServices();
  const state = useSignal(confirmDialogService.state);

  // Replaces @HostListener('document:keydown.escape').
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent): void => {
      if (event.key === 'Escape' && confirmDialogService.state()) {
        confirmDialogService.cancel();
      }
    };
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [confirmDialogService]);

  if (!state) {
    return null;
  }

  return (
    <div className="overlay" data-testid="confirm-overlay">
      <section
        className="dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="confirm-title"
        aria-describedby="confirm-message"
        data-testid="confirm-dialog"
      >
        <h2 id="confirm-title">{state.title}</h2>
        <p id="confirm-message">{state.message}</p>
        <div className="actions">
          <button
            className="button ghost"
            type="button"
            onClick={() => confirmDialogService.cancel()}
          >
            {state.cancelText}
          </button>
          <button
            className={state.danger ? 'button danger' : 'button'}
            type="button"
            data-testid="confirm-accept"
            onClick={() => confirmDialogService.accept()}
          >
            {state.confirmText}
          </button>
        </div>
      </section>
    </div>
  );
}

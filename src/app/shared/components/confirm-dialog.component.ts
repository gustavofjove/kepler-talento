import { Component, HostListener } from '@angular/core';
import { ConfirmDialogService } from './confirm-dialog.service';

@Component({
  selector: 'rrhh-confirm-dialog',
  standalone: true,
  styles: [
    `
      .overlay {
        align-items: center;
        background: rgba(22, 33, 54, 0.45);
        display: grid;
        inset: 0;
        justify-items: center;
        padding: 16px;
        position: fixed;
        z-index: 40;
      }
      .dialog {
        background: var(--bg-1);
        border: 1px solid var(--border-2);
        border-radius: var(--r-lg);
        box-shadow: 0 12px 30px rgba(0, 0, 0, 0.22);
        display: grid;
        gap: 12px;
        max-width: 520px;
        padding: 18px;
        width: 100%;
      }
      .dialog h2 {
        margin: 0;
      }
      .dialog p {
        margin: 0;
      }
      .actions {
        display: flex;
        gap: 8px;
        justify-content: flex-end;
      }
    `,
  ],
  template: `
    @if (confirm.state(); as state) {
      <div class="overlay" data-testid="confirm-overlay">
        <section
          class="dialog"
          role="dialog"
          aria-modal="true"
          aria-labelledby="confirm-title"
          aria-describedby="confirm-message"
          data-testid="confirm-dialog"
        >
          <h2 id="confirm-title">{{ state.title }}</h2>
          <p id="confirm-message">{{ state.message }}</p>
          <div class="actions">
            <button class="button ghost" type="button" (click)="confirm.cancel()">
              {{ state.cancelText }}
            </button>
            <button
              class="button"
              [class.danger]="state.danger"
              type="button"
              data-testid="confirm-accept"
              (click)="confirm.accept()"
            >
              {{ state.confirmText }}
            </button>
          </div>
        </section>
      </div>
    }
  `,
})
export class ConfirmDialogComponent {
  constructor(readonly confirm: ConfirmDialogService) {}

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.confirm.state()) {
      this.confirm.cancel();
    }
  }
}

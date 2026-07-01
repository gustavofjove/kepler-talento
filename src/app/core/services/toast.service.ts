import { Injectable, signal } from '@angular/core';

export interface ToastMessage {
  id: string;
  text: string;
  type: 'success' | 'warning' | 'error' | 'info';
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  readonly messages = signal<ToastMessage[]>([]);

  show(text: string, type: ToastMessage['type'] = 'info'): void {
    const id = crypto.randomUUID();
    this.messages.update((messages) => [...messages, { id, text, type }]);
    window.setTimeout(() => this.dismiss(id), 4200);
  }

  dismiss(id: string): void {
    this.messages.update((messages) => messages.filter((message) => message.id !== id));
  }
}

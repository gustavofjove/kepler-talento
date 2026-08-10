import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ObservabilityService {
  log(event: string, payload: Record<string, unknown> = {}): string {
    const requestId = this.createRequestId();
    const entry = {
      ts: new Date().toISOString(),
      request_id: requestId,
      event,
      ...payload,
    };
    console.info('[rrhh-observability]', JSON.stringify(entry));
    return requestId;
  }

  private createRequestId(): string {
    if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
      return crypto.randomUUID();
    }
    return `req_${Math.random().toString(36).slice(2, 10)}`;
  }
}

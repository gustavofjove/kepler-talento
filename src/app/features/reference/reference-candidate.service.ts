import { ApiTransport } from '../../core/http/api-transport';
import { signal, type WritableSignal } from '../../core/state/signal';

export interface ReferenceCandidate {
  id: string;
  firstName: string;
  lastName: string;
  isActive: boolean;
}

export class ReferenceCandidateService {
  readonly candidate: WritableSignal<ReferenceCandidate | null> = signal<ReferenceCandidate | null>(
    null,
  );
  readonly loading: WritableSignal<boolean> = signal(false);

  constructor(private readonly transport: ApiTransport) {}

  async load(id: string, abortSignal?: AbortSignal): Promise<ReferenceCandidate> {
    this.loading.set(true);
    try {
      const candidate = await this.transport.request<ReferenceCandidate>(
        `/reference/candidates/${encodeURIComponent(id)}`,
        {
          signal: abortSignal,
        },
      );
      this.candidate.set(candidate);
      return candidate;
    } finally {
      this.loading.set(false);
    }
  }
}

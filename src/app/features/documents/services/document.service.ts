import type { ApiDownload, ApiTransport } from '../../../core/http/api-transport';
import type { CandidateDocument } from '../../candidates/models/candidate.models';
import { CandidateService } from '../../candidates/services/candidate.service';
import type { UploadDocumentRequest } from '../models/document.models';

const MAX_CV_SIZE_BYTES = 20 * 1024 * 1024;
const ALLOWED_EXTENSIONS = new Set([
  'pdf',
  'doc',
  'docx',
  'odt',
  'rtf',
  'txt',
  'jpg',
  'jpeg',
  'png',
  'tif',
  'tiff',
  'bmp',
]);

/**
 * API-backed document content. Client checks are only a courtesy; the API remains the
 * security and validation boundary.
 */
export class DocumentService {
  constructor(
    private readonly candidateService: CandidateService,
    private readonly transport: ApiTransport,
  ) {}

  async upload(request: UploadDocumentRequest): Promise<CandidateDocument> {
    const extension = request.file.name.split('.').pop()?.toLowerCase() ?? '';
    if (!ALLOWED_EXTENSIONS.has(extension)) {
      throw new Error('El tipo de archivo no está permitido.');
    }
    if (request.file.size === 0) throw new Error('El archivo está vacío.');
    if (request.file.size > MAX_CV_SIZE_BYTES) {
      throw new Error('El archivo supera el máximo permitido de 20 MB.');
    }
    const form = new FormData();
    form.append('file', request.file);
    form.append('documentType', request.documentType ?? 'CV');
    form.append('isPrimary', String(request.isPrimary));
    const uploaded = await this.transport.request<CandidateDocument>(
      this.path(request.candidateId),
      {
        method: 'POST',
        body: form,
        timeoutMs: 60_000,
      },
    );
    await this.candidateService.refreshAggregate(request.candidateId);
    return uploaded;
  }

  list(candidateId: string): Promise<CandidateDocument[]> {
    return this.transport.request<CandidateDocument[]>(this.path(candidateId));
  }

  get(candidateId: string, documentId: string, signal?: AbortSignal): Promise<CandidateDocument> {
    return this.transport.request<CandidateDocument>(
      `${this.path(candidateId)}/${encodeURIComponent(documentId)}`,
      { signal },
    );
  }

  async observeUntilSettled(
    candidateId: string,
    documentId: string,
    onUpdate: (document: CandidateDocument) => void,
    signal: AbortSignal,
  ): Promise<CandidateDocument | undefined> {
    for (const delayMs of [1_000, 2_000, 5_000, 5_000, 5_000]) {
      await abortableDelay(delayMs, signal);
      const current = await this.get(candidateId, documentId, signal);
      onUpdate(current);
      if (current.availabilityState !== 'Pending') return current;
    }
    return undefined;
  }

  async setPrimary(candidateId: string, documentId: string): Promise<CandidateDocument> {
    const updated = await this.transport.request<CandidateDocument>(
      `${this.path(candidateId)}/${encodeURIComponent(documentId)}/primary`,
      { method: 'PUT' },
    );
    await this.candidateService.refreshAggregate(candidateId);
    return updated;
  }

  async remove(candidateId: string, documentId: string): Promise<void> {
    await this.transport.request<void>(
      `${this.path(candidateId)}/${encodeURIComponent(documentId)}`,
      { method: 'DELETE' },
    );
    await this.candidateService.refreshAggregate(candidateId);
  }

  async download(
    candidateId: string,
    documentId: string,
    fallbackFileName: string,
  ): Promise<ApiDownload> {
    const result = await this.transport.download(
      `${this.path(candidateId)}/${encodeURIComponent(documentId)}/content`,
      fallbackFileName,
    );
    const url = URL.createObjectURL(result.blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = result.fileName;
    anchor.click();
    URL.revokeObjectURL(url);
    return result;
  }

  private path(candidateId: string): string {
    return `/candidates/${encodeURIComponent(candidateId)}/documents`;
  }
}

function abortableDelay(milliseconds: number, signal: AbortSignal): Promise<void> {
  return new Promise((resolve, reject) => {
    if (signal.aborted) {
      reject(signal.reason);
      return;
    }
    const timeout = window.setTimeout(resolve, milliseconds);
    signal.addEventListener(
      'abort',
      () => {
        window.clearTimeout(timeout);
        reject(signal.reason);
      },
      { once: true },
    );
  });
}

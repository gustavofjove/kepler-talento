import { CandidateDocument } from '../../candidates/models/candidate.models';
import { CandidateService } from '../../candidates/services/candidate.service';
import { SecureDocumentUrl, UploadDocumentRequest } from '../models/document.models';

const MAX_CV_SIZE_BYTES = 10 * 1024 * 1024;

/**
 * Candidate document metadata.
 *
 * Only metadata moves to the API in KTL-8: the file bytes, virus scanning, quarantine and
 * secure download are KTL-9. `createSecureUrl` therefore keeps its placeholder behavior,
 * and the accepted file is validated but not transmitted.
 */
export class DocumentService {
  constructor(private readonly candidateService: CandidateService) {}

  async upload(request: UploadDocumentRequest): Promise<CandidateDocument> {
    if (request.file.type !== 'application/pdf') {
      throw new Error('Solo se permiten documentos PDF para el CV.');
    }
    if (request.file.size > MAX_CV_SIZE_BYTES) {
      throw new Error('El archivo supera el máximo permitido de 10 MB.');
    }
    const document: CandidateDocument = {
      id: crypto.randomUUID(),
      documentType: 'CV',
      originalFilename: request.file.name,
      mimeType: request.file.type,
      sizeBytes: request.file.size,
      isPrimary: request.isPrimary,
      uploadedAt: new Date().toISOString(),
    };
    await this.candidateService.ensureAggregate(request.candidateId);
    await this.candidateService.addDocument(request.candidateId, document);
    // The stored record is the server's, whose identifier and timestamp are authoritative.
    return this.find(request.candidateId, document.originalFilename) ?? document;
  }

  async setPrimary(candidateId: string, documentId: string): Promise<CandidateDocument> {
    const candidate = await this.require(candidateId);
    if (!candidate.documents.some((item) => item.id === documentId)) {
      throw new Error('Documento no encontrado');
    }
    const documents = candidate.documents.map((item) => ({
      ...item,
      isPrimary: item.id === documentId,
    }));
    const updated = await this.candidateService.setDocuments(candidateId, documents);
    const primary = updated.documents.find((item) => item.id === documentId);
    if (!primary) {
      throw new Error('Documento no encontrado');
    }
    return primary;
  }

  async remove(candidateId: string, documentId: string): Promise<void> {
    const candidate = await this.require(candidateId);
    const existing = candidate.documents.find((item) => item.id === documentId);
    if (!existing) {
      throw new Error('Documento no encontrado');
    }

    const remaining = candidate.documents.filter((item) => item.id !== documentId);
    // Removing the principal CV promotes the next document rather than leaving the
    // candidate with none marked, which is today's behavior.
    if (existing.isPrimary && remaining.length > 0 && !remaining.some((item) => item.isPrimary)) {
      remaining[0] = { ...remaining[0], isPrimary: true };
    }

    await this.candidateService.setDocuments(candidateId, remaining);
  }

  /**
   * Placeholder secure access, unchanged. Real storage, scanning and permission-checked
   * download arrive with KTL-9; until then no file bytes exist to serve.
   */
  createSecureUrl(candidateId: string, documentId: string): SecureDocumentUrl {
    const candidate = this.candidateService.find(candidateId);
    const document = candidate?.documents.find((item) => item.id === documentId);
    if (!document) {
      throw new Error('Documento no encontrado');
    }
    return {
      url: URL.createObjectURL(
        new Blob([`Demo local de acceso seguro al CV: ${document.originalFilename}`], {
          type: 'text/plain',
        }),
      ),
      expiresInSeconds: 300,
      documentId,
    };
  }

  private find(candidateId: string, filename: string): CandidateDocument | undefined {
    return this.candidateService
      .find(candidateId)
      ?.documents.find((item) => item.originalFilename === filename);
  }

  private async require(candidateId: string) {
    await this.candidateService.ensureAggregate(candidateId);
    const candidate = this.candidateService.find(candidateId);
    if (!candidate) {
      throw new Error('Candidato no encontrado');
    }
    return candidate;
  }
}

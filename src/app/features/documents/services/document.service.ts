import { CandidateDocument } from '../../candidates/models/candidate.models';
import { CandidateService } from '../../candidates/services/candidate.service';
import { SecureDocumentUrl, UploadDocumentRequest } from '../models/document.models';

const MAX_CV_SIZE_BYTES = 10 * 1024 * 1024;

export class DocumentService {
  constructor(private readonly candidateService: CandidateService) {}

  upload(request: UploadDocumentRequest): CandidateDocument {
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
    this.candidateService.addDocument(request.candidateId, document);
    return document;
  }

  setPrimary(candidateId: string, documentId: string): CandidateDocument {
    const candidate = this.candidateService.find(candidateId);
    if (!candidate) {
      throw new Error('Candidato no encontrado');
    }

    const exists = candidate.documents.some((item) => item.id === documentId);
    if (!exists) {
      throw new Error('Documento no encontrado');
    }

    const documents = candidate.documents.map((item) => ({
      ...item,
      isPrimary: item.id === documentId,
    }));
    this.candidateService.setDocuments(candidateId, documents);

    const updated = documents.find((item) => item.id === documentId);
    if (!updated) {
      throw new Error('Documento no encontrado');
    }
    return updated;
  }

  remove(candidateId: string, documentId: string): void {
    const candidate = this.candidateService.find(candidateId);
    if (!candidate) {
      throw new Error('Candidato no encontrado');
    }

    const existing = candidate.documents.find((item) => item.id === documentId);
    if (!existing) {
      throw new Error('Documento no encontrado');
    }

    const remaining = candidate.documents.filter((item) => item.id !== documentId);
    if (existing.isPrimary && remaining.length > 0 && !remaining.some((item) => item.isPrimary)) {
      remaining[0] = { ...remaining[0], isPrimary: true };
    }

    this.candidateService.setDocuments(candidateId, remaining);
  }

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
}

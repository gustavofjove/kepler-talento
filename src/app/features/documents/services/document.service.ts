import { Injectable } from '@angular/core';
import { CandidateDocument } from '../../candidates/models/candidate.models';
import { CandidateService } from '../../candidates/services/candidate.service';
import { SecureDocumentUrl, UploadDocumentRequest } from '../models/document.models';

@Injectable({ providedIn: 'root' })
export class DocumentService {
  constructor(private readonly candidateService: CandidateService) {}

  upload(request: UploadDocumentRequest): CandidateDocument {
    if (request.file.type !== 'application/pdf') {
      throw new Error('Solo se permiten documentos PDF para el CV.');
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

  createSecureUrl(candidateId: string, documentId: string): SecureDocumentUrl {
    const candidate = this.candidateService.find(candidateId);
    const document = candidate?.documents.find((item) => item.id === documentId);
    if (!document) {
      throw new Error('Documento no encontrado');
    }
    return {
      url: URL.createObjectURL(
        new Blob([`Demo local: ${document.originalFilename}`], { type: 'text/plain' }),
      ),
      expiresInSeconds: 300,
      documentId,
    };
  }
}

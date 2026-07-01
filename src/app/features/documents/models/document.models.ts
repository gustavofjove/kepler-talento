export interface SecureDocumentUrl {
  url: string;
  expiresInSeconds: number;
  documentId: string;
}

export interface UploadDocumentRequest {
  candidateId: string;
  file: File;
  isPrimary: boolean;
}

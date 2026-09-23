export interface UploadDocumentRequest {
  candidateId: string;
  file: File;
  isPrimary: boolean;
  documentType?: string;
}

import { CandidateService } from '../../src/app/features/candidates/services/candidate.service';
import { DocumentService } from '../../src/app/features/documents/services/document.service';
import { EMPTY_CANDIDATE_DRAFT } from '../../src/app/features/candidates/models/candidate.models';

describe('DocumentService', () => {
  let candidateService: CandidateService;
  let documents: DocumentService;
  let candidateId: string;

  beforeEach(() => {
    localStorage.clear();
    candidateService = new CandidateService();
    documents = new DocumentService(candidateService);
    candidateId = candidateService.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Marc',
      lastName: 'Pons',
    }).id;
  });

  it('uploads a PDF CV and records its metadata on the candidate', () => {
    const file = new File(['contenido'], 'cv.pdf', { type: 'application/pdf' });

    const document = documents.upload({ candidateId, file, isPrimary: true });

    expect(document.documentType).toBe('CV');
    expect(document.originalFilename).toBe('cv.pdf');
    expect(document.isPrimary).toBe(true);
    expect(candidateService.find(candidateId)?.documents).toHaveLength(1);
  });

  it('rejects a non-PDF file', () => {
    const file = new File(['contenido'], 'cv.docx', {
      type: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
    });

    expect(() => documents.upload({ candidateId, file, isPrimary: true })).toThrow(
      /solo se permiten/i,
    );
  });

  it('demotes the previous primary CV when a new primary CV is uploaded', () => {
    documents.upload({
      candidateId,
      file: new File(['a'], 'cv-old.pdf', { type: 'application/pdf' }),
      isPrimary: true,
    });
    documents.upload({
      candidateId,
      file: new File(['b'], 'cv-new.pdf', { type: 'application/pdf' }),
      isPrimary: true,
    });

    const primaryDocs = candidateService
      .find(candidateId)!
      .documents.filter((doc) => doc.isPrimary);
    expect(primaryDocs).toHaveLength(1);
    expect(primaryDocs[0].originalFilename).toBe('cv-new.pdf');
  });

  it('creates a time-limited secure URL for an existing document', () => {
    const uploaded = documents.upload({
      candidateId,
      file: new File(['a'], 'cv.pdf', { type: 'application/pdf' }),
      isPrimary: true,
    });

    const secure = documents.createSecureUrl(candidateId, uploaded.id);

    expect(secure.documentId).toBe(uploaded.id);
    expect(secure.expiresInSeconds).toBeGreaterThan(0);
    expect(secure.url).toBeTruthy();
  });

  it('throws when requesting a secure URL for a missing document', () => {
    expect(() => documents.createSecureUrl(candidateId, 'missing-doc')).toThrow(/no encontrado/i);
  });

  it('rejects a file bigger than the allowed size', () => {
    const bigFile = new File(['x'.repeat(11 * 1024 * 1024)], 'huge.pdf', {
      type: 'application/pdf',
    });

    expect(() => documents.upload({ candidateId, file: bigFile, isPrimary: true })).toThrow(
      /10 MB/i,
    );
  });

  it('can mark another document as primary', () => {
    const one = documents.upload({
      candidateId,
      file: new File(['a'], 'one.pdf', { type: 'application/pdf' }),
      isPrimary: true,
    });
    const two = documents.upload({
      candidateId,
      file: new File(['b'], 'two.pdf', { type: 'application/pdf' }),
      isPrimary: false,
    });

    documents.setPrimary(candidateId, two.id);

    const candidate = candidateService.find(candidateId)!;
    expect(candidate.documents.find((doc) => doc.id === one.id)?.isPrimary).toBe(false);
    expect(candidate.documents.find((doc) => doc.id === two.id)?.isPrimary).toBe(true);
  });

  it('removes a document and promotes another one if primary is deleted', () => {
    const one = documents.upload({
      candidateId,
      file: new File(['a'], 'one.pdf', { type: 'application/pdf' }),
      isPrimary: true,
    });
    const two = documents.upload({
      candidateId,
      file: new File(['b'], 'two.pdf', { type: 'application/pdf' }),
      isPrimary: false,
    });

    documents.remove(candidateId, one.id);

    const candidate = candidateService.find(candidateId)!;
    expect(candidate.documents.some((doc) => doc.id === one.id)).toBe(false);
    expect(candidate.documents.find((doc) => doc.id === two.id)?.isPrimary).toBe(true);
  });
});

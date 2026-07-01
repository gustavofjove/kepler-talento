import { TestBed } from '@angular/core/testing';
import { CandidateFormComponent } from '../../src/app/features/candidates/components/candidate-form.component';
import {
  Candidate,
  EMPTY_CANDIDATE_DRAFT,
} from '../../src/app/features/candidates/models/candidate.models';

describe('CandidateFormComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [CandidateFormComponent] });
  });

  it('rejects submission when first name and last name are missing', () => {
    const fixture = TestBed.createComponent(CandidateFormComponent);
    const emitSpy = jest.fn();
    fixture.componentInstance.save.subscribe(emitSpy);
    fixture.componentInstance.draft = { ...EMPTY_CANDIDATE_DRAFT, firstName: '', lastName: '  ' };

    fixture.componentInstance.submit();

    expect(emitSpy).not.toHaveBeenCalled();
    expect(fixture.componentInstance.error).toMatch(/obligatorios/i);
  });

  it('emits the draft when required fields are present', () => {
    const fixture = TestBed.createComponent(CandidateFormComponent);
    const emitSpy = jest.fn();
    fixture.componentInstance.save.subscribe(emitSpy);
    fixture.componentInstance.draft = {
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Sara',
      lastName: 'Pena',
    };

    fixture.componentInstance.submit();

    expect(emitSpy).toHaveBeenCalledWith(
      expect.objectContaining({ firstName: 'Sara', lastName: 'Pena' }),
    );
    expect(fixture.componentInstance.error).toBe('');
  });

  it('loads an existing candidate into the draft and strips relation/audit fields', () => {
    const fixture = TestBed.createComponent(CandidateFormComponent);
    const candidate: Candidate = {
      ...EMPTY_CANDIDATE_DRAFT,
      id: 'c1',
      firstName: 'Ona',
      lastName: 'Marti',
      createdAt: '2026-01-01T00:00:00Z',
      updatedAt: '2026-01-02T00:00:00Z',
      languages: [],
      programs: [],
      education: [],
      experience: [],
      skills: [],
      documents: [],
    };

    fixture.componentInstance.candidate = candidate;

    expect(fixture.componentInstance.draft.firstName).toBe('Ona');
    expect(fixture.componentInstance.draft).not.toHaveProperty('id');
    expect(fixture.componentInstance.draft).not.toHaveProperty('languages');
  });

  it('resets the draft to the empty template when the candidate input is cleared', () => {
    const fixture = TestBed.createComponent(CandidateFormComponent);
    fixture.componentInstance.draft.firstName = 'Cambiado';

    fixture.componentInstance.candidate = undefined;

    expect(fixture.componentInstance.draft.firstName).toBe(EMPTY_CANDIDATE_DRAFT.firstName);
  });
});

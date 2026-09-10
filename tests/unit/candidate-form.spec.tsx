import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CandidateForm } from '../../src/app/features/candidates/components/candidate-form';
import { toDraft } from '../../src/app/features/candidates/components/candidate-form.logic';
import {
  type Candidate,
  EMPTY_CANDIDATE_DRAFT,
} from '../../src/app/features/candidates/models/candidate.models';

describe('toDraft', () => {
  it('loads an existing candidate into the draft and strips relation/audit fields', () => {
    const candidate: Candidate = {
      ...EMPTY_CANDIDATE_DRAFT,
      id: 'c1',
      firstName: 'Ona',
      lastName: 'Marti',
      createdAt: '2026-01-01T00:00:00Z',
      updatedAt: '2026-01-02T00:00:00Z',
      version: 1,
      documentCount: 0,
      primaryDocumentId: null,
      languages: [],
      programs: [],
      education: [],
      experience: [],
      skills: [],
      documents: [],
    };

    const draft = toDraft(candidate);

    expect(draft.firstName).toBe('Ona');
    expect(draft).not.toHaveProperty('id');
    expect(draft).not.toHaveProperty('languages');
    // The version belongs to the service, which carries it into the write; a form that
    // round-tripped a stale one would defeat the conflict check.
    expect(draft).not.toHaveProperty('version');
  });

  it('resets the draft to the empty template when there is no candidate', () => {
    expect(toDraft(undefined).firstName).toBe(EMPTY_CANDIDATE_DRAFT.firstName);
  });
});

describe('CandidateForm', () => {
  it('rejects submission when first name and last name are missing', async () => {
    const onSave = vi.fn();
    render(<CandidateForm onSave={onSave} />);

    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }));

    expect(onSave).not.toHaveBeenCalled();
    expect(screen.getByText(/obligatorios/i)).toBeInTheDocument();
  });

  it('emits the draft when required fields are present', async () => {
    const onSave = vi.fn();
    render(<CandidateForm onSave={onSave} />);

    await userEvent.type(screen.getByLabelText('Nombre'), 'Sara');
    await userEvent.type(screen.getByLabelText('Apellidos'), 'Pena');
    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }));

    expect(onSave).toHaveBeenCalledWith(
      expect.objectContaining({ firstName: 'Sara', lastName: 'Pena' }),
    );
    expect(screen.queryByText(/obligatorios/i)).not.toBeInTheDocument();
  });
});

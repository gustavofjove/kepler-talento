import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CandidateForm } from '../../src/app/features/candidates/components/candidate-form';
import {
  applySuggestion,
  toDraft,
} from '../../src/app/features/candidates/components/candidate-form.logic';
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
      tags: [],
      customNotes: [],
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

  it('holds only the core record: tags are a section of the edit page (KTL-22)', () => {
    const candidate = {
      ...EMPTY_CANDIDATE_DRAFT,
      id: 'c1',
      firstName: 'Ona',
      lastName: 'Marti',
      tags: [{ id: 't1', tag: 'Recontratable' }],
    } as unknown as Candidate;
    render(<CandidateForm candidate={candidate} onSave={vi.fn()} />);

    expect(screen.getByLabelText('Nombre')).toHaveValue('Ona');
    expect(screen.queryByTestId('candidate-tags')).not.toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'En proceso' })).toBeInTheDocument();
  });
});

describe('applySuggestion (KTL-32)', () => {
  it('fills only the empty fields and reports what it used', () => {
    const draft = { ...EMPTY_CANDIDATE_DRAFT, firstName: 'Tecleado', phone: '   ' };

    const result = applySuggestion(draft, {
      firstName: { value: 'Sugerido', confidence: 'high' },
      lastName: { value: 'Pena', confidence: 'low' },
      phone: { value: '611 98 76 54', confidence: 'high' },
    });

    expect(result.draft.firstName).toBe('Tecleado');
    expect(result.draft.lastName).toBe('Pena');
    expect(result.draft.phone).toBe('611 98 76 54');
    expect(Object.keys(result.filled).sort()).toEqual(['lastName', 'phone']);
  });

  it('ignores a blank suggestion', () => {
    const result = applySuggestion(EMPTY_CANDIDATE_DRAFT, {
      email: { value: ' ', confidence: 'high' },
    });

    expect(result.draft.email).toBe('');
    expect(result.filled).toEqual({});
  });
});

describe('CandidateForm with a CV suggestion (KTL-32)', () => {
  const suggestion = {
    nonce: 1,
    fields: {
      firstName: { value: 'Ana', confidence: 'high' as const },
      lastName: { value: 'Ruiz Gil', confidence: 'low' as const },
      email: { value: 'ana@example.test', confidence: 'high' as const },
    },
  };

  it('fills empty fields, marks them, and flags low confidence for review', async () => {
    const applied = vi.fn();
    render(
      <CandidateForm onSave={vi.fn()} suggestion={suggestion} onSuggestionApplied={applied} />,
    );

    expect(await screen.findByLabelText('Nombre')).toHaveValue('Ana');
    expect(screen.getByLabelText('Apellidos')).toHaveValue('Ruiz Gil');
    expect(screen.getByLabelText('Email')).toHaveValue('ana@example.test');
    expect(screen.getByTestId('firstName-suggested')).toHaveTextContent('Sugerido del CV');
    expect(screen.getByTestId('lastName-suggested')).toHaveTextContent('Revisar');
    expect(screen.getByTestId('firstName-suggested')).not.toHaveTextContent('Revisar');
    expect(screen.getByLabelText('Nombre')).toHaveAccessibleDescription(/Sugerido del CV/);
    expect(screen.queryByTestId('phone-suggested')).not.toBeInTheDocument();
    expect(applied).toHaveBeenCalledWith(3);
  });

  it('never overwrites what the user already typed', async () => {
    const applied = vi.fn();
    const { rerender } = render(<CandidateForm onSave={vi.fn()} onSuggestionApplied={applied} />);
    await userEvent.type(screen.getByLabelText('Nombre'), 'Sara');

    rerender(
      <CandidateForm onSave={vi.fn()} suggestion={suggestion} onSuggestionApplied={applied} />,
    );

    await waitFor(() => expect(applied).toHaveBeenCalledWith(2));
    expect(screen.getByLabelText('Nombre')).toHaveValue('Sara');
    expect(screen.queryByTestId('firstName-suggested')).not.toBeInTheDocument();
    expect(screen.getByLabelText('Apellidos')).toHaveValue('Ruiz Gil');
  });

  it('drops the mark once the user edits the suggested value, and saves what the user kept', async () => {
    const onSave = vi.fn();
    render(<CandidateForm onSave={onSave} suggestion={suggestion} />);
    const lastName = await screen.findByLabelText('Apellidos');

    await userEvent.clear(lastName);
    await userEvent.type(lastName, 'Ruiz');
    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }));

    expect(screen.queryByTestId('lastName-suggested')).not.toBeInTheDocument();
    expect(screen.getByTestId('firstName-suggested')).toBeInTheDocument();
    expect(onSave).toHaveBeenCalledWith(
      expect.objectContaining({ firstName: 'Ana', lastName: 'Ruiz', email: 'ana@example.test' }),
    );
  });
});

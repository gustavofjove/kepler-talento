import {
  applyEntryDraft,
  optionsWithCurrent,
  replaceEntry,
} from '../../src/app/features/candidates/components/candidate-entry-form.logic';

describe('candidate entry form logic', () => {
  it('keeps a retired value selectable, first, without duplicating an active one', () => {
    expect(optionsWithCurrent(['Grado', 'Máster'], 'Doctorado')).toEqual([
      'Doctorado',
      'Grado',
      'Máster',
    ]);
    expect(optionsWithCurrent(['Grado', 'Máster'], 'Máster')).toEqual(['Grado', 'Máster']);
    expect(optionsWithCurrent(['Grado'], '')).toEqual(['Grado']);
  });

  it('applies only the fields that changed, keeping the rest of the entry', () => {
    const original: { id: string; company: string; endDate?: string; notes: string } = {
      id: 'x1',
      company: 'Kepler',
      endDate: undefined,
      notes: 'Interna',
    };

    const next = applyEntryDraft(original, { company: 'Acme', endDate: '' });

    expect(next).toEqual({ id: 'x1', company: 'Acme', endDate: undefined, notes: 'Interna' });
    expect(JSON.stringify(applyEntryDraft(original, { company: 'Kepler', endDate: '' }))).toBe(
      JSON.stringify(original),
    );
  });

  it('clears a field the form blanked', () => {
    expect(applyEntryDraft({ id: 'e1', specialty: 'Finanzas' }, { specialty: '' })).toEqual({
      id: 'e1',
      specialty: undefined,
    });
  });

  it('replaces an entry where it stands', () => {
    const list = [
      { id: 'a', value: 1 },
      { id: 'b', value: 2 },
    ];

    expect(replaceEntry(list, { id: 'a', value: 3 })).toEqual([
      { id: 'a', value: 3 },
      { id: 'b', value: 2 },
    ]);
  });
});

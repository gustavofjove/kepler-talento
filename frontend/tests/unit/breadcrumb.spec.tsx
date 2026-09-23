import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { Breadcrumb } from '../../src/app/shared/components/breadcrumb';

describe('Breadcrumb (KTL-23)', () => {
  const renderTrail = (items: Parameters<typeof Breadcrumb>[0]['items']) =>
    render(
      <MemoryRouter>
        <Breadcrumb items={items} />
      </MemoryRouter>,
    );

  it('is a navigation landmark named in Spanish, holding an ordered list', () => {
    renderTrail([
      { label: 'Candidatos', to: '/app/candidates' },
      { label: 'Ona', current: true },
    ]);

    const nav = screen.getByRole('navigation', { name: 'Ruta de navegación' });
    expect(nav).toHaveAttribute('data-testid', 'breadcrumb');
    const list = within(nav).getByRole('list');
    expect(list.tagName).toBe('OL');
    expect(within(list).getAllByRole('listitem')).toHaveLength(2);
  });

  it('renders links, the current page and plain text segments', () => {
    renderTrail([
      { label: 'Admin' },
      { label: 'Presets', to: '/app/admin/presets' },
      { label: 'Mi preset', current: true },
    ]);

    expect(screen.getByRole('link', { name: 'Presets' })).toHaveAttribute(
      'href',
      '/app/admin/presets',
    );
    expect(screen.queryByRole('link', { name: 'Admin' })).not.toBeInTheDocument();
    expect(screen.getByText('Admin')).not.toHaveAttribute('aria-current');
    expect(screen.getByText('Mi preset')).toHaveAttribute('aria-current', 'page');
    expect(screen.getAllByRole('link')).toHaveLength(1);
  });

  it('never links the current page, even when it has a destination', () => {
    renderTrail([{ label: 'Candidatos', to: '/app/candidates', current: true }]);

    expect(screen.queryByRole('link')).not.toBeInTheDocument();
    expect(screen.getByText('Candidatos')).toHaveAttribute('aria-current', 'page');
  });

  it('keeps a parent as a link when no segment is current', () => {
    renderTrail([{ label: 'Candidatos', to: '/app/candidates' }]);

    expect(screen.getByRole('link', { name: 'Candidatos' })).not.toHaveAttribute('aria-current');
  });

  it('passes test ids through and keeps the full label in the title', () => {
    const longName = 'María de las Mercedes Fernández-Villaverde y Pérez de Guzmán';
    renderTrail([
      { label: 'Candidatos', to: '/app/candidates' },
      { label: longName, to: '/app/candidates/c1', testId: 'candidate-edit-view' },
      { label: 'Editar', current: true },
    ]);

    const name = screen.getByTestId('candidate-edit-view');
    expect(name).toHaveAttribute('href', '/app/candidates/c1');
    expect(name).toHaveAttribute('title', longName);
    expect(name).toHaveAccessibleName(longName);
  });
});

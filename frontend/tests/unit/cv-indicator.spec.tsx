import { render, screen } from '@testing-library/react';
import { CvIndicator } from '../../src/app/shared/components/cv-indicator';

describe('CvIndicator (KTL-34)', () => {
  it('shows a tick named «Con CV» when the candidate has a CV', () => {
    render(<CvIndicator hasCv />);

    expect(screen.getByRole('img', { name: 'Con CV' })).toBeInTheDocument();
    expect(screen.queryByText('Sin CV')).toBeNull();
  });

  it('shows no tick and only screen-reader text when the candidate has no CV', () => {
    render(<CvIndicator hasCv={false} />);

    expect(screen.queryByRole('img')).toBeNull();
    expect(screen.getByTestId('cv-indicator-absent')).toHaveTextContent('Sin CV');
    expect(screen.getByTestId('cv-indicator-absent')).toHaveClass('cv-indicator-hidden');
  });

  it('never renders the old «Disponible» / «Pendiente» copy', () => {
    const { container } = render(
      <>
        <CvIndicator hasCv />
        <CvIndicator hasCv={false} />
      </>,
    );

    expect(container).not.toHaveTextContent(/Disponible|Pendiente/);
  });
});

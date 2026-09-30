import { render, screen } from '@testing-library/react';
import { FormError } from '../../src/app/shared/components/form-error';

describe('FormError (KTL-34)', () => {
  it('announces the message as an alert with the red error style', () => {
    render(<FormError message="Nombre y apellidos son obligatorios." testId="the-error" />);

    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('Nombre y apellidos son obligatorios.');
    expect(alert).toHaveClass('form-error');
    expect(alert).not.toHaveClass('empty-state');
    expect(alert).not.toHaveClass('muted');
    expect(alert).toHaveAttribute('data-testid', 'the-error');
  });

  it('keeps extra classes such as span-all', () => {
    render(<FormError message="Error" className="span-all" />);

    expect(screen.getByRole('alert')).toHaveClass('form-error', 'span-all');
  });

  it('renders nothing without a message', () => {
    const { container } = render(
      <>
        <FormError message="" />
        <FormError message={null} />
        <FormError message={undefined} />
      </>,
    );

    expect(container).toBeEmptyDOMElement();
  });
});

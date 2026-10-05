import { render, screen } from '@testing-library/react';
import { StatusChip, type ChipTone } from '../../src/app/shared/components/status-chip';
import { AvailabilityCell } from '../../src/app/features/candidates/components/availability-cell';

describe('StatusChip', () => {
  it.each<ChipTone>(['success', 'danger', 'neutral'])(
    'renders a noninteractive %s label',
    (tone) => {
      render(
        <StatusChip tone={tone} testId="chip">
          Label
        </StatusChip>,
      );
      const chip = screen.getByTestId('chip');
      expect(chip).toHaveClass('badge', `badge--${tone}`);
      expect(chip).toHaveAttribute('data-tone', tone);
      expect(chip).toHaveTextContent('Label');
      expect(chip).not.toHaveAttribute('role');
      expect(chip).not.toHaveAttribute('tabindex');
      expect(screen.queryByRole('button')).toBeNull();
    },
  );
});

describe('AvailabilityCell', () => {
  it.each([
    ['unknown', 'neutral', null],
    ['available', 'success', '2026-03-01'],
    ['unavailable', 'danger', '2026-03-01'],
  ] as const)('renders %s with separate elapsed text', (state, tone, checkedOn) => {
    render(<AvailabilityCell state={state} checkedOn={checkedOn} testIdPrefix="availability" />);
    expect(screen.getByTestId('availability-chip')).toHaveAttribute('data-tone', tone);
    expect(screen.getByTestId('availability-cell')).toContainElement(
      screen.getByTestId('availability-chip'),
    );
    if (checkedOn) expect(screen.getByTestId('availability-elapsed')).toBeVisible();
    else expect(screen.queryByTestId('availability-elapsed')).toBeNull();
  });
});

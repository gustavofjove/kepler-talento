import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CvDraftPicker } from '../../src/app/features/candidates/components/cv-draft-picker';

describe('CvDraftPicker attach choice (KTL-42)', () => {
  it('renders no attach choice without the attach prop', () => {
    render(<CvDraftPicker busy={false} status={null} onPick={vi.fn()} />);

    expect(screen.queryByTestId('cv-draft-attach')).not.toBeInTheDocument();
  });

  it('renders the choice as a named checkbox and reports changes', async () => {
    const onChange = vi.fn();
    render(
      <CvDraftPicker
        busy={false}
        status={null}
        onPick={vi.fn()}
        attach={{ checked: true, onChange }}
      />,
    );

    const choice = screen.getByRole('checkbox', { name: 'Adjuntar este CV al candidato' });
    expect(choice).toHaveAttribute('name', 'attachCv');
    expect(choice).toBeChecked();

    await userEvent.click(choice);

    expect(onChange).toHaveBeenCalledWith(false);
  });

  it('disables the choice while a CV is being read', () => {
    render(
      <CvDraftPicker
        busy
        status={null}
        onPick={vi.fn()}
        attach={{ checked: true, onChange: vi.fn() }}
      />,
    );

    expect(screen.getByTestId('cv-draft-attach')).toBeDisabled();
  });
});

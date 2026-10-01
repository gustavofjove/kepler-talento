import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { MemoryRouter } from 'react-router';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { signal } from '../../src/app/core/state/signal';
import {
  RowCvButton,
  RowCvInlineRow,
  RowCvPreviewProvider,
  RowCvSplit,
} from '../../src/app/features/candidates/components/row-cv-preview/row-cv-preview';
import { useRowCvTable } from '../../src/app/features/candidates/components/row-cv-preview/row-cv-preview.context';
import { CV_CLOSE_MS } from '../../src/app/features/candidates/components/row-cv-preview/row-cv-preview.logic';
import type { CandidateDocument } from '../../src/app/features/candidates/models/candidate.models';

const pdf = (candidateId: string): CandidateDocument => ({
  id: `doc-${candidateId}`,
  documentType: 'CV',
  originalFilename: 'cv.pdf',
  mimeType: 'application/pdf',
  sizeBytes: 10,
  isPrimary: true,
  uploadedAt: '2026-01-01T00:00:00Z',
  availabilityState: 'Available',
});

/** jsdom has no layout: the test drives the width the provider observes. */
class FakeResizeObserver {
  static instances: FakeResizeObserver[] = [];
  constructor(private readonly callback: ResizeObserverCallback) {
    FakeResizeObserver.instances.push(this);
  }
  observe(): void {}
  disconnect(): void {}
  resize(width: number): void {
    this.callback(
      [{ contentRect: { width } } as unknown as ResizeObserverEntry],
      this as unknown as ResizeObserver,
    );
  }
}

const resizeTo = (width: number) =>
  act(() => FakeResizeObserver.instances.forEach((observer) => observer.resize(width)));

interface Row {
  candidateId: string;
  name: string;
}

/** A minimal table wired like the real ones. */
function Table({ tableId, rows }: { tableId: string; rows: Row[] }) {
  const cv = useRowCvTable(
    tableId,
    rows.map((row) => row.candidateId),
  );
  return (
    <table data-testid={`table-${tableId}`}>
      <tbody>
        {rows.map((row) => [
          <tr
            key={row.candidateId}
            ref={cv.rowRef(row.candidateId)}
            className={cv.isOpen(row.candidateId) ? 'is-cv-open' : undefined}
            data-testid={`row-${tableId}-${row.candidateId}`}
          >
            <td>{row.name}</td>
            {cv.enabled ? (
              <td>
                <RowCvButton tableId={tableId} candidateId={row.candidateId} name={row.name} />
              </td>
            ) : null}
          </tr>,
          <RowCvInlineRow
            key={`${row.candidateId}-cv`}
            tableId={tableId}
            candidateId={row.candidateId}
            colSpan={2}
          />,
        ])}
      </tbody>
    </table>
  );
}

const ana: Row = { candidateId: 'c-1', name: 'Ana García' };
const eva: Row = { candidateId: 'c-2', name: 'Eva Ruiz' };

describe('Row CV preview (KTL-35)', () => {
  const openPreview = vi.fn();
  const list = vi.fn();
  let setMatches: (rows: Row[]) => void = () => undefined;

  function Page() {
    const [matches, setRows] = useState<Row[]>([ana, eva]);
    setMatches = setRows;
    return (
      <RowCvPreviewProvider>
        <RowCvSplit>
          <Table tableId="linked" rows={[ana]} />
          <Table tableId="matches" rows={matches} />
        </RowCvSplit>
      </RowCvPreviewProvider>
    );
  }

  const renderPage = (granted: string[] = ['documents.download']) =>
    render(
      <ServicesProvider
        value={
          {
            ...services,
            authService: {
              profile: signal(null),
              hasPermission: (permission: string) => granted.includes(permission),
            },
            documentService: {
              openPreview,
              list,
              download: vi.fn(),
              observeUntilSettled: vi.fn(),
            },
          } as unknown as Services
        }
      >
        <MemoryRouter>
          <Page />
        </MemoryRouter>
      </ServicesProvider>,
    );

  const toggle = (tableId: string, candidateId: string) =>
    within(screen.getByTestId(`row-${tableId}-${candidateId}`)).getByTestId('row-cv-toggle');

  beforeEach(() => {
    vi.clearAllMocks();
    FakeResizeObserver.instances = [];
    vi.stubGlobal('ResizeObserver', FakeResizeObserver);
    vi.stubGlobal('URL', {
      ...URL,
      createObjectURL: vi.fn(() => 'blob:cv'),
      revokeObjectURL: vi.fn(),
    });
    list.mockImplementation(async (candidateId: string) => [pdf(candidateId)]);
    openPreview.mockResolvedValue({
      blob: new Blob(['pdf']),
      fileName: 'cv.pdf',
      contentType: 'application/pdf',
    });
    Element.prototype.scrollIntoView = vi.fn();
  });

  afterEach(() => vi.unstubAllGlobals());

  it('toggles «Ver» and «Ocultar» with the expanded state and a named label', async () => {
    renderPage();
    const button = toggle('matches', 'c-1');
    // Icon-only: an eye and the direction the CV opens (under the row until measured wide).
    expect(button).toHaveTextContent(/^↓$/);
    expect(button).toHaveAccessibleName('Ver el CV de Ana García');
    expect(button).toHaveAttribute('aria-expanded', 'false');

    await userEvent.click(button);

    expect(button).toHaveTextContent(/^↑$/);
    expect(button).toHaveAccessibleName('Ocultar el CV de Ana García');
    expect(button).toHaveAttribute('aria-expanded', 'true');
    expect(button).toHaveAttribute('aria-controls', 'row-cv-matches-c-1');
    expect(screen.getByTestId('row-matches-c-1')).toHaveClass('is-cv-open');
    expect(await screen.findByRole('heading', { name: 'CV de Ana García' })).toBeInTheDocument();

    await userEvent.click(button);

    expect(button).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByTestId('row-cv-panel')).toBeNull();
    expect(screen.getByTestId('row-matches-c-1')).not.toHaveClass('is-cv-open');
  });

  it('keeps one CV open across both tables and toggles only the clicked table', async () => {
    renderPage();

    await userEvent.click(toggle('linked', 'c-1'));
    await userEvent.click(toggle('matches', 'c-1'));

    // The same candidate is in both tables: only the second click's row is open.
    expect(toggle('linked', 'c-1')).toHaveAttribute('aria-expanded', 'false');
    expect(toggle('matches', 'c-1')).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByTestId('row-linked-c-1')).not.toHaveClass('is-cv-open');
    expect(screen.getAllByTestId('row-cv-panel')).toHaveLength(1);

    await userEvent.click(toggle('matches', 'c-2'));

    expect(toggle('matches', 'c-1')).toHaveAttribute('aria-expanded', 'false');
    expect(screen.getAllByTestId('row-cv-panel')).toHaveLength(1);
    expect(await screen.findByRole('heading', { name: 'CV de Eva Ruiz' })).toBeInTheDocument();
  });

  it('shows the CV under its row when narrow and beside the tables when wide', async () => {
    renderPage();
    resizeTo(1200);
    await userEvent.click(toggle('matches', 'c-1'));

    const inline = screen.getByTestId('row-cv-row');
    expect(screen.getByTestId('row-matches-c-1').nextElementSibling).toBe(inline);
    expect(document.querySelector('.page-split__aside')).toBeEmptyDOMElement();

    resizeTo(1400);

    expect(screen.queryByTestId('row-cv-row')).toBeNull();
    expect(
      within(document.querySelector<HTMLElement>('.page-split__aside')!).getByTestId(
        'row-cv-panel',
      ),
    ).toBeInTheDocument();
  });

  it('moves between placements without requesting the content again', async () => {
    renderPage();
    resizeTo(1600);
    // Beside the table: «Ver» points right, «Ocultar» back left.
    expect(toggle('matches', 'c-1')).toHaveTextContent('→');
    await userEvent.click(toggle('matches', 'c-1'));
    expect(toggle('matches', 'c-1')).toHaveTextContent('←');
    await screen.findByTestId('cv-preview-viewer');
    expect(openPreview).toHaveBeenCalledOnce();

    resizeTo(1000);

    await within(screen.getByTestId('row-cv-row')).findByTestId('cv-preview-viewer');
    expect(toggle('matches', 'c-1')).toHaveTextContent('↑');
    expect(screen.getByTestId('row-matches-c-1').scrollIntoView).toHaveBeenCalled();
    expect(openPreview).toHaveBeenCalledOnce();
  });

  it('closes from the panel and returns focus to the row button', async () => {
    renderPage();
    await userEvent.click(toggle('matches', 'c-2'));

    await userEvent.click(await screen.findByTestId('row-cv-hide'));

    expect(screen.queryByTestId('row-cv-panel')).toBeNull();
    expect(toggle('matches', 'c-2')).toHaveFocus();
  });

  it('closes when the open row leaves its table', async () => {
    renderPage();
    await userEvent.click(toggle('matches', 'c-2'));
    expect(screen.getByTestId('row-cv-panel')).toBeInTheDocument();

    act(() => setMatches([ana]));

    expect(screen.queryByTestId('row-cv-panel')).toBeNull();
    // The linked table still has its own rows; nothing reopens.
    expect(toggle('linked', 'c-1')).toHaveAttribute('aria-expanded', 'false');
  });

  it('plays the hide animation before removing the CV, and reopens without waiting', async () => {
    vi.stubGlobal('matchMedia', (query: string) => ({ matches: false, media: query }));
    renderPage();
    const button = toggle('matches', 'c-1');
    await userEvent.click(button);
    expect(screen.getByTestId('row-cv-panel')).not.toHaveClass('is-closing');

    vi.useFakeTimers();
    try {
      act(() => button.click());
      // Already hidden for the row, still on screen while it animates.
      expect(button).toHaveAttribute('aria-expanded', 'false');
      expect(screen.getByTestId('row-matches-c-1')).not.toHaveClass('is-cv-open');
      expect(screen.getByTestId('row-cv-panel')).toHaveClass('is-closing');

      act(() => vi.advanceTimersByTime(CV_CLOSE_MS));
      expect(screen.queryByTestId('row-cv-panel')).toBeNull();

      // Opening another CV mid-animation is immediate and cancels the pending removal.
      act(() => button.click());
      act(() => button.click());
      act(() => toggle('matches', 'c-2').click());
      act(() => vi.advanceTimersByTime(CV_CLOSE_MS));
      expect(screen.getByTestId('row-cv-panel')).not.toHaveClass('is-closing');
      expect(toggle('matches', 'c-2')).toHaveAttribute('aria-expanded', 'true');
    } finally {
      vi.useRealTimers();
    }
  });

  it('offers no CV column without the download permission', () => {
    renderPage([]);
    expect(screen.queryByTestId('row-cv-toggle')).toBeNull();
  });
});

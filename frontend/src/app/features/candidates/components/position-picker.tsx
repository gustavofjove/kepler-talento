import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useServices } from '../../../core/di/services-context';
import { useErrorToast } from '../../../core/services/use-error-toast';
import { PickerDialog } from '../../positions/components/picker-dialog';
import type { PositionListItem } from '../../positions/position.models';

const PICKER_PAGE_SIZE = 10;
const PICKER_DEBOUNCE_MS = 250;

interface PositionPickerProps {
  linkedIds: ReadonlySet<string>;
  onSelect: (positionId: string) => Promise<void>;
  onClose: () => void;
}

type PickerStatus = 'loading' | 'ready' | 'error';

/** Adds the candidate to an open position, whatever its requirements (KTL-30). */
export function PositionPicker({ linkedIds, onSelect, onClose }: PositionPickerProps) {
  const { t } = useTranslation();
  const { positionService } = useServices();
  const notifyError = useErrorToast();
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<PositionListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [status, setStatus] = useState<PickerStatus>('loading');
  const [adding, setAdding] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    const timer = setTimeout(() => {
      setStatus('loading');
      positionService
        .search(
          {
            status: 'open',
            text: query.trim(),
            pageSize: PICKER_PAGE_SIZE,
            sortField: 'title',
            sortDirection: 'asc',
          },
          controller.signal,
        )
        .then((page) => {
          if (controller.signal.aborted) return;
          setResults(page.items);
          setTotal(page.totalCount);
          setStatus('ready');
        })
        .catch(() => {
          if (!controller.signal.aborted) setStatus('error');
        });
    }, PICKER_DEBOUNCE_MS);
    return () => {
      clearTimeout(timer);
      controller.abort();
    };
  }, [positionService, query]);

  const select = async (positionId: string): Promise<void> => {
    setAdding(positionId);
    try {
      await onSelect(positionId);
      onClose();
    } catch (error) {
      notifyError(error, t('candidate.profile.positions.addError'));
      setAdding(null);
    }
  };

  const message =
    status === 'loading'
      ? t('candidate.profile.positions.picker.searching')
      : status === 'error'
        ? t('candidate.profile.positions.picker.error')
        : results.length === 0
          ? t('candidate.profile.positions.picker.empty')
          : null;

  return (
    <PickerDialog
      title={t('candidate.profile.positions.picker.title')}
      searchLabel={t('candidate.profile.positions.picker.search')}
      closeLabel={t('candidate.profile.positions.picker.close')}
      query={query}
      onQueryChange={setQuery}
      onClose={onClose}
      testId="position-picker"
    >
      {message ? (
        <p className="muted" role="status">
          {message}
        </p>
      ) : (
        <>
          <ul className="picker-list" data-testid="position-picker-results">
            {results.map((position) => {
              const linked = linkedIds.has(position.id);
              return (
                <li key={position.id}>
                  <button
                    className="picker-option"
                    type="button"
                    data-testid="position-picker-option"
                    aria-disabled={linked || adding !== null}
                    disabled={adding !== null && adding !== position.id}
                    onClick={() => {
                      if (!linked && adding === null) void select(position.id);
                    }}
                  >
                    <span className="picker-option-name">{position.title}</span>
                    {position.location ? <span className="muted">{position.location}</span> : null}
                    {linked ? (
                      <span className="badge">
                        {t('candidate.profile.positions.picker.linked')}
                      </span>
                    ) : null}
                  </button>
                </li>
              );
            })}
          </ul>
          {total > results.length ? (
            <p className="muted">
              {t('candidate.profile.positions.picker.more', { shown: results.length, total })}
            </p>
          ) : null}
        </>
      )}
    </PickerDialog>
  );
}

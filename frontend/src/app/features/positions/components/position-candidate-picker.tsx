import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useServices } from '../../../core/di/services-context';
import { useErrorToast } from '../../../core/services/use-error-toast';
import { EMPTY_SEARCH_FILTERS, type SearchResult } from '../../search/models/search.models';
import { PickerDialog } from './picker-dialog';

const PICKER_PAGE_SIZE = 10;
const PICKER_DEBOUNCE_MS = 250;

interface PositionCandidatePickerProps {
  linkedIds: ReadonlySet<string>;
  onSelect: (candidateId: string) => Promise<void>;
  onClose: () => void;
}

type PickerStatus = 'loading' | 'ready' | 'error';

/**
 * Adds any active candidate to a position, whether or not they meet its requirements (KTL-30).
 * It searches through the existing candidate search, so the same privacy rules apply.
 */
export function PositionCandidatePicker({
  linkedIds,
  onSelect,
  onClose,
}: PositionCandidatePickerProps) {
  const { t } = useTranslation();
  const { candidateSearchService } = useServices();
  const notifyError = useErrorToast();
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<SearchResult[]>([]);
  const [total, setTotal] = useState(0);
  const [status, setStatus] = useState<PickerStatus>('loading');
  const [adding, setAdding] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    const timer = setTimeout(() => {
      setStatus('loading');
      candidateSearchService
        .search(
          { ...EMPTY_SEARCH_FILTERS, text: query.trim() },
          { page: 1, pageSize: PICKER_PAGE_SIZE, signal: controller.signal },
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
  }, [candidateSearchService, query]);

  const select = async (candidateId: string): Promise<void> => {
    setAdding(candidateId);
    try {
      await onSelect(candidateId);
      onClose();
    } catch (error) {
      notifyError(error, t('positions.candidates.addError'));
      setAdding(null);
    }
  };

  const message =
    status === 'loading'
      ? t('positions.candidatePicker.searching')
      : status === 'error'
        ? t('positions.candidatePicker.error')
        : results.length === 0
          ? t('positions.candidatePicker.empty')
          : null;

  return (
    <PickerDialog
      title={t('positions.candidatePicker.title')}
      searchLabel={t('positions.candidatePicker.search')}
      closeLabel={t('positions.candidatePicker.close')}
      hint={t('positions.candidatePicker.hint')}
      query={query}
      onQueryChange={setQuery}
      onClose={onClose}
      testId="position-candidate-picker"
    >
      {message ? (
        <p className="muted" role="status">
          {message}
        </p>
      ) : (
        <>
          <ul className="picker-list" data-testid="position-candidate-picker-results">
            {results.map((result) => {
              const linked = linkedIds.has(result.candidateId);
              const name = `${result.firstName} ${result.lastName}`;
              return (
                <li key={result.candidateId}>
                  <button
                    className="picker-option"
                    type="button"
                    data-testid="position-candidate-picker-option"
                    aria-disabled={linked || adding !== null}
                    disabled={adding !== null && adding !== result.candidateId}
                    onClick={() => {
                      if (!linked && adding === null) void select(result.candidateId);
                    }}
                  >
                    <span className="picker-option-name">{name}</span>
                    {result.email ? <span className="muted">{result.email}</span> : null}
                    {linked ? (
                      <span className="badge">{t('positions.candidatePicker.linked')}</span>
                    ) : null}
                  </button>
                </li>
              );
            })}
          </ul>
          {total > results.length ? (
            <p className="muted">
              {t('positions.candidatePicker.more', { shown: results.length, total })}
            </p>
          ) : null}
        </>
      )}
    </PickerDialog>
  );
}

import { useCallback, useEffect, useRef, useState, type ChangeEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useServices } from '../../../core/di/services-context';
import { formatDate, formatNumber } from '../../../core/i18n/format';
import { TranslatableError } from '../../../core/i18n/translatable-error';
import { useErrorToast } from '../../../core/services/use-error-toast';
import {
  canCommit,
  importErrorKey,
  isRefused,
  isTransient,
  reasonKey,
  ROW_REPORT_PAGE_SIZE,
  rowReportCsv,
} from './import.logic';
import type { ImportBatch, ImportRowOutcome, ImportRowReport } from './import.models';

type Busy = 'upload' | 'commit' | 'open' | null;

/**
 * Importación (KTL-17). Two steps, as before: upload and validate as a dry run, then commit.
 * What changed is that both now happen on the server, can take time, and can really fail — so
 * the page shows the batch's state while it polls, stops polling after a bounded wait, and says
 * why a batch is stuck rather than appearing to hang.
 */
export function ImportPage() {
  const { t } = useTranslation();
  const { importService, toastService, candidateService } = useServices();
  const notifyError = useErrorToast();
  const [file, setFile] = useState<File | null>(null);
  const [batch, setBatch] = useState<ImportBatch | null>(null);
  const [report, setReport] = useState<ImportRowReport | null>(null);
  const [history, setHistory] = useState<ImportBatch[]>([]);
  const [busy, setBusy] = useState<Busy>(null);
  const [stalled, setStalled] = useState(false);
  const polling = useRef<AbortController | null>(null);

  const refreshHistory = useCallback(async () => {
    try {
      setHistory((await importService.listBatches()).items);
    } catch (error) {
      notifyError(error, t('admin.import.errors.history'));
    }
  }, [importService, notifyError, t]);

  useEffect(() => {
    void refreshHistory();
    return () => polling.current?.abort();
  }, [refreshHistory]);

  const loadReport = useCallback(
    async (target: ImportBatch, page: number) => {
      setReport(await importService.getRowReport(target.id, page, ROW_REPORT_PAGE_SIZE));
    },
    [importService],
  );

  const fail = (error: unknown) => {
    if (error instanceof TranslatableError && error.key === 'admin.import.errors.pollTimeout') {
      setStalled(true);
    }
    const key = importErrorKey(error);
    notifyError(key ? new TranslatableError(key) : error, t('admin.import.errors.generic'));
  };

  const settle = async (current: ImportBatch): Promise<ImportBatch> => {
    polling.current?.abort();
    const controller = new AbortController();
    polling.current = controller;
    const settled = await importService.waitUntilSettled(current, {
      signal: controller.signal,
      onProgress: setBatch,
    });
    setBatch(settled);
    return settled;
  };

  const select = (event: ChangeEvent<HTMLInputElement>) => {
    setFile(event.target.files?.[0] ?? null);
  };

  const uploadAndValidate = async () => {
    setBusy('upload');
    setStalled(false);
    setReport(null);
    try {
      let current = await importService.upload(file);
      setBatch(current);
      current = await settle(current);
      if (current.state === 'scanned') {
        current = await importService.validate(current);
        setBatch(current);
        current = await settle(current);
      }
      if (current.state === 'validated') {
        await loadReport(current, 1);
        toastService.show(t('admin.import.validated'), 'info');
      }
    } catch (error) {
      fail(error);
    } finally {
      setBusy(null);
      void refreshHistory();
    }
  };

  const commit = async () => {
    if (!batch) return;
    setBusy('commit');
    setStalled(false);
    try {
      let current = await importService.commit(batch);
      setBatch(current);
      current = await settle(current);
      await loadReport(current, 1);
      if (current.state === 'committed') {
        if (current.loadedRows > 0) {
          // The candidate list is cached for the session; without this the people just created
          // would not appear on Candidatos until a reload. A caller who may import but not read
          // candidates gets a refusal here, which is not an import failure.
          void candidateService.reload().catch(() => undefined);
        }
        toastService.show(
          t('admin.import.committed', { loaded: formatNumber(current.loadedRows) }),
          'success',
        );
      }
    } catch (error) {
      fail(error);
    } finally {
      setBusy(null);
      void refreshHistory();
    }
  };

  const open = async (target: ImportBatch) => {
    setBusy('open');
    setStalled(false);
    setBatch(target);
    try {
      await loadReport(target, 1);
      if (isTransient(target.state)) {
        await settle(target);
      }
    } catch (error) {
      fail(error);
    } finally {
      setBusy(null);
    }
  };

  const changeReportPage = async (page: number) => {
    if (!batch) return;
    try {
      await loadReport(batch, page);
    } catch (error) {
      fail(error);
    }
  };

  const downloadReport = async () => {
    if (!batch) return;
    try {
      const rows: ImportRowOutcome[] = [];
      for (let page = 1; ; page += 1) {
        const chunk = await importService.getRowReport(batch.id, page, 500);
        rows.push(...chunk.items);
        if (rows.length >= chunk.totalCount || chunk.items.length === 0) break;
      }
      const url = URL.createObjectURL(new Blob([rowReportCsv(rows)], { type: 'text/csv' }));
      const link = document.createElement('a');
      link.href = url;
      link.download = 'informe-importacion.csv';
      link.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      fail(error);
    }
  };

  const pageCount = report ? Math.max(1, Math.ceil(report.totalCount / report.pageSize)) : 1;

  return (
    <section className="page">
      <div className="page-header">
        <h1>{t('admin.import.title')}</h1>
        <p className="muted">{t('admin.import.subtitle')}</p>
      </div>

      <div className="panel grid">
        <div className="field">
          <label htmlFor="csv">{t('admin.import.fileLabel')}</label>
          <input
            id="csv"
            name="file"
            type="file"
            accept=".csv,text/csv"
            data-testid="import-file"
            onChange={select}
          />
        </div>
        <p className="muted">{t('admin.import.contract')}</p>
        <p className="muted">{t('admin.import.steps')}</p>
        <button
          className="button"
          type="button"
          data-testid="import-validate"
          disabled={!file || busy !== null}
          onClick={() => void uploadAndValidate()}
        >
          {t('admin.import.validate')}
        </button>
        {busy && batch ? (
          <p className="muted" role="status" data-testid="import-progress">
            {t('admin.import.progress', { state: t(`admin.import.state.${batch.state}`) })}
          </p>
        ) : null}
        {stalled ? (
          <p className="muted" role="alert" data-testid="import-stalled">
            {t('admin.import.stalled')}
          </p>
        ) : null}
      </div>

      {!batch && !busy ? <p className="empty-state">{t('admin.import.empty')}</p> : null}

      {batch ? (
        <div className="panel stack" data-testid="import-summary">
          <h2>{t('admin.import.summary')}</h2>
          <p>
            <strong>{t('admin.import.stateLabel')}</strong>{' '}
            <span className="badge" data-testid="import-state" data-state={batch.state}>
              {t(`admin.import.state.${batch.state}`)}
            </span>
          </p>
          {batch.originalFileName ? (
            <p>
              <strong>{t('admin.import.source')}</strong> {batch.originalFileName}
            </p>
          ) : null}
          {batch.rowCount !== null ? (
            <dl className="grid two">
              <div>
                <dt>{t('admin.import.rows')}</dt>
                <dd data-testid="import-count-rows">{formatNumber(batch.rowCount)}</dd>
              </div>
              <div>
                <dt>
                  {batch.state === 'committed' || batch.committedAt
                    ? t('admin.import.loaded')
                    : t('admin.import.loadable')}
                </dt>
                <dd data-testid="import-count-loaded">{formatNumber(batch.loadedRows)}</dd>
              </div>
              <div>
                <dt>{t('admin.import.rejected')}</dt>
                <dd data-testid="import-count-rejected">{formatNumber(batch.rejectedRows)}</dd>
              </div>
              <div>
                <dt>{t('admin.import.skipped')}</dt>
                <dd data-testid="import-count-skipped">{formatNumber(batch.skippedRows)}</dd>
              </div>
            </dl>
          ) : null}

          {isRefused(batch.state) ? (
            <p role="alert" data-testid="import-refused">
              {t(`admin.import.refusedExplanation.${batch.state}`)}
            </p>
          ) : null}
          {batch.state === 'failed' ? (
            <p role="alert" data-testid="import-failure">
              {t(reasonKey(batch.failureCode), { detail: batch.failureDetail ?? '' })}
            </p>
          ) : null}
          {!batch.fileRetained ? (
            <p className="muted" data-testid="import-file-purged">
              {t('admin.import.filePurged')}
            </p>
          ) : null}
          {batch.sameFileCommittedAt ? (
            <p className="muted" data-testid="import-same-file">
              {t('admin.import.sameFile', { date: formatDate(batch.sameFileCommittedAt) })}
            </p>
          ) : null}
          {batch.unresolvedValues.length ? (
            <div data-testid="import-unresolved">
              <p>{t('admin.import.unresolved')}</p>
              <ul>
                {batch.unresolvedValues.map((value) => (
                  <li key={`${value.family}-${value.value}`}>
                    {t('admin.import.unresolvedValue', {
                      family: t(`admin.import.family.${value.family}`),
                      value: value.value,
                      occurrences: formatNumber(value.occurrences),
                    })}
                  </li>
                ))}
              </ul>
            </div>
          ) : null}

          <div className="toolbar">
            <button
              className="button"
              type="button"
              data-testid="import-commit"
              disabled={!canCommit(batch) || busy !== null}
              onClick={() => void commit()}
            >
              {t('admin.import.commit')}
            </button>
            <button
              className="button secondary"
              type="button"
              data-testid="import-download-report"
              disabled={!report?.totalCount}
              onClick={() => void downloadReport()}
            >
              {t('admin.import.downloadReport')}
            </button>
          </div>
          {batch.state === 'validated' && batch.rejectedRows > 0 ? (
            <p className="muted">{t('admin.import.fixAndRetry')}</p>
          ) : null}

          {report && report.items.length ? (
            <>
              <h3>
                {report.phase === 'commit'
                  ? t('admin.import.reportCommit')
                  : t('admin.import.reportValidation')}
              </h3>
              <div className="table-wrap">
                <table data-testid="import-row-report">
                  <thead>
                    <tr>
                      <th>{t('admin.import.column.row')}</th>
                      <th>{t('admin.import.column.outcome')}</th>
                      <th>{t('admin.import.column.field')}</th>
                      <th>{t('admin.import.column.reason')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {report.items.map((row) => (
                      <tr key={row.rowNumber} data-outcome={row.outcome}>
                        <td>{formatNumber(row.rowNumber)}</td>
                        <td>{t(`admin.import.outcome.${row.outcome}`)}</td>
                        <td>{row.field ?? ''}</td>
                        <td>
                          {row.reasonCode ? t(reasonKey(row.reasonCode), { detail: '' }) : ''}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              {pageCount > 1 ? (
                <div className="toolbar">
                  <button
                    className="button secondary"
                    type="button"
                    disabled={report.page <= 1}
                    onClick={() => void changeReportPage(report.page - 1)}
                  >
                    {t('admin.import.previous')}
                  </button>
                  <span className="muted">
                    {t('admin.import.pageOf', {
                      page: formatNumber(report.page),
                      pages: formatNumber(pageCount),
                    })}
                  </span>
                  <button
                    className="button secondary"
                    type="button"
                    disabled={report.page >= pageCount}
                    onClick={() => void changeReportPage(report.page + 1)}
                  >
                    {t('admin.import.next')}
                  </button>
                </div>
              ) : null}
            </>
          ) : null}
        </div>
      ) : null}

      <div className="panel stack">
        <h2>{t('admin.import.history')}</h2>
        {history.length ? (
          <div className="table-wrap">
            <table data-testid="import-history">
              <thead>
                <tr>
                  <th>{t('admin.import.column.date')}</th>
                  <th>{t('admin.import.column.file')}</th>
                  <th>{t('admin.import.column.state')}</th>
                  <th>{t('admin.import.column.counts')}</th>
                  <th>{t('admin.common.actions')}</th>
                </tr>
              </thead>
              <tbody>
                {history.map((item) => (
                  <tr key={item.id}>
                    <td>
                      {formatDate(item.createdAt, { dateStyle: 'short', timeStyle: 'short' })}
                    </td>
                    <td>{item.originalFileName ?? t('admin.import.otherUploader')}</td>
                    <td>
                      <span className="badge" data-state={item.state}>
                        {t(`admin.import.state.${item.state}`)}
                      </span>
                    </td>
                    <td>
                      {t('admin.import.countsSummary', {
                        loaded: formatNumber(item.loadedRows),
                        rejected: formatNumber(item.rejectedRows),
                        skipped: formatNumber(item.skippedRows),
                      })}
                    </td>
                    <td>
                      <button
                        className="button secondary"
                        type="button"
                        data-testid={`import-history-open-${item.id}`}
                        disabled={busy !== null}
                        onClick={() => void open(item)}
                      >
                        {t('admin.import.open')}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <p className="empty-state">{t('admin.import.historyEmpty')}</p>
        )}
      </div>
    </section>
  );
}

import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useBlocker, useParams } from 'react-router';
import { usePermission, useServices } from '../../../core/di/services-context';
import { Breadcrumb, type BreadcrumbItem } from '../../../shared/components/breadcrumb';
import { useErrorToast } from '../../../core/services/use-error-toast';
import { CandidateCvPreview } from '../components/candidate-cv-preview';
import { CandidateDocuments } from '../components/candidate-documents';
import { CandidateEducation } from '../components/candidate-education';
import { CandidateExperience } from '../components/candidate-experience';
import { CandidateMainPanel } from '../components/candidate-main-panel';
import { CandidateNotes } from '../components/candidate-notes';
import { CandidatePanel } from '../components/candidate-panel';
import { CandidatePositionsPanel } from '../components/candidate-positions-panel';
import type { PanelControl, PanelId } from '../components/candidate-panel.logic';
import { CandidateCompetencies } from '../components/candidate-competencies';
import { candidateFullName } from '../candidate-name';
import { mailtoHref, telHref } from '../contact-links';
import { useCandidate } from '../use-candidates';

export function CandidateDetailPage() {
  const { t } = useTranslation();
  const { id: candidateId = '' } = useParams<{ id: string }>();
  const { confirmDialogService } = useServices();
  const notifyError = useErrorToast();
  const candidateService = useCandidate(candidateId);
  const item = candidateService.find(candidateId);
  const canEdit = usePermission('candidates.update');
  const canUpload = usePermission('documents.upload');
  const aggregate = candidateService.aggregateStatus(candidateId);
  const canReadList = usePermission('candidates.read');
  const canReadPositions = usePermission('positions.read');
  // The list is offered in every state, even while loading or after a failure; the name only
  // once the candidate has loaded.
  const trail: BreadcrumbItem[] = [
    {
      label: t('breadcrumb.candidates'),
      to: canReadList ? '/app/candidates' : undefined,
      testId: 'breadcrumb-candidates',
    },
  ];
  if (aggregate !== 'loading' && item)
    trail.push({ label: candidateFullName(item), current: true });

  // KTL-29: one page, one panel in edit mode at a time. Each panel reports whether it holds
  // unsaved changes; only the open panel's report counts (design D1, D6).
  const [requested, setRequested] = useState<PanelId | null>(null);
  const [dirtyPanels, setDirtyPanels] = useState<Partial<Record<PanelId, boolean>>>({});

  // Who may open each panel mirrors the API (D7). The API refuses relation and note writes on
  // a removed candidate, so those panels offer no «Editar» then; the core record and documents
  // keep following their permissions.
  const isActive = item?.isActive ?? false;
  const allowed: Record<PanelId, boolean> = {
    main: canEdit,
    competencies: canEdit && isActive,
    education: canEdit && isActive,
    experience: canEdit && isActive,
    notes: canEdit && isActive,
    documents: canUpload,
  };
  // A panel that stops being allowed while open (the candidate is removed, say) closes at
  // once, in the same render, so its editor never outlives the permission.
  const editing = requested !== null && allowed[requested] ? requested : null;
  const dirty = editing !== null && Boolean(dirtyPanels[editing]);
  useEffect(() => {
    if (requested !== null && editing === null) setRequested(null);
  }, [requested, editing]);

  const reportDirty = useMemo(() => {
    const report =
      (id: PanelId) =>
      (value: boolean): void =>
        setDirtyPanels((current) =>
          current[id] === value ? current : { ...current, [id]: value },
        );
    return {
      main: report('main'),
      competencies: report('competencies'),
      education: report('education'),
      experience: report('experience'),
      notes: report('notes'),
      documents: report('documents'),
    } satisfies Record<PanelId, (value: boolean) => void>;
  }, []);

  const confirmDiscard = useCallback(
    (leaving: boolean): Promise<boolean> =>
      confirmDialogService.confirm({
        title: t(leaving ? 'candidate.panel.leaveTitle' : 'candidate.panel.discardTitle'),
        message: t(leaving ? 'candidate.panel.leaveMessage' : 'candidate.panel.discardMessage'),
        confirmText: t('candidate.panel.discardConfirm'),
        cancelText: t('candidate.panel.keepEditing'),
        danger: true,
      }),
    [confirmDialogService, t],
  );

  const open = async (id: PanelId): Promise<void> => {
    if (editing === id) return;
    if (dirty && !(await confirmDiscard(false))) return;
    setRequested(id);
  };
  const close = useCallback(() => setRequested(null), []);

  // Leaving the page with unsaved changes asks first: in-app navigation through the router,
  // reload and tab close through the browser's own prompt.
  const blocker = useBlocker(dirty);
  useEffect(() => {
    if (blocker.state !== 'blocked') return;
    void confirmDiscard(true).then((leave) => (leave ? blocker.proceed() : blocker.reset()));
  }, [blocker, confirmDiscard]);
  useEffect(() => {
    if (!dirty) return;
    const warn = (event: BeforeUnloadEvent): void => event.preventDefault();
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [dirty]);

  const control = (id: PanelId): PanelControl => ({
    editing: editing === id,
    canEdit: allowed[id],
    onEdit: () => void open(id),
    onClose: close,
    onDirtyChange: reportDirty[id],
  });

  const setActive = async (active: boolean): Promise<void> => {
    if (!item) return;
    const confirmed = await confirmDialogService.confirm({
      title: t(active ? 'candidate.detail.activateTitle' : 'candidate.detail.deactivateTitle'),
      message: t(
        active ? 'candidate.detail.activateMessage' : 'candidate.detail.deactivateMessage',
      ),
      confirmText: t(active ? 'candidate.detail.activate' : 'candidate.detail.deactivate'),
      cancelText: t('candidate.detail.cancel'),
      danger: !active,
    });
    if (!confirmed) return;
    try {
      if (active) await candidateService.reactivate(item.id);
      else await candidateService.deactivate(item.id);
    } catch (error) {
      notifyError(error, t('candidate.detail.stateFailure'));
    }
  };

  if (aggregate === 'loading')
    return (
      <>
        <Breadcrumb items={trail} />
        <section className="panel">
          <p className="empty-state">{t('candidate.detail.loading')}</p>
        </section>
      </>
    );
  if (aggregate === 'error')
    return (
      <>
        <Breadcrumb items={trail} />
        <section className="panel" data-testid="candidate-detail-error">
          <h1>{t('candidate.detail.loadFailure')}</h1>
          <p className="empty-state">
            {candidateService.error?.message ?? t('candidate.detail.tryLater')}
          </p>
          <Link className="button" to="/app/candidates">
            {t('candidate.detail.back')}
          </Link>
        </section>
      </>
    );
  if (!item)
    return (
      <>
        <Breadcrumb items={trail} />
        <section className="panel">
          <h1>{t('candidate.detail.notFound')}</h1>
          <Link className="button" to="/app/candidates">
            {t('candidate.detail.back')}
          </Link>
        </section>
      </>
    );

  return (
    <section className="page">
      <Breadcrumb items={trail} />
      <div className="toolbar">
        <div className="page-header">
          <h1>
            {item.firstName} {item.lastName}
          </h1>
          <p className="muted contact-links">
            {item.email ? <a href={mailtoHref(item.email)}>{item.email}</a> : null}
            {item.email && item.phone ? ' · ' : null}
            {item.phone ? <a href={telHref(item.phone)}>{item.phone}</a> : null}
          </p>
        </div>
        <div className="toolbar">
          {canEdit ? (
            <>
              <button
                className={item.isActive ? 'button danger' : 'button secondary'}
                type="button"
                onClick={() => void setActive(!item.isActive)}
              >
                {t(
                  item.isActive
                    ? 'candidate.detail.logicalDeactivate'
                    : 'candidate.detail.logicalActivate',
                )}
              </button>
            </>
          ) : null}
        </div>
      </div>
      {canEdit && !isActive ? (
        <p className="muted" data-testid="candidate-removed-hint">
          {t('candidate.detail.removedHint')}
        </p>
      ) : null}
      {/* One full-width panel per row, in reading order (KTL-27). Each editable panel switches
          to its editor in place (KTL-29). The CV preview sits beside the panels on wide
          screens and after them otherwise (KTL-28). */}
      <div className="page-split">
        <div className="page-split__layout">
          <div className="page-split__main">
            <div className="grid">
              <CandidateMainPanel candidate={item} control={control('main')} />
              <article className="panel">
                <h2>{t('candidate.detail.audit')}</h2>
                <dl className="prop-list">
                  <dt>{t('candidate.detail.created')}</dt>
                  <dd>{item.createdAt.slice(0, 19)}</dd>
                  <dt>{t('candidate.detail.updated')}</dt>
                  <dd>{item.updatedAt.slice(0, 19)}</dd>
                  <dt>{t('candidate.detail.active')}</dt>
                  <dd data-testid="candidate-active">
                    {t(item.isActive ? 'candidate.detail.yes' : 'candidate.detail.no')}
                  </dd>
                </dl>
              </article>
              <CandidateCompetencies candidate={item} control={control('competencies')} />
              <CandidateEducation candidate={item} control={control('education')} />
              <CandidateExperience candidate={item} control={control('experience')} />
              {/* KTL-30: acts immediately, so it stays outside the edit-mode coordinator. */}
              {canReadPositions ? (
                <CandidatePositionsPanel candidateId={item.id} candidateIsActive={isActive} />
              ) : null}
              <CandidatePanel
                id="notes"
                title={t('candidate.profile.notes.title')}
                control={control('notes')}
                mode="actions"
              >
                <CandidateNotes
                  candidateId={item.id}
                  initialNotes={item.customNotes}
                  readOnly={editing !== 'notes'}
                  onDirtyChange={reportDirty.notes}
                />
              </CandidatePanel>
              <CandidatePanel
                id="documents"
                title={t('candidate.profile.documents.title')}
                control={control('documents')}
                mode="actions"
              >
                <CandidateDocuments
                  candidate={item}
                  readOnly={editing !== 'documents'}
                  onDirtyChange={reportDirty.documents}
                />
              </CandidatePanel>
            </div>
          </div>
          <aside className="page-split__aside">
            <CandidateCvPreview candidate={item} />
          </aside>
        </div>
      </div>
    </section>
  );
}

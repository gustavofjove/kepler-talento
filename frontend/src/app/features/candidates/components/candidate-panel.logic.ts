/** The candidate page panels that can be put in edit mode (KTL-29). Auditoría never is. */
export type PanelId = 'main' | 'competencies' | 'education' | 'experience' | 'notes' | 'documents';

/**
 * What the candidate page hands each editable panel. The page owns which panel is open; the
 * panel owns its draft and reports whether it holds unsaved changes (design D1).
 */
export interface PanelControl {
  editing: boolean;
  canEdit: boolean;
  onEdit: () => void;
  onClose: () => void;
  onDirtyChange: (dirty: boolean) => void;
}

/** Test identifiers of a panel's controls, stable across the unit and e2e suites. */
export const panelTestIds = (id: PanelId) => ({
  edit: `candidate-panel-${id}-edit`,
  save: `candidate-panel-${id}-save`,
  cancel: `candidate-panel-${id}-cancel`,
  done: `candidate-panel-${id}-done`,
});

/** The first control a user would operate inside a panel body, for focus on entering edit. */
export const FIRST_CONTROL =
  'input:not([type="hidden"]):not([disabled]), select:not([disabled]), textarea:not([disabled]), button:not([disabled])';

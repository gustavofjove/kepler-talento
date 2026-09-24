import {
  useContext,
  useEffect,
  useId,
  useMemo,
  useRef,
  useState,
  type FocusEvent,
  type KeyboardEvent,
  type ReactNode,
  type RefObject,
} from 'react';
import {
  Button,
  ComboBox,
  ComboBoxStateContext,
  Dialog,
  Input,
  type InputProps,
  type Key,
  ListBox,
  ListBoxItem,
  Popover,
  Select,
  SelectValue,
  Tag,
  TagGroup,
  TagList,
  ToggleButton,
  ToggleButtonGroup,
} from 'react-aria-components';
import { useTranslation } from 'react-i18next';
import {
  filterOptions,
  MAX_TOGGLE_LEVELS,
  pickerTestIds,
  type PickerItem,
} from './catalog-value-picker.logic';
import './catalog-value-picker.css';

export interface CatalogValuePickerProps {
  /** Prefix of every test identifier and of the input's `name`, e.g. `search-skill`. */
  idPrefix: string;
  /** Names the chip list and, unless hidden, heads the picker. */
  label: string;
  /** Hide the label visually when the host already shows a heading for it. */
  labelHidden?: boolean;
  /** The input's accessible name and placeholder, e.g. «Añadir idioma…». */
  addLabel: string;
  /** Active names of the value family, in catalog order. */
  valueOptions: string[];
  /** Active names of the level family, in catalog order. Absent for families without levels. */
  levelOptions?: string[];
  levelMode?: 'optional' | 'required';
  /** Chip and option text for an empty level in an optional-level picker. */
  anyLevelLabel?: string;
  items: PickerItem[];
  /** Shown instead of the chips while there are none. */
  emptyText?: string;
  readOnly?: boolean;
  disabled?: boolean;
  /** Rendered next to the label, e.g. the search mode toggle. */
  headerAction?: ReactNode;
  /** Extra chip text for an item's details, e.g. a certification. */
  detailText?: (item: PickerItem) => string | undefined;
  /** Family-specific fields inside the chip editor. `set` changes the editor's draft only. */
  renderDetails?: (draft: PickerItem, set: (next: PickerItem) => void) => ReactNode;
  onAdd: (item: PickerItem) => void;
  onChange: (item: PickerItem) => void;
  onRemove: (item: PickerItem) => void;
  onRetry?: (item: PickerItem) => void;
}

/** Key of the one uncommitted item a required-level picker holds while its level is chosen. */
const DRAFT_KEY = '__catalog-picker-draft__';
/** Collection key standing for the empty level, which react-aria cannot key on. */
const ANY_LEVEL = '__any-level__';

/**
 * The single control for choosing business-catalog values: a type-to-filter input, one chip
 * per value, and an editor on the chip for its level and details.
 *
 * Purely presentational. The host owns the items and hears synchronous intents; an async host
 * reflects its progress back through each item's `status`. The picker keeps only transient UI
 * state: the typed text, which chip's editor is open, and - for a required level - the one
 * value being added until its level is chosen, so no host can receive a partial entry.
 */
export function CatalogValuePicker({
  idPrefix,
  label,
  labelHidden = false,
  addLabel,
  valueOptions,
  levelOptions,
  levelMode = 'optional',
  anyLevelLabel = '',
  items,
  emptyText,
  readOnly = false,
  disabled = false,
  headerAction,
  detailText,
  renderDetails,
  onAdd,
  onChange,
  onRemove,
  onRetry,
}: CatalogValuePickerProps) {
  const { t } = useTranslation();
  const ids = pickerTestIds(idPrefix);
  const labelId = useId();
  const [query, setQuery] = useState('');
  const [draft, setDraft] = useState<PickerItem | null>(null);
  const [editingKey, setEditingKey] = useState<string | null>(null);
  const chipRefs = useRef(new Map<string, HTMLDivElement>());
  const inputRef = useRef<HTMLInputElement>(null);
  const refocusInput = useRef(false);
  const [comboKey, setComboKey] = useState(0);

  // The input exists only while adding; at rest the picker shows a (+) button in its place.
  const [adding, setAdding] = useState(false);
  const addButtonRef = useRef<HTMLButtonElement>(null);
  const openListOnMount = useRef(false);
  const focusAddButton = useRef(false);
  const editingRef = useRef<string | null>(null);

  useEffect(() => {
    if (!refocusInput.current) return;
    refocusInput.current = false;
    inputRef.current?.focus();
  }, [comboKey]);

  useEffect(() => {
    editingRef.current = editingKey;
  }, [editingKey]);

  useEffect(() => {
    if (adding || !focusAddButton.current) return;
    focusAddButton.current = false;
    addButtonRef.current?.focus();
  }, [adding]);

  const startAdding = (): void => {
    openListOnMount.current = true;
    setAdding(true);
  };

  const collapse = (focusButton: boolean): void => {
    focusAddButton.current = focusButton;
    setQuery('');
    setAdding(false);
  };

  // Back to (+) once focus has settled somewhere other than the input or its list. The level
  // editor of a value being added also takes focus, but the input stays for the next value.
  const collapseWhenIdle = (): void => {
    requestAnimationFrame(() => {
      if (editingRef.current === DRAFT_KEY) return;
      const active = document.activeElement;
      if (active === inputRef.current || active?.closest('.catalog-picker-popover')) return;
      collapse(false);
    });
  };
  const hasLevels = levelOptions !== undefined;
  const required = hasLevels && levelMode === 'required';

  const shown = useMemo(() => (draft ? [...items, draft] : items), [items, draft]);
  const offered = useMemo(
    () => filterOptions(valueOptions, query, shown).map((value) => ({ id: value })),
    [valueOptions, query, shown],
  );
  const editing =
    editingKey === DRAFT_KEY ? draft : (items.find((item) => item.key === editingKey) ?? null);

  // Read when the popover positions itself, after the chip it anchors to has mounted.
  const anchorRef = useMemo<RefObject<HTMLElement | null>>(
    () => ({
      get current() {
        return editingKey ? (chipRefs.current.get(editingKey) ?? null) : null;
      },
    }),
    [editingKey],
  );

  const closeEditor = (): void => {
    if (editingKey === DRAFT_KEY) {
      setDraft(null);
      // The draft chip the editor would return focus to is gone once it closes, so focus would
      // fall to the page. Put it back in the input, ready for the next value, after react-aria
      // has finished restoring - but never take it from wherever the user has clicked.
      requestAnimationFrame(() =>
        requestAnimationFrame(() => {
          const active = document.activeElement;
          if (!active || active === document.body) inputRef.current?.focus();
        }),
      );
    }
    setEditingKey(null);
  };

  const choose = (key: Key | null): void => {
    if (key === null) return;
    const value = String(key);
    setQuery('');
    if (required) {
      setDraft({ key: DRAFT_KEY, value, level: '', details: {} });
      setEditingKey(DRAFT_KEY);
      return;
    }
    onAdd({ key: value, value, level: '' });
    // With a controlled selection react-aria leaves the list open after a choice; a fresh
    // combobox closes it, and focus goes back to its input for the next value.
    refocusInput.current = true;
    setComboKey((current) => current + 1);
  };

  const commit = (item: PickerItem): void => {
    if (item.key === DRAFT_KEY) {
      const { key: _draftKey, ...rest } = item;
      onAdd({ ...rest, key: item.value });
      setDraft(null);
    } else {
      onChange(item);
    }
  };

  const remove = (keys: Set<Key>): void => {
    for (const key of keys) {
      if (key === DRAFT_KEY) {
        closeEditor();
        continue;
      }
      const item = items.find((candidate) => candidate.key === key);
      if (item) onRemove(item);
    }
  };

  const failed = items.filter((item) => item.status === 'error');
  const canEdit = hasLevels && !readOnly && !disabled;

  return (
    <div
      className="catalog-picker"
      data-testid={ids.root}
      data-readonly={readOnly || undefined}
      data-disabled={disabled || undefined}
    >
      <div className="catalog-picker-row">
        <span id={labelId} className="catalog-picker-label" data-hidden={labelHidden || undefined}>
          {label}
        </span>
        {headerAction}

        {shown.length ? (
          <TagGroup
            aria-labelledby={labelId}
            className="catalog-picker-chips"
            onRemove={readOnly ? undefined : remove}
          >
            <TagList items={shown} className="catalog-picker-chip-list">
              {(item) => (
                <Tag
                  id={item.key}
                  textValue={chipText(item, hasLevels, anyLevelLabel)}
                  ref={(element: HTMLDivElement | null) => {
                    if (element) chipRefs.current.set(item.key, element);
                    else chipRefs.current.delete(item.key);
                  }}
                  className="catalog-picker-chip"
                  data-testid={ids.chip}
                  data-value={item.value}
                  data-status={item.status}
                  data-draft={item.key === DRAFT_KEY || undefined}
                  onAction={canEdit ? () => setEditingKey(item.key) : undefined}
                >
                  <span className="catalog-picker-chip-value">{item.value}</span>
                  {hasLevels && item.key !== DRAFT_KEY ? (
                    <span className="catalog-picker-chip-level">{item.level || anyLevelLabel}</span>
                  ) : null}
                  {item.key !== DRAFT_KEY && detailText?.(item) ? (
                    <span className="catalog-picker-chip-detail">{detailText(item)}</span>
                  ) : null}
                  {item.status === 'pending' ? (
                    <span className="catalog-picker-chip-pending">
                      {t('catalogPicker.pending')}
                    </span>
                  ) : null}
                  {readOnly ? null : (
                    <RemoveButton
                      label={t('catalogPicker.remove', { value: item.value })}
                      testId={ids.remove}
                    />
                  )}
                </Tag>
              )}
            </TagList>
          </TagGroup>
        ) : null}

        {readOnly ? null : adding ? (
          <ComboBox
            key={comboKey}
            aria-label={addLabel}
            className="catalog-picker-combo"
            items={offered}
            inputValue={query}
            onInputChange={setQuery}
            selectedKey={null}
            onSelectionChange={choose}
            // Filtering is ours: accent-insensitive and excluding values already held.
            defaultFilter={() => true}
            allowsEmptyCollection
            isDisabled={disabled}
          >
            <PickerInput
              ref={inputRef}
              className="catalog-picker-input"
              name={idPrefix}
              data-testid={ids.input}
              placeholder={addLabel}
              openOnMount={openListOnMount}
              onBlur={collapseWhenIdle}
              onIdleEscape={() => collapse(true)}
            />
            <Popover className="catalog-picker-popover" placement="bottom start">
              <ListBox
                className="catalog-picker-options"
                renderEmptyState={() => (
                  <p className="catalog-picker-no-results">{t('catalogPicker.noResults')}</p>
                )}
              >
                {(option: { id: string }) => (
                  <ListBoxItem
                    id={option.id}
                    textValue={option.id}
                    className="catalog-picker-option"
                  >
                    {option.id}
                  </ListBoxItem>
                )}
              </ListBox>
            </Popover>
          </ComboBox>
        ) : (
          <button
            ref={addButtonRef}
            type="button"
            className="catalog-picker-add"
            data-testid={ids.add}
            aria-label={addLabel}
            title={addLabel}
            disabled={disabled}
            onClick={startAdding}
          >
            <span aria-hidden="true">+</span>
          </button>
        )}
      </div>

      {/* Where values can be added the (+) says enough; a read-only empty picker says why. */}
      {!shown.length && readOnly && emptyText ? (
        <p className="empty-state catalog-picker-empty-state">{emptyText}</p>
      ) : null}

      {failed.length ? (
        <ul className="catalog-picker-errors">
          {failed.map((item) => (
            <li key={item.key} role="alert">
              <span>{t('catalogPicker.error', { value: item.value, message: item.error })}</span>
              {onRetry ? (
                <button
                  className="button ghost small"
                  type="button"
                  data-testid={ids.retry}
                  aria-label={t('catalogPicker.retryValue', { value: item.value })}
                  onClick={() => onRetry(item)}
                >
                  {t('catalogPicker.retry')}
                </button>
              ) : null}
            </li>
          ))}
        </ul>
      ) : null}

      {editing && canEdit ? (
        <Popover
          triggerRef={anchorRef}
          isOpen
          onOpenChange={(open) => {
            if (!open) closeEditor();
          }}
          placement="bottom start"
          className="catalog-picker-popover catalog-picker-editor-popover"
        >
          <Dialog
            className="catalog-picker-editor"
            aria-label={t('catalogPicker.editor', { value: editing.value })}
            data-testid={ids.editor}
          >
            <ChipEditor
              key={editing.key}
              item={editing}
              isDraft={editing.key === DRAFT_KEY}
              levelOptions={levelOptions ?? []}
              optional={!required}
              anyLevelLabel={anyLevelLabel}
              renderDetails={renderDetails}
              onCommit={commit}
              onClose={closeEditor}
            />
          </Dialog>
        </Popover>
      ) : null}
    </div>
  );
}

interface PickerInputProps extends InputProps {
  ref: RefObject<HTMLInputElement | null>;
  /** Set by the (+) button: focus the input and show the whole list once it mounts. */
  openOnMount: RefObject<boolean>;
  /** Escape pressed while the list is already closed. */
  onIdleEscape: () => void;
}

/**
 * The combobox input. It opens the full list when revealed by (+) or clicked; react-aria alone
 * opens it only on typing or ArrowDown, and its `menuTrigger="focus"` would also fire on the
 * programmatic refocus after each add and undo the close.
 */
function PickerInput({ ref, openOnMount, onIdleEscape, ...props }: PickerInputProps) {
  const state = useContext(ComboBoxStateContext);

  // Once, when revealed: remounts after an add keep the list closed.
  useEffect(() => {
    if (openOnMount.current) ref.current?.focus();
  }, [openOnMount, ref]);

  return (
    <Input
      {...props}
      ref={ref}
      onFocus={() => {
        // Opened from the focus event, once react-aria has registered the focus itself.
        if (!openOnMount.current || !state) return;
        openOnMount.current = false;
        state.open(null, 'manual');
      }}
      onClick={() => {
        if (state && !state.isOpen) state.open(null, 'manual');
      }}
      onKeyDown={(event) => {
        // react-aria's own Escape runs first and closes the list; a second one leaves adding.
        if (event.key === 'Escape' && state && !state.isOpen) onIdleEscape();
      }}
    />
  );
}

/**
 * react-aria labels a tag's remove button with its own label plus the whole tag's, which
 * would read the value twice. The label is pointed at a single hidden sentence instead.
 */
function RemoveButton({ label, testId }: { label: string; testId: string }) {
  const labelId = useId();
  return (
    <Button
      slot="remove"
      className="catalog-picker-chip-remove"
      aria-labelledby={labelId}
      data-testid={testId}
    >
      <span aria-hidden="true">×</span>
      <span id={labelId} hidden>
        {label}
      </span>
    </Button>
  );
}

interface ChipEditorProps {
  item: PickerItem;
  isDraft: boolean;
  levelOptions: string[];
  optional: boolean;
  anyLevelLabel: string;
  renderDetails?: CatalogValuePickerProps['renderDetails'];
  onCommit: (item: PickerItem) => void;
  onClose: () => void;
}

/**
 * The level and details of one chip. Choosing a level commits the whole draft and closes;
 * detail fields commit when focus leaves them or on Enter, once the item has a level.
 */
function ChipEditor({
  item,
  isDraft,
  levelOptions,
  optional,
  anyLevelLabel,
  renderDetails,
  onCommit,
  onClose,
}: ChipEditorProps) {
  const { t } = useTranslation();
  const [current, setCurrent] = useState(item);
  const [committed, setCommitted] = useState(item);
  const levels = optional
    ? [{ id: ANY_LEVEL, name: anyLevelLabel }, ...levelOptions.map((name) => ({ id: name, name }))]
    : levelOptions.map((name) => ({ id: name, name }));
  const selectedLevel = current.level || (optional ? ANY_LEVEL : null);
  const hasLevel = optional || Boolean(current.level);

  const chooseLevel = (key: Key | null | undefined): void => {
    if (key === null || key === undefined) return;
    const level = key === ANY_LEVEL ? '' : String(key);
    onCommit({ ...current, level });
    onClose();
  };

  const commitDetails = (): void => {
    if (isDraft || !hasLevel || sameItem(current, committed)) return;
    onCommit(current);
    setCommitted(current);
  };

  // Enter in a detail field saves and closes, like submitting a one-field form. The editor is
  // deliberately not a <form>: it is portalled, and React would bubble its submit into the
  // host's own form (the search form runs the search on submit).
  const confirmDetails = (event: KeyboardEvent<HTMLDivElement>): void => {
    if (event.key !== 'Enter' || !(event.target instanceof HTMLInputElement)) return;
    event.preventDefault();
    if (isDraft || !hasLevel) return;
    commitDetails();
    onClose();
  };

  const leaveDetails = (event: FocusEvent<HTMLDivElement>): void => {
    if (!event.currentTarget.contains(event.relatedTarget as Node | null)) commitDetails();
  };

  return (
    <div className="catalog-picker-editor-form">
      {levelOptions.length > MAX_TOGGLE_LEVELS ? (
        <Select
          aria-label={t('catalogPicker.level')}
          className="catalog-picker-level-select"
          selectedKey={selectedLevel}
          onSelectionChange={chooseLevel}
          placeholder={t('catalogPicker.level')}
        >
          <Button className="catalog-picker-select-button">
            <SelectValue />
          </Button>
          <Popover className="catalog-picker-popover" placement="bottom start">
            <ListBox className="catalog-picker-options" items={levels}>
              {(level) => (
                <ListBoxItem id={level.id} textValue={level.name} className="catalog-picker-option">
                  {level.name}
                </ListBoxItem>
              )}
            </ListBox>
          </Popover>
        </Select>
      ) : (
        <ToggleButtonGroup
          aria-label={t('catalogPicker.level')}
          className="catalog-picker-levels"
          selectionMode="single"
          disallowEmptySelection
          selectedKeys={selectedLevel ? [selectedLevel] : []}
          onSelectionChange={(keys) => chooseLevel([...keys][0])}
        >
          {levels.map((level, index) => (
            <ToggleButton
              key={level.id}
              id={level.id}
              className="catalog-picker-level"
              autoFocus={selectedLevel ? level.id === selectedLevel : index === 0}
            >
              {level.name}
            </ToggleButton>
          ))}
        </ToggleButtonGroup>
      )}
      {isDraft ? (
        <p className="catalog-picker-hint">
          {t('catalogPicker.levelRequired', { value: item.value })}
        </p>
      ) : null}
      {renderDetails ? (
        <div className="catalog-picker-details" onBlur={leaveDetails} onKeyDown={confirmDetails}>
          {renderDetails(current, setCurrent)}
        </div>
      ) : null}
    </div>
  );
}

function chipText(item: PickerItem, hasLevels: boolean, anyLevelLabel: string): string {
  const level = hasLevels ? item.level || anyLevelLabel : '';
  return level ? `${item.value} ${level}` : item.value;
}

function sameItem(a: PickerItem, b: PickerItem): boolean {
  return a.level === b.level && JSON.stringify(a.details ?? {}) === JSON.stringify(b.details ?? {});
}

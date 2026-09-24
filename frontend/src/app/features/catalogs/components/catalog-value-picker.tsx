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
  levelLabel?: string;
  formatLevel?: (level: string) => string;
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

/** Collection key standing for the empty level, which react-aria cannot key on. */
const ANY_LEVEL = '__any-level__';

/**
 * The single control for choosing business-catalog values: a type-to-filter input, one chip
 * per value, and an editor on the chip for its level and details.
 *
 * Purely presentational. The host owns the items and hears synchronous intents; an async host
 * reflects its progress back through each item's `status`. The picker keeps only transient UI
 * state: the typed text and which chip's editor is open. A required level is never left empty:
 * a new item starts at the lowest level (the first option, in catalog order), and a picker
 * with no level to offer cannot add.
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
  levelLabel,
  formatLevel,
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

  useEffect(() => {
    if (!refocusInput.current) return;
    refocusInput.current = false;
    inputRef.current?.focus();
  }, [comboKey]);

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

  // Back to (+) once focus has settled somewhere other than the input or its list.
  const collapseWhenIdle = (): void => {
    requestAnimationFrame(() => {
      const active = document.activeElement;
      if (active === inputRef.current || active?.closest('.catalog-picker-popover')) return;
      collapse(false);
    });
  };
  const hasLevels = levelOptions !== undefined;
  const required = hasLevels && levelMode === 'required';
  // A required level with nothing to offer would add an entry without one.
  const addDisabled = disabled || (required && !levelOptions?.length);

  const offered = useMemo(
    () => filterOptions(valueOptions, query, items).map((value) => ({ id: value })),
    [valueOptions, query, items],
  );
  const editing = items.find((item) => item.key === editingKey) ?? null;

  // Read when the popover positions itself, after the chip it anchors to has mounted.
  const anchorRef = useMemo<RefObject<HTMLElement | null>>(
    () => ({
      get current() {
        return editingKey ? (chipRefs.current.get(editingKey) ?? null) : null;
      },
    }),
    [editingKey],
  );

  const closeEditor = (): void => setEditingKey(null);

  const choose = (key: Key | null): void => {
    if (key === null) return;
    const value = String(key);
    setQuery('');
    onAdd(
      required
        ? { key: value, value, level: levelOptions?.[0] ?? '', details: {} }
        : { key: value, value, level: '' },
    );
    // With a controlled selection react-aria leaves the list open after a choice; a fresh
    // combobox closes it, and focus goes back to its input for the next value.
    refocusInput.current = true;
    setComboKey((current) => current + 1);
  };

  const remove = (keys: Set<Key>): void => {
    for (const key of keys) {
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

        {items.length ? (
          <TagGroup
            aria-labelledby={labelId}
            className="catalog-picker-chips"
            onRemove={readOnly ? undefined : remove}
          >
            <TagList items={items} className="catalog-picker-chip-list">
              {(item) => (
                <Tag
                  id={item.key}
                  textValue={chipText(item, hasLevels, anyLevelLabel, formatLevel)}
                  ref={(element: HTMLDivElement | null) => {
                    if (element) chipRefs.current.set(item.key, element);
                    else chipRefs.current.delete(item.key);
                  }}
                  className="catalog-picker-chip"
                  data-testid={ids.chip}
                  data-value={item.value}
                  data-status={item.status}
                  onAction={canEdit ? () => setEditingKey(item.key) : undefined}
                >
                  <span className="catalog-picker-chip-value">{item.value}</span>
                  {hasLevels ? (
                    <span className="catalog-picker-chip-level">
                      {item.level ? (formatLevel?.(item.level) ?? item.level) : anyLevelLabel}
                    </span>
                  ) : null}
                  {detailText?.(item) ? (
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
            isDisabled={addDisabled}
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
            disabled={addDisabled}
            onClick={startAdding}
          >
            <span aria-hidden="true">+</span>
          </button>
        )}
      </div>

      {/* Where values can be added the (+) says enough; a read-only empty picker says why. */}
      {!items.length && readOnly && emptyText ? (
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
              levelOptions={levelOptions ?? []}
              optional={!required}
              anyLevelLabel={anyLevelLabel}
              levelLabel={levelLabel}
              renderDetails={renderDetails}
              onCommit={onChange}
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
  levelOptions: string[];
  optional: boolean;
  anyLevelLabel: string;
  levelLabel?: string;
  renderDetails?: CatalogValuePickerProps['renderDetails'];
  onCommit: (item: PickerItem) => void;
  onClose: () => void;
}

/**
 * The level and details of one chip. Choosing a level commits the item and closes; detail
 * fields commit when focus leaves them or on Enter, once the item has a level.
 */
function ChipEditor({
  item,
  levelOptions,
  optional,
  anyLevelLabel,
  levelLabel,
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
    if (!hasLevel || sameItem(current, committed)) return;
    onCommit(current);
    setCommitted(current);
  };

  // Enter in a detail field saves and closes, like submitting a one-field form. The editor is
  // deliberately not a <form>: it is portalled, and React would bubble its submit into the
  // host's own form (the search form runs the search on submit).
  const confirmDetails = (event: KeyboardEvent<HTMLDivElement>): void => {
    if (event.key !== 'Enter' || !(event.target instanceof HTMLInputElement)) return;
    event.preventDefault();
    if (!hasLevel) return;
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
          aria-label={levelLabel ?? t('catalogPicker.level')}
          className="catalog-picker-level-select"
          selectedKey={selectedLevel}
          onSelectionChange={chooseLevel}
          placeholder={levelLabel ?? t('catalogPicker.level')}
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
          aria-label={levelLabel ?? t('catalogPicker.level')}
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
      {renderDetails ? (
        <div className="catalog-picker-details" onBlur={leaveDetails} onKeyDown={confirmDetails}>
          {renderDetails(current, setCurrent)}
        </div>
      ) : null}
    </div>
  );
}

function chipText(
  item: PickerItem,
  hasLevels: boolean,
  anyLevelLabel: string,
  formatLevel?: (level: string) => string,
): string {
  const level = hasLevels
    ? item.level
      ? (formatLevel?.(item.level) ?? item.level)
      : anyLevelLabel
    : '';
  return level ? `${item.value} ${level}` : item.value;
}

function sameItem(a: PickerItem, b: PickerItem): boolean {
  return a.level === b.level && JSON.stringify(a.details ?? {}) === JSON.stringify(b.details ?? {});
}

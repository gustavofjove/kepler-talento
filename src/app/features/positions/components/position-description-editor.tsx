import { $generateHtmlFromNodes, $generateNodesFromDOM } from '@lexical/html';
import {
  ListItemNode,
  ListNode,
  INSERT_ORDERED_LIST_COMMAND,
  INSERT_UNORDERED_LIST_COMMAND,
} from '@lexical/list';
import { LexicalComposer } from '@lexical/react/LexicalComposer';
import { ContentEditable } from '@lexical/react/LexicalContentEditable';
import { HistoryPlugin } from '@lexical/react/LexicalHistoryPlugin';
import { ListPlugin } from '@lexical/react/LexicalListPlugin';
import { OnChangePlugin } from '@lexical/react/LexicalOnChangePlugin';
import { RichTextPlugin } from '@lexical/react/LexicalRichTextPlugin';
import { useLexicalComposerContext } from '@lexical/react/LexicalComposerContext';
import { $getRoot, $insertNodes, FORMAT_TEXT_COMMAND, type EditorState } from 'lexical';
import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { BoldIcon, BulletListIcon, ItalicIcon, NumberedListIcon } from './position-editor-icons';

const COLLAPSED_DESCRIPTION_HEIGHT = 350;

function Toolbar() {
  const [editor] = useLexicalComposerContext();
  const { t } = useTranslation();
  return (
    <div
      className="position-editor-toolbar"
      role="toolbar"
      aria-label={t('positions.description.toolbar')}
    >
      <button
        type="button"
        name="descriptionBold"
        onClick={() => editor.dispatchCommand(FORMAT_TEXT_COMMAND, 'bold')}
      >
        <BoldIcon />
        <span>{t('positions.description.bold')}</span>
      </button>
      <button
        type="button"
        name="descriptionItalic"
        onClick={() => editor.dispatchCommand(FORMAT_TEXT_COMMAND, 'italic')}
      >
        <ItalicIcon />
        <span>{t('positions.description.italic')}</span>
      </button>
      <button
        type="button"
        name="descriptionBullets"
        onClick={() => editor.dispatchCommand(INSERT_UNORDERED_LIST_COMMAND, undefined)}
      >
        <BulletListIcon />
        <span>{t('positions.description.bullets')}</span>
      </button>
      <button
        type="button"
        name="descriptionNumbers"
        onClick={() => editor.dispatchCommand(INSERT_ORDERED_LIST_COMMAND, undefined)}
      >
        <NumberedListIcon />
        <span>{t('positions.description.numbers')}</span>
      </button>
    </div>
  );
}

export function PositionDescriptionEditor({
  value,
  onChange,
}: {
  value: string;
  onChange: (html: string) => void;
}) {
  const { t } = useTranslation();
  return (
    <LexicalComposer
      initialConfig={{
        namespace: 'PositionDescription',
        nodes: [ListNode, ListItemNode],
        onError: (error) => {
          throw error;
        },
        editorState: (editor) => {
          if (!value) return;
          const document = new DOMParser().parseFromString(value, 'text/html');
          $getRoot().clear();
          $insertNodes($generateNodesFromDOM(editor, document));
        },
      }}
    >
      <div className="position-editor" data-testid="position-description-editor">
        <Toolbar />
        {/* The placeholder is positioned against this body, not the editor, so a toolbar that
            wraps onto several rows at narrow widths never covers it. */}
        <div className="position-editor-body">
          <RichTextPlugin
            contentEditable={
              <ContentEditable
                name="description"
                aria-label={t('positions.form.description')}
                className="position-editor-input"
              />
            }
            placeholder={
              <span className="position-editor-placeholder">
                {t('positions.description.placeholder')}
              </span>
            }
            ErrorBoundary={({ children }) => children}
          />
        </div>
        <HistoryPlugin />
        <ListPlugin />
        <OnChangePlugin
          onChange={(state: EditorState, editor) =>
            state.read(() => onChange($generateHtmlFromNodes(editor)))
          }
        />
      </div>
    </LexicalComposer>
  );
}

export function PositionDescription({ html }: { html: string }) {
  const { t } = useTranslation();
  const descriptionRef = useRef<HTMLDivElement>(null);
  const [expanded, setExpanded] = useState(false);
  const [overflows, setOverflows] = useState(false);

  const toggleExpanded = () => {
    if (!expanded) {
      setExpanded(true);
      return;
    }
    setExpanded(false);
    window.requestAnimationFrame(() =>
      descriptionRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' }),
    );
  };

  useEffect(() => {
    const description = descriptionRef.current;
    if (!description) return;
    const measure = () => setOverflows(description.scrollHeight > COLLAPSED_DESCRIPTION_HEIGHT);
    measure();
    if (typeof ResizeObserver === 'undefined') return;
    const observer = new ResizeObserver(measure);
    observer.observe(description);
    return () => observer.disconnect();
  }, [html]);

  return (
    <div className={`position-description-shell${expanded ? ' is-expanded' : ''}`}>
      <div
        ref={descriptionRef}
        className="position-description"
        data-testid="position-description"
        dangerouslySetInnerHTML={{ __html: html }}
      />
      {overflows ? (
        <div className="position-description-toggle">
          <button type="button" aria-expanded={expanded} onClick={toggleExpanded}>
            {t(expanded ? 'positions.description.showLess' : 'positions.description.showMore')}
          </button>
        </div>
      ) : null}
    </div>
  );
}

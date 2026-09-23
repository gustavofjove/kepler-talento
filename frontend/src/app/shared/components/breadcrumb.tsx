import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import './breadcrumb.css';

export interface BreadcrumbItem {
  /** Already translated copy, or loaded record data such as a candidate's name. */
  label: string;
  /** Rendered as a link only when set. The page omits it when the viewer may not open it. */
  to?: string;
  /**
   * Marks the segment naming the current page. It is explicit rather than "the last item"
   * because a page still loading its record ends on a parent, which must stay a link.
   */
  current?: boolean;
  testId?: string;
}

/**
 * A presentational trail: which segments link, and where, is decided by the page, which
 * already holds the permissions and the loaded record. Separators are drawn in CSS so that
 * assistive technology does not announce them.
 */
export function Breadcrumb({ items }: { items: BreadcrumbItem[] }) {
  const { t } = useTranslation();
  return (
    <nav className="breadcrumb" aria-label={t('breadcrumb.ariaLabel')} data-testid="breadcrumb">
      <ol>
        {items.map((item, index) => (
          <li key={index}>
            {item.current ? (
              <span aria-current="page" title={item.label} data-testid={item.testId}>
                {item.label}
              </span>
            ) : item.to ? (
              <Link to={item.to} title={item.label} data-testid={item.testId}>
                {item.label}
              </Link>
            ) : (
              <span title={item.label} data-testid={item.testId}>
                {item.label}
              </span>
            )}
          </li>
        ))}
      </ol>
    </nav>
  );
}

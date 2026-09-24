import { useTranslation } from 'react-i18next';
import { usePermission } from '../../../core/di/services-context';
import { CatalogFamilyRows } from '../../catalogs/components/catalog-family-rows';
import { CATALOG_FAMILY_ORDER } from '../../catalogs/components/catalog-family-rows.logic';
import { CatalogStatusNotice } from '../../catalogs/components/catalog-status';
import { useCatalogStatus } from '../../catalogs/components/use-catalog-status';
import { useCatalogs } from '../../catalogs/use-catalogs';
import type { Candidate } from '../models/candidate.models';
import { familiesWithoutLevels } from './candidate-competencies.logic';
import { CandidateRelationSection } from './candidate-relation-section';
import { RELATION_DEFINITIONS } from './candidate-relation-section.logic';

interface Props {
  candidate: Candidate;
  /** The detail page shows the rows read-only, whatever the viewer may do. */
  readOnly?: boolean;
}

/**
 * Skills, languages, programs and tags of a candidate in one panel, on the family rows every
 * search and preset host uses too (KTL-27). The catalog notice is shown once, for the panel.
 */
export function CandidateCompetencies({ candidate, readOnly = false }: Props) {
  const { t } = useTranslation();
  const canUpdate = usePermission('candidates.update');
  const catalogs = useCatalogs();
  const catalogStatus = useCatalogStatus();
  const canEdit = !readOnly && canUpdate;

  const withoutLevels = catalogStatus.message
    ? []
    : familiesWithoutLevels(CATALOG_FAMILY_ORDER, (family) => catalogs.activeNames(family));
  const message =
    catalogStatus.message ??
    (withoutLevels
      .map((kind) =>
        t('candidate.profile.competencies.noLevels', {
          family: t(RELATION_DEFINITIONS[kind].titleKey),
        }),
      )
      .join(' ') ||
      null);

  return (
    <article className="panel" data-testid="candidate-competencies">
      <h2>{t('candidate.profile.competencies.title')}</h2>
      <CatalogFamilyRows
        notice={
          canEdit ? <CatalogStatusNotice status={{ ...catalogStatus, message }} /> : undefined
        }
        renderRow={(kind) => (
          <CandidateRelationSection kind={kind} candidate={candidate} readOnly={readOnly} />
        )}
      />
    </article>
  );
}

import { CandidateService } from '../../candidates/services/candidate.service';
import {
  CriteriaFilter,
  EMPTY_SEARCH_FILTERS,
  MultiValueMode,
  SearchFilters,
  SearchResult,
} from '../models/search.models';

interface LeveledValue {
  value: string;
  level: string;
}

export class CandidateSearchService {
  constructor(private readonly candidateService: CandidateService) {}

  emptyFilters(): SearchFilters {
    return structuredClone(EMPTY_SEARCH_FILTERS);
  }

  /**
   * Text, status and CV filters are answered from the candidate list alone.
   *
   * Language, program and skill criteria are not: they read collections the list endpoint
   * deliberately omits, so those searches load each candidate's aggregate first. That
   * costs one request per candidate and is a deliberate stopgap — KTL-10 moves search to
   * the server, where the filtering belongs and the fan-out disappears — so it is paid
   * only by the searches that actually need it, never by the default one every visit to
   * the screen runs.
   */
  async search(filters: SearchFilters): Promise<SearchResult[]> {
    const needsCollections =
      filters.skillCriteria.length > 0 ||
      filters.languageCriteria.length > 0 ||
      filters.programCriteria.length > 0;
    if (needsCollections) {
      await this.candidateService.ensureAllAggregates();
    } else {
      await this.candidateService.ensureLoaded();
    }

    const text = filters.text.trim().toLocaleLowerCase();
    return this.candidateService
      .list(false)
      .filter((summary) => {
        const textMatch =
          !text ||
          [summary.firstName, summary.lastName, summary.email, summary.phone, summary.notes]
            .join(' ')
            .toLocaleLowerCase()
            .includes(text);
        const statusMatch =
          filters.statusValues.length === 0 || filters.statusValues.includes(summary.status);
        const hasPrimaryCv = summary.primaryDocumentId !== null;
        const cvMatch = !filters.hasCv || (filters.hasCv === 'yes' ? hasPrimaryCv : !hasPrimaryCv);
        if (!textMatch || !statusMatch || !cvMatch) {
          return false;
        }
        if (!needsCollections) {
          return true;
        }
        // Loaded above, so present. A candidate whose aggregate could not be read is
        // excluded rather than silently treated as having no languages at all.
        const candidate = this.candidateService.find(summary.id);
        if (!candidate) {
          return false;
        }
        return (
          this.matchesCriteria(
            candidate.skills.map((item) => ({ value: item.skill, level: item.level })),
            filters.skillCriteria,
            filters.skillMode,
          ) &&
          this.matchesCriteria(
            candidate.languages.map((item) => ({ value: item.language, level: item.level })),
            filters.languageCriteria,
            filters.languageMode,
          ) &&
          this.matchesCriteria(
            candidate.programs.map((item) => ({ value: item.program, level: item.level })),
            filters.programCriteria,
            filters.programMode,
          )
        );
      })
      .map((summary) => ({
        primaryCvDocumentId: summary.primaryDocumentId ?? undefined,
        candidateId: summary.id,
        firstName: summary.firstName,
        lastName: summary.lastName,
        phone: summary.phone,
        email: summary.email,
        status: summary.status,
        hasPrimaryCv: summary.primaryDocumentId !== null,
        updatedAt: summary.updatedAt,
      }));
  }

  /**
   * Each criterion matches when the candidate holds the same value and, when a level is set,
   * that exact level. ALL requires every criterion of the type; ANY only one. Types are
   * always combined with AND between them.
   */
  private matchesCriteria(
    source: LeveledValue[],
    criteria: CriteriaFilter[],
    mode: MultiValueMode,
  ): boolean {
    if (criteria.length === 0) {
      return true;
    }
    const matchesOne = (criterion: CriteriaFilter): boolean =>
      source.some(
        (item) =>
          this.equals(item.value, criterion.value) &&
          (!criterion.level.trim() || this.equals(item.level, criterion.level)),
      );

    return mode === 'ALL' ? criteria.every(matchesOne) : criteria.some(matchesOne);
  }

  private equals(left: string, right: string): boolean {
    return (left || '').trim().toLocaleLowerCase() === (right || '').trim().toLocaleLowerCase();
  }
}

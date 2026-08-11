import { Injectable } from '@angular/core';
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

@Injectable({ providedIn: 'root' })
export class CandidateSearchService {
  constructor(private readonly candidateService: CandidateService) {}

  emptyFilters(): SearchFilters {
    return structuredClone(EMPTY_SEARCH_FILTERS);
  }

  search(filters: SearchFilters): SearchResult[] {
    const text = filters.text.trim().toLocaleLowerCase();
    return this.candidateService
      .list(false)
      .filter((candidate) => {
        const textMatch =
          !text ||
          [
            candidate.firstName,
            candidate.lastName,
            candidate.email,
            candidate.phone,
            candidate.notes,
          ]
            .join(' ')
            .toLocaleLowerCase()
            .includes(text);
        const statusMatch =
          filters.statusValues.length === 0 || filters.statusValues.includes(candidate.status);
        const skillMatch = this.matchesCriteria(
          candidate.skills.map((item) => ({ value: item.skill, level: item.level })),
          filters.skillCriteria,
          filters.skillMode,
        );
        const languageMatch = this.matchesCriteria(
          candidate.languages.map((item) => ({ value: item.language, level: item.level })),
          filters.languageCriteria,
          filters.languageMode,
        );
        const programMatch = this.matchesCriteria(
          candidate.programs.map((item) => ({ value: item.program, level: item.level })),
          filters.programCriteria,
          filters.programMode,
        );
        const hasPrimaryCv = candidate.documents.some((document) => document.isPrimary);
        const cvMatch = !filters.hasCv || (filters.hasCv === 'yes' ? hasPrimaryCv : !hasPrimaryCv);
        return textMatch && statusMatch && skillMatch && languageMatch && programMatch && cvMatch;
      })
      .map((candidate) => ({
        primaryCvDocumentId: candidate.documents.find((document) => document.isPrimary)?.id,
        candidateId: candidate.id,
        firstName: candidate.firstName,
        lastName: candidate.lastName,
        phone: candidate.phone,
        email: candidate.email,
        status: candidate.status,
        hasPrimaryCv: candidate.documents.some((document) => document.isPrimary),
        updatedAt: candidate.updatedAt,
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

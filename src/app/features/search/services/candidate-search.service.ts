import { Injectable } from '@angular/core';
import { CandidateService } from '../../candidates/services/candidate.service';
import { EMPTY_SEARCH_FILTERS, SearchFilters, SearchResult } from '../models/search.models';

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
        const languageMatch = this.matchesValues(
          candidate.languages.map((item) => item.language),
          filters.languageValues,
          filters.languageMode,
        );
        const programMatch = this.matchesValues(
          candidate.programs.map((item) => item.program),
          filters.programValues,
          filters.programMode,
        );
        const hasPrimaryCv = candidate.documents.some((document) => document.isPrimary);
        const cvMatch = !filters.hasCv || (filters.hasCv === 'yes' ? hasPrimaryCv : !hasPrimaryCv);
        return textMatch && statusMatch && languageMatch && programMatch && cvMatch;
      })
      .map((candidate) => ({
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

  private matchesValues(source: string[], selected: string[], mode: 'ANY' | 'ALL'): boolean {
    if (selected.length === 0) {
      return true;
    }
    const sourceSet = new Set(source);
    return mode === 'ALL'
      ? selected.every((value) => sourceSet.has(value))
      : selected.some((value) => sourceSet.has(value));
  }
}

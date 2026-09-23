/** "First Last" as the candidate pages show it, without a stray space when a part is empty. */
export function candidateFullName(candidate: { firstName: string; lastName: string }): string {
  return [candidate.firstName, candidate.lastName].filter(Boolean).join(' ');
}

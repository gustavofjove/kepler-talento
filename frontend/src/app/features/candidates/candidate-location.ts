/**
 * "Location (Province)" as the candidate pages show it (KTL-34). With only one part, that part
 * alone: never empty parentheses or a stray space.
 */
export function candidateLocation(candidate: { location?: string; province?: string }): string {
  const location = candidate.location?.trim() ?? '';
  const province = candidate.province?.trim() ?? '';
  if (location && province) return `${location} (${province})`;
  return location || province;
}

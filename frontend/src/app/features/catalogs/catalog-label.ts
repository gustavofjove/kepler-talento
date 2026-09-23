// The display name of a catalog value. Spanish is the only active language, so this is
// `nameEs`; new code goes through it so showing `nameEn` later is a one-line change.
export function catalogLabel(item: { nameEs: string; nameEn?: string }): string {
  return item.nameEs;
}

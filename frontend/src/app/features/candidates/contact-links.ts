/** `mailto:` href for a candidate email, shown as typed. Phones stay plain text (KTL-31). */
export function mailtoHref(email: string): string {
  return `mailto:${email}`;
}

/** A leading Spanish country code as people write it: `+34`, `0034`, `(+34)`, `(34)`. */
const SPANISH_PREFIX = /^\s*(?:\(\s*\+?34\s*\)|\+34|0034)[\s.-]*/;

/**
 * A phone as the pages show it (KTL-34): almost every candidate is Spanish, so a leading Spanish
 * country code is dropped when what follows is a 9-digit Spanish number. The rest keeps its
 * spacing as typed. Any other number, foreign or unusual, is shown unchanged. Display only: the
 * stored value, the form and the export keep the number as entered.
 */
export function displayPhone(phone: string): string {
  const prefix = SPANISH_PREFIX.exec(phone);
  if (!prefix) return phone;
  const rest = phone.slice(prefix[0].length).trim();
  return rest.replace(/\D/g, '').length === 9 ? rest : phone;
}

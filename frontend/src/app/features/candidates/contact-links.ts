/** `mailto:` href for a candidate email, shown as typed. Phones stay plain text (KTL-31). */
export function mailtoHref(email: string): string {
  return `mailto:${email}`;
}

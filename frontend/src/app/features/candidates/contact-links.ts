/** `mailto:` href for a candidate email, shown as typed. */
export function mailtoHref(email: string): string {
  return `mailto:${email}`;
}

/** `tel:` href for a candidate phone. tel: URIs allow only digits and a leading +,
 * so spaces, dashes and brackets are dropped; the display keeps the typed form. */
export function telHref(phone: string): string {
  return `tel:${phone.replace(/[^\d+]/g, '')}`;
}

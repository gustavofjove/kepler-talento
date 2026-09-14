import i18next from 'i18next';
import { initReactI18next } from 'react-i18next';
import es from '../../../assets/i18n/es.json';

// Spanish is the only active language: no detector, so browser language and stored
// preferences have no effect. Resources are bundled, so init is synchronous and the
// build always ships the copy. en.json is deliberately not loaded yet.
void i18next.use(initReactI18next).init({
  lng: 'es',
  fallbackLng: 'es',
  supportedLngs: ['es'],
  resources: { es: { translation: es } },
  initAsync: false,
  // Keys are flat strings such as `candidate.profile.languages.title`.
  keySeparator: false,
  nsSeparator: false,
  interpolation: { escapeValue: false },
  saveMissing: true,
  missingKeyHandler: (_lngs, _ns, key) => {
    if (import.meta.env.MODE === 'test') {
      throw new Error(`Missing translation key: ${key}`);
    }
    console.warn(`Missing translation key: ${key}`);
  },
});

export const i18n = i18next;

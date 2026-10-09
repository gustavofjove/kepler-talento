// Pure parts of the KTL-40 demo dataset: the fixed tables and the target guard. No I/O here, so
// the frontend unit suite can check the dataset's invariants (tests/unit/seed-demo-data.lib.spec.ts).
// Everything in it is fabricated.

/** Reserved domain of every demo candidate's e-mail; it is how a re-run recognises them. */
const DEMO_DOMAIN = 'demo.kepler-talento.local';

/**
 * 24 fabricated candidates. `availability` is the check to record: a state, how many days ago it
 * was checked, and for some unavailable ones how many days ahead they become available again.
 * `cv` uploads a synthetic one-page PDF as the primary CV. `removed` withdraws them logically
 * afterwards, so the home page has an «inactivos» figure. They are created in this order, so the
 * last ones are «Últimos añadidos».
 */
const CANDIDATES = [
  {
    first: 'Lucía',
    last: 'Martín Ortega',
    city: 'Madrid',
    source: 'LinkedIn',
    availability: ['available', 1],
    cv: true,
  },
  {
    first: 'Hugo',
    last: 'Serrano Gil',
    city: 'Valencia',
    source: 'InfoJobs',
    availability: ['unavailable', 3, 45],
    cv: true,
  },
  {
    first: 'Martina',
    last: 'Ruiz Delgado',
    city: 'Sevilla',
    source: 'Referencia',
    availability: null,
    cv: false,
  },
  {
    first: 'Pablo',
    last: 'Navarro Cano',
    city: 'Madrid',
    source: 'LinkedIn',
    availability: ['available', 4],
    cv: true,
  },
  {
    first: 'Sofía',
    last: 'Romero Vidal',
    city: 'Bilbao',
    source: 'Web corporativa',
    availability: null,
    cv: true,
  },
  {
    first: 'Daniel',
    last: 'Torres Molina',
    city: 'Málaga',
    source: 'InfoJobs',
    availability: ['unavailable', 9],
    cv: false,
  },
  {
    first: 'Valeria',
    last: 'Domínguez Ríos',
    city: 'Madrid',
    source: 'LinkedIn',
    availability: ['available', 7],
    cv: false,
  },
  {
    first: 'Álvaro',
    last: 'Castillo Peña',
    city: 'Zaragoza',
    source: 'Referencia',
    availability: null,
    cv: true,
  },
  {
    first: 'Carmen',
    last: 'Iglesias Soto',
    city: 'Valencia',
    source: 'Feria de empleo',
    availability: ['available', 12],
    cv: true,
  },
  {
    first: 'Javier',
    last: 'Prieto Lozano',
    city: 'Madrid',
    source: 'LinkedIn',
    availability: null,
    cv: false,
  },
  {
    first: 'Elena',
    last: 'Garrido Fuentes',
    city: 'Barcelona',
    source: 'InfoJobs',
    availability: ['unavailable', 15, 20],
    cv: true,
  },
  {
    first: 'Mario',
    last: 'Herrera Campos',
    city: 'Sevilla',
    source: 'Web corporativa',
    availability: null,
    cv: false,
  },
  {
    first: 'Noa',
    last: 'Méndez Blanco',
    city: 'Madrid',
    source: 'LinkedIn',
    availability: ['available', 2],
    cv: true,
  },
  {
    first: 'Adrián',
    last: 'Rubio Herrero',
    city: 'A Coruña',
    source: 'Referencia',
    availability: null,
    cv: true,
  },
  {
    first: 'Paula',
    last: 'Moreno Pastor',
    city: 'Valencia',
    source: 'InfoJobs',
    availability: ['available', 18],
    cv: false,
  },
  {
    first: 'Diego',
    last: 'Santos Calvo',
    city: 'Madrid',
    source: 'LinkedIn',
    availability: null,
    cv: false,
  },
  {
    first: 'Irene',
    last: 'Medina Vega',
    city: 'Málaga',
    source: 'Feria de empleo',
    availability: ['unavailable', 21],
    cv: true,
  },
  {
    first: 'Sergio',
    last: 'Cortés Reyes',
    city: 'Bilbao',
    source: 'Web corporativa',
    availability: null,
    cv: false,
  },
  {
    first: 'Claudia',
    last: 'Aguilar Marín',
    city: 'Madrid',
    source: 'LinkedIn',
    availability: ['available', 26],
    cv: true,
  },
  {
    first: 'Raúl',
    last: 'Nieto Santana',
    city: 'Zaragoza',
    source: 'InfoJobs',
    availability: null,
    cv: false,
  },
  {
    first: 'Andrea',
    last: 'Vargas León',
    city: 'Sevilla',
    source: 'Referencia',
    availability: ['unavailable', 28, 60],
    cv: false,
  },
  {
    first: 'Iván',
    last: 'Ramos Benítez',
    city: 'Madrid',
    source: 'LinkedIn',
    availability: null,
    cv: true,
  },
  {
    first: 'Laura',
    last: 'Gómez Sanz',
    city: 'Valencia',
    source: 'InfoJobs',
    availability: ['available', 0],
    cv: false,
    removed: true,
  },
  {
    first: 'Óscar',
    last: 'Fernández Mora',
    city: 'Madrid',
    source: 'Web corporativa',
    availability: null,
    cv: true,
    removed: true,
  },
];

const NOTES = {
  Madrid: 'Disponible para trabajar en Madrid capital y alrededores.',
  default: 'Perfil recibido para la base de datos de demostración.',
};

/**
 * 8 positions, listed most recently updated first: they are created in reverse, so the first
 * here ends up on top of «Posiciones abiertas». `links` maps candidate indexes (into CANDIDATES)
 * to the stage they sit at.
 */
const POSITIONS = [
  {
    title: 'Desarrollador .NET sénior',
    location: 'Madrid',
    links: {
      0: 'interview',
      3: 'shortlisted',
      6: 'new',
      12: 'new',
      9: 'new',
      18: 'rejected',
      21: 'hired',
    },
  },
  {
    title: 'Técnico de soporte N2',
    location: 'Valencia',
    links: { 1: 'rejected', 8: 'interview', 14: 'shortlisted', 10: 'new' },
  },
  {
    title: 'Analista de datos',
    location: 'Remoto',
    links: { 4: 'new', 7: 'new', 13: 'shortlisted', 19: 'rejected', 16: 'interview' },
  },
  {
    title: 'Jefe de proyecto',
    location: 'Madrid',
    links: { 15: 'hired', 9: 'rejected', 2: 'interview' },
  },
  { title: 'Administrativo de RR. HH.', location: 'Sevilla', links: { 11: 'new', 20: 'new' } },
  { title: 'Diseñador UX/UI', location: 'Bilbao', links: {} },
  {
    title: 'Comercial B2B',
    location: 'Málaga',
    links: { 5: 'shortlisted', 17: 'new', 23: 'rejected' },
  },
  {
    title: 'Becario de marketing',
    location: 'Barcelona',
    closed: true,
    links: { 22: 'hired', 11: 'rejected' },
  },
];

/**
 * Languages, programs and skills per candidate index (into CANDIDATES), as catalog names from
 * the deployment seed (backend/Infrastructure/Persistence/CatalogSeedData.cs). Languages are
 * `[language, level]`, programs `[program, level, years]`, skills `[skill, level]`.
 *
 * Built for the «Inglés B2 + Navision + AutoCAD» search (preset of the same name). The language
 * level is a minimum, so it finds the six candidates marked MATCH and Sofía (index 4, Inglés C1).
 * The NEAR ones miss on exactly one criterion, so the filter visibly does something.
 */
const COMPETENCIES = {
  0: {
    // MATCH
    languages: [
      ['Inglés', 'B2'],
      ['Francés', 'A2'],
    ],
    programs: [
      ['Navision', 'Avanzado', 4],
      ['AutoCAD', 'Medio', 3],
      ['Excel', 'Avanzado', 6],
    ],
    skills: [
      ['Contabilidad', 'Alto'],
      ['Facturación', 'Medio'],
    ],
  },
  1: {
    languages: [['Inglés', 'C1']],
    programs: [
      ['Python', 'Avanzado', 5],
      ['SQL', 'Avanzado', 5],
      ['Git', 'Medio', 4],
    ],
    skills: [['Análisis', 'Alto']],
  },
  2: {
    languages: [['Inglés', 'B1']],
    programs: [
      ['Excel', 'Medio', 3],
      ['Word', 'Avanzado', 5],
    ],
    skills: [
      ['Gestión de proyectos', 'Alto'],
      ['Liderazgo de equipos', 'Medio'],
    ],
  },
  3: {
    // MATCH
    languages: [['Inglés', 'B2']],
    programs: [
      ['AutoCAD', 'Avanzado', 6],
      ['Navision', 'Medio', 2],
      ['SolidWorks', 'Medio', 3],
    ],
    skills: [
      ['Lectura de planos', 'Experto'],
      ['Control de calidad', 'Alto'],
    ],
  },
  4: {
    // Matches through the minimum level: C1 is above B2.
    languages: [
      ['Inglés', 'C1'],
      ['Alemán', 'B1'],
    ],
    programs: [
      ['Navision', 'Experto', 8],
      ['AutoCAD', 'Básico', 1],
      ['Power BI', 'Medio', 2],
    ],
    skills: [
      ['Compras', 'Alto'],
      ['Negociación', 'Alto'],
    ],
  },
  5: {
    languages: [['Inglés', 'A2']],
    programs: [['Excel', 'Básico', 1]],
    skills: [
      ['Atención al cliente', 'Alto'],
      ['Negociación', 'Medio'],
    ],
  },
  6: {
    languages: [['Inglés', 'C2']],
    programs: [
      ['JavaScript', 'Experto', 7],
      ['HTML', 'Avanzado', 7],
      ['CSS', 'Avanzado', 7],
    ],
    skills: [['Trabajo en equipo', 'Alto']],
  },
  7: {
    // NEAR: English below the minimum.
    languages: [['Inglés', 'B1']],
    programs: [
      ['Navision', 'Avanzado', 5],
      ['AutoCAD', 'Avanzado', 5],
    ],
    skills: [
      ['Mantenimiento preventivo', 'Alto'],
      ['Lectura de planos', 'Alto'],
    ],
  },
  8: {
    // MATCH
    languages: [
      ['Inglés', 'B2'],
      ['Italiano', 'B1'],
    ],
    programs: [
      ['Navision', 'Avanzado', 3],
      ['AutoCAD', 'Avanzado', 4],
      ['Excel', 'Experto', 9],
    ],
    skills: [
      ['Logística de almacén', 'Alto'],
      ['Gestión documental', 'Medio'],
    ],
  },
  9: {
    languages: [['Inglés', 'C1']],
    programs: [
      ['C#', 'Experto', 8],
      ['SQL', 'Avanzado', 8],
    ],
    skills: [['Liderazgo de equipos', 'Alto']],
  },
  10: {
    // NEAR: no Navision.
    languages: [['Inglés', 'B2']],
    programs: [
      ['AutoCAD', 'Experto', 10],
      ['Revit', 'Avanzado', 6],
    ],
    skills: [['Lectura de planos', 'Experto']],
  },
  11: {
    languages: [['Inglés', 'A2']],
    programs: [
      ['A3', 'Avanzado', 4],
      ['ContaPlus', 'Medio', 3],
    ],
    skills: [
      ['Nóminas', 'Alto'],
      ['Selección de personal', 'Medio'],
    ],
  },
  12: {
    // MATCH
    languages: [['Inglés', 'B2']],
    programs: [
      ['AutoCAD', 'Medio', 2],
      ['Navision', 'Medio', 2],
      ['Word', 'Avanzado', 4],
    ],
    skills: [['Atención al cliente', 'Alto']],
  },
  13: {
    // MATCH
    languages: [
      ['Inglés', 'B2'],
      ['Portugués', 'B1'],
    ],
    programs: [
      ['Navision', 'Avanzado', 6],
      ['AutoCAD', 'Avanzado', 5],
      ['EPLAN', 'Medio', 2],
    ],
    skills: [
      ['Montaje industrial', 'Alto'],
      ['Prevención de riesgos laborales', 'Medio'],
    ],
  },
  14: {
    languages: [['Francés', 'B2']],
    programs: [['Excel', 'Medio', 2]],
    skills: [['Atención al cliente', 'Experto']],
  },
  15: {
    // NEAR: no AutoCAD.
    languages: [['Inglés', 'B2']],
    programs: [
      ['Navision', 'Experto', 9],
      ['SAP', 'Avanzado', 5],
    ],
    skills: [
      ['Gestión de proyectos', 'Experto'],
      ['Contabilidad', 'Medio'],
    ],
  },
  16: {
    languages: [['Inglés', 'B1']],
    programs: [
      ['Power BI', 'Avanzado', 3],
      ['Tableau', 'Medio', 2],
    ],
    skills: [['Análisis', 'Experto']],
  },
  17: {
    // MATCH
    languages: [['Inglés', 'B2']],
    programs: [
      ['Navision', 'Medio', 3],
      ['AutoCAD', 'Medio', 3],
    ],
    skills: [
      ['Compras', 'Medio'],
      ['Negociación', 'Alto'],
    ],
  },
  18: {
    languages: [['Inglés', 'C1']],
    programs: [
      ['Photoshop', 'Experto', 6],
      ['Illustrator', 'Avanzado', 6],
    ],
    skills: [['Trabajo en equipo', 'Alto']],
  },
  19: {
    languages: [['Alemán', 'C1']],
    programs: [
      ['MATLAB', 'Avanzado', 4],
      ['Simulink', 'Medio', 3],
    ],
    skills: [['Análisis', 'Alto']],
  },
  20: {
    languages: [['Inglés', 'B1']],
    programs: [['Word', 'Medio', 3]],
    skills: [
      ['Selección de personal', 'Alto'],
      ['Gestión documental', 'Alto'],
    ],
  },
  21: {
    languages: [['Inglés', 'B2']],
    programs: [
      ['CATIA', 'Medio', 2],
      ['SolidWorks', 'Avanzado', 4],
    ],
    skills: [
      ['Soldadura', 'Experto'],
      ['Mecanizado', 'Alto'],
    ],
  },
};

/** The three PUT bodies' collections for one profile; new rows, so every `id` is null. */
function competencyBodies(profile) {
  return {
    languages: profile.languages.map(([language, level]) => ({ id: null, language, level })),
    programs: profile.programs.map(([program, level, yearsExperience]) => ({
      id: null,
      program,
      level,
      yearsExperience,
    })),
    skills: profile.skills.map(([skill, level]) => ({ id: null, skill, level })),
  };
}

const DESCRIPTION = (title) =>
  `<p>Posición de demostración: <strong>${title}</strong>. Datos ficticios creados para enseñar la página de inicio.</p>`;

/**
 * 4 shared presets. `used` gives the order in which they are applied after creation (1 first),
 * so the last one used is the most recent; `null` is never used.
 */
const PRESETS = [
  {
    name: 'Disponibles con CV',
    used: 3,
    filters: { availabilityValues: ['available'], hasCv: 'yes' },
  },
  { name: 'Pendientes de comprobar', used: 1, filters: { availabilityValues: ['unknown'] } },
  { name: 'Candidatos en Madrid', used: 2, filters: { text: 'Madrid' } },
  {
    name: 'No disponibles con CV',
    used: null,
    filters: { availabilityValues: ['unavailable'], hasCv: 'yes' },
  },
  {
    name: 'Inglés B2 + Navision + AutoCAD',
    used: 4,
    filters: {
      languageCriteria: [{ value: 'Inglés', level: 'B2' }],
      languageMode: 'ALL',
      programCriteria: [
        { value: 'Navision', level: '' },
        { value: 'AutoCAD', level: '' },
      ],
      programMode: 'ALL',
    },
  },
];

const STAGES = ['new', 'shortlisted', 'interview', 'hired', 'rejected'];

/** Lower-case ASCII local part, e.g. «Lucía Martín Ortega» → «lucia.martin». */
function emailFor(candidate) {
  const plain = (value) =>
    value
      .normalize('NFD')
      .replace(/[̀-ͯ]/g, '')
      .toLowerCase()
      .replace(/[^a-z]/g, '');
  return `${plain(candidate.first)}.${plain(candidate.last.split(' ')[0])}@${DEMO_DOMAIN}`;
}

/** A Spanish mobile number derived from the index: 9 digits, never the e2e 13-digit marker. */
function phoneFor(index) {
  return `+34 6${String(10 + index).padStart(2, '0')} ${String(100 + index * 7).padStart(3, '0')} ${String(200 + index * 13).padStart(3, '0')}`;
}

/** `yyyy-MM-dd` of `today` moved by `days`, in UTC, as the API's wire dates expect. */
function dayOffset(today, days) {
  const day = new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), today.getUTCDate()));
  day.setUTCDate(day.getUTCDate() + days);
  return day.toISOString().slice(0, 10);
}

/** The create body for one candidate. */
function candidateBody(candidate, index, today) {
  return {
    firstName: candidate.first,
    lastName: candidate.last,
    phone: phoneFor(index),
    email: emailFor(candidate),
    location: candidate.city,
    province: candidate.city,
    country: 'España',
    source: candidate.source,
    notes: NOTES[candidate.city] ?? NOTES.default,
    receivedAt: dayOffset(today, -40 - index),
    consentAt: dayOffset(today, -40 - index),
    reviewDueAt: dayOffset(today, 690 - index),
  };
}

/** The availability body for one candidate, or null when it stays unchecked. */
function availabilityBody(candidate, today) {
  if (!candidate.availability) return null;
  const [state, daysAgo, untilDays] = candidate.availability;
  return {
    state,
    checkedOn: dayOffset(today, -daysAgo),
    until: untilDays ? dayOffset(today, untilDays) : null,
  };
}

/**
 * Refuses any target but this machine. The dev token endpoint is never mapped in Production; this
 * is the second guard, so a mistyped URL cannot point fabricated data at a shared server.
 */
function assertLoopback(baseUrl) {
  let url;
  try {
    url = new URL(baseUrl);
  } catch {
    throw new Error(`'${baseUrl}' is not a URL.`);
  }
  if (url.protocol !== 'http:' && url.protocol !== 'https:') {
    throw new Error(`'${baseUrl}' must be an http(s) URL.`);
  }
  if (!['localhost', '127.0.0.1', '[::1]'].includes(url.hostname)) {
    throw new Error(
      `Refusing '${url.hostname}': demo data is written only to a stack on this machine (localhost, 127.0.0.1 or ::1).`,
    );
  }
  return url;
}

module.exports = {
  CANDIDATES,
  COMPETENCIES,
  DEMO_DOMAIN,
  DESCRIPTION,
  POSITIONS,
  PRESETS,
  STAGES,
  assertLoopback,
  availabilityBody,
  candidateBody,
  competencyBodies,
  dayOffset,
  emailFor,
  phoneFor,
};

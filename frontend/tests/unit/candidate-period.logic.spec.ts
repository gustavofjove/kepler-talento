import { i18n } from '../../src/app/core/i18n/i18n';
import {
  educationPeriod,
  experiencePeriod,
} from '../../src/app/features/candidates/components/candidate-period.logic';
import type {
  CandidateEducation,
  CandidateExperience,
} from '../../src/app/features/candidates/models/candidate.models';

const t = i18n.t.bind(i18n);

const experience = (overrides: Partial<CandidateExperience> = {}): CandidateExperience => ({
  id: 'x1',
  company: 'Kepler',
  position: 'Técnico',
  sector: 'Industria',
  isCurrent: false,
  ...overrides,
});

const education = (endYear?: number): CandidateEducation => ({
  id: 'e1',
  educationType: 'Grado',
  degree: 'Grado en ADE',
  institution: 'UCM',
  status: 'Finalizada',
  endYear,
});

describe('candidate periods', () => {
  it('spans an experience from its start year to its end year', () => {
    expect(
      experiencePeriod(experience({ startDate: '2019-03-01', endDate: '2024-06-30' }), t),
    ).toBe('2019–2024');
  });

  it('shows a single year when only one is known or both are the same', () => {
    expect(experiencePeriod(experience({ startDate: '2019-03-01' }), t)).toBe('2019');
    expect(experiencePeriod(experience({ endDate: '2024-06-30' }), t)).toBe('2024');
    expect(
      experiencePeriod(experience({ startDate: '2024-01-01', endDate: '2024-06-30' }), t),
    ).toBe('2024');
  });

  it('runs a current role to the present, and has no period without dates', () => {
    expect(
      experiencePeriod(
        experience({ startDate: '2020-01-01', endDate: '2023-01-01', isCurrent: true }),
        t,
      ),
    ).toBe('2020–actualidad');
    expect(experiencePeriod(experience({ isCurrent: true }), t)).toBeUndefined();
    expect(experiencePeriod(experience(), t)).toBeUndefined();
  });

  it('gives education its end year, when it has one', () => {
    expect(educationPeriod(education(2020), t)).toBe('2020');
    expect(educationPeriod(education(), t)).toBeUndefined();
  });
});

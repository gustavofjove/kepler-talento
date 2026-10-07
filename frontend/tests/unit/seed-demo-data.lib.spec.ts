import { createRequire } from 'node:module';
import { resolve } from 'node:path';
import { repoRoot } from '../repo-root';

interface DemoCandidate {
  first: string;
  last: string;
  city: string;
  availability: [string, number, number?] | null;
  cv: boolean;
  removed?: boolean;
}

interface DemoLib {
  CANDIDATES: DemoCandidate[];
  POSITIONS: { title: string; closed?: boolean; links: Record<string, string> }[];
  PRESETS: { name: string; used: number | null; filters: Record<string, unknown> }[];
  STAGES: string[];
  DEMO_DOMAIN: string;
  assertLoopback(url: string): URL;
  availabilityBody(
    candidate: DemoCandidate,
    today: Date,
  ): { state: string; checkedOn: string; until: string | null } | null;
  candidateBody(candidate: DemoCandidate, index: number, today: Date): Record<string, string>;
  emailFor(candidate: DemoCandidate): string;
}

/** The KTL-40 demo dataset (scripts/seed-demo-data.lib.js), loaded as the script loads it. */
const lib = createRequire(__filename)(
  resolve(repoRoot, 'scripts/seed-demo-data.lib.js'),
) as DemoLib;
const today = new Date(Date.UTC(2026, 9, 7));

describe('demo dataset (KTL-40)', () => {
  it('writes only to a stack on this machine', () => {
    for (const url of ['http://localhost:4200', 'http://127.0.0.1:4300', 'http://[::1]:4200']) {
      expect(() => lib.assertLoopback(url)).not.toThrow();
    }
    for (const url of [
      'https://kepler-talento.example.com',
      'http://10.0.0.5:4200',
      'http://localhost.evil.test',
      'file:///etc/passwd',
      'not a url',
    ]) {
      expect(() => lib.assertLoopback(url)).toThrow();
    }
  });

  it('never carries the 13-digit marker the e2e teardown purges by', () => {
    const everything = JSON.stringify({
      candidates: lib.CANDIDATES.map((candidate, index) =>
        lib.candidateBody(candidate, index, today),
      ),
      positions: lib.POSITIONS,
      presets: lib.PRESETS,
    });

    expect(everything).not.toMatch(/\d{13}/);
  });

  it('gives every candidate a distinct e-mail in the reserved domain', () => {
    const emails = lib.CANDIDATES.map((candidate) => lib.emailFor(candidate));

    expect(new Set(emails).size).toBe(emails.length);
    for (const email of emails) {
      expect(email).toMatch(
        new RegExp(`^[a-z]+\\.[a-z]+@${lib.DEMO_DOMAIN.replace(/\./g, '\\.')}$`),
      );
    }
  });

  it('has the population the spec promises', () => {
    const active = lib.CANDIDATES.filter((candidate) => !candidate.removed);
    const state = (value: string) =>
      active.filter((candidate) => candidate.availability?.[0] === value).length;

    expect(active.length).toBeGreaterThanOrEqual(20);
    expect(lib.CANDIDATES.length - active.length).toBeGreaterThanOrEqual(2);
    expect(state('available')).toBeGreaterThanOrEqual(6);
    expect(state('unavailable')).toBeGreaterThanOrEqual(4);
    expect(active.some((candidate) => candidate.availability === null)).toBe(true);
    const withCv = active.filter((candidate) => candidate.cv).length;
    expect(withCv / active.length).toBeGreaterThan(0.35);
    expect(withCv / active.length).toBeLessThan(0.65);
  });

  it('records checks within the last 30 days, with some «hasta» dates', () => {
    const bodies = lib.CANDIDATES.map((candidate) => lib.availabilityBody(candidate, today)).filter(
      (body) => body !== null,
    );

    for (const body of bodies) {
      expect(body.checkedOn >= '2026-09-07' && body.checkedOn <= '2026-10-07').toBe(true);
      if (body.until) expect(body.until > body.checkedOn).toBe(true);
    }
    expect(bodies.some((body) => body.state === 'unavailable' && body.until)).toBe(true);
  });

  it('links candidates at every stage of open positions and leaves one open position empty', () => {
    const open = lib.POSITIONS.filter((position) => !position.closed);
    const stages = new Set(open.flatMap((position) => Object.values(position.links)));

    expect(open.length).toBeGreaterThanOrEqual(6);
    expect(lib.POSITIONS.filter((position) => position.closed)).toHaveLength(1);
    expect([...stages].sort()).toEqual([...lib.STAGES].sort());
    expect(open.some((position) => Object.keys(position.links).length === 0)).toBe(true);
    for (const position of lib.POSITIONS) {
      for (const index of Object.keys(position.links)) {
        expect(lib.CANDIDATES[Number(index)]).toBeDefined();
      }
    }
  });

  it('has used presets in a known order and one never used', () => {
    const used = lib.PRESETS.map((preset) => preset.used).filter((order) => order !== null);

    expect(lib.PRESETS.length).toBeGreaterThanOrEqual(4);
    expect(used.length).toBeGreaterThanOrEqual(3);
    expect(new Set(used).size).toBe(used.length);
    expect(lib.PRESETS.some((preset) => preset.used === null)).toBe(true);
  });
});

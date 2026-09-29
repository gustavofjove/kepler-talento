import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { repoRoot } from '../repo-root';

const read = (relative: string): string => readFileSync(join(repoRoot, relative), 'utf8');

/** Source without comments, so an explanatory remark naming a forbidden construct is not a hit. */
const code = (relative: string): string =>
  read(relative)
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/^\s*\/\/.*$/gm, '');

const endpoint = 'backend/Web/Features/Candidates/CandidateDraftEndpoints.cs';
const handler = 'backend/Application/Features/Candidates/CvDraft/CreateCandidateDraft.cs';
const spaFiles = [
  'frontend/src/app/features/candidates/services/candidate-draft.service.ts',
  'frontend/src/app/features/candidates/pages/candidate-create-page.tsx',
  'frontend/src/app/features/candidates/components/cv-draft-picker.tsx',
  'frontend/src/app/features/candidates/components/candidate-form.tsx',
];

describe('KTL-32 CV draft security boundary', () => {
  it('checks candidates.create before reading the body, and takes a slot only after it', () => {
    const source = code(endpoint);
    const guard = source.indexOf('actor.HasPermission(Permissions.CandidatesCreate)');
    expect(guard).toBeGreaterThan(-1);
    for (const later of ['gate.TryEnter()', 'ReadFileAsync(context.Request', 'sender.Send(']) {
      expect(source.indexOf(later), later).toBeGreaterThan(guard);
    }
    // No routing constraint that would answer before authorization.
    expect(source).not.toMatch(/\.Accepts</);
    // The body is never spooled to disk by the form reader.
    expect(source).not.toMatch(/ReadFormAsync/);
    expect(read('backend/Web/Program.cs')).toContain('app.MapCandidateDraftEndpoints()');
  });

  it('repeats the guard first in the handler and parses only after a clean scan', () => {
    const source = code(handler);
    const body = source.slice(source.indexOf('public async Task<CandidateDraftResponse> Handle'));
    const guard = body.indexOf('CandidateGuards.RequireCreate(actor);');
    expect(guard).toBeGreaterThan(-1);
    for (const later of ['inspector.', 'scanner.', 'reader.', 'extractor.']) {
      const use = body.indexOf(later);
      expect(use === -1 || use > guard, `${later} before the guard`).toBe(true);
    }
    const scan = body.indexOf('scanner.ScanAsync');
    const clean = body.indexOf('case ScanVerdict.Clean:');
    const read = body.indexOf('reader.ReadAsync');
    expect(scan).toBeGreaterThan(-1);
    expect(clean).toBeGreaterThan(scan);
    expect(read).toBeGreaterThan(clean);
  });

  it('writes nothing to storage and exposes no path or text in the response contract', () => {
    const source = code(handler);
    expect(source).not.toMatch(/IDocumentStorage\b|IDocumentRepository\b|ICandidateRepository\b/);
    const contract = source.slice(
      source.indexOf('record CandidateDraftResponse'),
      source.indexOf('public static class CvDraftCodes'),
    );
    expect(contract).not.toMatch(/StorageKey|Path|FileName|Text\b/);
    const endpointSource = code(endpoint);
    expect(endpointSource).not.toMatch(
      /IDocumentStorage\b|File\.(Write|Create)|Path\.GetTempFileName/,
    );
  });

  it('never asks for a named culture, which the invariant-globalization container lacks', () => {
    const folder = 'backend/Infrastructure/CvExtraction';
    for (const file of readdirSync(join(repoRoot, folder)).filter((name) => name.endsWith('.cs'))) {
      expect(code(`${folder}/${file}`), file).not.toMatch(
        /GetCultureInfo\(|new CultureInfo\(|CreateSpecificCulture\(/,
      );
    }
  });

  it('logs identifiers and an outcome code only', () => {
    const logCalls = code(endpoint).match(/Log(Information|Warning|Error)\([^;]*;/g) ?? [];
    expect(logCalls).toHaveLength(1);
    expect(logCalls[0]).toMatch(/\{DraftId\}.*\{OutcomeCode\}/s);
    expect(logCalls[0]).not.toMatch(/fileName|content|response\.Fields/i);
  });

  it('reaches the draft only through the API transport and keeps nothing in the browser', () => {
    for (const file of spaFiles) {
      const source = code(file);
      expect(source, file).not.toMatch(/localStorage|sessionStorage|indexedDB/);
      expect(source, file).not.toMatch(/\bfetch\(|XMLHttpRequest|supabase/i);
    }
    expect(code(spaFiles[0])).toContain('this.transport.request<CvDraftResponse>(');
  });
});

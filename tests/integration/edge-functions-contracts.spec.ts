import { readFileSync } from 'node:fs';
import * as path from 'node:path';
import { requestContext, requireBearerAuth } from '../../supabase/functions/_shared/http';
import { EDGE_LIMITS, parsePositiveInt } from '../../supabase/functions/_shared/limits';

describe('Edge function contracts', () => {
  it('builds request context with request_id and idempotency key', () => {
    const request = {
      headers: {
        get: (name: string) => {
          const key = name.toLowerCase();
          if (key === 'x-request-id') return 'req-123';
          if (key === 'x-idempotency-key') return 'idem-abc';
          return null;
        },
      },
    } as unknown as Request;

    const context = requestContext(request);

    expect(context.requestId).toBe('req-123');
    expect(context.idempotencyKey).toBe('idem-abc');
  });

  it('defines standardized error envelope fields in shared http module', () => {
    const filePath = path.join(process.cwd(), 'supabase/functions/_shared/http.ts');
    const source = readFileSync(filePath, 'utf-8');

    expect(source).toMatch(/jsonError\(/);
    expect(source).toMatch(/request_id/);
    expect(source).toMatch(/VALIDATION_ERROR/);
  });

  it('requires bearer auth for protected functions', () => {
    const request = {
      headers: {
        get: (name: string) => (name.toLowerCase() === 'authorization' ? 'Basic abc' : null),
      },
    } as unknown as Request;

    expect(() => requireBearerAuth(request)).toThrow(/bearer token is required/i);
  });

  it('parses positive integers and applies fallback for invalid values', () => {
    expect(parsePositiveInt(25, 10)).toBe(25);
    expect(parsePositiveInt('16', 10)).toBe(16);
    expect(parsePositiveInt('x', 10)).toBe(10);
    expect(parsePositiveInt(-1, 10)).toBe(10);
  });

  it('documents max operational limits for import and export', () => {
    expect(EDGE_LIMITS.maxExportRows).toBeGreaterThan(0);
    expect(EDGE_LIMITS.maxImportFiles).toBeGreaterThan(0);
    expect(EDGE_LIMITS.signedUrlTtlSeconds).toBeLessThanOrEqual(300);
  });

  it('uses standardized error and request_id patterns in edge functions', () => {
    const filePath = path.join(
      process.cwd(),
      'supabase/functions/candidate-export-results/index.ts',
    );
    const source = readFileSync(filePath, 'utf-8');

    expect(source).toMatch(/jsonError\(/);
    expect(source).toMatch(/request_id/);
    expect(source).toMatch(/max_export_rows_exceeded/);
  });
});

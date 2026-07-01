import { corsHeaders, json, readJson } from '../_shared/http.ts';

interface RequestBody {
  filters?: Record<string, unknown>;
  format?: 'csv' | 'xlsx';
  field_set?: string;
}

Deno.serve(async (request) => {
  if (request.method === 'OPTIONS') {
    return new Response('ok', { headers: corsHeaders });
  }

  try {
    const body = await readJson<RequestBody>(request);
    return json({
      export_id: crypto.randomUUID(),
      download_url: 'export-placeholder',
      row_count: 0,
      expires_in_seconds: 300,
      format: body.format ?? 'csv',
      field_set: body.field_set ?? 'rrhh-default',
    });
  } catch (error) {
    return json({ error: error instanceof Error ? error.message : 'unexpected_error' }, 400);
  }
});

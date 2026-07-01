import { corsHeaders, json, readJson } from '../_shared/http.ts';

interface RequestBody {
  source_name?: string;
  dry_run?: boolean;
}

Deno.serve(async (request) => {
  if (request.method === 'OPTIONS') {
    return new Response('ok', { headers: corsHeaders });
  }

  try {
    const body = await readJson<RequestBody>(request);
    if (!body.source_name) {
      return json({ error: 'source_name_required' }, 400);
    }

    return json({
      batch_id: crypto.randomUUID(),
      status: body.dry_run ? 'validated' : 'loaded',
      total_rows: 0,
      loaded_rows: 0,
      error_rows: 0,
    });
  } catch (error) {
    return json({ error: error instanceof Error ? error.message : 'unexpected_error' }, 400);
  }
});

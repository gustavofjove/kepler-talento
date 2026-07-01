import { corsHeaders, json, readJson } from '../_shared/http.ts';

interface RequestBody {
  as_of?: string;
}

Deno.serve(async (request) => {
  if (request.method === 'OPTIONS') {
    return new Response('ok', { headers: corsHeaders });
  }

  try {
    const body = await readJson<RequestBody>(request);
    const asOf = body.as_of ?? new Date().toISOString().slice(0, 10);
    return json({
      as_of: asOf,
      overdue_count: 0,
      candidate_ids: [],
    });
  } catch (error) {
    return json({ error: error instanceof Error ? error.message : 'unexpected_error' }, 400);
  }
});

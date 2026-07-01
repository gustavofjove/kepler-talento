import { corsHeaders, json, readJson } from '../_shared/http.ts';

interface RequestBody {
  document_id?: string;
}

Deno.serve(async (request) => {
  if (request.method === 'OPTIONS') {
    return new Response('ok', { headers: corsHeaders });
  }

  if (request.method !== 'POST') {
    return json({ error: 'method_not_allowed' }, 405);
  }

  try {
    const body = await readJson<RequestBody>(request);
    if (!body.document_id) {
      return json({ error: 'document_id_required' }, 400);
    }

    return json({
      url: `signed-url-placeholder/${body.document_id}`,
      expires_in_seconds: 300,
      document_id: body.document_id,
    });
  } catch (error) {
    return json({ error: error instanceof Error ? error.message : 'unexpected_error' }, 400);
  }
});

import {
  EdgeHttpError,
  corsHeaders,
  json,
  jsonError,
  logEdgeEvent,
  readJson,
  requestContext,
  requireBearerAuth,
} from '../_shared/http.ts';

interface RequestBody {
  as_of?: string;
}

Deno.serve(async (request) => {
  const context = requestContext(request);

  if (request.method === 'OPTIONS') {
    return new Response('ok', { headers: corsHeaders });
  }

  try {
    const technicalSecret = Deno.env.get('RETENTION_SHARED_SECRET') ?? '';
    const providedSecret = request.headers.get('x-outbox-secret') ?? '';
    if (!technicalSecret || providedSecret !== technicalSecret) {
      requireBearerAuth(request);
    }

    const body = await readJson<RequestBody>(request);
    const asOf = body.as_of ?? new Date().toISOString().slice(0, 10);
    logEdgeEvent('retention.check.executed', context, { as_of: asOf });
    return json(
      {
        request_id: context.requestId,
        as_of: asOf,
        overdue_count: 0,
        candidate_ids: [],
      },
      200,
      context.requestId,
    );
  } catch (error) {
    if (error instanceof EdgeHttpError) {
      return jsonError(context, error.code, error.message, error.status, error.details);
    }
    return jsonError(context, 'INTERNAL_ERROR', 'unexpected_error', 500);
  }
});

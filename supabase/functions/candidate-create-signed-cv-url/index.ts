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
import { EDGE_LIMITS } from '../_shared/limits.ts';

interface RequestBody {
  document_id?: string;
}

Deno.serve(async (request) => {
  const context = requestContext(request);

  if (request.method === 'OPTIONS') {
    return new Response('ok', { headers: corsHeaders });
  }

  if (request.method !== 'POST') {
    return jsonError(context, 'VALIDATION_ERROR', 'method_not_allowed', 405);
  }

  try {
    requireBearerAuth(request);
    const body = await readJson<RequestBody>(request);
    if (!body.document_id) {
      return jsonError(context, 'VALIDATION_ERROR', 'document_id_required', 400);
    }

    logEdgeEvent('signed_url.create', context, {
      document_id: body.document_id,
    });

    return json(
      {
        url: `signed-url-placeholder/${body.document_id}`,
        expires_in_seconds: EDGE_LIMITS.signedUrlTtlSeconds,
        document_id: body.document_id,
        request_id: context.requestId,
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

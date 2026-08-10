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
  source_name?: string;
  dry_run?: boolean;
  files?: Array<{ kind?: string; storage_reference?: string }>;
}

Deno.serve(async (request) => {
  const context = requestContext(request);

  if (request.method === 'OPTIONS') {
    return new Response('ok', { headers: corsHeaders });
  }

  try {
    requireBearerAuth(request);
    const body = await readJson<RequestBody>(request);
    if (!body.source_name) {
      return jsonError(context, 'VALIDATION_ERROR', 'source_name_required', 400);
    }
    if ((body.files?.length ?? 0) > EDGE_LIMITS.maxImportFiles) {
      return jsonError(
        context,
        'VALIDATION_ERROR',
        `max_import_files_exceeded:${EDGE_LIMITS.maxImportFiles}`,
        400,
      );
    }

    const batchId = crypto.randomUUID();
    const idempotencyKey = context.idempotencyKey || `auto-${batchId}`;
    const status = body.dry_run ? 'validated' : 'loaded';

    logEdgeEvent('import.batch.processed', context, {
      source_name: body.source_name,
      dry_run: !!body.dry_run,
      status,
      idempotency_key: idempotencyKey,
      file_count: body.files?.length ?? 0,
    });

    return json(
      {
        request_id: context.requestId,
        idempotency_key: idempotencyKey,
        batch_id: batchId,
        status,
        total_rows: 0,
        loaded_rows: 0,
        error_rows: 0,
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

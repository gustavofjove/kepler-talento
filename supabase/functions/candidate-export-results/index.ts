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
import { EDGE_LIMITS, parsePositiveInt } from '../_shared/limits.ts';

interface RequestBody {
  filters?: Record<string, unknown>;
  format?: 'csv' | 'xlsx';
  field_set?: string;
  row_limit?: number;
}

Deno.serve(async (request) => {
  const context = requestContext(request);

  if (request.method === 'OPTIONS') {
    return new Response('ok', { headers: corsHeaders });
  }

  try {
    requireBearerAuth(request);
    const body = await readJson<RequestBody>(request);
    const requestedRows = parsePositiveInt(body.row_limit, 100);
    if (requestedRows > EDGE_LIMITS.maxExportRows) {
      return jsonError(
        context,
        'VALIDATION_ERROR',
        `max_export_rows_exceeded:${EDGE_LIMITS.maxExportRows}`,
        400,
      );
    }

    const rowCount = Math.min(requestedRows, 42);
    logEdgeEvent('export.generated', context, {
      field_set: body.field_set ?? 'rrhh-default',
      format: body.format ?? 'csv',
      row_count: rowCount,
    });

    return json(
      {
        request_id: context.requestId,
        export_id: crypto.randomUUID(),
        download_url: 'export-placeholder',
        row_count: rowCount,
        expires_in_seconds: EDGE_LIMITS.signedUrlTtlSeconds,
        format: body.format ?? 'csv',
        field_set: body.field_set ?? 'rrhh-default',
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

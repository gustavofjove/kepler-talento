export const corsHeaders = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Headers':
    'authorization, x-client-info, apikey, content-type, x-outbox-secret, x-request-id, x-idempotency-key',
  'Access-Control-Allow-Methods': 'POST, OPTIONS',
};

export type EdgeErrorCode =
  | 'UNAUTHENTICATED'
  | 'FORBIDDEN'
  | 'VALIDATION_ERROR'
  | 'NOT_FOUND'
  | 'CONFLICT'
  | 'RATE_LIMITED'
  | 'INTERNAL_ERROR';

export interface RequestContext {
  requestId: string;
  idempotencyKey: string;
}

export class EdgeHttpError extends Error {
  constructor(
    readonly code: EdgeErrorCode,
    readonly status: number,
    message: string,
    readonly details: Record<string, unknown> = {},
  ) {
    super(message);
  }
}

export function requestContext(request: Request): RequestContext {
  return {
    requestId: request.headers.get('x-request-id') ?? crypto.randomUUID(),
    idempotencyKey: request.headers.get('x-idempotency-key') ?? '',
  };
}

export function json(body: unknown, status = 200, requestId?: string): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      ...corsHeaders,
      'content-type': 'application/json; charset=utf-8',
      ...(requestId ? { 'x-request-id': requestId } : {}),
    },
  });
}

export function jsonError(
  context: RequestContext,
  code: EdgeErrorCode,
  message: string,
  status: number,
  details: Record<string, unknown> = {},
): Response {
  return json(
    {
      error: {
        code,
        message,
        request_id: context.requestId,
        details,
      },
    },
    status,
    context.requestId,
  );
}

export function requireBearerAuth(request: Request): void {
  const authorization = request.headers.get('authorization') ?? '';
  if (!authorization.toLowerCase().startsWith('bearer ')) {
    throw new EdgeHttpError('UNAUTHENTICATED', 401, 'Authorization bearer token is required.');
  }
}

export function logEdgeEvent(
  event: string,
  context: RequestContext,
  payload: Record<string, unknown> = {},
): void {
  console.log(
    JSON.stringify({
      ts: new Date().toISOString(),
      event,
      request_id: context.requestId,
      ...payload,
    }),
  );
}

export async function readJson<T>(request: Request): Promise<T> {
  try {
    return (await request.json()) as T;
  } catch {
    throw new EdgeHttpError('VALIDATION_ERROR', 400, 'invalid_json');
  }
}

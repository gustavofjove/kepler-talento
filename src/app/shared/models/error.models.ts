export type AppErrorCode =
  | 'UNAUTHENTICATED'
  | 'FORBIDDEN'
  | 'VALIDATION_ERROR'
  | 'NOT_FOUND'
  | 'CONFLICT'
  | 'RATE_LIMITED'
  | 'INTERNAL_ERROR';

export interface AppErrorEnvelope {
  error: {
    code: AppErrorCode;
    message: string;
    request_id?: string;
    details?: Record<string, unknown>;
  };
}

export const APP_ERROR_MESSAGES: Record<AppErrorCode, string> = {
  UNAUTHENTICATED: 'Sesión no valida o expirada.',
  FORBIDDEN: 'Operación no autorizada.',
  VALIDATION_ERROR: 'Hay datos inválidos en la solicitud.',
  NOT_FOUND: 'El recurso solicitado no existe.',
  CONFLICT: 'La solicitud entra en conflicto con el estado actual.',
  RATE_LIMITED: 'Demasiadas solicitudes. Intenta de nuevo en unos segundos.',
  INTERNAL_ERROR: 'Se produjo un error interno inesperado.',
};

export class AppError extends Error {
  constructor(
    readonly code: AppErrorCode,
    message?: string,
    readonly details?: Record<string, unknown>,
  ) {
    super(message ?? APP_ERROR_MESSAGES[code]);
  }
}

export function toAppError(
  error: unknown,
  fallbackCode: AppErrorCode = 'INTERNAL_ERROR',
): AppError {
  if (error instanceof AppError) {
    return error;
  }
  if (error instanceof Error) {
    return new AppError(fallbackCode, error.message);
  }
  return new AppError(fallbackCode);
}

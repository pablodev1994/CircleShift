import { HttpErrorResponse } from '@angular/common/http';

/** The error envelope every `/api/v1` failure uses (see services/api/README.md). */
export interface ApiError {
  readonly status: number;
  readonly code: string;
  readonly message: string;
  readonly requestId?: string;
  readonly details?: Readonly<Record<string, readonly string[]>>;
}

/** Turns anything HttpClient throws into an {@link ApiError}, so pages handle one shape. */
export function toApiError(error: unknown): ApiError {
  if (error instanceof HttpErrorResponse) {
    const body = error.error as Partial<ApiError> | null;
    if (body && typeof body === 'object' && typeof body.code === 'string') {
      return {
        status: error.status,
        code: body.code,
        message: typeof body.message === 'string' ? body.message : 'Something went wrong.',
        requestId: body.requestId,
        details: body.details,
      };
    }
    if (error.status === 0) {
      return { status: 0, code: 'network.unavailable', message: 'The server could not be reached. Try again.' };
    }
    return { status: error.status, code: 'http.error', message: 'Something went wrong. Try again.' };
  }
  return { status: 0, code: 'client.error', message: 'Something went wrong. Try again.' };
}

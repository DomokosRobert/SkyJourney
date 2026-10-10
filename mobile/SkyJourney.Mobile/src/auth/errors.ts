import { ApiError } from '@/api/client';

/** User-facing message for a failed sign-in or registration. */
export function describeAuthError(error: unknown): string {
  if (!(error instanceof ApiError)) {
    return 'Something went wrong. Please try again.';
  }
  switch (error.code) {
    case 'invalid_credentials':
      return 'The e-mail address or password is incorrect.';
    case 'account_locked':
      return 'Too many failed attempts. Wait a few minutes, then try again.';
    case 'email_taken':
      return 'An account with this e-mail address already exists. Sign in instead.';
    case 'rate_limited':
      return 'Too many attempts from this device. Wait a minute, then try again.';
    default:
      return error.message;
  }
}

/** Server field errors keyed by form field, first message each. Only fields the form knows are kept. */
export function fieldErrorsFor<T extends string>(error: unknown, fields: readonly T[]): Partial<Record<T, string>> {
  if (!(error instanceof ApiError)) {
    return {};
  }
  const result: Partial<Record<T, string>> = {};
  for (const field of fields) {
    const message = error.fieldErrors[field]?.[0];
    if (message) {
      result[field] = message;
    }
  }
  return result;
}

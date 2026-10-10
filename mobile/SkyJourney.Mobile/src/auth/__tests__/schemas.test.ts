import { ApiError } from '@/api/client';
import { describeAuthError, fieldErrorsFor } from '../errors';
import { loginSchema, registerSchema } from '../schemas';

const valid = { firstName: 'Ada', lastName: 'Lovelace', email: 'ada@example.com', password: 'Correct-Horse-9' };

function messagesFor(input: unknown) {
  const result = registerSchema.safeParse(input);
  return result.success ? {} : result.error.flatten().fieldErrors;
}

describe('registerSchema', () => {
  it('accepts a valid registration and trims names and e-mail', () => {
    expect(registerSchema.parse({ ...valid, firstName: '  Ada ', email: ' ada@example.com ' })).toEqual(valid);
  });

  it.each([
    ['too short', 'Short-1a', 'Use at least 10 characters.'],
    ['no uppercase', 'correct-horse-9', 'Include an uppercase letter.'],
    ['no lowercase', 'CORRECT-HORSE-9', 'Include a lowercase letter.'],
    ['no digit', 'Correct-Horse', 'Include a digit.'],
  ])('rejects a password with %s', (_, password, message) => {
    expect(messagesFor({ ...valid, password }).password).toContain(message);
  });

  it('rejects an invalid e-mail and blank names', () => {
    const errors = messagesFor({ ...valid, email: 'not-an-email', firstName: '   ', lastName: '' });
    expect(errors.email).toEqual(['Enter a valid e-mail address.']);
    expect(errors.firstName).toEqual(['Enter your first name.']);
    expect(errors.lastName).toEqual(['Enter your last name.']);
  });

  it('rejects names longer than the API allows', () => {
    expect(messagesFor({ ...valid, lastName: 'x'.repeat(51) }).lastName).toEqual([
      'Your last name can be at most 50 characters.',
    ]);
  });
});

describe('loginSchema', () => {
  it('does not apply password rules at sign-in (old passwords stay usable)', () => {
    expect(loginSchema.safeParse({ email: 'ada@example.com', password: 'x' }).success).toBe(true);
  });

  it('requires both fields', () => {
    expect(loginSchema.safeParse({ email: '', password: '' }).success).toBe(false);
  });
});

describe('auth error helpers', () => {
  it('maps known error codes to friendly messages', () => {
    expect(describeAuthError(new ApiError('x', 401, 'account_locked'))).toMatch(/Too many failed attempts/);
    expect(describeAuthError(new ApiError('x', 409, 'email_taken'))).toMatch(/already exists/);
    expect(describeAuthError(new ApiError('x', 429, 'rate_limited'))).toMatch(/Wait a minute/);
    expect(describeAuthError(new ApiError('Server says no', 500, 'server_error'))).toBe('Server says no');
    expect(describeAuthError(new Error('boom'))).toBe('Something went wrong. Please try again.');
  });

  it('picks the first server message for known form fields only', () => {
    const error = new ApiError('Validation failed', 400, 'validation_failed', {
      password: ['Passwords must have at least one digit.', 'second'],
      unknown: ['ignored'],
    });
    expect(fieldErrorsFor(error, ['email', 'password'] as const)).toEqual({
      password: 'Passwords must have at least one digit.',
    });
  });
});

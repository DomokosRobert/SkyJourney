import { z } from 'zod';

// Mirror the API's AccountRules so most mistakes are caught before a round trip.
export const PASSWORD_MIN_LENGTH = 10;
const PASSWORD_MAX_LENGTH = 128;
const EMAIL_MAX_LENGTH = 254;
const NAME_MAX_LENGTH = 50;

const email = z
  .string()
  .trim()
  .min(1, 'Enter your e-mail address.')
  .max(EMAIL_MAX_LENGTH, 'That e-mail address is too long.')
  .pipe(z.email('Enter a valid e-mail address.'));

const name = (label: string) =>
  z
    .string()
    .trim()
    .min(1, `Enter your ${label}.`)
    .max(NAME_MAX_LENGTH, `Your ${label} can be at most ${NAME_MAX_LENGTH} characters.`);

export const loginSchema = z.object({
  email,
  password: z.string().min(1, 'Enter your password.'),
});

export const passwordSchema = z
  .string()
  .min(PASSWORD_MIN_LENGTH, `Use at least ${PASSWORD_MIN_LENGTH} characters.`)
  .max(PASSWORD_MAX_LENGTH, `Use at most ${PASSWORD_MAX_LENGTH} characters.`)
  .regex(/[a-z]/, 'Include a lowercase letter.')
  .regex(/[A-Z]/, 'Include an uppercase letter.')
  .regex(/[0-9]/, 'Include a digit.');

export const registerSchema = z.object({
  firstName: name('first name'),
  lastName: name('last name'),
  email,
  password: passwordSchema,
});

export const profileSchema = z.object({
  firstName: name('first name'),
  lastName: name('last name'),
});

export type LoginForm = z.infer<typeof loginSchema>;
export type RegisterForm = z.infer<typeof registerSchema>;
export type ProfileForm = z.infer<typeof profileSchema>;

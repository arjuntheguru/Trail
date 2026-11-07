import { z } from 'zod'

// Mirrors the API's rules (RuleExtensions.cs); the server stays the authority and its errors are shown too.
export const PASSWORD_MIN = 10
export const PASSWORD_MAX = 128

const email = z
  .string()
  .trim()
  .min(1, 'Enter your email address.')
  .max(254, 'That email address is too long.')
  .pipe(z.email('Enter a valid email address.'))

const personName = (label: string) =>
  z.string().trim().min(1, `Enter your ${label}.`).max(100, `Your ${label} is too long.`)

export const password = z
  .string()
  .min(PASSWORD_MIN, `Use at least ${PASSWORD_MIN} characters.`)
  .max(PASSWORD_MAX, `Use at most ${PASSWORD_MAX} characters.`)

export const loginSchema = z.object({
  email,
  password: z.string().min(1, 'Enter your password.'),
})
export type LoginValues = z.infer<typeof loginSchema>

export const registerSchema = z.object({
  organizationName: z
    .string()
    .trim()
    .min(1, 'Enter your business name.')
    .max(200, 'That name is too long.'),
  firstName: personName('first name'),
  lastName: personName('last name'),
  email,
  password,
})
export type RegisterValues = z.infer<typeof registerSchema>

export const forgotPasswordSchema = z.object({ email })
export type ForgotPasswordValues = z.infer<typeof forgotPasswordSchema>

export const resetPasswordSchema = z
  .object({ newPassword: password, confirmPassword: z.string() })
  .refine((v) => v.newPassword === v.confirmPassword, {
    path: ['confirmPassword'],
    message: 'The passwords do not match.',
  })
export type ResetPasswordValues = z.infer<typeof resetPasswordSchema>

/** Names and password are only needed by people who have no Trail account yet, so all three are optional. */
export const acceptInvitationSchema = z
  .object({
    firstName: z.string().trim().max(100, 'Your first name is too long.'),
    lastName: z.string().trim().max(100, 'Your last name is too long.'),
    password: z.string().max(PASSWORD_MAX, `Use at most ${PASSWORD_MAX} characters.`),
  })
  .superRefine((v, ctx) => {
    if (!v.password && !v.firstName && !v.lastName) return
    if (!v.firstName) {
      ctx.addIssue({ code: 'custom', path: ['firstName'], message: 'Enter your first name.' })
    }
    if (!v.lastName) {
      ctx.addIssue({ code: 'custom', path: ['lastName'], message: 'Enter your last name.' })
    }
    if (v.password.length < PASSWORD_MIN) {
      ctx.addIssue({
        code: 'custom',
        path: ['password'],
        message: `Use at least ${PASSWORD_MIN} characters.`,
      })
    }
  })
export type AcceptInvitationValues = z.infer<typeof acceptInvitationSchema>

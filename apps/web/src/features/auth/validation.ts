export type AuthMode =
  | "login"
  | "register"
  | "forgot-password"
  | "reset-password";
export function validateAuth(
  mode: AuthMode,
  values: {
    email: string;
    password: string;
    displayName: string;
    confirmation: string;
  },
) {
  const errors: Record<string, string> = {};
  if (
    mode !== "reset-password" &&
    (!/^[^\s@]+@[^\s@]+$/.test(values.email.trim()) ||
      /[^\x21-\x7e]/.test(values.email.trim()) ||
      values.email.trim().length > 254)
  )
    errors.email = "Enter a valid ASCII email address.";
  if (
    mode === "register" &&
    (!values.displayName.trim() || values.displayName.trim().length > 120)
  )
    errors.displayName = "Enter your name (up to 120 characters).";
  if (mode !== "forgot-password") {
    if (!values.password || values.password.length > 128)
      errors.password = "Enter your password (up to 128 characters).";
    if (
      mode !== "login" &&
      (values.password.length < 15 ||
        !values.password.trim() ||
        /[\x00-\x1f\x7f]/.test(values.password))
    )
      errors.password = "Use 15–128 characters without control characters.";
    if (mode !== "login" && values.password !== values.confirmation)
      errors.confirmation = "Your passwords do not match.";
  }
  return errors;
}

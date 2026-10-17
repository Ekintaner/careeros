// Only workspace routes may be used as post-authentication destinations.
export function safeReturnTo(value: unknown): string {
  if (
    typeof value !== "string" ||
    value.length > 4096 ||
    /[\\\x00-\x20\x7f]/.test(value)
  )
    return "/";
  if (!value.startsWith("/") || value.startsWith("//")) return "/";
  try {
    const url = new URL(value, "http://workspace.invalid");
    if (
      url.origin !== "http://workspace.invalid" ||
      !/^\/(?:$|(?:profile|settings|companies|jobs|applications|resumes|documents|skills|interviews|tasks|analytics|assistance)(?:\/|$))/.test(
        url.pathname,
      )
    )
      return "/";
    // Encoded path separators/control characters have different interpretations across routers.
    if (/%(?:2f|5c|0[0-9a-f]|1[0-9a-f]|7f)/i.test(url.pathname)) return "/";
    return url.pathname + url.search + url.hash;
  } catch {
    return "/";
  }
}
export function loginDestination(value: unknown, login = "/login"): string {
  const target = safeReturnTo(value);
  return target === "/"
    ? login
    : `${login}${login.includes("?") ? "&" : "?"}returnTo=${encodeURIComponent(target)}`;
}

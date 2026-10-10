import type { ProblemDetails } from "@/lib/types/account";

export class AccountApiError extends Error {
  constructor(
    readonly status: number,
    readonly problem: ProblemDetails | null,
  ) {
    super(problem?.title ?? `Request failed with status ${status}.`);
  }

  // First validation message for a field, or the problem title.
  fieldMessage(field: string) {
    const match = Object.entries(this.problem?.errors ?? {}).find(([key]) => key.toLowerCase() === field.toLowerCase());
    return match?.[1]?.[0] ?? null;
  }
}

// Calls the same-origin /api/account proxy, which attaches the httpOnly session cookie server-side.
export async function accountRequest<T>(path: string, init: { method?: string; body?: unknown } = {}): Promise<T> {
  const response = await fetch(`/api/account/${path}`, {
    method: init.method ?? "GET",
    headers: init.body === undefined ? { Accept: "application/json" } : { Accept: "application/json", "Content-Type": "application/json" },
    body: init.body === undefined ? undefined : JSON.stringify(init.body),
    cache: "no-store",
  });

  if (response.status === 401) {
    window.location.assign(`/sign-in?next=${encodeURIComponent(window.location.pathname)}`);
  }

  if (!response.ok) {
    throw new AccountApiError(response.status, (await response.json().catch(() => null)) as ProblemDetails | null);
  }

  return (response.status === 204 ? undefined : await response.json()) as T;
}

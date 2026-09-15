export const API_BASE_URL = (
  import.meta.env.VITE_API_BASE_URL ?? "https://localhost:7201"
).replace(/\/$/, "");
let getAccessToken: () => string | null = () => null;
let handleUnauthorized: () => void = () => undefined;

export function configureHttpClient(authentication: {
  getAccessToken: () => string | null;
  onUnauthorized: () => void;
}) {
  getAccessToken = authentication.getAccessToken;
  handleUnauthorized = authentication.onUnauthorized;
}

export class ApiError extends Error {
  readonly status: number;
  readonly details?: unknown;

  constructor(message: string, status: number, details?: unknown) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.details = details;
  }
}

const statusMessages: Record<number, string> = {
  400: "Check the information you entered and try again.",
  401: "Your session has expired. Sign in and try again.",
  403: "You do not have permission to perform this action.",
  404: "The requested record was not found.",
  409: "The action conflicts with the current state. Refresh and try again.",
  429: "Too many attempts. Wait a moment and try again.",
};

function problemMessage(details: unknown) {
  if (typeof details === "string" && details.trim()) return details;
  if (!details || typeof details !== "object") return null;
  if ("errors" in details && details.errors && typeof details.errors === "object") {
    const messages = Object.values(details.errors).flatMap(value =>
      Array.isArray(value) ? value.filter(item => typeof item === "string") : [],
    );
    if (messages.length) return messages.join(" ");
  }
  if ("detail" in details && typeof details.detail === "string" && details.detail.trim())
    return details.detail;
  if ("title" in details && typeof details.title === "string" && details.title.trim())
    return details.title;
  return null;
}

export function getApiErrorMessage(error: unknown, fallback = "The request could not be completed. Please try again.") {
  if (!(error instanceof ApiError)) return error instanceof Error ? error.message : fallback;
  if (error.status >= 500) return "The service is temporarily unavailable. Please try again.";
  return problemMessage(error.details) ?? statusMessages[error.status] ?? fallback;
}

type RequestOptions = Omit<RequestInit, "body"> & { body?: unknown };

export async function request<T>(
  path: string,
  options: RequestOptions = {},
): Promise<T> {
  const headers = new Headers(options.headers);
  headers.set("Accept", "application/json");
  if (options.body !== undefined)
    headers.set("Content-Type", "application/json");
  const accessToken = getAccessToken();
  if (accessToken) headers.set("Authorization", `Bearer ${accessToken}`);
  const response = await fetch(
    `${API_BASE_URL}${path.startsWith("/") ? path : `/${path}`}`,
    {
      ...options,
      headers,
      body:
        options.body === undefined ? undefined : JSON.stringify(options.body),
    },
  );
  if (!response.ok) {
    if (response.status === 401) handleUnauthorized();
    const details = await response.json().catch(() => undefined);
    const error = new ApiError(statusMessages[response.status] ?? "The request could not be completed. Please try again.", response.status,
      response.status >= 500 ? undefined : details);
    error.message = getApiErrorMessage(error);
    throw error;
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

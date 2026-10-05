import type { ApiErrorBody, ApiEnvelope } from "./models";

const apiRoot = "/api/v1";
const authRoutes = new Set(["/auth/login", "/auth/register", "/auth/logout", "/auth/refresh"]);
let refreshRequest: Promise<boolean> | undefined;

export class ApiClientError extends Error {
  readonly status: number;
  readonly code: string | undefined;
  readonly traceId: string | undefined;

  constructor(
    message: string,
    status: number,
    code?: string,
    traceId?: string,
  ) {
    super(message);
    this.name = "ApiClientError";
    this.status = status;
    this.code = code;
    this.traceId = traceId;
  }
}

function isApiEnvelope(value: unknown): value is ApiEnvelope<unknown> {
  return (
    typeof value === "object" &&
    value !== null &&
    "data" in value &&
    "message" in value &&
    typeof value.message === "string"
  );
}

function isApiErrorBody(value: unknown): value is ApiErrorBody {
  if (typeof value !== "object" || value === null || !("error" in value)) {
    return false;
  }

  const error = value.error;
  return (
    typeof error === "object" &&
    error !== null &&
    "code" in error &&
    "message" in error &&
    typeof error.code === "string" &&
    typeof error.message === "string"
  );
}

async function send(path: string, init: RequestInit): Promise<Response> {
  const headers = new Headers(init.headers);
  headers.set("Accept", "application/json");
  if (init.body !== undefined && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  return fetch(`${apiRoot}${path}`, {
    ...init,
    headers,
    credentials: "include",
    cache: "no-store",
  });
}

function refreshSession(): Promise<boolean> {
  if (!refreshRequest) {
    refreshRequest = send("/auth/refresh", { method: "POST" })
      .then((response) => response.ok)
      .finally(() => {
        refreshRequest = undefined;
      });
  }

  return refreshRequest;
}

async function decode<T>(response: Response): Promise<T> {
  if (response.status === 204) {
    return undefined as T;
  }

  const text = await response.text();
  let body: unknown;
  if (text.length > 0) {
    try {
      body = JSON.parse(text) as unknown;
    } catch {
      throw new ApiClientError(
        "The server returned an unreadable response. Try again, and contact support if it continues.",
        response.status,
      );
    }
  }

  if (!response.ok) {
    if (isApiErrorBody(body)) {
      throw new ApiClientError(
        body.error.message,
        response.status,
        body.error.code,
        body.traceId,
      );
    }

    throw new ApiClientError(
      `The request failed with status ${response.status}. Try again.`,
      response.status,
    );
  }

  if (!isApiEnvelope(body)) {
    throw new ApiClientError(
      "The server response did not match the expected API contract.",
      response.status,
    );
  }

  return body.data as T;
}

export async function apiRequest<T>(
  path: string,
  init: RequestInit = {},
  options: { refreshOnUnauthorized?: boolean } = {},
): Promise<T> {
  let response = await send(path, init);
  const refreshOnUnauthorized =
    options.refreshOnUnauthorized ?? !authRoutes.has(path);

  if (response.status === 401 && refreshOnUnauthorized) {
    if (await refreshSession()) {
      response = await send(path, init);
    }
  }

  return decode<T>(response);
}

export function jsonRequest(
  method: "POST" | "PUT" | "PATCH",
  value?: unknown,
): RequestInit {
  return {
    method,
    ...(value === undefined ? {} : { body: JSON.stringify(value) }),
  };
}

export function withQuery(
  path: string,
  values: Record<string, string | number | boolean | null | undefined>,
): string {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(values)) {
    if (value !== null && value !== undefined && value !== "") {
      params.set(key, String(value));
    }
  }

  const query = params.toString();
  return query.length > 0 ? `${path}?${query}` : path;
}

export const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? 'https://localhost:7201').replace(/\/$/, '')
let getAccessToken: () => string | null = () => null
let handleUnauthorized: () => void = () => undefined

export function configureHttpClient(authentication: { getAccessToken: () => string | null; onUnauthorized: () => void }) {
  getAccessToken = authentication.getAccessToken
  handleUnauthorized = authentication.onUnauthorized
}

export class ApiError extends Error {
  readonly status: number
  readonly details?: unknown

  constructor(message: string, status: number, details?: unknown) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.details = details
  }
}

type RequestOptions = Omit<RequestInit, 'body'> & { body?: unknown }

export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const headers = new Headers(options.headers)
  headers.set('Accept', 'application/json')
  if (options.body !== undefined) headers.set('Content-Type', 'application/json')
  const accessToken = getAccessToken()
  if (accessToken) headers.set('Authorization', `Bearer ${accessToken}`)
  const response = await fetch(`${API_BASE_URL}${path.startsWith('/') ? path : `/${path}`}`, {
    ...options, headers, body: options.body === undefined ? undefined : JSON.stringify(options.body),
  })
  if (!response.ok) {
    if (response.status === 401) handleUnauthorized()
    const details = await response.json().catch(() => undefined)
    const message = typeof details === 'object' && details && 'title' in details ? String(details.title) : `Request failed with status ${response.status}`
    throw new ApiError(message, response.status, details)
  }
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

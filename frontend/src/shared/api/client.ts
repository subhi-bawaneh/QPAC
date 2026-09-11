import axios, { AxiosError, type AxiosRequestConfig } from 'axios'
import { tokenStorage } from '@/shared/auth/tokenStorage'
import type { AuthResult, ProblemDetails } from './types'

// Default matches the API's launchSettings profile, so `npm run dev` next to
// `dotnet run` needs no .env at all. See docs/local-dev.md.
export const apiBaseUrl = (import.meta.env.VITE_API_URL ?? 'http://localhost:5001').replace(/\/$/, '')

export const api = axios.create({
  baseURL: apiBaseUrl,
  headers: { 'Content-Type': 'application/json' },
})

api.interceptors.request.use((config) => {
  const token = tokenStorage.getAccessToken()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// A single refresh runs at a time; everything that 401s while it is in flight waits
// for the same promise instead of starting its own refresh.
let refreshing: Promise<string | null> | null = null

async function refreshAccessToken(): Promise<string | null> {
  const refreshToken = tokenStorage.getRefreshToken()
  if (!refreshToken) return null

  try {
    // A bare client: this request must not go through the 401 interceptor.
    const response = await axios.post<AuthResult>(`${apiBaseUrl}/api/auth/refresh`, { refreshToken })
    tokenStorage.save(response.data)
    return response.data.accessToken
  } catch {
    tokenStorage.clear()
    return null
  }
}

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const request = error.config as (AxiosRequestConfig & { _retried?: boolean }) | undefined

    if (error.response?.status !== 401 || !request || request._retried) {
      return Promise.reject(error)
    }

    request._retried = true
    refreshing ??= refreshAccessToken().finally(() => {
      refreshing = null
    })

    const token = await refreshing
    if (!token) {
      onSessionExpired?.()
      return Promise.reject(error)
    }

    request.headers = { ...request.headers, Authorization: `Bearer ${token}` }
    return api.request(request)
  },
)

// The auth provider registers a callback so an expired session lands on the login
// page instead of leaving the app in a half-authenticated state.
let onSessionExpired: (() => void) | null = null
export function setSessionExpiredHandler(handler: (() => void) | null) {
  onSessionExpired = handler
}

/** Turns an axios failure into the message the API actually sent. */
export function apiErrorMessage(error: unknown, fallback = 'Something went wrong'): string {
  if (axios.isAxiosError(error)) {
    const problem = error.response?.data as ProblemDetails | undefined
    if (problem?.errors) {
      const first = Object.values(problem.errors).flat()[0]
      if (first) return first
    }
    if (problem?.detail) return problem.detail
    if (problem?.title) return problem.title
    if (error.code === 'ERR_NETWORK') return 'Cannot reach the API. Is it running?'
  }
  return fallback
}

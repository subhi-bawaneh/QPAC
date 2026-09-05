import type { AuthResult, UserSummary } from '@/shared/api/types'

// The access token lives in memory only: it is short-lived and keeping it out of
// storage means an XSS payload cannot read it back later. The refresh token is
// persisted so a page reload can restore the session (PLAN.md § 8 — until the API
// serves it as an httpOnly cookie).
const REFRESH_KEY = 'dip.refreshToken'
const USER_KEY = 'dip.user'

let accessToken: string | null = null

export const tokenStorage = {
  getAccessToken: () => accessToken,
  setAccessToken: (token: string | null) => {
    accessToken = token
  },

  getRefreshToken: () => safeRead(REFRESH_KEY),
  getCachedUser: (): UserSummary | null => {
    const raw = safeRead(USER_KEY)
    if (!raw) return null
    try {
      return JSON.parse(raw) as UserSummary
    } catch {
      return null
    }
  },

  save: (auth: AuthResult) => {
    accessToken = auth.accessToken
    safeWrite(REFRESH_KEY, auth.refreshToken)
    safeWrite(USER_KEY, JSON.stringify(auth.user))
  },

  clear: () => {
    accessToken = null
    safeRemove(REFRESH_KEY)
    safeRemove(USER_KEY)
  },
}

// Storage throws in private-mode browsers and when site data is blocked.
function safeRead(key: string) {
  try {
    return window.localStorage.getItem(key)
  } catch {
    return null
  }
}

function safeWrite(key: string, value: string) {
  try {
    window.localStorage.setItem(key, value)
  } catch {
    /* session simply won't survive a reload */
  }
}

function safeRemove(key: string) {
  try {
    window.localStorage.removeItem(key)
  } catch {
    /* nothing to do */
  }
}

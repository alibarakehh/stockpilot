export const UNAUTHORIZED_EVENT = 'stockpilot:unauthorized'

const LEGACY_KEYS = ['stockpilot_session', 'stockpilot_session_expiry', 'stockpilot_user'] as const

export function clearAuthentication() {
  for (const key of LEGACY_KEYS) {
    sessionStorage.removeItem(key)
    localStorage.removeItem(key)
  }
}

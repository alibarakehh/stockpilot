import { beforeEach, describe, expect, it } from 'vitest'
import { clearAuthentication } from './session'

describe('authentication storage', () => {
  beforeEach(() => {
    sessionStorage.clear()
    localStorage.clear()
  })

  it('removes browser-token state left by earlier releases', () => {
    sessionStorage.setItem('stockpilot_session', 'legacy-token')
    sessionStorage.setItem('stockpilot_session_expiry', new Date().toISOString())
    localStorage.setItem('stockpilot_user', '{}')

    clearAuthentication()

    expect(sessionStorage.getItem('stockpilot_session')).toBeNull()
    expect(sessionStorage.getItem('stockpilot_session_expiry')).toBeNull()
    expect(localStorage.getItem('stockpilot_user')).toBeNull()
  })

  it('does not persist authentication material in browser storage', () => {
    clearAuthentication()

    expect(sessionStorage.length).toBe(0)
    expect(localStorage.length).toBe(0)
  })
})

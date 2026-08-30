import { createPinia, setActivePinia } from 'pinia'
import { afterEach, describe, expect, it } from 'vitest'
import { useLocaleStore } from '../stores/locale'

describe('locale store', () => {
  afterEach(() => {
    window.localStorage.clear()
    document.documentElement.lang = 'vi'
  })

  it('defaults to Vietnamese when no preference exists', () => {
    window.localStorage.clear()
    setActivePinia(createPinia())

    const locale = useLocaleStore()

    expect(locale.locale).toBe('vi')
    expect(locale.t('nav.dashboard')).toBe('Bảng điều khiển')
    expect(document.documentElement.lang).toBe('vi')
  })

  it('persists the selected language and updates translations', () => {
    setActivePinia(createPinia())
    const locale = useLocaleStore()

    locale.setLocale('en')

    expect(locale.locale).toBe('en')
    expect(locale.t('nav.dashboard')).toBe('Dashboard')
    expect(window.localStorage.getItem('tcflow.locale')).toBe('en')
    expect(document.documentElement.lang).toBe('en')
  })
})

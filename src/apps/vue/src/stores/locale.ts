import { defineStore } from 'pinia'
import { ref } from 'vue'

export type Locale = 'vi' | 'en'

export const localeOptions = [
  { value: 'vi', label: 'Tiếng Việt' },
  { value: 'en', label: 'English' },
] as const

const storageKey = 'tcflow.locale'

const messages: Record<Locale, Record<string, string>> = {
  vi: {
    'common.skipToContent': 'Đi đến nội dung chính',
    'common.home': 'Trang chủ VietAIS TCFlow',
    'common.workspaceOnline': 'Không gian làm việc đang hoạt động',
    'common.activeProject': 'Dự án đang chọn',
    'common.primaryNavigation': 'Điều hướng chính',
    'common.quickNavigation': 'Điều hướng nhanh',
    'common.selectedProject': 'Dự án được chọn',
    'common.selectProject': 'Chọn một dự án',
    'common.sourceAwarePlanner': 'Lập kế hoạch dựa trên source',
    'common.portfolio': 'Danh mục dự án',
    'common.signOut': 'Đăng xuất',
    'common.language': 'Ngôn ngữ',
    'common.toggleNavigation': 'Mở hoặc đóng điều hướng',
    'common.backendEnforced': 'Backend thực thi',
    'common.scopedAuthorization': 'Phân quyền theo phạm vi',
    'common.requires': 'Yêu cầu',
    'section.workspace': 'Không gian làm việc',
    'section.project': 'Dự án',
    'section.platform': 'Nền tảng',
    'nav.dashboard': 'Bảng điều khiển',
    'nav.projects': 'Dự án',
    'nav.repositories': 'Kho mã nguồn',
    'nav.analysis': 'Phân tích',
    'nav.impactGraph': 'Đồ thị ảnh hưởng',
    'nav.features': 'Tính năng',
    'nav.taskBoard': 'Bảng công việc',
    'nav.projectAdmin': 'Quản trị dự án',
    'nav.systemAdmin': 'Quản trị hệ thống',
  },
  en: {
    'common.skipToContent': 'Skip to content',
    'common.home': 'VietAIS TCFlow home',
    'common.workspaceOnline': 'Workspace online',
    'common.activeProject': 'Active project',
    'common.primaryNavigation': 'Primary navigation',
    'common.quickNavigation': 'Quick navigation',
    'common.selectedProject': 'Selected project',
    'common.selectProject': 'Select a project',
    'common.sourceAwarePlanner': 'Source-aware planner',
    'common.portfolio': 'Portfolio',
    'common.signOut': 'Sign out',
    'common.language': 'Language',
    'common.toggleNavigation': 'Toggle navigation',
    'common.backendEnforced': 'Backend enforced',
    'common.scopedAuthorization': 'Scoped authorization',
    'common.requires': 'Requires',
    'section.workspace': 'Workspace',
    'section.project': 'Project',
    'section.platform': 'Platform',
    'nav.dashboard': 'Dashboard',
    'nav.projects': 'Projects',
    'nav.repositories': 'Repositories',
    'nav.analysis': 'Analysis',
    'nav.impactGraph': 'Impact graph',
    'nav.features': 'Features',
    'nav.taskBoard': 'Task board',
    'nav.projectAdmin': 'Project admin',
    'nav.systemAdmin': 'System admin',
  },
}

function readLocale(): Locale {
  if (typeof window === 'undefined') return 'vi'
  return window.localStorage.getItem(storageKey) === 'en' ? 'en' : 'vi'
}

function applyDocumentLocale(locale: Locale): void {
  if (typeof document !== 'undefined') document.documentElement.lang = locale
}

export const useLocaleStore = defineStore('locale', () => {
  const locale = ref<Locale>(readLocale())
  applyDocumentLocale(locale.value)

  function setLocale(value: string): void {
    const nextLocale: Locale = value === 'en' ? 'en' : 'vi'
    locale.value = nextLocale
    if (typeof window !== 'undefined') window.localStorage.setItem(storageKey, nextLocale)
    applyDocumentLocale(nextLocale)
  }

  function t(key: string): string {
    return messages[locale.value][key] ?? messages.en[key] ?? key
  }

  return { locale, localeOptions, setLocale, t }
})

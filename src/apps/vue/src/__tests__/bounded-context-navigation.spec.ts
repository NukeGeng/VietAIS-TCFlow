import { describe, expect, it } from 'vitest'
import { projectNavigationFor } from '../modules/navigation'

describe('bounded-context navigation', () => {
  it('keeps project navigation routes scoped to the selected project', () => {
    const items = projectNavigationFor('project-1')
    expect(items.map((item) => item.path)).toContain('/projects/project-1/tasks')
    expect(items.every((item) => item.path.startsWith('/projects/project-1/'))).toBe(true)
  })

  it('exposes permission metadata for every context entry', () => {
    expect(projectNavigationFor('project-1').every((item) => item.permission.length > 0)).toBe(true)
  })
})

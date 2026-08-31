export type ProjectNavigationItem = {
  key: string
  icon: string
  path: string
  permission: string
  additionalPermissions?: string[]
}

/** Frontend navigation map mirrors backend bounded-context ownership. */
export function projectNavigationFor(projectId: string): ProjectNavigationItem[] {
  const prefix = `/projects/${projectId}`
  return [
    {
      key: 'repositories',
      icon: '◈',
      path: `${prefix}/repositories`,
      permission: 'repository.view',
    },
    { key: 'analysis', icon: '◌', path: `${prefix}/analysis`, permission: 'analysis.view' },
    { key: 'impactGraph', icon: '⌁', path: `${prefix}/impacts`, permission: 'task.view' },
    { key: 'features', icon: '✦', path: `${prefix}/features`, permission: 'feature.view' },
    { key: 'taskBoard', icon: '✓', path: `${prefix}/tasks`, permission: 'task.view' },
    {
      key: 'projectAdmin',
      icon: '⚙',
      path: `${prefix}/admin`,
      permission: 'role.view',
      additionalPermissions: [
        'member.view',
        'component.view',
        'project.update',
        'authority.view',
        'convention.view',
        'ai.policy.update',
        'audit.view',
      ],
    },
  ]
}

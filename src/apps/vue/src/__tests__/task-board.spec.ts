import { createPinia, setActivePinia } from 'pinia'
import { nextTick } from 'vue'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import TaskBoardView from '../views/TaskBoardView.vue'
import { useWorkspaceStore } from '../stores/workspace'
import {
  HumanApprovalStatus,
  TaskLifecycleStatus,
  type PermissionGrantTrace,
} from '../types/contracts'
import { taskFixture } from './fixtures'

describe('task board review boundary', () => {
  afterEach(() => vi.restoreAllMocks())

  it('keeps completion disabled until human approval exists', async () => {
    setActivePinia(createPinia())
    const workspace = useWorkspaceStore()
    const task = taskFixture({
      status: TaskLifecycleStatus.ReadyForReview,
      humanApproval: HumanApprovalStatus.Pending,
    })
    workspace.selectProject(task.projectId)
    workspace.tasks = [task]
    workspace.tasksState = { status: 'ready' }
    workspace.effectivePermissions = {
      projectId: task.projectId,
      userId: task.createdBy,
      grants: [grant('task.view'), grant('task.approve')],
    }
    vi.spyOn(workspace, 'loadTasks').mockResolvedValue()
    vi.spyOn(workspace, 'loadRepositories').mockResolvedValue()

    const wrapper = mount(TaskBoardView, {
      global: { stubs: { RouterLink: { template: '<a><slot /></a>' } } },
    })
    const complete = wrapper.findAll('button').find((button) => button.text() === 'Completed')
    expect(complete?.attributes('disabled')).toBeDefined()
    expect(complete?.attributes('title')).toBe('Requires explicit human approval')

    workspace.tasks = [taskFixture({ ...task, humanApproval: HumanApprovalStatus.Approved })]
    await nextTick()
    const enabled = wrapper.findAll('button').find((button) => button.text() === 'Completed')
    expect(enabled?.attributes('disabled')).toBeUndefined()
  })

  it('renders suggested tasks and gates promotion on task creation permission', async () => {
    setActivePinia(createPinia())
    const workspace = useWorkspaceStore()
    const task = taskFixture({ status: TaskLifecycleStatus.Suggested })
    workspace.selectProject(task.projectId)
    workspace.tasks = [task]
    workspace.tasksState = { status: 'ready' }
    workspace.effectivePermissions = {
      projectId: task.projectId,
      userId: task.createdBy,
      grants: [grant('task.view')],
    }
    vi.spyOn(workspace, 'loadTasks').mockResolvedValue()
    vi.spyOn(workspace, 'loadRepositories').mockResolvedValue()

    const wrapper = mount(TaskBoardView, {
      global: { stubs: { RouterLink: { template: '<a><slot /></a>' } } },
    })
    expect(wrapper.text()).toContain('Suggested')
    const promote = wrapper.findAll('button').find((button) => button.text() === 'Upcoming')
    expect(promote?.attributes('disabled')).toBeDefined()
    expect(promote?.attributes('title')).toBe('Requires task.create')

    workspace.effectivePermissions = {
      ...workspace.effectivePermissions,
      grants: [grant('task.view'), grant('task.create')],
    }
    await nextTick()
    expect(promote?.attributes('disabled')).toBeUndefined()
  })
})

function grant(permissionCode: string): PermissionGrantTrace {
  return {
    permissionCode,
    roleId: '40000000-0000-0000-0000-000000000001',
    roleName: 'Reviewer',
    resourceScope: 1,
    componentScopes: [],
  }
}

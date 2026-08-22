import { createPinia, setActivePinia } from 'pinia'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { tcflowApi } from '../services/tcflow-api'
import { useWorkspaceStore } from '../stores/workspace'
import { TaskLifecycleStatus } from '../types/contracts'
import { page, taskFixture } from './fixtures'

describe('workspace store', () => {
  afterEach(() => vi.restoreAllMocks())

  it('reloads the board from backend state after a task transition', async () => {
    setActivePinia(createPinia())
    const before = taskFixture()
    const after = taskFixture({
      status: TaskLifecycleStatus.ReadyForReview,
      currentVersion: 2,
    })
    vi.spyOn(tcflowApi, 'tasks')
      .mockResolvedValueOnce(page([before]))
      .mockResolvedValueOnce(page([after]))
    const transition = vi.spyOn(tcflowApi, 'transitionTask').mockResolvedValue(after)

    const workspace = useWorkspaceStore()
    workspace.selectProject(before.projectId)
    await workspace.loadTasks()
    await workspace.transitionTask(before.id, TaskLifecycleStatus.ReadyForReview)

    expect(transition).toHaveBeenCalledWith(
      before.projectId,
      before.id,
      TaskLifecycleStatus.ReadyForReview,
      undefined,
    )
    expect(tcflowApi.tasks).toHaveBeenCalledTimes(2)
    expect(workspace.tasks[0]?.status).toBe(TaskLifecycleStatus.ReadyForReview)
    expect(workspace.tasks[0]?.currentVersion).toBe(2)
  })
})

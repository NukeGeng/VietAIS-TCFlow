<script setup lang="ts">
import { computed, ref } from 'vue'
import { storeToRefs } from 'pinia'
import PermissionNotice from '../components/PermissionNotice.vue'
import ResourceState from '../components/ResourceState.vue'
import { useWorkspaceStore } from '../stores/workspace'
import {
  HumanApprovalStatus,
  TaskLifecycleStatus,
  TaskPriority,
  taskPriorityLabel,
  taskStatusLabel,
  type EngineeringTask,
} from '../types/contracts'

const workspace = useWorkspaceStore()
const { tasks, tasksState, repositories, selectedProject } = storeToRefs(workspace)
const keyword = ref('')
const title = ref('')
const description = ref('')
const repositoryId = ref('')
const priority = ref(TaskPriority.Medium)
const showCreate = ref(false)
const actionError = ref('')

const statuses = Object.values(TaskLifecycleStatus).filter(
  (value): value is TaskLifecycleStatus => typeof value === 'number',
)
const columns = computed(() =>
  statuses.map((status) => ({
    status,
    tasks: tasks.value.filter((task) => task.status === status),
  })),
)

const transitions: Partial<Record<TaskLifecycleStatus, TaskLifecycleStatus[]>> = {
  [TaskLifecycleStatus.Upcoming]: [TaskLifecycleStatus.InProgress, TaskLifecycleStatus.Cancelled],
  [TaskLifecycleStatus.InProgress]: [
    TaskLifecycleStatus.ReadyForReview,
    TaskLifecycleStatus.Blocked,
    TaskLifecycleStatus.Cancelled,
  ],
  [TaskLifecycleStatus.ReadyForReview]: [
    TaskLifecycleStatus.Completed,
    TaskLifecycleStatus.Rejected,
    TaskLifecycleStatus.InProgress,
  ],
  [TaskLifecycleStatus.Blocked]: [TaskLifecycleStatus.InProgress, TaskLifecycleStatus.Cancelled],
  [TaskLifecycleStatus.Rejected]: [TaskLifecycleStatus.InProgress, TaskLifecycleStatus.Cancelled],
}

function transitionPermission(status: TaskLifecycleStatus): string {
  if (status === TaskLifecycleStatus.Completed) return 'task.approve'
  if (status === TaskLifecycleStatus.Rejected) return 'task.reject'
  return 'task.status.update'
}

function transitionUnavailableReason(task: EngineeringTask, status: TaskLifecycleStatus): string {
  const permission = transitionPermission(status)
  if (!workspace.hasPermission(permission)) return `Requires ${permission}`
  if (
    status === TaskLifecycleStatus.Completed &&
    task.humanApproval !== HumanApprovalStatus.Approved
  ) {
    return 'Requires explicit human approval'
  }
  return ''
}

async function move(task: EngineeringTask, status: TaskLifecycleStatus): Promise<void> {
  actionError.value = ''
  try {
    await workspace.transitionTask(task.id, status)
  } catch (error) {
    actionError.value = error instanceof Error ? error.message : 'Unable to update task status.'
  }
}

async function createTask(): Promise<void> {
  actionError.value = ''
  try {
    await workspace.createTask({
      repositoryId: repositoryId.value || undefined,
      title: title.value,
      description: description.value || undefined,
      priority: priority.value,
      artifactIds: [],
      impactIds: [],
      affectedArtifacts: [],
      inputs: [],
      outputs: [],
      businessRules: [],
      dependencies: [],
    })
    title.value = ''
    description.value = ''
    showCreate.value = false
  } catch (error) {
    actionError.value = error instanceof Error ? error.message : 'Unable to create task.'
  }
}

async function search(): Promise<void> {
  await workspace.loadTasks({ keyword: keyword.value || undefined })
}

Promise.all([workspace.loadTasks(), workspace.loadRepositories()])
</script>

<template>
  <section class="page-heading page-heading--with-actions">
    <div>
      <span class="eyebrow">{{ selectedProject?.name }}</span>
      <h1>Task board</h1>
      <p>Lifecycle state is reloaded from the backend after every transition.</p>
    </div>
    <button
      v-if="workspace.hasPermission('task.create')"
      class="primary-button"
      type="button"
      @click="showCreate = !showCreate"
    >
      {{ showCreate ? 'Close form' : 'New task' }}
    </button>
  </section>

  <form class="toolbar" role="search" @submit.prevent="search">
    <label class="search-field"
      ><span class="sr-only">Search tasks</span
      ><input v-model="keyword" placeholder="Search title or description"
    /></label>
    <button class="secondary-button" type="submit">Search</button>
    <span>{{ tasks.length }} visible tasks</span>
  </form>

  <form v-if="showCreate" class="form-card form-card--horizontal" @submit.prevent="createTask">
    <label
      >Title<input
        v-model="title"
        required
        maxlength="240"
        placeholder="Implement verified contract"
    /></label>
    <label
      >Repository<select v-model="repositoryId">
        <option value="">Project-wide</option>
        <option v-for="repository in repositories" :key="repository.id" :value="repository.id">
          {{ repository.name }}
        </option>
      </select></label
    >
    <label
      >Priority<select v-model="priority">
        <option v-for="(_, value) in taskPriorityLabel" :key="value" :value="Number(value)">
          {{ taskPriorityLabel[Number(value) as TaskPriority] }}
        </option>
      </select></label
    >
    <label class="field-grow"
      >Description<input v-model="description" placeholder="Acceptance criteria and evidence"
    /></label>
    <button class="primary-button" type="submit">Create</button>
  </form>

  <div v-if="actionError" class="inline-alert" role="alert">{{ actionError }}</div>

  <ResourceState
    :state="tasksState"
    empty-title="No tasks match this view"
    empty-message="Create a task or clear the active search."
    @retry="workspace.loadTasks()"
  >
    <div class="kanban" aria-label="Task lifecycle board">
      <section v-for="column in columns" :key="column.status" class="kanban-column">
        <header>
          <span :class="['status-dot-small', `status-${column.status}`]"></span
          ><strong>{{ taskStatusLabel[column.status] }}</strong
          ><span>{{ column.tasks.length }}</span>
        </header>
        <div class="kanban-stack">
          <article v-for="task in column.tasks" :key="task.id" class="task-card">
            <div class="task-card-meta">
              <span>{{ taskPriorityLabel[task.priority] }}</span
              ><span>v{{ task.currentVersion }}</span>
            </div>
            <RouterLink :to="`/projects/${task.projectId}/tasks/${task.id}`"
              ><h3>{{ task.title }}</h3></RouterLink
            >
            <p>{{ task.description || 'No description provided.' }}</p>
            <div class="trace-row">
              <span>{{ task.sourceTrace.artifactIds.length }} artifacts</span
              ><span>{{ task.sourceTrace.evidenceIds.length }} evidence</span>
            </div>
            <div v-if="transitions[task.status]?.length" class="task-actions">
              <button
                v-for="target in transitions[task.status]"
                :key="target"
                type="button"
                :disabled="Boolean(transitionUnavailableReason(task, target))"
                :title="
                  transitionUnavailableReason(task, target) || `Move to ${taskStatusLabel[target]}`
                "
                @click="move(task, target)"
              >
                {{ taskStatusLabel[target] }}
              </button>
            </div>
          </article>
          <p v-if="!column.tasks.length" class="column-empty">No tasks</p>
        </div>
      </section>
    </div>
  </ResourceState>

  <PermissionNotice :allowed="workspace.hasPermission('task.view')" permission="task.view" />
</template>

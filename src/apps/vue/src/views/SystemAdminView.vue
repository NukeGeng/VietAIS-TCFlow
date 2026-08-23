<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import ResourceState from '../components/ResourceState.vue'
import { ApiError } from '../services/http'
import { tcflowApi } from '../services/tcflow-api'
import { useSessionStore } from '../stores/session'
import {
  ProjectLifecycleStatus,
  type AuditRecord,
  type ResourceState as State,
  type SystemPermissionDefinition,
  type SystemProjectSummary,
  type SystemRole,
  type UserProfile,
  type UserRoleDetail,
} from '../types/contracts'

const session = useSessionStore()
const users = ref<UserProfile[]>([])
const projects = ref<SystemProjectSummary[]>([])
const roles = ref<SystemRole[]>([])
const permissionDefinitions = ref<SystemPermissionDefinition[]>([])
const audit = ref<AuditRecord[]>([])
const userRoles = ref<UserRoleDetail[]>([])

const usersState = ref<State>({ status: 'idle' })
const projectsState = ref<State>({ status: 'idle' })
const rolesState = ref<State>({ status: 'idle' })
const definitionsState = ref<State>({ status: 'idle' })
const auditState = ref<State>({ status: 'idle' })
const userRolesState = ref<State>({ status: 'idle' })

const selectedUserId = ref('')
const selectedRoleId = ref('')
const selectedRolePermissions = ref<string[]>([])
const roleName = ref('')
const roleDescription = ref('')
const formError = ref('')
const successMessage = ref('')

const mutableRoles = computed(() =>
  roles.value.filter((role) => role.name !== 'Admin' && role.name !== 'Basic'),
)
const systemPermissionDefinitions = computed(() =>
  permissionDefinitions.value.filter((definition) => definition.scope === 0),
)

function errorState(error: unknown, fallback: string): State {
  return {
    status: error instanceof ApiError && error.status === 403 ? 'forbidden' : 'error',
    message: error instanceof Error ? error.message : fallback,
  }
}

function forbidden(permission: string): State {
  return { status: 'forbidden', message: `Requires ${permission}.` }
}

async function loadUsers(): Promise<void> {
  if (!session.hasSystemPermission('Permissions.Users.View')) {
    usersState.value = forbidden('Permissions.Users.View')
    return
  }
  usersState.value = { status: 'loading' }
  try {
    users.value = await tcflowApi.users()
    usersState.value = { status: users.value.length ? 'ready' : 'empty' }
  } catch (error) {
    usersState.value = errorState(error, 'Unable to load users.')
  }
}

async function loadProjects(): Promise<void> {
  if (!session.hasSystemPermission('project.inspect')) {
    projectsState.value = forbidden('project.inspect')
    return
  }
  projectsState.value = { status: 'loading' }
  try {
    projects.value = (await tcflowApi.systemProjects()).items
    projectsState.value = { status: projects.value.length ? 'ready' : 'empty' }
  } catch (error) {
    projectsState.value = errorState(error, 'Unable to load system projects.')
  }
}

async function loadRoles(): Promise<void> {
  if (!session.hasSystemPermission('Permissions.Roles.View')) {
    rolesState.value = forbidden('Permissions.Roles.View')
    return
  }
  rolesState.value = { status: 'loading' }
  try {
    roles.value = await tcflowApi.systemRoles()
    rolesState.value = { status: roles.value.length ? 'ready' : 'empty' }
  } catch (error) {
    rolesState.value = errorState(error, 'Unable to load system roles.')
  }
}

async function loadDefinitions(): Promise<void> {
  if (!session.hasSystemPermission('permission-definition.manage')) {
    definitionsState.value = forbidden('permission-definition.manage')
    return
  }
  definitionsState.value = { status: 'loading' }
  try {
    permissionDefinitions.value = await tcflowApi.systemPermissionDefinitions()
    definitionsState.value = {
      status: permissionDefinitions.value.length ? 'ready' : 'empty',
    }
  } catch (error) {
    definitionsState.value = errorState(error, 'Unable to load permission definitions.')
  }
}

async function loadAudit(): Promise<void> {
  if (!session.hasSystemPermission('system-audit.view')) {
    auditState.value = forbidden('system-audit.view')
    return
  }
  auditState.value = { status: 'loading' }
  try {
    audit.value = (await tcflowApi.systemAudit()).items
    auditState.value = { status: audit.value.length ? 'ready' : 'empty' }
  } catch (error) {
    auditState.value = errorState(error, 'Unable to load system audit.')
  }
}

async function loadUserRoles(): Promise<void> {
  if (!selectedUserId.value) {
    userRoles.value = []
    userRolesState.value = { status: 'idle' }
    return
  }
  userRolesState.value = { status: 'loading' }
  try {
    userRoles.value = await tcflowApi.userRoles(selectedUserId.value)
    userRolesState.value = { status: userRoles.value.length ? 'ready' : 'empty' }
  } catch (error) {
    userRolesState.value = errorState(error, 'Unable to load user roles.')
  }
}

async function loadSelectedRole(): Promise<void> {
  selectedRolePermissions.value = []
  if (!selectedRoleId.value) return
  try {
    const role = await tcflowApi.systemRole(selectedRoleId.value)
    selectedRolePermissions.value = [...(role.permissions ?? [])]
  } catch (error) {
    formError.value = error instanceof Error ? error.message : 'Unable to load role permissions.'
  }
}

async function run(action: () => Promise<void>, success: string): Promise<void> {
  formError.value = ''
  successMessage.value = ''
  try {
    await action()
    successMessage.value = success
  } catch (error) {
    formError.value = error instanceof Error ? error.message : 'System administration failed.'
  }
}

async function toggleUser(user: UserProfile): Promise<void> {
  await run(
    async () => {
      await tcflowApi.toggleUserStatus(user.id, !user.isActive)
      await loadUsers()
    },
    `User ${user.isActive ? 'suspended' : 'activated'}.`,
  )
}

async function updateProjectStatus(project: SystemProjectSummary): Promise<void> {
  const status =
    project.state.status === ProjectLifecycleStatus.Active
      ? ProjectLifecycleStatus.Suspended
      : ProjectLifecycleStatus.Active
  await run(
    async () => {
      await tcflowApi.updateSystemProjectStatus(project.project.id, status)
      await Promise.all([loadProjects(), loadAudit()])
    },
    `Project ${status === ProjectLifecycleStatus.Active ? 'activated' : 'suspended'} and audited.`,
  )
}

async function createRole(): Promise<void> {
  await run(async () => {
    const role = await tcflowApi.createSystemRole(
      roleName.value,
      roleDescription.value || undefined,
    )
    roleName.value = ''
    roleDescription.value = ''
    await loadRoles()
    selectedRoleId.value = role.id
  }, 'Custom system role created.')
}

async function deleteRole(role: SystemRole): Promise<void> {
  await run(async () => {
    await tcflowApi.deleteSystemRole(role.id)
    if (selectedRoleId.value === role.id) selectedRoleId.value = ''
    await loadRoles()
  }, 'Custom system role deleted.')
}

async function saveRolePermissions(): Promise<void> {
  await run(async () => {
    await tcflowApi.updateSystemRolePermissions(selectedRoleId.value, selectedRolePermissions.value)
  }, 'System role permissions updated.')
}

async function saveUserRoles(): Promise<void> {
  await run(async () => {
    await tcflowApi.assignUserRoles(selectedUserId.value, userRoles.value)
  }, 'Platform roles assigned to user.')
}

watch(selectedUserId, loadUserRoles)
watch(selectedRoleId, loadSelectedRole)
Promise.all([loadUsers(), loadProjects(), loadRoles(), loadDefinitions(), loadAudit()])
</script>

<template>
  <section class="page-heading">
    <div>
      <span class="eyebrow">Platform boundary</span>
      <h1>System administration</h1>
      <p>Manage platform resources without implicitly becoming a Project Owner.</p>
    </div>
  </section>

  <div v-if="formError" class="inline-alert" role="alert">{{ formError }}</div>
  <div v-if="successMessage" class="success-alert" role="status">{{ successMessage }}</div>

  <div class="admin-grid">
    <section class="panel">
      <div class="section-heading section-heading--compact">
        <div>
          <span class="eyebrow">Identity</span>
          <h2>Platform users</h2>
        </div>
        <span class="count-badge">{{ users.length }}</span>
      </div>
      <ResourceState
        :state="usersState"
        empty-title="No users found"
        empty-message="The platform identity store returned no users."
        @retry="loadUsers"
      >
        <div class="user-table">
          <article v-for="user in users" :key="user.id">
            <span class="avatar-mark">{{
              (user.firstName || user.userName || user.email || 'U').slice(0, 2).toUpperCase()
            }}</span>
            <span
              ><strong>{{
                [user.firstName, user.lastName].filter(Boolean).join(' ') || user.userName
              }}</strong
              ><small>{{ user.email }}</small></span
            >
            <span
              :class="['state-pill', user.isActive ? 'state-pill--ready' : 'state-pill--planned']"
              >{{ user.isActive ? 'active' : 'inactive' }}</span
            >
            <button
              v-if="session.hasSystemPermission('Permissions.Users.Update')"
              :class="user.isActive ? 'danger-button' : 'secondary-button'"
              type="button"
              @click="toggleUser(user)"
            >
              {{ user.isActive ? 'Suspend' : 'Activate' }}
            </button>
          </article>
        </div>
      </ResourceState>
    </section>

    <section class="panel">
      <div class="section-heading section-heading--compact">
        <div>
          <span class="eyebrow">Project lifecycle</span>
          <h2>All projects</h2>
        </div>
        <span class="count-badge">{{ projects.length }}</span>
      </div>
      <ResourceState :state="projectsState" empty-title="No projects" @retry="loadProjects">
        <div class="audit-table">
          <article v-for="project in projects" :key="project.project.id">
            <span
              ><strong>{{ project.project.name }}</strong
              ><small>Owner {{ project.project.primaryOwnerId }}</small></span
            >
            <span
              :class="[
                'state-pill',
                project.state.status === ProjectLifecycleStatus.Active
                  ? 'state-pill--ready'
                  : 'state-pill--planned',
              ]"
              >{{ ProjectLifecycleStatus[project.state.status] }}</span
            >
            <button
              v-if="session.hasSystemPermission('project.suspend')"
              :class="
                project.state.status === ProjectLifecycleStatus.Active
                  ? 'danger-button'
                  : 'secondary-button'
              "
              type="button"
              @click="updateProjectStatus(project)"
            >
              {{ project.state.status === ProjectLifecycleStatus.Active ? 'Suspend' : 'Activate' }}
            </button>
          </article>
        </div>
      </ResourceState>
    </section>
  </div>

  <div class="admin-grid">
    <section class="panel">
      <div class="section-heading section-heading--compact">
        <div>
          <span class="eyebrow">Identity roles</span>
          <h2>System roles</h2>
        </div>
        <span class="count-badge">{{ roles.length }}</span>
      </div>
      <ResourceState :state="rolesState" empty-title="No system roles" @retry="loadRoles">
        <div class="permission-list">
          <article v-for="role in roles" :key="role.id">
            <code>{{
              role.name === 'Admin' || role.name === 'Basic' ? 'built-in' : 'custom'
            }}</code>
            <span>{{ role.name }}</span
            ><small>{{ role.description || 'No description' }}</small>
            <button
              v-if="
                mutableRoles.some((item) => item.id === role.id) &&
                session.hasSystemPermission('Permissions.Roles.Delete')
              "
              class="danger-button"
              type="button"
              @click="deleteRole(role)"
            >
              Delete
            </button>
          </article>
        </div>
      </ResourceState>
    </section>

    <form
      v-if="session.hasSystemPermission('Permissions.Roles.Create')"
      class="form-card"
      @submit.prevent="createRole"
    >
      <span class="eyebrow">Custom system role</span>
      <h2>Create role</h2>
      <label>Name<input v-model="roleName" required /></label>
      <label>Description<textarea v-model="roleDescription" rows="3"></textarea></label>
      <button class="primary-button" type="submit">Create role</button>
    </form>
  </div>

  <div class="admin-grid">
    <form
      v-if="session.hasSystemPermission('Permissions.UserRoles.Update')"
      class="form-card"
      @submit.prevent="saveUserRoles"
    >
      <span class="eyebrow">Platform membership</span>
      <h2>Assign system roles</h2>
      <label
        >User<select v-model="selectedUserId" required>
          <option value="">Select user</option>
          <option v-for="user in users" :key="user.id" :value="user.id">
            {{ user.email || user.userName || user.id }}
          </option>
        </select></label
      >
      <ResourceState :state="userRolesState" empty-title="No assignable roles">
        <fieldset>
          <legend>Roles</legend>
          <label v-for="role in userRoles" :key="role.roleId" class="check-row"
            ><input v-model="role.enabled" type="checkbox" />{{ role.roleName }}</label
          >
        </fieldset>
      </ResourceState>
      <button class="secondary-button" type="submit" :disabled="!selectedUserId">
        Save user roles
      </button>
    </form>

    <form
      v-if="session.hasSystemPermission('Permissions.RoleClaims.Update')"
      class="form-card"
      @submit.prevent="saveRolePermissions"
    >
      <span class="eyebrow">Permission claims</span>
      <h2>Configure custom role</h2>
      <label
        >Role<select v-model="selectedRoleId" required>
          <option value="">Select role</option>
          <option v-for="role in mutableRoles" :key="role.id" :value="role.id">
            {{ role.name }}
          </option>
        </select></label
      >
      <fieldset v-if="selectedRoleId">
        <legend>System permissions</legend>
        <label
          v-for="definition in systemPermissionDefinitions"
          :key="definition.id"
          class="check-row"
          ><input
            v-model="selectedRolePermissions"
            type="checkbox"
            :value="definition.id"
          /><code>{{ definition.id }}</code></label
        >
      </fieldset>
      <button class="secondary-button" type="submit" :disabled="!selectedRoleId">
        Save permissions
      </button>
    </form>
  </div>

  <section class="panel">
    <div class="section-heading section-heading--compact">
      <div>
        <span class="eyebrow">Permission catalog</span>
        <h2>System-defined permissions</h2>
      </div>
      <span class="count-badge">{{ permissionDefinitions.length }}</span>
    </div>
    <ResourceState :state="definitionsState" empty-title="No definitions" @retry="loadDefinitions">
      <div class="permission-list">
        <article v-for="definition in permissionDefinitions" :key="definition.id">
          <code>{{ definition.scope === 0 ? 'system' : 'project' }}</code
          ><span>{{ definition.id }}</span
          ><small>{{ definition.description }}</small>
        </article>
      </div>
    </ResourceState>
  </section>

  <section class="panel">
    <div class="section-heading section-heading--compact">
      <div>
        <span class="eyebrow">Platform audit</span>
        <h2>System-wide activity</h2>
      </div>
      <span class="count-badge">{{ audit.length }}</span>
    </div>
    <ResourceState :state="auditState" empty-title="No audit records" @retry="loadAudit">
      <div class="audit-table">
        <article v-for="record in audit" :key="record.id">
          <span
            ><strong>{{ record.action }}</strong
            ><small>{{ record.targetType }} · {{ record.targetId }}</small></span
          >
          <span>{{ record.actorType }} {{ record.actorId.slice(0, 8) }}</span>
          <time>{{ new Date(record.occurredAt).toLocaleString() }}</time>
        </article>
      </div>
    </ResourceState>
  </section>
</template>

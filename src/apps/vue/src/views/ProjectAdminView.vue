<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { storeToRefs } from 'pinia'
import PermissionNotice from '../components/PermissionNotice.vue'
import ResourceState from '../components/ResourceState.vue'
import { useWorkspaceStore } from '../stores/workspace'
import { AiTrustLevel, ComponentScopeKind } from '../types/contracts'

const workspace = useWorkspaceStore()
const {
  selectedProject,
  effectivePermissions,
  permissionDefinitions,
  createdRoles,
  audit,
  administrationState,
  permissionsState,
} = storeToRefs(workspace)

const roleName = ref('')
const selectedRoleId = ref('')
const selectedPermissionCodes = ref<string[]>([])
const resourceScope = ref(1)
const resourceId = ref('')
const componentScopes = ref<ComponentScopeKind[]>([])
const memberId = ref('')
const memberRoleId = ref('')
const aiTrustLevel = ref(AiTrustLevel.SuggestOnly)
const aiPermissions = ref<string[]>(['ai.analysis.run', 'ai.task.suggest'])
const newOwnerId = ref('')
const formError = ref('')
const successMessage = ref('')

const aiDefinitions = computed(() =>
  permissionDefinitions.value.filter((definition) => definition.id.startsWith('ai.')),
)
const componentOptions = Object.values(ComponentScopeKind).filter(
  (value): value is ComponentScopeKind => typeof value === 'number',
)
const aiPermissionsByTrust: Record<AiTrustLevel, string[]> = {
  [AiTrustLevel.SuggestOnly]: ['ai.analysis.run', 'ai.task.suggest'],
  [AiTrustLevel.CreateTasks]: ['ai.analysis.run', 'ai.task.suggest', 'ai.task.create'],
  [AiTrustLevel.UpdateTasks]: [
    'ai.analysis.run',
    'ai.task.suggest',
    'ai.task.create',
    'ai.task.update',
    'ai.task.close',
  ],
  [AiTrustLevel.CodeGeneration]: [
    'ai.analysis.run',
    'ai.task.suggest',
    'ai.task.create',
    'ai.task.update',
    'ai.task.close',
    'ai.code.generate',
  ],
  [AiTrustLevel.PullRequestCreation]: [
    'ai.analysis.run',
    'ai.task.suggest',
    'ai.task.create',
    'ai.task.update',
    'ai.task.close',
    'ai.code.generate',
    'ai.pull_request.create',
  ],
}

watch(aiTrustLevel, (trustLevel) => {
  aiPermissions.value = [...aiPermissionsByTrust[trustLevel]]
})

async function run(action: () => Promise<void>, success: string): Promise<void> {
  formError.value = ''
  successMessage.value = ''
  try {
    await action()
    successMessage.value = success
  } catch (error) {
    formError.value = error instanceof Error ? error.message : 'Administrative action failed.'
  }
}

async function createRole(): Promise<void> {
  await run(async () => {
    const role = await workspace.createRole(roleName.value)
    selectedRoleId.value = role.id
    memberRoleId.value = role.id
    roleName.value = ''
  }, 'Role created. Select its permission grants below.')
}

async function saveRolePermissions(): Promise<void> {
  await run(
    () =>
      workspace.updateRolePermissions(
        selectedRoleId.value,
        selectedPermissionCodes.value,
        resourceScope.value,
        componentScopes.value,
        resourceId.value || undefined,
      ),
    'Role permissions updated and audited.',
  )
}

workspace.loadAdministration()
</script>

<template>
  <section class="page-heading">
    <div>
      <span class="eyebrow">Project administration</span>
      <h1>{{ selectedProject?.name }}</h1>
      <p>
        Project Owner authority stops at this project boundary. System permissions cannot be
        delegated here.
      </p>
    </div>
  </section>

  <div v-if="formError" class="inline-alert" role="alert">{{ formError }}</div>
  <div v-if="successMessage" class="success-alert" role="status">{{ successMessage }}</div>

  <ResourceState :state="administrationState" @retry="workspace.loadAdministration()">
    <div class="admin-grid">
      <section class="panel">
        <div class="section-heading section-heading--compact">
          <div>
            <span class="eyebrow">Effective access</span>
            <h2>Grant trace</h2>
          </div>
          <span class="count-badge">{{ effectivePermissions?.grants.length || 0 }}</span>
        </div>
        <ResourceState :state="permissionsState">
          <div class="permission-list">
            <article
              v-for="grant in effectivePermissions?.grants"
              :key="`${grant.roleId}-${grant.permissionCode}-${grant.resourceScope}`"
            >
              <code>{{ grant.permissionCode }}</code
              ><span>{{ grant.roleName }}</span>
              <small
                >scope {{ grant.resourceScope
                }}<template v-if="grant.componentScopes.length">
                  · components {{ grant.componentScopes.join(', ') }}</template
                ></small
              >
            </article>
          </div>
        </ResourceState>
      </section>

      <PermissionNotice :allowed="workspace.hasPermission('role.create')" permission="role.create">
        <form class="form-card" @submit.prevent="createRole">
          <span class="eyebrow">Custom project role</span>
          <h2>Create role</h2>
          <p>Permissions remain selected from system-defined project definitions.</p>
          <label
            >Role name<input v-model="roleName" minlength="2" maxlength="100" required
          /></label>
          <button class="primary-button" type="submit">Create role</button>
        </form>
      </PermissionNotice>
    </div>

    <PermissionNotice :allowed="workspace.hasPermission('role.update')" permission="role.update">
      <form class="panel permission-editor" @submit.prevent="saveRolePermissions">
        <div class="section-heading section-heading--compact">
          <div>
            <span class="eyebrow">Permission matrix</span>
            <h2>Configure a newly created role</h2>
          </div>
        </div>
        <div class="form-grid">
          <label
            >Role<select v-model="selectedRoleId" required>
              <option value="">Select role</option>
              <option v-for="role in createdRoles" :key="role.id" :value="role.id">
                {{ role.name }}
              </option>
            </select></label
          >
          <label
            >Resource scope<select v-model="resourceScope">
              <option :value="1">Project</option>
              <option :value="2">Repository</option>
              <option :value="3">Component</option>
              <option :value="4">Own</option>
              <option :value="5">Assigned</option>
              <option :value="6">All</option>
            </select></label
          >
          <label v-if="resourceScope === 2"
            >Repository UUID<input v-model="resourceId" required
          /></label>
        </div>
        <fieldset>
          <legend>Permission definitions</legend>
          <label v-for="definition in permissionDefinitions" :key="definition.id" class="check-row"
            ><input
              v-model="selectedPermissionCodes"
              type="checkbox"
              :value="definition.id"
            /><code>{{ definition.id }}</code></label
          >
        </fieldset>
        <fieldset>
          <legend>Optional component scopes</legend>
          <label v-for="scope in componentOptions" :key="scope" class="check-row"
            ><input v-model="componentScopes" type="checkbox" :value="scope" />{{
              ComponentScopeKind[scope]
            }}</label
          >
        </fieldset>
        <button class="primary-button" type="submit">Save role grants</button>
      </form>
    </PermissionNotice>

    <div class="admin-grid">
      <PermissionNotice
        :allowed="workspace.hasPermission('member.role.assign')"
        permission="member.role.assign"
      >
        <form
          class="form-card"
          @submit.prevent="
            run(
              () => workspace.assignMemberRoles(memberId, [memberRoleId]),
              'Member roles updated and audited.',
            )
          "
        >
          <span class="eyebrow">Membership</span>
          <h2>Assign role</h2>
          <label>Active member UUID<input v-model="memberId" required /></label>
          <label
            >Role<select v-model="memberRoleId" required>
              <option value="">Select role</option>
              <option v-for="role in createdRoles" :key="role.id" :value="role.id">
                {{ role.name }}
              </option>
            </select></label
          >
          <button class="secondary-button" type="submit">Assign role</button>
        </form>
      </PermissionNotice>

      <PermissionNotice
        :allowed="workspace.hasPermission('ai.policy.update')"
        permission="ai.policy.update"
      >
        <form
          class="form-card"
          @submit.prevent="
            run(
              () => workspace.updateAiPolicy(aiTrustLevel, aiPermissions),
              'AI policy updated and audited.',
            )
          "
        >
          <span class="eyebrow">Progressive trust</span>
          <h2>AI policy</h2>
          <label
            >Trust level<select v-model="aiTrustLevel">
              <option
                v-for="level in Object.values(AiTrustLevel).filter(
                  (value) => typeof value === 'number',
                )"
                :key="level"
                :value="level"
              >
                {{ AiTrustLevel[level as AiTrustLevel] }}
              </option>
            </select></label
          >
          <fieldset>
            <legend>Allowed AI actions</legend>
            <label v-for="definition in aiDefinitions" :key="definition.id" class="check-row"
              ><input v-model="aiPermissions" type="checkbox" :value="definition.id" />{{
                definition.id
              }}</label
            >
          </fieldset>
          <button class="secondary-button" type="submit">Update AI policy</button>
        </form>
      </PermissionNotice>
    </div>

    <PermissionNotice
      :allowed="workspace.hasPermission('project.ownership.transfer')"
      permission="project.ownership.transfer"
    >
      <form
        class="form-card ownership-form"
        @submit.prevent="
          run(
            () => workspace.transferOwnership(newOwnerId),
            'Project ownership transferred and audited.',
          )
        "
      >
        <div>
          <span class="eyebrow">Sensitive action</span>
          <h2>Transfer ownership</h2>
          <p>
            The target must already be an active project member. Confirmation is sent explicitly.
          </p>
        </div>
        <label>New owner UUID<input v-model="newOwnerId" required /></label>
        <button class="danger-button" type="submit">Transfer ownership</button>
      </form>
    </PermissionNotice>

    <PermissionNotice :allowed="workspace.hasPermission('role.view')" permission="role.view">
      <section class="panel">
        <div class="section-heading section-heading--compact">
          <div>
            <span class="eyebrow">Permission catalog</span>
            <h2>Project definitions</h2>
          </div>
          <span class="count-badge">{{ permissionDefinitions.length }}</span>
        </div>
        <div class="tag-cloud">
          <code v-for="definition in permissionDefinitions" :key="definition.id">{{
            definition.id
          }}</code>
        </div>
      </section>
    </PermissionNotice>
    <PermissionNotice :allowed="workspace.hasPermission('audit.view')" permission="audit.view">
      <section class="panel">
        <div class="section-heading section-heading--compact">
          <div>
            <span class="eyebrow">Audit</span>
            <h2>Administrative trail</h2>
          </div>
          <span class="count-badge">{{ audit.length }}</span>
        </div>
        <div class="audit-table">
          <article v-for="record in audit" :key="record.id">
            <span
              ><strong>{{ record.action }}</strong
              ><small>{{ record.targetType }} · {{ record.targetId.slice(0, 12) }}</small></span
            ><span>{{ record.actorType }} {{ record.actorId.slice(0, 8) }}</span
            ><time>{{ new Date(record.occurredAt).toLocaleString() }}</time>
          </article>
        </div>
      </section>
    </PermissionNotice>
  </ResourceState>
</template>

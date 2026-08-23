<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { storeToRefs } from 'pinia'
import PermissionNotice from '../components/PermissionNotice.vue'
import ResourceState from '../components/ResourceState.vue'
import { useWorkspaceStore } from '../stores/workspace'
import {
  AiTrustLevel,
  AuthorityKnowledgeKind,
  AuthoritySourceKind,
  ComponentScopeKind,
  ConventionProfileStatus,
  type ProjectRole,
} from '../types/contracts'

interface RoleGrantDraft {
  enabled: boolean
  resourceScope: number
  resourceId: string
  componentScopes: ComponentScopeKind[]
}

const workspace = useWorkspaceStore()
const {
  selectedProject,
  effectivePermissions,
  permissionDefinitions,
  projectRoles,
  projectMembers,
  components,
  repositories,
  aiPolicy,
  authorityPolicy,
  conventionProfile,
  audit,
  administrationState,
  permissionsState,
} = storeToRefs(workspace)

const roleName = ref('')
const selectedRoleId = ref('')
const roleGrants = ref<Record<string, RoleGrantDraft>>({})
const newMemberId = ref('')
const selectedMemberId = ref('')
const selectedMemberRoleIds = ref<string[]>([])
const componentRepositoryId = ref('')
const componentName = ref('')
const componentRootPath = ref('')
const componentScope = ref(ComponentScopeKind.Backend)
const aiTrustLevel = ref(AiTrustLevel.SuggestOnly)
const aiPermissions = ref<string[]>(['ai.analysis.run', 'ai.task.suggest'])
const authoritySelections = ref<Record<number, AuthoritySourceKind>>({
  [AuthorityKnowledgeKind.ApiContract]: AuthoritySourceKind.Backend,
  [AuthorityKnowledgeKind.UiRequirement]: AuthoritySourceKind.Frontend,
  [AuthorityKnowledgeKind.BusinessLogic]: AuthoritySourceKind.Backend,
  [AuthorityKnowledgeKind.Persistence]: AuthoritySourceKind.Database,
})
const architectures = ref('')
const apiStyles = ref('')
const persistencePatterns = ref('')
const validationPatterns = ref('')
const dtoPatterns = ref('')
const newOwnerId = ref('')
const formError = ref('')
const successMessage = ref('')

const customRoles = computed(() => projectRoles.value.filter((role) => !role.isSystemDefined))
const assignableRoles = computed(() => projectRoles.value.filter((role) => !role.isOwner))
const aiDefinitions = computed(() =>
  permissionDefinitions.value.filter((definition) => definition.id.startsWith('ai.')),
)
const componentOptions = Object.values(ComponentScopeKind).filter(
  (value): value is ComponentScopeKind => typeof value === 'number',
)
const knowledgeOptions = Object.values(AuthorityKnowledgeKind).filter(
  (value): value is AuthorityKnowledgeKind => typeof value === 'number',
)
const authorityOptions = Object.values(AuthoritySourceKind).filter(
  (value): value is AuthoritySourceKind => typeof value === 'number',
)
const trustOptions = Object.values(AiTrustLevel).filter(
  (value): value is AiTrustLevel => typeof value === 'number',
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

function splitList(value: string): string[] {
  return [
    ...new Set(
      value
        .split(',')
        .map((item) => item.trim())
        .filter(Boolean),
    ),
  ]
}

function roleNames(roleIds: string[]): string {
  return roleIds
    .map(
      (roleId) => projectRoles.value.find((role) => role.id === roleId)?.name ?? roleId.slice(0, 8),
    )
    .join(', ')
}

function resetRoleGrantDrafts(): void {
  const role = projectRoles.value.find((item) => item.id === selectedRoleId.value)
  const drafts: Record<string, RoleGrantDraft> = {}
  for (const definition of permissionDefinitions.value) {
    const grant = role?.permissions.find((item) => item.permissionCode === definition.id)
    drafts[definition.id] = {
      enabled: Boolean(grant),
      resourceScope:
        grant?.resourceScope ??
        (definition.allowedResourceScopes.includes(1)
          ? 1
          : (definition.allowedResourceScopes[0] ?? 1)),
      resourceId: grant?.resourceId ?? '',
      componentScopes: [...(grant?.componentScopes ?? [])],
    }
  }
  roleGrants.value = drafts
}

function roleGrant(permissionCode: string): RoleGrantDraft {
  return roleGrants.value[permissionCode]!
}

watch([selectedRoleId, projectRoles, permissionDefinitions], resetRoleGrantDrafts, { deep: true })
watch(
  aiPolicy,
  (policy) => {
    if (!policy) return
    aiTrustLevel.value = policy.trustLevel
    aiPermissions.value = [...policy.allowedPermissions]
  },
  { immediate: true },
)
watch(
  authorityPolicy,
  (policy) => {
    if (!policy) return
    for (const rule of policy.rules) authoritySelections.value[rule.knowledge] = rule.source
  },
  { immediate: true },
)
watch(
  conventionProfile,
  (profile) => {
    if (!profile) return
    architectures.value = profile.architectures.join(', ')
    apiStyles.value = profile.apiStyles.join(', ')
    persistencePatterns.value = profile.persistencePatterns.join(', ')
    validationPatterns.value = profile.validationPatterns.join(', ')
    dtoPatterns.value = profile.dtoPatterns.join(', ')
  },
  { immediate: true },
)

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
    selectedMemberRoleIds.value = [role.id]
    roleName.value = ''
  }, 'Role created and available after reload.')
}

async function saveRolePermissions(): Promise<void> {
  const permissions: ProjectRole['permissions'] = permissionDefinitions.value.flatMap(
    (definition) => {
      const draft = roleGrants.value[definition.id]
      if (!draft?.enabled) return []
      return [
        {
          permissionCode: definition.id,
          resourceScope: draft.resourceScope,
          resourceId: draft.resourceId || undefined,
          componentScopes: draft.componentScopes,
        },
      ]
    },
  )
  await run(
    () => workspace.updateRolePermissions(selectedRoleId.value, permissions),
    'Role permission matrix updated and audited.',
  )
}

function applyTrustDefaults(): void {
  aiPermissions.value = [...aiPermissionsByTrust[aiTrustLevel.value]]
}

Promise.all([workspace.loadAdministration(), workspace.loadRepositories()])
</script>

<template>
  <section class="page-heading">
    <div>
      <span class="eyebrow">Project administration</span>
      <h1>{{ selectedProject?.name }}</h1>
      <p>Manage project-scoped access, repository conventions, authority, and AI trust.</p>
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
                >scope {{ grant.resourceScope }} ·
                {{
                  grant.componentScopes.map((scope) => ComponentScopeKind[scope]).join(', ') ||
                  'all components'
                }}</small
              >
            </article>
          </div>
        </ResourceState>
      </section>

      <PermissionNotice
        :allowed="workspace.hasPermission('member.invite')"
        permission="member.invite"
      >
        <form
          class="form-card"
          @submit.prevent="
            run(async () => {
              await workspace.addMember(newMemberId)
              newMemberId = ''
            }, 'Member added and audited.')
          "
        >
          <span class="eyebrow">Membership</span>
          <h2>Add existing user</h2>
          <p>Use an existing platform user UUID. Project access begins with no custom role.</p>
          <label>User UUID<input v-model="newMemberId" required /></label>
          <button class="primary-button" type="submit">Add member</button>
        </form>
      </PermissionNotice>
    </div>

    <PermissionNotice :allowed="workspace.hasPermission('member.view')" permission="member.view">
      <section class="panel">
        <div class="section-heading section-heading--compact">
          <div>
            <span class="eyebrow">Members</span>
            <h2>Active project membership</h2>
          </div>
          <span class="count-badge">{{ projectMembers.length }}</span>
        </div>
        <div class="audit-table">
          <article v-for="member in projectMembers" :key="member.id">
            <span
              ><strong>{{ member.userId }}</strong
              ><small>{{
                roleNames(member.roles.map((role) => role.roleId)) || 'No assigned role'
              }}</small></span
            >
            <span>{{
              member.userId === selectedProject?.primaryOwnerId ? 'Primary Owner' : 'Member'
            }}</span>
            <button
              v-if="
                member.userId !== selectedProject?.primaryOwnerId &&
                workspace.hasPermission('member.remove')
              "
              class="danger-button"
              type="button"
              @click="
                run(() => workspace.removeMember(member.userId), 'Member removed and audited.')
              "
            >
              Remove
            </button>
          </article>
        </div>
      </section>
    </PermissionNotice>

    <PermissionNotice :allowed="workspace.hasPermission('role.view')" permission="role.view">
      <section class="panel">
        <div class="section-heading section-heading--compact">
          <div>
            <span class="eyebrow">Project roles</span>
            <h2>Persisted access profiles</h2>
          </div>
          <span class="count-badge">{{ projectRoles.length }}</span>
        </div>
        <div class="permission-list">
          <article v-for="role in projectRoles" :key="role.id">
            <code>{{ role.isOwner ? 'owner' : role.isSystemDefined ? 'system' : 'custom' }}</code>
            <span>{{ role.name }}</span>
            <small>{{ role.permissions.length }} permission grants</small>
          </article>
        </div>
      </section>
    </PermissionNotice>

    <div class="admin-grid">
      <PermissionNotice :allowed="workspace.hasPermission('role.create')" permission="role.create">
        <form class="form-card" @submit.prevent="createRole">
          <span class="eyebrow">Custom role</span>
          <h2>Create role</h2>
          <label
            >Role name<input v-model="roleName" minlength="2" maxlength="100" required
          /></label>
          <button class="primary-button" type="submit">Create role</button>
        </form>
      </PermissionNotice>

      <PermissionNotice
        :allowed="workspace.hasPermission('member.role.assign')"
        permission="member.role.assign"
      >
        <form
          class="form-card"
          @submit.prevent="
            run(
              () => workspace.assignMemberRoles(selectedMemberId, selectedMemberRoleIds),
              'Member role updated and audited.',
            )
          "
        >
          <span class="eyebrow">Role assignment</span>
          <h2>Assign member role</h2>
          <label
            >Member<select v-model="selectedMemberId" required>
              <option value="">Select member</option>
              <option
                v-for="member in projectMembers.filter(
                  (item) => item.userId !== selectedProject?.primaryOwnerId,
                )"
                :key="member.id"
                :value="member.userId"
              >
                {{ member.userId }}
              </option>
            </select></label
          >
          <label
            >Roles<select v-model="selectedMemberRoleIds" multiple required>
              <option v-for="role in assignableRoles" :key="role.id" :value="role.id">
                {{ role.name }}
              </option>
            </select></label
          >
          <button class="secondary-button" type="submit">Assign role</button>
        </form>
      </PermissionNotice>
    </div>

    <PermissionNotice :allowed="workspace.hasPermission('role.update')" permission="role.update">
      <form class="panel permission-editor" @submit.prevent="saveRolePermissions">
        <div class="section-heading section-heading--compact">
          <div>
            <span class="eyebrow">Permission matrix</span>
            <h2>Role grants and scopes</h2>
          </div>
          <button
            v-if="selectedRoleId && workspace.hasPermission('role.delete')"
            class="danger-button"
            type="button"
            @click="
              run(async () => {
                await workspace.deleteRole(selectedRoleId)
                selectedRoleId = ''
              }, 'Custom role deleted and audited.')
            "
          >
            Delete role
          </button>
        </div>
        <label
          >Custom role<select v-model="selectedRoleId" required>
            <option value="">Select role</option>
            <option v-for="role in customRoles" :key="role.id" :value="role.id">
              {{ role.name }}
            </option>
          </select></label
        >
        <div v-if="selectedRoleId" class="audit-table">
          <article v-for="definition in permissionDefinitions" :key="definition.id">
            <label class="check-row"
              ><input v-model="roleGrant(definition.id).enabled" type="checkbox" /><code>{{
                definition.id
              }}</code></label
            >
            <select
              v-model="roleGrant(definition.id).resourceScope"
              :disabled="!roleGrant(definition.id).enabled"
            >
              <option v-for="scope in definition.allowedResourceScopes" :key="scope" :value="scope">
                scope {{ scope }}
              </option>
            </select>
            <input
              v-if="roleGrant(definition.id).resourceScope === 2"
              v-model="roleGrant(definition.id).resourceId"
              :disabled="!roleGrant(definition.id).enabled"
              placeholder="Repository UUID"
            />
            <select
              v-else
              v-model="roleGrant(definition.id).componentScopes"
              multiple
              :disabled="!roleGrant(definition.id).enabled"
            >
              <option
                v-for="scope in definition.allowedComponentScopes"
                :key="scope"
                :value="scope"
              >
                {{ ComponentScopeKind[scope] }}
              </option>
            </select>
          </article>
        </div>
        <button v-if="selectedRoleId" class="primary-button" type="submit">
          Save permission matrix
        </button>
      </form>
    </PermissionNotice>

    <div class="admin-grid">
      <PermissionNotice
        :allowed="workspace.hasPermission('component.view')"
        permission="component.view"
      >
        <section class="panel">
          <div class="section-heading section-heading--compact">
            <div>
              <span class="eyebrow">Component access</span>
              <h2>Repository boundaries</h2>
            </div>
            <span class="count-badge">{{ components.length }}</span>
          </div>
          <div class="permission-list">
            <article v-for="component in components" :key="component.id">
              <code>{{ ComponentScopeKind[component.scope] }}</code
              ><span>{{ component.name }}</span
              ><small
                >{{
                  repositories.find((repository) => repository.id === component.repositoryId)
                    ?.name || component.repositoryId
                }}
                · {{ component.rootPath || 'repository root' }}</small
              >
            </article>
          </div>
        </section>
      </PermissionNotice>

      <PermissionNotice
        :allowed="workspace.hasPermission('component.create')"
        permission="component.create"
      >
        <form
          class="form-card"
          @submit.prevent="
            run(async () => {
              await workspace.createComponent({
                repositoryId: componentRepositoryId,
                name: componentName,
                scope: componentScope,
                rootPath: componentRootPath || undefined,
              })
              componentName = ''
              componentRootPath = ''
            }, 'Component created and audited.')
          "
        >
          <span class="eyebrow">Repository boundary</span>
          <h2>Create component</h2>
          <label
            >Repository<select v-model="componentRepositoryId" required>
              <option value="">Select repository</option>
              <option
                v-for="repository in repositories"
                :key="repository.id"
                :value="repository.id"
              >
                {{ repository.name }}
              </option>
            </select></label
          >
          <label>Name<input v-model="componentName" required /></label>
          <label
            >Scope<select v-model="componentScope">
              <option v-for="scope in componentOptions" :key="scope" :value="scope">
                {{ ComponentScopeKind[scope] }}
              </option>
            </select></label
          >
          <label>Root path<input v-model="componentRootPath" /></label>
          <button class="secondary-button" type="submit">Create component</button>
        </form>
      </PermissionNotice>
    </div>

    <div class="admin-grid">
      <PermissionNotice
        :allowed="workspace.hasPermission('authority.view')"
        permission="authority.view"
      >
        <form
          class="form-card"
          @submit.prevent="
            run(
              () =>
                workspace.updateAuthorityPolicy(
                  knowledgeOptions.map((knowledge) => ({
                    knowledge,
                    source: authoritySelections[knowledge] as AuthoritySourceKind,
                  })),
                ),
              'Authority policy updated and audited.',
            )
          "
        >
          <span class="eyebrow">Source of truth</span>
          <h2>Authority policy</h2>
          <label v-for="knowledge in knowledgeOptions" :key="knowledge"
            >{{ AuthorityKnowledgeKind[knowledge]
            }}<select
              v-model="authoritySelections[knowledge]"
              :disabled="!workspace.hasPermission('authority.update')"
            >
              <option v-for="source in authorityOptions" :key="source" :value="source">
                {{ AuthoritySourceKind[source] }}
              </option>
            </select></label
          >
          <button
            v-if="workspace.hasPermission('authority.update')"
            class="secondary-button"
            type="submit"
          >
            Save authority
          </button>
        </form>
      </PermissionNotice>

      <PermissionNotice
        :allowed="workspace.hasPermission('convention.view')"
        permission="convention.view"
      >
        <form
          class="form-card"
          @submit.prevent="
            run(
              () =>
                workspace.updateConventionProfile({
                  status: ConventionProfileStatus.Confirmed,
                  architectures: splitList(architectures),
                  apiStyles: splitList(apiStyles),
                  persistencePatterns: splitList(persistencePatterns),
                  validationPatterns: splitList(validationPatterns),
                  dtoPatterns: splitList(dtoPatterns),
                }),
              'Convention profile updated and audited.',
            )
          "
        >
          <span class="eyebrow">Repository-aware planning</span>
          <h2>Convention profile</h2>
          <p>Comma-separated values, initialized from deterministic analysis.</p>
          <label
            >Architecture<input
              v-model="architectures"
              :disabled="!workspace.hasPermission('convention.update')"
          /></label>
          <label
            >API styles<input
              v-model="apiStyles"
              :disabled="!workspace.hasPermission('convention.update')"
          /></label>
          <label
            >Persistence<input
              v-model="persistencePatterns"
              :disabled="!workspace.hasPermission('convention.update')"
          /></label>
          <label
            >Validation<input
              v-model="validationPatterns"
              :disabled="!workspace.hasPermission('convention.update')"
          /></label>
          <label
            >DTO naming<input
              v-model="dtoPatterns"
              :disabled="!workspace.hasPermission('convention.update')"
          /></label>
          <button
            v-if="workspace.hasPermission('convention.update')"
            class="secondary-button"
            type="submit"
          >
            Confirm conventions
          </button>
        </form>
      </PermissionNotice>
    </div>

    <div class="admin-grid">
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
            >Trust level<select v-model="aiTrustLevel" @change="applyTrustDefaults">
              <option v-for="level in trustOptions" :key="level" :value="level">
                {{ AiTrustLevel[level] }}
              </option>
            </select></label
          >
          <fieldset>
            <legend>Allowed AI actions</legend>
            <label v-for="definition in aiDefinitions" :key="definition.id" class="check-row"
              ><input v-model="aiPermissions" type="checkbox" :value="definition.id" /><code>{{
                definition.id
              }}</code></label
            >
          </fieldset>
          <button class="secondary-button" type="submit">Update AI policy</button>
        </form>
      </PermissionNotice>

      <PermissionNotice
        :allowed="workspace.hasPermission('project.ownership.transfer')"
        permission="project.ownership.transfer"
      >
        <form
          class="form-card"
          @submit.prevent="
            run(
              () => workspace.transferOwnership(newOwnerId),
              'Project ownership transferred and audited.',
            )
          "
        >
          <span class="eyebrow">Sensitive action</span>
          <h2>Transfer ownership</h2>
          <label
            >Active member<select v-model="newOwnerId" required>
              <option value="">Select member</option>
              <option
                v-for="member in projectMembers.filter(
                  (item) => item.userId !== selectedProject?.primaryOwnerId,
                )"
                :key="member.id"
                :value="member.userId"
              >
                {{ member.userId }}
              </option>
            </select></label
          >
          <button class="danger-button" type="submit">Transfer ownership</button>
        </form>
      </PermissionNotice>
    </div>

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

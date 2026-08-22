<script setup lang="ts">
import { ref } from 'vue'
import { storeToRefs } from 'pinia'
import PermissionNotice from '../components/PermissionNotice.vue'
import ResourceState from '../components/ResourceState.vue'
import { useWorkspaceStore } from '../stores/workspace'
import { RepositoryProviderKind } from '../types/contracts'

const workspace = useWorkspaceStore()
const { repositories, repositoriesState, selectedProject } = storeToRefs(workspace)
const name = ref('')
const provider = ref(RepositoryProviderKind.GitHub)
const location = ref('')
const defaultBranch = ref('main')
const formError = ref('')

async function createRepository(): Promise<void> {
  formError.value = ''
  try {
    await workspace.createRepository({
      name: name.value,
      provider: provider.value,
      localPath: provider.value === RepositoryProviderKind.Local ? location.value : undefined,
      remoteUrl: provider.value === RepositoryProviderKind.GitHub ? location.value : undefined,
      defaultBranch: defaultBranch.value,
    })
    name.value = ''
    location.value = ''
  } catch (error) {
    formError.value = error instanceof Error ? error.message : 'Unable to connect repository.'
  }
}

workspace.loadRepositories()
</script>

<template>
  <section class="page-heading">
    <div>
      <span class="eyebrow">{{ selectedProject?.name }}</span>
      <h1>Repositories</h1>
      <p>Code sources stay inside the selected project and permission scope.</p>
    </div>
  </section>
  <div class="content-split">
    <section class="panel">
      <ResourceState
        :state="repositoriesState"
        empty-title="No repositories connected"
        empty-message="Connect a local path or GitHub HTTPS remote to establish a source boundary."
        @retry="workspace.loadRepositories()"
      >
        <div class="item-list">
          <article
            v-for="repository in repositories"
            :key="repository.id"
            class="list-row list-row--static"
          >
            <span class="avatar-mark">{{
              repository.provider === RepositoryProviderKind.GitHub ? 'GH' : 'LO'
            }}</span>
            <span
              ><strong>{{ repository.name }}</strong
              ><small
                >{{ repository.remoteUrl || repository.localPath }} ·
                {{ repository.defaultBranch }}</small
              ></span
            >
            <span class="state-pill state-pill--planned">{{
              repository.status === 0 ? 'pending' : repository.status === 1 ? 'active' : 'disabled'
            }}</span>
          </article>
        </div>
      </ResourceState>
    </section>

    <PermissionNotice
      :allowed="workspace.hasPermission('repository.create')"
      permission="repository.create"
    >
      <form class="form-card" @submit.prevent="createRepository">
        <span class="eyebrow">Source connection</span>
        <h2>Add repository</h2>
        <label
          >Name<input v-model="name" required maxlength="150" placeholder="web-platform"
        /></label>
        <label
          >Provider<select v-model="provider">
            <option :value="RepositoryProviderKind.GitHub">GitHub</option>
            <option :value="RepositoryProviderKind.Local">Local</option>
          </select></label
        >
        <label
          >{{ provider === RepositoryProviderKind.GitHub ? 'HTTPS remote' : 'Local path'
          }}<input
            v-model="location"
            required
            :placeholder="
              provider === RepositoryProviderKind.GitHub
                ? 'https://github.com/org/repo.git'
                : '/workspace/repo'
            "
        /></label>
        <label>Default branch<input v-model="defaultBranch" required /></label>
        <div v-if="formError" class="inline-alert" role="alert">{{ formError }}</div>
        <button class="primary-button" type="submit">Connect repository</button>
      </form>
    </PermissionNotice>
  </div>
</template>

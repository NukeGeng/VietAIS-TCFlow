<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { externalNavigation } from '../services/external-navigation'
import { gitHubConnectionSession } from '../services/github-connection-session'
import { tcflowApi } from '../services/tcflow-api'

const route = useRoute()
const router = useRouter()
const status = ref('Verifying the GitHub connection…')
const errorMessage = ref('')

function queryValue(name: string): string {
  const value = route.query[name]
  return typeof value === 'string' ? value : ''
}

async function handleCallback(): Promise<void> {
  const state = queryValue('state')
  const githubError = queryValue('error_description') || queryValue('error')
  if (githubError) throw new Error(`GitHub denied the connection: ${githubError}`)
  if (!state) throw new Error('GitHub returned no connection state. Start the connection again.')

  const code = queryValue('code')
  if (code) {
    const pending = gitHubConnectionSession.read(state)
    if (!pending) {
      throw new Error('GitHub authorization expired or was opened in another browser session.')
    }

    status.value = 'Confirming repository access…'
    const result = await tcflowApi.completeGitHubConnection(state, code, pending.codeVerifier)
    gitHubConnectionSession.remove(state)
    await router.replace({
      name: 'repositories',
      params: { projectId: result.projectId },
      query: {
        github: 'connected',
        installation: String(result.installation.installationId),
      },
    })
    return
  }

  const installationId = Number(queryValue('installation_id'))
  if (!Number.isSafeInteger(installationId) || installationId <= 0) {
    throw new Error('GitHub returned no valid installation. Start the connection again.')
  }

  status.value = 'Preparing secure GitHub authorization…'
  const authorization = await tcflowApi.prepareGitHubAuthorization(state, installationId)
  gitHubConnectionSession.write(
    authorization.state,
    authorization.codeVerifier,
    authorization.expiresAt,
  )
  externalNavigation.assign(authorization.authorizationUrl)
}

onMounted(async () => {
  try {
    await handleCallback()
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : 'Unable to connect GitHub.'
    status.value = ''
  }
})
</script>

<template>
  <section class="callback-panel" aria-live="polite">
    <span class="avatar-mark">GH</span>
    <span class="eyebrow">GitHub App</span>
    <h1>{{ errorMessage ? 'Connection failed' : 'Connecting GitHub' }}</h1>
    <p v-if="status">{{ status }}</p>
    <div v-if="errorMessage" class="inline-alert" role="alert">{{ errorMessage }}</div>
    <RouterLink v-if="errorMessage" class="secondary-button" to="/projects">
      Return to projects
    </RouterLink>
  </section>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { storeToRefs } from 'pinia'
import { useRoute, useRouter } from 'vue-router'
import { useSessionStore } from '../stores/session'

const session = useSessionStore()
const { state } = storeToRefs(session)
const route = useRoute()
const router = useRouter()
const email = ref('admin@root.com')
const password = ref('')
const tenant = ref('root')

async function submit(): Promise<void> {
  try {
    await session.login(email.value, password.value, tenant.value)
    const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : '/'
    await router.replace(redirect)
  } catch {
    // The session store exposes the backend problem in an accessible alert.
  }
}
</script>

<template>
  <main class="login-page">
    <section class="login-story">
      <RouterLink class="brand brand--login" to="/" aria-label="VietAIS TCFlow home">
        <span class="brand-mark" aria-hidden="true">TC</span>
        <span><strong>VietAIS</strong><small>TCFlow</small></span>
      </RouterLink>
      <div>
        <span class="eyebrow">Source-aware delivery</span>
        <h1>Trace every task back to evidence.</h1>
        <p>
          One workspace for repository contracts, scoped permissions, impact, review, and auditable
          engineering decisions.
        </p>
      </div>
      <small
        >Frontend checks improve the experience. The backend always makes the final decision.</small
      >
    </section>

    <section class="login-form-wrap" aria-labelledby="login-heading">
      <form class="form-card login-form" @submit.prevent="submit">
        <span class="eyebrow">Secure workspace</span>
        <h2 id="login-heading">Sign in</h2>
        <p>Use your FullStackHero identity and tenant.</p>

        <label>
          Email
          <input v-model="email" type="email" autocomplete="username" required />
        </label>
        <label>
          Password
          <input v-model="password" type="password" autocomplete="current-password" required />
        </label>
        <label>
          Tenant
          <input v-model="tenant" autocomplete="organization" required />
        </label>

        <div
          v-if="state.status === 'error' || state.status === 'forbidden'"
          class="inline-alert"
          role="alert"
        >
          {{ state.message }}
        </div>
        <button class="primary-button" type="submit" :disabled="state.status === 'loading'">
          {{ state.status === 'loading' ? 'Signing in…' : 'Continue to workspace' }}
        </button>
      </form>
    </section>
  </main>
</template>

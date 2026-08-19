<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { useWorkspaceStore } from '../stores/workspace'

const workspace = useWorkspaceStore()
const { capabilities, readyCount } = storeToRefs(workspace)
</script>

<template>
  <section class="hero-panel">
    <div>
      <span class="eyebrow">Repository intelligence</span>
      <h1>Plan from evidence,<br />not assumptions.</h1>
      <p>
        TCFlow connects frontend intent, backend contracts, persistence, and permissions into an
        explainable engineering plan.
      </p>
    </div>

    <div class="hero-metric" aria-label="Foundation readiness">
      <strong>{{ readyCount }}/{{ capabilities.length }}</strong>
      <span>foundation capabilities ready</span>
    </div>
  </section>

  <section aria-labelledby="capability-heading">
    <div class="section-heading">
      <div>
        <span class="eyebrow">Delivery map</span>
        <h2 id="capability-heading">Build status</h2>
      </div>
      <span class="evidence-badge">CONFIRMED SOURCE</span>
    </div>

    <div class="capability-grid">
      <article v-for="capability in capabilities" :key="capability.name" class="capability-card">
        <span :class="['state-pill', `state-pill--${capability.state}`]">
          {{ capability.state }}
        </span>
        <h3>{{ capability.name }}</h3>
        <p>{{ capability.description }}</p>
      </article>
    </div>
  </section>

  <section class="next-step" aria-labelledby="next-step-heading">
    <span class="step-number">01</span>
    <div>
      <span class="eyebrow">Next verified contract</span>
      <h2 id="next-step-heading">Identity, ownership, and permission scope</h2>
      <p>
        Product screens remain intentionally read-only placeholders until the P2 and P3 backend
        contracts are implemented and verified.
      </p>
    </div>
  </section>
</template>

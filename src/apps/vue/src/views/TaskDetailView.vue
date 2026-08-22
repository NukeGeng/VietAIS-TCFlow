<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { storeToRefs } from 'pinia'
import { useRoute } from 'vue-router'
import ResourceState from '../components/ResourceState.vue'
import TaskStatusPill from '../components/TaskStatusPill.vue'
import { useWorkspaceStore } from '../stores/workspace'
import {
  AiVerificationStatus,
  HumanApprovalStatus,
  TaskLifecycleStatus,
  TaskReviewDecision,
  taskPriorityLabel,
} from '../types/contracts'

const route = useRoute()
const workspace = useWorkspaceStore()
const { taskDetails, taskHistory, taskState } = storeToRefs(workspace)
const reviewComment = ref('')
const assigneeId = ref('')
const evidenceSummary = ref('')
const evidenceLocation = ref('')
const actionError = ref('')
const taskId = computed(() => String(route.params.taskId))

async function load(): Promise<void> {
  await workspace.loadTask(taskId.value)
}

async function review(decision: TaskReviewDecision): Promise<void> {
  actionError.value = ''
  try {
    await workspace.reviewTask(taskId.value, decision, reviewComment.value || undefined)
    reviewComment.value = ''
  } catch (error) {
    actionError.value = error instanceof Error ? error.message : 'Unable to record review.'
  }
}

async function assign(): Promise<void> {
  actionError.value = ''
  try {
    await workspace.assignTask(taskId.value, assigneeId.value)
    assigneeId.value = ''
  } catch (error) {
    actionError.value = error instanceof Error ? error.message : 'Unable to assign task.'
  }
}

async function addEvidence(): Promise<void> {
  actionError.value = ''
  try {
    await workspace.addEvidence(
      taskId.value,
      evidenceSummary.value,
      evidenceLocation.value || undefined,
    )
    evidenceSummary.value = ''
    evidenceLocation.value = ''
  } catch (error) {
    actionError.value = error instanceof Error ? error.message : 'Unable to add evidence.'
  }
}

watch(taskId, load, { immediate: true })
</script>

<template>
  <ResourceState :state="taskState" @retry="load">
    <template v-if="taskDetails">
      <section class="page-heading page-heading--with-actions">
        <div>
          <RouterLink class="back-link" :to="`/projects/${taskDetails.task.projectId}/tasks`"
            >← Back to board</RouterLink
          >
          <h1>{{ taskDetails.task.title }}</h1>
          <p>{{ taskDetails.task.description || 'No description provided.' }}</p>
        </div>
        <TaskStatusPill :status="taskDetails.task.status" />
      </section>

      <div v-if="actionError" class="inline-alert" role="alert">{{ actionError }}</div>

      <div class="detail-grid">
        <section class="panel detail-main">
          <div class="detail-facts">
            <div>
              <span>Priority</span
              ><strong>{{ taskPriorityLabel[taskDetails.task.priority] }}</strong>
            </div>
            <div>
              <span>AI verification</span
              ><strong>{{ AiVerificationStatus[taskDetails.task.aiVerification] }}</strong>
            </div>
            <div>
              <span>Human approval</span
              ><strong>{{ HumanApprovalStatus[taskDetails.task.humanApproval] }}</strong>
            </div>
            <div>
              <span>Version</span><strong>{{ taskDetails.task.currentVersion }}</strong>
            </div>
          </div>

          <section class="detail-section">
            <span class="eyebrow">Source trace</span>
            <h2>Why this task exists</h2>
            <div class="trace-grid">
              <article>
                <strong>{{ taskDetails.task.sourceTrace.sourceChangeId ? '1' : '0' }}</strong
                ><span>source change</span>
              </article>
              <article>
                <strong>{{ taskDetails.task.sourceTrace.artifactIds.length }}</strong
                ><span>artifacts</span>
              </article>
              <article>
                <strong>{{ taskDetails.task.sourceTrace.evidenceIds.length }}</strong
                ><span>evidence</span>
              </article>
              <article>
                <strong>{{ taskDetails.task.sourceTrace.impactIds.length }}</strong
                ><span>impacts</span>
              </article>
            </div>
          </section>

          <section class="detail-section">
            <span class="eyebrow">Evidence</span>
            <h2>Recorded proof</h2>
            <div v-if="taskDetails.evidence.length" class="timeline">
              <article v-for="evidence in taskDetails.evidence" :key="evidence.id">
                <span></span>
                <div>
                  <strong>{{ evidence.summary }}</strong
                  ><small
                    >{{ evidence.location || 'No source location' }} ·
                    {{ new Date(evidence.createdAt).toLocaleString() }}</small
                  >
                </div>
              </article>
            </div>
            <p v-else class="muted-copy">No evidence has been attached.</p>
          </section>

          <section class="detail-section">
            <span class="eyebrow">History</span>
            <h2>Version trail</h2>
            <div class="timeline">
              <article v-for="version in [...taskHistory].reverse()" :key="version.id">
                <span></span>
                <div>
                  <strong>v{{ version.version }} · {{ version.changeReason }}</strong
                  ><small
                    >{{ new Date(version.changedAt).toLocaleString() }} · actor
                    {{ version.changedBy.slice(0, 8) }}</small
                  >
                </div>
              </article>
            </div>
          </section>
        </section>

        <aside class="detail-aside">
          <section class="form-card">
            <span class="eyebrow">Human decision</span>
            <h2>Review</h2>
            <p>AI verification never substitutes for explicit human approval.</p>
            <label
              >Comment<textarea
                v-model="reviewComment"
                rows="4"
                placeholder="Explain the decision"
              ></textarea>
            </label>
            <div class="button-stack">
              <button
                class="primary-button"
                type="button"
                :disabled="
                  !workspace.hasPermission('task.review') ||
                  taskDetails.task.status !== TaskLifecycleStatus.ReadyForReview
                "
                @click="review(TaskReviewDecision.Approve)"
              >
                Approve
              </button>
              <button
                class="secondary-button"
                type="button"
                :disabled="
                  !workspace.hasPermission('task.review') ||
                  taskDetails.task.status !== TaskLifecycleStatus.ReadyForReview
                "
                @click="review(TaskReviewDecision.RequestChanges)"
              >
                Request changes
              </button>
              <button
                class="danger-button"
                type="button"
                :disabled="
                  !workspace.hasPermission('task.review') ||
                  taskDetails.task.status !== TaskLifecycleStatus.ReadyForReview
                "
                @click="review(TaskReviewDecision.Reject)"
              >
                Reject
              </button>
            </div>
            <small v-if="!workspace.hasPermission('task.review')">Requires task.review.</small
            ><small v-else-if="taskDetails.task.status !== TaskLifecycleStatus.ReadyForReview"
              >Review is available only in Ready for review.</small
            >
          </section>
          <form class="form-card" @submit.prevent="assign">
            <span class="eyebrow">Assignment</span>
            <h2>
              {{
                taskDetails.assignment
                  ? taskDetails.assignment.assigneeId.slice(0, 8)
                  : 'Unassigned'
              }}
            </h2>
            <p>
              {{
                taskDetails.assignment
                  ? `Assigned ${new Date(taskDetails.assignment.assignedAt).toLocaleDateString()}`
                  : 'Assign an active project member.'
              }}
            </p>
            <label
              >Member UUID<input
                v-model="assigneeId"
                required
                placeholder="00000000-0000-0000-0000-000000000000" /></label
            ><button
              class="secondary-button"
              type="submit"
              :disabled="!workspace.hasPermission('task.assign')"
            >
              Assign member</button
            ><small v-if="!workspace.hasPermission('task.assign')">Requires task.assign.</small>
          </form>
          <form class="form-card" @submit.prevent="addEvidence">
            <span class="eyebrow">Verification evidence</span>
            <h2>Add proof</h2>
            <label>Summary<textarea v-model="evidenceSummary" rows="3" required></textarea></label
            ><label
              >Source location<input
                v-model="evidenceLocation"
                placeholder="src/module/file.ts:42" /></label
            ><button
              class="secondary-button"
              type="submit"
              :disabled="!workspace.hasPermission('task.update')"
            >
              Add evidence</button
            ><small v-if="!workspace.hasPermission('task.update')">Requires task.update.</small>
          </form>
        </aside>
      </div>
    </template>
  </ResourceState>
</template>

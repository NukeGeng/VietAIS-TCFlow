import {
  AiVerificationStatus,
  HumanApprovalStatus,
  TaskLifecycleStatus,
  TaskPriority,
  type EngineeringTask,
  type PagedList,
} from '../types/contracts'

export function taskFixture(overrides: Partial<EngineeringTask> = {}): EngineeringTask {
  return {
    id: '10000000-0000-0000-0000-000000000001',
    projectId: '20000000-0000-0000-0000-000000000001',
    title: 'Verify task lifecycle',
    description: 'Reload from the backend after transition.',
    status: TaskLifecycleStatus.InProgress,
    priority: TaskPriority.High,
    sourceTrace: { artifactIds: [], evidenceIds: [], impactIds: [] },
    affectedArtifacts: [],
    inputs: [],
    outputs: [],
    businessRules: [],
    dependencies: [],
    createdBy: '30000000-0000-0000-0000-000000000001',
    createdByType: 0,
    createdAt: '2026-08-20T00:00:00Z',
    updatedAt: '2026-08-20T00:00:00Z',
    currentVersion: 1,
    aiVerification: AiVerificationStatus.NotRun,
    humanApproval: HumanApprovalStatus.Pending,
    ...overrides,
  }
}

export function page<T>(items: T[]): PagedList<T> {
  return {
    items,
    pageNumber: 1,
    pageSize: 100,
    totalCount: items.length,
    totalPages: items.length ? 1 : 0,
    hasPrevious: false,
    hasNext: false,
  }
}

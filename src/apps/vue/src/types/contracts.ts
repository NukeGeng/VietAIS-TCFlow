export type ResourceStatus = 'idle' | 'loading' | 'ready' | 'empty' | 'forbidden' | 'error'

export interface ResourceState {
  status: ResourceStatus
  message?: string
}

export interface PagedList<T> {
  items: T[]
  pageNumber: number
  pageSize: number
  totalCount: number
  totalPages: number
  hasPrevious: boolean
  hasNext: boolean
}

export interface TokenResponse {
  token: string
  refreshToken: string
  refreshTokenExpiryTime: string
}

export interface UserProfile {
  id: string
  userName?: string
  firstName?: string
  lastName?: string
  email?: string
  isActive: boolean
  emailConfirmed: boolean
  phoneNumber?: string
  imageUrl?: string
}

export interface Project {
  id: string
  name: string
  primaryOwnerId: string
  createdAt: string
}

export enum RepositoryProviderKind {
  Local = 0,
  GitHub = 1,
}

export enum RepositoryLifecycleStatus {
  Pending = 0,
  Active = 1,
  Disabled = 2,
}

export interface ProjectRepository {
  id: string
  projectId: string
  name: string
  provider: RepositoryProviderKind
  localPath?: string
  remoteUrl?: string
  defaultBranch: string
  status: RepositoryLifecycleStatus
  createdAt: string
  createdBy: string
}

export interface GitHubAppInstallation {
  id: string
  projectId: string
  installationId: number
  accountId: number
  accountLogin: string
  accountKind: number
  repositorySelection: number
  status: number
  createdAt: string
  updatedAt: string
  updatedBy: string
}

export interface GitHubRepositorySummary {
  id: number
  name: string
  fullName: string
  private: boolean
  defaultBranch: string
  htmlUrl: string
}

export interface GitHubInstallationStart {
  installationUrl: string
  expiresAt: string
}

export interface GitHubAuthorizationStart {
  projectId: string
  authorizationUrl: string
  state: string
  codeVerifier: string
  expiresAt: string
}

export interface GitHubConnectionResult {
  projectId: string
  installation: GitHubAppInstallation
  repositories: GitHubRepositorySummary[]
}

export interface ConnectedGitHubRepository {
  repository: ProjectRepository
  access: {
    id: string
    projectId: string
    projectRepositoryId: string
    installationDocumentId: string
    installationId: number
    gitHubRepositoryId: number
    fullName: string
    isSelected: boolean
    selectedAt: string
    selectedBy: string
  }
}

export enum ComponentScopeKind {
  Frontend = 0,
  Backend = 1,
  Database = 2,
  Tests = 3,
  Documentation = 4,
  Infrastructure = 5,
  SharedLibrary = 6,
  Service = 7,
}

export interface ProjectFeature {
  id: string
  projectId: string
  name: string
  description?: string
  createdAt: string
  createdBy: string
}

export enum TaskLifecycleStatus {
  Upcoming = 0,
  InProgress = 1,
  ReadyForReview = 2,
  Completed = 3,
  Blocked = 4,
  Rejected = 5,
  Cancelled = 6,
  Suggested = 7,
}

export enum TaskPriority {
  Low = 0,
  Medium = 1,
  High = 2,
  Critical = 3,
}

export enum AiVerificationStatus {
  NotRun = 0,
  Passed = 1,
  Failed = 2,
  Inconclusive = 3,
}

export enum HumanApprovalStatus {
  Pending = 0,
  Approved = 1,
  Rejected = 2,
  ChangesRequested = 3,
}

export enum AiTrustLevel {
  SuggestOnly = 0,
  CreateTasks = 1,
  UpdateTasks = 2,
  CodeGeneration = 3,
  PullRequestCreation = 4,
}

export enum TaskReviewDecision {
  Approve = 0,
  Reject = 1,
  RequestChanges = 2,
}

export enum TaskEvidenceKind {
  SourceChange = 0,
  Artifact = 1,
  Contract = 2,
  Dependency = 3,
  Impact = 4,
  Verification = 5,
}

export interface TaskSourceTrace {
  sourceChangeId?: string
  artifactIds: string[]
  evidenceIds: string[]
  impactIds: string[]
}

export interface EngineeringTask {
  id: string
  projectId: string
  repositoryId?: string
  componentId?: string
  componentScope?: ComponentScopeKind
  featureId?: string
  title: string
  description?: string
  status: TaskLifecycleStatus
  priority: TaskPriority
  sourceTrace: TaskSourceTrace
  affectedArtifacts: string[]
  inputs: string[]
  outputs: string[]
  businessRules: string[]
  dependencies: string[]
  createdBy: string
  createdByType: number
  createdAt: string
  updatedAt: string
  currentVersion: number
  aiVerification: AiVerificationStatus
  humanApproval: HumanApprovalStatus
}

export interface TaskAssignment {
  id: string
  projectId: string
  taskId: string
  assigneeId: string
  assignedBy: string
  assignedAt: string
}

export interface TaskReview {
  id: string
  projectId: string
  taskId: string
  reviewerId: string
  decision: TaskReviewDecision
  comment?: string
  createdAt: string
}

export interface TaskEvidence {
  id: string
  projectId: string
  taskId: string
  kind: TaskEvidenceKind
  summary: string
  location?: string
  sourceChangeId?: string
  artifactId?: string
  impactId?: string
  confidence?: number
  createdAt: string
  createdBy: string
  createdByType: number
}

export interface EngineeringTaskDetails {
  task: EngineeringTask
  assignment?: TaskAssignment
  reviews: TaskReview[]
  evidence: TaskEvidence[]
}

export interface EngineeringTaskSnapshot {
  title: string
  description?: string
  status: TaskLifecycleStatus
  priority: TaskPriority
  sourceTrace: TaskSourceTrace
  affectedArtifacts: string[]
  inputs: string[]
  outputs: string[]
  businessRules: string[]
  dependencies: string[]
  version: number
  aiVerification: AiVerificationStatus
  humanApproval: HumanApprovalStatus
}

export interface TaskVersion {
  id: string
  projectId: string
  taskId: string
  version: number
  snapshot: EngineeringTaskSnapshot
  assignment?: TaskAssignment
  review?: TaskReview
  evidence?: TaskEvidence
  changedBy: string
  changedByType: number
  changeReason: string
  changedAt: string
}

export interface PermissionGrantTrace {
  permissionCode: string
  roleId: string
  roleName: string
  resourceScope: number
  resourceId?: string
  componentScopes: ComponentScopeKind[]
}

export interface EffectivePermissionResult {
  projectId: string
  userId: string
  grants: PermissionGrantTrace[]
}

export interface PermissionDefinition {
  id: string
  description: string
  scope: number
  allowedResourceScopes: number[]
  allowedComponentScopes: ComponentScopeKind[]
}

export interface ProjectRole {
  id: string
  projectId: string
  name: string
  isSystemDefined: boolean
  isOwner: boolean
  permissions: Array<{
    permissionCode: string
    resourceScope: number
    resourceId?: string
    componentScopes: ComponentScopeKind[]
  }>
}

export interface AuditRecord {
  id: string
  projectId?: string
  actorId: string
  actorType: string
  action: string
  occurredAt: string
  targetType: string
  targetId: string
  before?: string
  after?: string
}

export interface ApiProblem {
  title?: string
  detail?: string
  status?: number
  errors?: Record<string, string[]>
}

export const taskStatusLabel: Record<TaskLifecycleStatus, string> = {
  [TaskLifecycleStatus.Upcoming]: 'Upcoming',
  [TaskLifecycleStatus.InProgress]: 'In progress',
  [TaskLifecycleStatus.ReadyForReview]: 'Ready for review',
  [TaskLifecycleStatus.Completed]: 'Completed',
  [TaskLifecycleStatus.Blocked]: 'Blocked',
  [TaskLifecycleStatus.Rejected]: 'Rejected',
  [TaskLifecycleStatus.Cancelled]: 'Cancelled',
  [TaskLifecycleStatus.Suggested]: 'Suggested',
}

export const taskPriorityLabel: Record<TaskPriority, string> = {
  [TaskPriority.Low]: 'Low',
  [TaskPriority.Medium]: 'Medium',
  [TaskPriority.High]: 'High',
  [TaskPriority.Critical]: 'Critical',
}

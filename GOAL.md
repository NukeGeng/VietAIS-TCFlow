# GOAL.md

# Source-Aware Engineering Planner

## 1. Mục tiêu tài liệu

Tài liệu này là đặc tả mục tiêu và phạm vi gốc của dự án **Source-Aware Engineering Planner**.

Dự án hướng tới việc xây dựng một hệ thống có khả năng hiểu source code hiện tại của một dự án phần mềm, theo dõi thay đổi của source code, xác định dependency và impact giữa các phần trong hệ thống, sau đó sinh hoặc cập nhật các engineering task phù hợp với convention, authority rule và quyền hạn của từng project/team.

Tài liệu này là nguồn tham chiếu chính cho:

- Phạm vi sản phẩm.
- Kiến trúc tổng thể.
- Tech stack.
- Domain model.
- Permission model.
- Workflow.
- AI/Codex integration.
- GitHub integration.
- Source analysis.
- Task lifecycle.
- Convention và authority.
- Repository intelligence.
- Roadmap triển khai.
- Acceptance criteria.

Dự án không được xem là một ứng dụng Kanban có AI.

Kanban chỉ là một giao diện biểu diễn output của hệ thống.

Giá trị cốt lõi của sản phẩm là:

> **Hiểu source code, xác định capability và contract, theo dõi dependency và impact, sau đó biến thay đổi của codebase thành engineering plan có thể hành động được.**

---

# 2. Tầm nhìn sản phẩm

## 2.1. Vấn đề cần giải quyết

Trong nhiều team phát triển phần mềm, frontend, backend, database, test, documentation và infrastructure thường được phát triển song song.

Một thay đổi ở một phần của source code có thể kéo theo nhiều thay đổi ở phần khác.

Ví dụ:

```text
Frontend
    ↓
thêm categoryId vào Create Product
    ↓
Backend
    ↓
Request DTO phải thay đổi
    ↓
Validation phải thay đổi
    ↓
Persistence model có thể thay đổi
    ↓
Tests phải được cập nhật
```

Hiện tại phần lớn việc xác định impact vẫn phụ thuộc vào con người:

- Developer phải tự đọc source.
- Tech Lead phải tự xác định dependency.
- Task phải được viết thủ công.
- Task dễ bị thiếu.
- Task dễ bị duplicate.
- Task dễ trở nên stale khi source thay đổi.
- Frontend và backend có thể lệch contract.
- Convention của từng team khác nhau.
- Không phải công ty nào cũng dùng frontend hoặc backend làm source of truth.

Dự án giải quyết vấn đề này bằng cách xây dựng một **Repository Intelligence Engine**.

---

# 3. Định nghĩa sản phẩm

Sản phẩm là:

> **Một source-aware engineering planner có khả năng hiểu repository, xây dựng dependency graph, phát hiện meaningful source changes, xác định cross-layer impact, hiểu convention và authority của project, sau đó tạo hoặc reconcile engineering task phù hợp.**

Sản phẩm không hard-code:

```text
Frontend → Backend
```

và cũng không hard-code:

```text
Backend → Frontend
```

Thay vào đó hệ thống sử dụng mô hình:

```text
Existing Source
      ↓
Extract Evidence
      ↓
Infer Capability
      ↓
Resolve Contract
      ↓
Dependency + Authority + Convention
      ↓
Impact Analysis
      ↓
Engineering Plan
      ↓
Task Reconciliation
```

---

# 4. Scope công nghệ ban đầu

## 4.1. Frontend

Frontend chính của sản phẩm sử dụng:

```text
Vue 3
TypeScript
Vite
```

Vue cũng là technology đầu tiên mà Source Analyzer phải hỗ trợ.

---

## 4.2. Backend

Backend của sản phẩm sử dụng:

```text
ASP.NET Core
FullStackHero dotnet-starter-kit
Baseline: release 2.0.4-rc
```

FullStackHero được sử dụng làm **backend foundation**, không chỉ copy một vài utility class.

Backend mới phải cố gắng giữ convention và module organization của FullStackHero.

---

## 4.3. Persistence

Persistence nghiệp vụ mới sử dụng:

```text
Marten
PostgreSQL
```

Marten được sử dụng trước tiên theo hướng:

```text
Document Database
```

Không đưa Event Sourcing vào phạm vi đầu tiên.

Các phần infrastructure có sẵn trong FullStackHero có thể tiếp tục sử dụng cơ chế hiện có nếu việc thay đổi toàn bộ sang Marten gây phá vỡ hoặc làm phình scope.

Không thực hiện migration toàn bộ FullStackHero từ EF Core sang Marten ngay lập tức.

Nguyên tắc:

> **Business module mới ưu tiên Marten. Infrastructure có sẵn của starter kit được giữ lại nếu hợp lý.**

---

# 5. Local Infrastructure

Local infrastructure sử dụng:

```text
.NET Aspire
```

Aspire lấy từ cấu trúc có sẵn của FullStackHero 2.0.4-rc.

Mục tiêu local development:

```text
Aspire Host
    │
    ├── ASP.NET API
    ├── PostgreSQL
    ├── Redis nếu framework cần
    └── Vue frontend
```

Một developer có thể start hệ thống từ một Aspire Host.

---

# 6. Source Vue hiện có

Đã có một source Vue.js đầy đủ chức năng.

Source này không phải frontend chính của sản phẩm.

Nó đóng vai trò:

```text
Test Fixture
Sample Repository
Ground Truth Repository
```

Ví dụ cấu trúc:

```text
workspace/

├── product/
│   ├── backend/
│   └── frontend/
│
└── samples/
    └── vue-full-application/
```

Source Vue mẫu được sử dụng để:

- Học Vue.
- Học cách frontend thể hiện requirement.
- Tự xây backend ASP.NET + Marten tương ứng.
- Tạo dữ liệu kiểm thử cho Vue Analyzer.
- Kiểm nghiệm rule extraction.
- Kiểm nghiệm API contract extraction.
- Kiểm nghiệm AI task generation sau này.

---

# 7. Triết lý phát triển

Dự án đi theo nguyên tắc:

> **Học gì thì áp dụng trực tiếp vào chính sản phẩm.**

Ví dụ:

```text
Học IDocumentSession
→ triển khai Create Repository

Học IQuerySession
→ triển khai Get Repositories

Học Pagination
→ triển khai repository/task pagination

Học Authentication
→ triển khai user/project membership

Học Permission
→ triển khai project authorization

Học Vue API calls
→ vừa xây frontend, vừa hiểu pattern mà analyzer phải parse

Học Aspire
→ đưa infrastructure vào Aspire
```

---

# 8. Khái niệm trung tâm

## 8.1. Repository

Repository đại diện cho source code được hệ thống quản lý hoặc phân tích.

Ví dụ:

```text
Repository
- Id
- ProjectId
- Name
- Provider
- LocalPath
- RemoteUrl
- DefaultBranch
- Status
```

---

## 8.2. Component

Component là một phần logic của hệ thống.

Ví dụ:

```text
Frontend
Backend
Database
Tests
Documentation
Infrastructure
Shared Library
Service
```

V1 tập trung vào:

```text
Vue Frontend
ASP.NET Backend
Marten Persistence
```

---

## 8.3. Artifact

Artifact là đơn vị kỹ thuật có thể phân tích.

Ví dụ:

```text
Vue Component
Composable
TypeScript Interface
API Call
ASP.NET Endpoint
Request DTO
Response DTO
Validator
Handler
Service
Marten Document
```

Ví dụ:

```json
{
  "type": "api_endpoint",
  "technology": "aspnetcore",
  "name": "CreateProduct",
  "path": "Features/Products/Create"
}
```

---

## 8.4. Dependency

Dependency thể hiện quan hệ giữa artifacts hoặc components.

Ví dụ:

```text
CreateProduct.vue
      │
      │ calls
      ▼
POST /api/products
      │
      │ accepts
      ▼
CreateProductRequest
      │
      │ stores
      ▼
Product
```

---

## 8.5. Evidence

Evidence là dữ kiện hệ thống lấy trực tiếp hoặc suy ra từ source.

Ví dụ:

```text
api.post('/api/products')
```

là evidence mạnh cho:

```text
POST /api/products
```

---

# 9. Evidence Levels

Mọi kết luận không được xem là chắc chắn như nhau.

Hệ thống sử dụng ba mức:

## 9.1. CONFIRMED

Có bằng chứng trực tiếp từ source.

Ví dụ:

```ts
api.post('/api/login')
```

Kết luận:

```text
POST /api/login
```

---

## 9.2. INFERRED

Có thể suy ra tương đối mạnh từ source.

Ví dụ:

```text
Login form
email
password
submit button
```

nhưng chưa có API call.

Hệ thống có thể suy ra:

```text
Authentication capability probably required.
```

---

## 9.3. PROPOSED

Là đề xuất kiến trúc hoặc contract của AI.

Ví dụ:

```text
Suggested endpoint:
POST /api/auth/login
```

Không được thể hiện PROPOSED như fact.

---

# 10. Capability Model

Hệ thống không đi thẳng từ UI sang API.

Nó sử dụng:

```text
Source Evidence
      ↓
Capability
      ↓
Contract
      ↓
Implementation
```

Ví dụ:

```text
Capability:
Create Product
```

Contract:

```text
POST /api/products

Input
- name
- price

Output
- id
- name
- price
```

Implementation frontend:

```text
CreateProduct.vue
ProductForm.vue
```

Implementation backend:

```text
CreateProductEndpoint
CreateProductRequest
CreateProductValidator
Product
```

---

# 11. Frontend-only Workflow

Trường hợp project chỉ có frontend và backend chưa tồn tại.

Workflow:

```text
Vue Source
    ↓
Vue Analyzer
    ↓
Extract Evidence
    ↓
Infer Capabilities
    ↓
Infer / Extract Contract
    ↓
Generate Backend Plan
```

Ví dụ:

```ts
await api.post('/api/auth/login', {
    email,
    password
});

authStore.setToken(response.data.accessToken);
```

Analyzer thu được:

```text
POST /api/auth/login

Input
- email
- password

Expected output
- accessToken
```

Task backend có thể được sinh:

```text
Implement Login API

Endpoint
POST /api/auth/login

Input
- email
- password

Output
- accessToken

Required backend responsibilities
- validate credentials
- authenticate user
- return token
```

Nếu source chưa có API call mà chỉ có UI intent:

```text
Form + submit
```

thì endpoint chỉ được đánh dấu:

```text
PROPOSED
```

---

# 12. Backend-only Workflow

Trường hợp chỉ có backend và chưa có frontend.

Workflow:

```text
ASP.NET Source
      ↓
Backend Analyzer
      ↓
Extract Capability + Contract
      ↓
Infer User Actions
      ↓
Generate Frontend Plan
```

Ví dụ backend:

```text
POST   /api/products
GET    /api/products
GET    /api/products/{id}
DELETE /api/products/{id}
```

Hệ thống có thể suy ra:

```text
Product capability

- Create product
- List products
- View product
- Delete product
```

Task frontend:

```text
Create Product UI
Product List UI
Product Detail UI
Delete Product Action
```

Hệ thống không được tự quyết định UX cụ thể nếu không có evidence.

Ví dụ không được mặc định:

```text
Bootstrap modal
4-column table
blue buttons
```

trừ khi Convention Profile cho thấy đó là convention của project.

---

# 13. Frontend + Backend Workflow

Khi cả hai phía tồn tại:

```text
Frontend Contract
       ↕
Backend Contract
       ↓
Compare
       ↓
Mismatch / Match
       ↓
Impact Analysis
```

Ví dụ:

Frontend:

```text
POST /api/products

name
price
categoryId
```

Backend:

```text
POST /api/products

name
price
```

Kết quả:

```text
Mismatch:
categoryId exists in frontend expectation
but not in backend contract
```

Authority Policy quyết định hướng xử lý.

---

# 14. Authority Model

Không tồn tại một rule toàn hệ thống kiểu:

```text
frontend_first = true
```

Authority được xác định theo từng loại knowledge.

Ví dụ:

```yaml
authority:
  api_contract: backend
  ui_requirement: frontend
  business_logic: backend
  persistence: backend
```

Một team khác:

```yaml
authority:
  api_contract: frontend
  ui_requirement: frontend
  business_logic: backend
  persistence: backend
```

Một team khác:

```yaml
authority:
  api_contract: openapi
  ui_requirement: frontend
  business_logic: backend
  persistence: backend
```

Authority trả lời:

> **Phần nào là source of truth cho loại thông tin này?**

---

# 15. Authority Workflow

Frontend thêm:

```text
categoryId
```

Backend chưa có.

Nếu:

```text
api_contract authority = frontend
```

thì:

```text
Frontend change
    ↓
Backend mismatch
    ↓
Backend impact
    ↓
Generate backend task
```

Nếu:

```text
api_contract authority = backend
```

thì:

```text
Frontend change
    ↓
Backend mismatch
    ↓
Frontend is not authoritative
    ↓
Create conflict / alignment task
```

Ví dụ:

```text
Frontend currently sends categoryId,
but backend contract does not expose this field.

Backend is configured as API authority.
```

---

# 16. Convention Profile

AI không được tự invent architecture.

Hệ thống phải hiểu convention hiện tại của repository.

Ví dụ:

```text
Features/
  Products/
    Create/
      CreateProductEndpoint.cs
      CreateProductRequest.cs
      CreateProductValidator.cs
```

Convention Profile có thể lưu:

```text
Architecture
- Feature-based

API
- Minimal API

Persistence
- Marten

Validation
- static validator classes

DTO
- Request / Response naming
```

Task generation phải dựa trên:

```text
Generic AI Knowledge
        +
Repository Knowledge
        +
Convention Profile
        +
Authority Policy
        +
Current Source
```

---

# 17. Repository Intelligence Engine

RAG không phải toàn bộ engine.

Core module được gọi là:

```text
Repository Intelligence Engine
```

Bên trong:

```text
Repository Intelligence Engine

├── Source Scanner
├── Technology Detector
├── Vue Analyzer
├── ASP.NET Analyzer
├── Marten Analyzer
├── AST / Syntax Analyzer
├── Symbol Index
├── Contract Analyzer
├── Convention Analyzer
├── Dependency Graph
├── Semantic Index
├── Retrieval Engine
└── AI Reasoning Engine
```

---

# 18. Static Analysis trước AI

AI không chịu trách nhiệm cho những việc deterministic code có thể làm chắc chắn.

Ví dụ AI không nên được dùng chỉ để:

- Tìm file.
- Detect Git diff.
- Detect HTTP method.
- Detect route string.
- Detect class.
- Detect property.
- Detect import.
- Detect DTO property.
- Detect `session.Store`.
- Detect `IQuerySession`.
- Detect `IDocumentSession`.

Các việc trên nên do analyzer thực hiện trước.

AI tập trung vào:

- Ý nghĩa business của thay đổi.
- Capability inference.
- Ambiguous impact.
- Task planning.
- Convention-aware reasoning.
- Dependency reasoning khó.
- Business logic inference.
- Task reconciliation reasoning.

---

# 19. Vue Analyzer

V1 Vue Analyzer tập trung vào các signal có cross-layer impact.

Phân tích:

```text
.vue components
<script setup>
props
emits
reactive state
form fields
API calls
request body
response usage
TypeScript interfaces
Pinia stores
Vue Router
validation
loading state
error state
permissions
buttons/actions
tables
filters
pagination
search
```

Không ưu tiên phân tích:

```text
CSS
color
margin
padding
animation
```

trừ khi chúng có relevance trực tiếp tới feature/logic.

---

# 20. ASP.NET Analyzer

ASP.NET Analyzer tập trung vào:

```text
Endpoints
HTTP methods
Routes
Request DTOs
Response DTOs
Validation
Authorization
Dependencies
Handlers
Services
Interfaces
OpenAPI contract
```

---

# 21. Marten Analyzer

V1 tập trung vào document database patterns.

Ví dụ:

```text
session.Store(...)
session.LoadAsync(...)
session.Query<T>()
session.Delete(...)
SaveChangesAsync()
IQuerySession
IDocumentSession
```

Có thể xây quan hệ:

```text
Endpoint
   ↓
Handler
   ↓
IDocumentSession
   ↓
Marten Document
```

Event Store chưa nằm trong scope chính của V1.

---

# 22. Repository Knowledge Graph

Sau initial scan, hệ thống xây graph.

Ví dụ:

```text
CreateProduct.vue
       │
       │ calls
       ▼
POST /api/products
       │
       │ accepts
       ▼
CreateProductRequest
       │
       │ validated by
       ▼
CreateProductValidator
       │
       │ persists
       ▼
Product
```

Graph là nguồn retrieval chính cho cross-layer impact.

---

# 23. Retrieval / RAG

Không gửi toàn repository vào AI.

Workflow:

```text
Repository
    ↓
Static Analysis
    ↓
Knowledge Graph
    ↓
Changed Artifacts
    ↓
Graph Neighborhood
    ↓
Relevant Context
    ↓
AI
```

Ví dụ repository có:

```text
20,000 files
```

nhưng commit chỉ chạm:

```text
CreateProduct.vue
```

Retrieval có thể chỉ lấy:

```text
CreateProduct.vue
CreateProductEndpoint.cs
CreateProductRequest.cs
CreateProductValidator.cs
Product.cs
```

---

# 24. AI Provider

AI integration phải được abstraction.

Ví dụ backend:

```csharp
public interface IAiReasoningProvider
{
    Task<ImpactAnalysisResult> AnalyzeImpactAsync(
        ImpactAnalysisContext context,
        CancellationToken cancellationToken);
}
```

Core không phụ thuộc cứng vào một model/vendor.

---

# 25. Codex / ChatGPT Authentication

Giai đoạn chưa thương mại hóa ưu tiên:

```text
ChatGPT account
     ↓
Codex authentication
     ↓
Codex runtime
```

Không dựa vào OpenAI API billing ngay từ đầu.

Không tự reverse-engineer ChatGPT OAuth.

Ứng dụng không tự lấy ChatGPT cookie/token.

Authentication AI phải được Codex runtime/App Server quản lý.

---

# 26. GitHub Authentication

GitHub và ChatGPT có trách nhiệm khác nhau.

```text
GitHub
→ identity + repository access

ChatGPT/Codex
→ AI reasoning entitlement/runtime
```

Không gộp hai cơ chế.

---

# 27. GitHub Integration

Giai đoạn đầu có thể hỗ trợ local repository.

Sau đó:

```text
Login with GitHub
      ↓
Install GitHub App
      ↓
Select Repository
      ↓
Initial Full Scan
      ↓
Webhook Monitoring
```

GitHub App dùng để:

- Repository access.
- Selected repository permission.
- Push events.
- Pull request events.
- Merge events.
- Commit metadata.
- Changed files.

---

# 28. Local-first

Ưu tiên mô hình local-first ở giai đoạn đầu.

Ví dụ:

```text
Product UI
    ↓
Local Agent / Backend
    ↓
Local Repository
    ↓
Analyzer
    ↓
Codex
```

Không bắt buộc upload toàn bộ source lên server trung tâm.

Có thể chỉ lưu:

```text
artifact metadata
summaries
dependencies
impacts
tasks
audit data
```

Điều này giúp:

- Prototype dễ hơn.
- Kiểm nghiệm cá nhân dễ hơn.
- Giảm rủi ro source code doanh nghiệp.
- Có thể trở thành selling point sau này.

---

# 29. AI Runtime Workflow

AI không scan repository trực tiếp.

Workflow chuẩn:

```text
Repository
    ↓
Static Analyzer
    ↓
Knowledge Graph
    ↓
Retrieval
    ↓
Relevant Context
    ↓
Codex Reasoning
    ↓
Structured Result
```

Structured result ví dụ:

```json
{
  "impact": "backend_change_required",
  "confidence": 0.96,
  "reason": "Frontend now submits categoryId but backend contract does not accept it.",
  "tasks": [
    {
      "title": "Support categoryId when creating products",
      "affectedArtifacts": [
        "CreateProductRequest",
        "CreateProductValidator",
        "Product"
      ],
      "requirements": [
        "Add categoryId to request contract",
        "Validate category",
        "Persist category relationship"
      ]
    }
  ]
}
```

---

# 30. AI Permission

AI được xem là một actor có permission.

Không dùng đơn giản:

```text
AI Enabled = true
```

Permission examples:

```text
ai.analysis.run
ai.task.suggest
ai.task.create
ai.task.update
ai.task.close
ai.code.suggest
ai.code.generate
ai.pull_request.create
```

Project Owner có thể cấu hình AI policy trong project.

Ví dụ:

```text
✓ Analyze source
✓ Suggest tasks
✓ Update task detail

✗ Auto-close task
✗ Generate code
✗ Create pull request
```

Nguyên tắc:

> **AI có quyền đề xuất theo policy. Con người vẫn là authority cuối cùng đối với business decision quan trọng.**

---

# 31. Change Detection

Initial connection:

```text
Repository
    ↓
Full Scan
```

Sau đó không full-scan mỗi lần.

Realtime workflow:

```text
push / pull_request / merge
        ↓
changed files
        ↓
incremental parse
        ↓
affected artifacts
        ↓
update graph
        ↓
retrieve affected neighborhood
        ↓
impact analysis
        ↓
task reconciliation
```

---

# 32. Meaningful Change Filter

Không phải mọi diff đều gọi AI.

Ví dụ:

```diff
- color: red
+ color: blue
```

có thể kết luận:

```text
UI-only cosmetic change
Cross-layer impact: none
```

AI calls:

```text
0
```

Mục tiêu là chỉ gọi AI khi change có khả năng ảnh hưởng contract, capability, dependency hoặc business behavior.

---

# 33. Impact Model

Impact mô tả:

```text
Source
Affected Resource
Severity
Reason
Confidence
Evidence
```

Ví dụ:

```json
{
  "source": "CreateProduct.vue",
  "affected": "CreateProductRequest",
  "severity": "high",
  "reason": "Frontend added categoryId",
  "confidence": 0.96
}
```

---

# 34. Impact Graph

UI có thể biểu diễn:

```text
                  Checkout UI
                      │
                      ▼
                 Checkout API
                      │
              ┌───────┴────────┐
              ▼                ▼
        Order Service     Checkout Tests
              │
              ▼
          PostgreSQL
```

Mức impact:

```text
Unaffected
Possible Impact
Action Required
```

---

# 35. Feature Model

Task không nên tồn tại hoàn toàn rời rạc.

Feature nhóm các task liên quan.

Ví dụ:

```text
Feature: Coupon Checkout

Frontend
✓ Add coupon input

Backend
◐ Add coupon validation

Database
○ Store coupon usage

Tests
○ Integration tests

Docs
○ API documentation
```

---

# 36. Task Model

Task có thể chứa:

```text
Id
ProjectId
RepositoryId
FeatureId
Title
Description
Status
Priority
SourceChangeId
AffectedArtifacts
Evidence
Confidence
Input
Output
BusinessRules
Dependencies
Assignee
Reviewer
CreatedBy
CreatedByType
```

CreatedByType có thể là:

```text
User
AI
System
```

---

# 37. Task Status Workflow

Task board mặc định:

```text
Upcoming
    ↓
In Progress
    ↓
Ready for Review
    ↓
Completed
```

Có thể có:

```text
Rejected
Blocked
Cancelled
```

---

# 38. Task Reconciliation

AI không được cứ thấy change mới là tạo task mới.

Workflow:

```text
New Change
    ↓
Find Related Existing Tasks
    ↓
Determine Action
    ↓
Create?
Update?
Merge?
Close?
Reopen?
Ignore?
```

Ví dụ:

Commit A:

```text
+ couponCode
```

Task:

```text
Support couponCode
```

Commit B:

```text
+ coupon validation
```

Không tạo duplicate task.

Cập nhật task hiện tại:

```text
Support couponCode

Requirements
✓ accept couponCode
○ validate coupon
```

---

# 39. Task Source Awareness

Mọi task do source change sinh ra phải truy ngược được source.

Ví dụ:

```text
Task #42
    ↓ generated from
Commit abc123
    ↓
CreateProduct.vue
```

Nếu commit bị revert:

```text
Task #42 may no longer be required.
```

AI có thể đề xuất:

```text
Close / Cancel / Re-evaluate
```

---

# 40. Task Verification

Khi developer code:

```text
Task
    ↓
Source update
    ↓
Analyzer
    ↓
Expected vs Actual
    ↓
Verification
```

Ví dụ:

```text
Expected:
POST /api/products

Input
name
price
categoryId

Actual:
POST /api/products

Input
name
price
categoryId
```

Nếu match:

```text
Ready for Review
```

Nếu thiếu:

```text
Missing:
category validation
```

---

# 41. Product User Model

Hệ thống phân biệt rõ:

```text
System Admin
```

và:

```text
Project Owner
```

---

# 42. System Admin

System Admin là người quản lý **toàn bộ platform**.

System Admin không phải Owner của từng project.

Scope:

```text
SYSTEM
```

System Admin có thể:

- Quản lý users toàn hệ thống.
- Xem danh sách projects.
- Suspend/disable project.
- Suspend user.
- Quản lý system settings.
- Quản lý permission definitions.
- Quản lý global AI configuration.
- Xem system audit logs.
- Transfer project ownership khi cần.
- Quản lý platform-level policies.

---

# 43. Project Owner

Project Owner là chủ sở hữu của **một project cụ thể**.

Scope:

```text
PROJECT
```

Project Owner có thể:

- Quản lý project của mình.
- Quản lý members trong project.
- Tạo custom roles.
- Gán permission cho custom roles.
- Assign roles cho members.
- Quản lý repository.
- Quản lý components.
- Quản lý authority policy.
- Quản lý convention profile.
- Quản lý AI permissions.
- Xem audit log của project.
- Transfer ownership.

Project Owner không được:

- Quản lý user toàn platform.
- Quản lý project khác.
- Gán system permission.
- Chỉnh system permission definitions.
- Xem system-wide audit nếu không phải System Admin.

---

# 44. Owner không phải Custom Role

`Project Owner` là một system-defined project role.

Không thể xóa Owner như custom role.

Project phải luôn có một Primary Owner.

Owner có thể transfer ownership.

Ví dụ:

```text
Project A

Owner:
Hao
```

Transfer:

```text
Hao → Minh
```

Sau đó:

```text
Minh → Owner
Hao → Member / custom role
```

Action phải:

```text
Require confirmation
+
Create audit log
```

---

# 45. Custom Project Roles

Owner có thể tạo role như:

```text
Backend Lead
Frontend Lead
Backend Developer
Frontend Developer
QA
Reviewer
Project Manager
Security Reviewer
Viewer
Intern
```

Role là:

```text
Permission Set
```

Không hard-code behavior theo tên role.

Ví dụ:

```text
Role: Backend Developer

✓ repository.view
✓ source.view
✓ analysis.view
✓ task.view
✓ task.status.update
✓ task.comment

✗ role.update
✗ member.remove
✗ project.delete
```

---

# 46. Permission Definitions

Permission Definition do hệ thống định nghĩa.

Owner không được tự tạo permission code tùy ý.

System cung cấp:

```text
task.view
task.create
task.update
task.assign
...
```

Owner chọn các permission có sẵn để tạo Role.

Flow:

```text
System Permission Definitions
          ↓
Available Project Permissions
          ↓
Project Owner selects
          ↓
Project Role
```

---

# 47. Permission Naming Convention

Sử dụng:

```text
resource.action
```

Ví dụ:

```text
project.view
project.update

member.view
member.invite
member.remove

role.view
role.create
role.update
role.delete

repository.view
repository.create
repository.update
repository.delete

source.view
source.analyze

analysis.view
analysis.run

task.view
task.create
task.update
task.delete
task.assign
task.approve
task.reject
task.comment
task.review

convention.view
convention.update

authority.view
authority.update

ai.analysis.run
ai.task.suggest
ai.task.create
ai.task.update
ai.task.close
ai.code.generate
ai.pull_request.create

audit.view
```

---

# 48. Authorization Architecture

Authorization sử dụng:

```text
RBAC
+
Permission-based Authorization
+
Resource Scope
+
Component Scope
```

Không dùng:

```csharp
if (user.Role == "Admin")
```

Ưu tiên:

```text
RequirePermission("task.assign")
```

và resource check.

---

# 49. Resource Scope

Permission chỉ nói action chưa đủ.

Cần scope.

Ví dụ:

```text
workspace/project
repository
component
own
assigned
all
```

Developer có thể:

```text
task.update
scope = assigned
```

Tech Lead:

```text
task.update
scope = repository
```

Owner:

```text
task.update
scope = project
```

---

# 50. Component Scope

Repository có:

```text
Frontend
Backend
Database
Tests
```

Backend Lead:

```text
Component Scope:
Backend
Database
```

Frontend Lead:

```text
Component Scope:
Frontend
```

Khi AI sinh Backend Task:

Backend Lead:

```text
Approve
Assign
Reject
Edit
```

Frontend Lead:

```text
View
Comment
```

nếu permission của họ chỉ cho phép như vậy.

---

# 51. Effective Permission

Hệ thống phải có khả năng tính:

```text
Effective Permissions
```

Ví dụ:

```text
User: Hao
Project: Shop
Repository: Backend
```

Output:

```text
repository.view     ✓
source.view         ✓
analysis.run        ✓
task.view           ✓
task.update         ✓
task.assign         ✓
task.approve        ✓
role.update         ✗
project.delete      ✗
```

Mỗi permission có thể trace nguồn:

```text
task.assign

Granted by:
Backend Lead

Scope:
Backend Repository
```

---

# 52. System Administration UI

System Admin có menu:

```text
System Administration

├── Users
├── Projects
├── System Roles
├── Permission Definitions
├── AI Providers
├── Global Settings
├── Usage
└── Audit Logs
```

---

# 53. Project Administration UI

Project Owner có menu:

```text
Project Administration

├── Members
├── Roles & Permissions
├── Permission Matrix
├── Repository Access
├── Component Access
├── Repositories
├── Authority Policies
├── Convention Profile
├── AI Permissions
├── Effective Permissions
├── Audit Logs
└── Project Settings
```

---

# 54. Role Permission Matrix

Owner phải có màn hình matrix.

Ví dụ:

```text
                    Owner Lead Dev Reviewer Viewer

Repository View       ✓     ✓    ✓      ✓      ✓
Repository Edit       ✓     ✓    ✗      ✗      ✗
Run Analysis          ✓     ✓    ✓      ✓      ✗

Task Create           ✓     ✓    ✗      ✗      ✗
Task Edit             ✓     ✓   own     ✗      ✗
Task Assign           ✓     ✓    ✗      ✗      ✗
Task Approve          ✓     ✓    ✗      ✓      ✗
```

---

# 55. Audit Log

Audit là bắt buộc.

Track:

```text
Who
Did what
When
On what
Before
After
```

Ví dụ:

```text
Hao changed role

Developer
→ Backend Lead
```

hoặc:

```text
Owner changed permission:

Backend Developer

+ task.assign
```

AI actions cũng phải audit:

```text
AI updated Task #143
```

---

# 56. Authority và Permission phải tách nhau

Authority trả lời:

> **Phần nào là source of truth?**

Permission trả lời:

> **User nào được thực hiện action?**

Ví dụ:

```text
API Authority = Backend
```

không đồng nghĩa mọi Backend Developer được update authority.

Chỉ role có:

```text
authority.update
```

mới được chỉnh.

---

# 57. Core Domain Model

Domain dự kiến:

```text
User
Project
ProjectMembership
ProjectRole
PermissionDefinition
RolePermission
MemberRole

Repository
Component
Artifact
Dependency
Change
Impact
Feature
Task
TaskAssignment

AuthorityPolicy
ConventionProfile
AiPermissionPolicy

AuditLog
```

---

# 58. System vs Project Roles

System Role:

```text
SystemAdmin
```

Project-level:

```text
ProjectOwner
CustomProjectRole
```

Không trộn hai loại.

---

# 59. Backend Module Organization

Backend giữ định hướng module-based của FullStackHero.

Ví dụ:

```text
src/

├── api/
│   ├── framework/
│   ├── modules/
│   │   └── RepositoryIntelligence/
│   │       ├── Application/
│   │       ├── Domain/
│   │       └── Infrastructure/
│   └── server/
│
├── apps/
│   └── vue/
│
└── aspire/
    ├── Host/
    └── service-defaults/
```

Tên module có thể chốt là:

```text
RepositoryIntelligence
```

---

# 60. Initial Learning Module

Trước khi analyzer phức tạp, module đầu tiên có thể quản lý:

```text
Project
Repository
Task
Membership
Role
Permission
```

Mục tiêu là dùng chính project để học Marten.

---

# 61. Marten Learning Path trong dự án

Các chức năng được map vào kiến thức Marten.

Ví dụ:

```text
Create Repository
→ IDocumentSession.Store
→ SaveChangesAsync

Get Repository
→ IQuerySession.LoadAsync

Repository List
→ IQuerySession.Query

Delete Repository
→ IDocumentSession.Delete

Pagination
→ IQueryable Skip/Take

Keyword Search
→ IQueryable / Expression-based filtering
```

---

# 62. Vue Product UI

Frontend chính có thể gồm:

```text
Dashboard
Projects
Repositories
Repository Analysis
Impact Graph
Tasks
Task Board
Features

Project Administration
System Administration
```

Task Board:

```text
Upcoming
In Progress
Ready for Review
Completed
```

---

# 63. GitHub + AI Login Experience

User flow:

```text
User Login
    ↓
Connect GitHub
    ↓
Select Repository
    ↓
Connect Codex / ChatGPT
    ↓
Analyze Repository
```

GitHub:

```text
repository identity/access
```

Codex:

```text
AI reasoning
```

---

# 64. Initial Repository Scan Workflow

```text
Select Repository
      ↓
Technology Detection
      ↓
File Discovery
      ↓
Vue Analyzer
      ↓
ASP.NET Analyzer
      ↓
Marten Analyzer
      ↓
Artifact Extraction
      ↓
Dependency Extraction
      ↓
Convention Detection
      ↓
Build Knowledge Graph
      ↓
Store Repository Knowledge
```

---

# 65. Incremental Change Workflow

```text
GitHub Push
    ↓
Webhook
    ↓
Changed Files
    ↓
Meaningful Change Filter
    ↓
Parse Changed Artifacts
    ↓
Update Dependency Graph
    ↓
Find Affected Neighborhood
    ↓
Retrieve Context
    ↓
AI Reasoning if needed
    ↓
Impact
    ↓
Task Reconciliation
    ↓
Audit
```

---

# 66. Task Generation Workflow

```text
Change
    ↓
Evidence
    ↓
Capability
    ↓
Contract
    ↓
Authority
    ↓
Convention
    ↓
Dependency
    ↓
Impact
    ↓
Task Proposal
    ↓
AI Permission Policy
    ↓
Suggest / Create
```

---

# 67. Task Work Workflow

```text
Upcoming
    ↓
Developer starts
    ↓
In Progress
    ↓
Source changes
    ↓
System re-analyzes
    ↓
Implementation compared with expected contract
    ↓
Ready for Review
    ↓
Reviewer approves
    ↓
Completed
```

---

# 68. Review Workflow

Reviewer sees:

```text
Task expectation
Source evidence
Changed files
Impact
Contract comparison
Missing requirements
AI confidence
```

Reviewer:

```text
Approve
Reject
Request Changes
```

---

# 69. Revert Workflow

```text
Source Change A
    ↓
Task created
    ↓
Change A reverted
    ↓
System detects revert
    ↓
Task becomes potentially obsolete
    ↓
Reconcile
    ↓
Close / Cancel / Re-open analysis
```

---

# 70. Duplicate Task Prevention

Task generator phải search existing tasks theo:

```text
Feature
Affected artifacts
Source change
Capability
Contract
Semantic similarity
```

Trước khi tạo.

---

# 71. Non-goals ban đầu

Không ưu tiên ngay:

```text
React
Angular
Java
Go
Python backend
Kubernetes
Kafka
Microservices
Marten Event Sourcing
Complex Projections
Automatic PR creation
Automatic code merge
Multi-vendor AI routing
Standalone graph database
```

Architecture phải cho phép mở rộng, nhưng implementation không cần support ngay.

---

# 72. Scope ban đầu thực tế

Vertical slice đầu tiên:

```text
Vue Form
   ↓
HTTP API
   ↓
ASP.NET Endpoint
   ↓
Request / Response
   ↓
Validation
   ↓
Marten Document
```

Engine phải hiểu chuỗi này thật tốt trước khi mở rộng.

---

# 73. Development Phases

## Phase 1 — Backend Foundation

```text
FullStackHero 2.0.4-rc
Aspire
PostgreSQL
Marten integration
```

---

## Phase 2 — Identity & Authorization

```text
User
Project
Project Owner
System Admin
Membership
Role
Permission
Permission Matrix
Audit
```

---

## Phase 3 — Project Management Core

```text
Project
Repository
Task
Kanban
Assignment
Review
```

---

## Phase 4 — Vue Product Frontend

```text
Project UI
Repository UI
Task Board
Admin UI
Permission UI
```

---

## Phase 5 — Vue Source Analyzer

Detect:

```text
components
forms
API calls
input
response usage
types
routes
```

---

## Phase 6 — ASP.NET Analyzer

Detect:

```text
endpoint
route
request
response
validation
authorization
dependencies
```

---

## Phase 7 — Marten Analyzer

Detect:

```text
documents
query
load
store
delete
session usage
```

---

## Phase 8 — Contract Comparison

```text
Frontend Expected Contract
        ↕
Backend Actual Contract
```

---

## Phase 9 — Repository Knowledge Graph

```text
Artifact
Dependency
Capability
Contract
```

---

## Phase 10 — Convention & Authority Engine

```text
Convention Profile
Authority Policy
```

---

## Phase 11 — AI / Codex

```text
Retrieval
Context Building
Impact Reasoning
Task Generation
Task Reconciliation
```

---

## Phase 12 — GitHub Integration

```text
GitHub Login
GitHub App
Repository Install
Initial Scan
```

---

## Phase 13 — Realtime Source Monitoring

```text
Webhook
Incremental Analysis
Task Reconciliation
```

---

# 74. Acceptance Criteria — Core Product

Một repository Vue + ASP.NET + Marten mẫu được xem là supported khi hệ thống có thể:

1. Detect technology.
2. Detect Vue API calls.
3. Detect request fields.
4. Detect response fields được frontend sử dụng.
5. Detect ASP.NET endpoints.
6. Detect request DTO.
7. Detect response DTO.
8. Detect Marten document.
9. Connect frontend API call với backend endpoint.
10. Connect backend endpoint với persistence artifacts.
11. Detect contract mismatch.
12. Apply authority rule.
13. Use repository convention.
14. Generate impact.
15. Generate hoặc update task.
16. Trace task về source change.
17. Reconcile task khi source tiếp tục thay đổi.
18. Respect permission và component scope.
19. Audit user và AI actions.

---

# 75. Acceptance Criteria — Permission System

Permission system chỉ được xem là hoàn chỉnh khi:

1. System Admin và Project Owner tách biệt.
2. Owner chỉ quản lý project của mình.
3. Owner tạo được custom role.
4. Owner chỉ chọn permission definitions mà system cho phép.
5. User có thể có role khác nhau theo project.
6. Permission có resource/component scope.
7. Backend enforce permission.
8. Frontend hide/disable UI theo effective permission.
9. Direct API calls không đủ quyền trả 403.
10. Effective permission có thể trace.
11. Role permission matrix hoạt động.
12. Audit mọi thay đổi role/permission.
13. AI có permission policy riêng.
14. Project Owner có thể transfer ownership.
15. System Admin có thể quản trị platform-level resources.

---

# 76. Acceptance Criteria — AI

AI layer chỉ được xem là đúng kiến trúc khi:

1. AI không cần nhận toàn repository mỗi lần.
2. Static analyzer chạy trước.
3. Knowledge graph được dùng cho retrieval.
4. Context chỉ chứa artifact liên quan.
5. AI output có structured schema.
6. AI result có confidence/evidence.
7. AI phân biệt CONFIRMED / INFERRED / PROPOSED.
8. AI không tự invent convention trái với repository.
9. AI task respect authority.
10. AI action respect AI permissions.
11. AI-generated task được audit.
12. AI không tạo duplicate task vô tội vạ.

---

# 77. Nguyên tắc kiến trúc bắt buộc

## 77.1. Technology-specific logic nằm trong Analyzer

Core engine không hard-code Vue/Marten logic.

Ví dụ:

```text
Core Engine
│
├── Vue Analyzer
├── ASP.NET Analyzer
└── Marten Analyzer
```

Sau này có thể thêm:

```text
React Analyzer
Java Analyzer
Go Analyzer
Terraform Analyzer
```

---

## 77.2. Domain model không phụ thuộc technology name

Không tạo:

```text
VueTask
MartenTask
BackendTask
```

Ưu tiên:

```text
Artifact
Dependency
Impact
Task
```

technology là metadata.

---

## 77.3. AI không thay static analyzer

AI là reasoning layer.

Static analysis là source-of-evidence layer.

---

## 77.4. Task phải source-aware

Task không được trở thành một TODO độc lập mất liên kết với source.

---

## 77.5. Authority và Permission phải độc lập

Authority không quyết định user permission.

Permission không quyết định source of truth.

---

## 77.6. Convention phải repository-aware

AI không được áp kiến trúc chung lên mọi project.

---

# 78. Định nghĩa thành công của dự án

Dự án thành công khi một developer có thể:

```text
Connect Repository
      ↓
System hiểu cấu trúc source
      ↓
System hiểu capability và contract hiện tại
      ↓
Source thay đổi
      ↓
System biết phần nào bị ảnh hưởng
      ↓
System giải thích vì sao
      ↓
System đề xuất hoặc tạo đúng engineering task
      ↓
Developer thực hiện
      ↓
System kiểm tra implementation
      ↓
Task được reconcile theo source thực tế
```

Trong đó hệ thống luôn tôn trọng:

```text
Source Evidence
Team Convention
Authority Policy
Permission
AI Policy
```

---

# 79. Product Boundary

Sản phẩm không được định vị đơn giản là:

> AI Task Manager

hoặc:

> AI Jira Clone

Định vị đúng:

> **Source-Aware Engineering Planner**

Hoặc:

> **Repository Intelligence and Change-to-Task Engine**

Thông điệp cốt lõi:

> **Your codebase writes its own engineering plan.**

---

# 80. Final Architecture Summary

```text
                         SYSTEM
                           │
                    System Admin
                           │
                        Project
                           │
                    Project Owner
                           │
               Roles / Permissions
                           │
                       Repository
                           │
            ┌──────────────┼───────────────┐
            ▼              ▼               ▼
      Vue Analyzer   ASP.NET Analyzer   Marten Analyzer
            │              │               │
            └──────────────┼───────────────┘
                           ▼
                  Repository Knowledge
                           │
                  Dependency Graph
                           │
          ┌────────────────┼─────────────────┐
          ▼                ▼                 ▼
     Convention        Authority          Evidence
          │                │                 │
          └────────────────┼─────────────────┘
                           ▼
                       Retrieval
                           │
                           ▼
                    Codex Reasoning
                           │
                           ▼
                    Impact Analysis
                           │
                           ▼
                    Task Reconciler
                           │
                     ┌─────┼─────┐
                     ▼     ▼     ▼
                  Create Update Close
                     │
                     ▼
                   Board
                     │
                     ▼
                  Developer
                     │
                     ▼
                 Source Change
                     │
                     └──────────────→ Analyze again
```

---

# 81. Quyết định đã chốt

Các quyết định sau được xem là baseline hiện tại của dự án:

```text
Frontend:
Vue 3 + TypeScript + Vite

Backend:
ASP.NET Core
FullStackHero 2.0.4-rc

Persistence:
Marten + PostgreSQL

Local Infrastructure:
.NET Aspire

Initial Analyzer Scope:
Vue + ASP.NET + Marten

AI:
Codex / ChatGPT-authenticated runtime trước
API abstraction giữ mở

Git:
GitHub integration sau khi local analyzer ổn

Architecture:
Static Analysis + Knowledge Graph + Retrieval + AI Reasoning

Task Model:
Source-aware + reconciliation

Authority:
Configurable theo project/type

Convention:
Repository-aware

System Authorization:
System Admin

Project Authorization:
Project Owner + Custom Roles

Permission:
RBAC + Permission + Resource Scope + Component Scope

AI:
Actor có permission riêng

Audit:
Bắt buộc
```

---

# 82. Nguyên tắc cuối cùng

Khi có bất kỳ quyết định thiết kế mới nào, ưu tiên kiểm tra theo thứ tự:

```text
1. Có làm core engine phụ thuộc cứng vào một framework không?
2. Có phá vỡ source-awareness không?
3. Có khiến AI phải làm việc deterministic không?
4. Có bỏ qua convention của repository không?
5. Có bỏ qua authority policy không?
6. Có bỏ qua permission/scope không?
7. Có tạo task không trace được về evidence không?
8. Có khiến task dễ duplicate/stale không?
9. Có làm full repository scan không cần thiết không?
10. Có làm architecture phình scope trước khi vertical slice hiện tại chạy tốt không?
```

Nếu có, cần xem lại thiết kế trước khi triển khai.

---

**Status:** Baseline specification đã chốt từ quá trình trao đổi hiện tại.
**Primary implementation scope:** Vue 3 + ASP.NET Core + FullStackHero + Marten + PostgreSQL + Aspire.
**Core product identity:** Source-Aware Engineering Planner.

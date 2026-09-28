# Development Plan

## 1. Purpose

This document defines the implementation plan for the **Mayorx Content Engine**.

It translates the product requirements, AI architecture, system architecture, database schema, API specification, and design system into an ordered development process.

The goal is to build the product as a reliable, modular content operating system that can:

1. Understand a user's brand and audience.
2. Develop a content strategy.
3. Discover content opportunities.
4. Generate platform-specific content.
5. Evaluate and improve generated content.
6. Allow users to review and approve content.
7. Schedule and publish approved content.
8. Collect performance data.
9. Learn from content performance over time.

The first release should prioritize a **complete working content lifecycle** over building every planned feature.

---

# 2. Development Philosophy

## 2.1 Build vertical slices

Development should prioritize complete workflows rather than isolated infrastructure.

Instead of building every backend service first and every frontend screen afterward, build features through the full stack.

Example:

```text
Create Content Idea
        ↓
API
        ↓
Database
        ↓
Frontend
        ↓
AI Generation
        ↓
Review
        ↓
Approval
```

This allows the team to validate the product continuously.

---

## 2.2 Build the smallest reliable version first

The system should avoid premature complexity.

V1 should use:

- Modular monolith architecture.
- One PostgreSQL database.
- Redis.
- Background workers.
- Clear internal service boundaries.
- Provider abstractions where external integrations are likely to change.
- REST APIs.
- A single frontend application.

Do not introduce microservices unless a real scaling or ownership problem requires them.

---

## 2.3 AI should not control critical application behavior

The AI layer is responsible for:

- Recommendations.
- Ideas.
- Content generation.
- Content evaluation.
- Research interpretation.
- Strategy suggestions.

The application is responsible for:

- Authentication.
- Authorization.
- Validation.
- Scheduling.
- Publishing.
- Data integrity.
- Rate limits.
- Idempotency.
- Security.
- Auditability.

The boundary should remain:

```text
AI recommends
Application validates
User approves
System executes
```

---

## 2.4 Prefer structured AI outputs

AI features should return structured data whenever possible.

For example:

```json
{
  "title": "Why your business website is losing customers",
  "angle": "Problem → insight → solution",
  "hook": "Your website might be costing you customers without you realizing it.",
  "content": "...",
  "cta": "...",
  "hashtags": [],
  "confidence": 0.86
}
```

Structured outputs make AI behavior easier to validate, test, store, and display.

---

# 3. Technology Stack

## Frontend

- Next.js
- React
- Tailwind CSS
- shadcn/ui
- TanStack Query
- Zustand
- TypeScript

## Backend

- .NET 9
- ASP.NET Core
- Entity Framework Core
- PostgreSQL
- Redis
- Background workers

## AI

- Provider abstraction
- LLM provider(s)
- Embedding provider
- Structured JSON generation
- Retrieval-augmented generation where appropriate

## Infrastructure

- Docker
- Docker Compose
- GitHub Actions
- PostgreSQL
- Redis
- Production application hosting
- Secure secrets management

## External Platforms

Initial:

- X
- LinkedIn

Future:

- Instagram
- Facebook
- Threads
- Other supported platforms

---

# 4. Implementation Order

The recommended implementation order is:

```text
1. Repository & Development Environment
        ↓
2. Backend Foundation
        ↓
3. Database & EF Core
        ↓
4. Frontend Foundation
        ↓
5. Authentication & Workspaces
        ↓
6. Brand / Audience / Strategy
        ↓
7. AI Foundation
        ↓
8. Content Ideas & Generation
        ↓
9. Review & Approval Workflow
        ↓
10. Social Account Connections
        ↓
11. Scheduling & Publishing
        ↓
12. Analytics
        ↓
13. Learning Loop
        ↓
14. Hardening
        ↓
15. Production Deployment
```

The system should become increasingly useful after each major phase.

---

# 5. Phase 0 — Repository and Development Environment

## Objective

Create a clean development foundation before implementing business functionality.

## Tasks

### Repository

Create:

```text
mayorx-content-engine/
├── apps/
│   ├── web/
│   ├── api/
│   └── worker/
├── packages/
│   ├── shared/
│   └── contracts/
├── docs/
├── tests/
│   ├── unit/
│   ├── integration/
│   └── e2e/
├── docker/
└── README.md
```

### Git

Configure:

- `main`
- `develop`
- feature branches
- pull requests
- branch protection
- conventional commit style

Example:

```text
feature/content-generation
feature/linkedin-integration
fix/publication-timezone
refactor/ai-orchestration
```

### Local infrastructure

Create Docker Compose services for:

```text
PostgreSQL
Redis
```

The API, worker, and frontend may initially run directly from their development environments.

### Configuration

Establish environment-specific configuration:

```text
appsettings.json
appsettings.Development.json
.env.local
```

Never commit secrets.

## Deliverable

A developer should be able to clone the repository and start the core infrastructure with a documented command.

---

# 6. Phase 1 — Backend Foundation

## Objective

Create the base ASP.NET Core application and internal architecture.

## Tasks

Create:

```text
apps/api/
├── Controllers/
├── Contracts/
├── Domain/
├── Services/
├── Infrastructure/
├── Persistence/
├── Authorization/
├── Middleware/
├── Configuration/
└── Program.cs
```

Implement:

- Dependency injection.
- Global exception handling.
- Request logging.
- API versioning.
- Health checks.
- Validation.
- Standard API responses.
- Standard error responses.
- Trace IDs.
- Configuration binding.

## Health endpoints

Example:

```http
GET /health
GET /health/ready
```

Readiness should eventually verify dependencies such as:

- PostgreSQL.
- Redis.

## Deliverable

The API starts successfully and exposes documented health endpoints.

---

# 7. Phase 2 — Database and Persistence

## Objective

Implement PostgreSQL and the initial domain model.

## Tasks

Configure:

- EF Core.
- Npgsql.
- PostgreSQL migrations.
- UUID identifiers.
- UTC timestamps.
- Soft deletion where applicable.
- Database indexes.
- Foreign keys.
- Concurrency considerations.

Implement the MVP tables:

```text
users
workspaces
workspace_members
brand_profiles
audience_profiles
content_strategies
content_pillars
content_ideas
content_drafts
content_variants
social_accounts
schedules
publications
publication_metrics
ai_runs
content_evaluations
audit_logs
```

## Database rules

All core business data should be relational.

Use JSONB only when flexibility is genuinely required.

Examples:

- AI metadata.
- Platform-specific metadata.
- Provider responses.
- Flexible configuration.

Do not use JSONB as a replacement for properly modeled domain relationships.

## Deliverable

A fresh developer environment can create the complete MVP database using migrations.

---

# 8. Phase 3 — Frontend Foundation

## Objective

Create the application shell and design system.

## Tasks

Initialize:

- Next.js.
- Tailwind CSS.
- shadcn/ui.
- TanStack Query.
- Zustand.
- API client.

Implement:

```text
Application Shell
├── Sidebar
├── Topbar
├── Main Content
└── Responsive Mobile Navigation
```

Create foundational components:

```text
Button
Input
Textarea
Select
Dialog
Drawer
Dropdown
Tabs
Badge
Card
Table
Tooltip
Toast
Skeleton
EmptyState
ErrorState
```

Create application routes:

```text
/dashboard
/content
/content/ideas
/content/drafts
/content/calendar
/analytics
/strategy
/ai-insights
/connections
/settings
```

## Deliverable

The frontend has the final application shell and can navigate between the major product areas.

---

# 9. Phase 4 — Authentication and Workspaces

## Objective

Establish secure user identity and multi-tenant workspace isolation.

## Backend

Implement:

- Registration.
- Login.
- Refresh tokens.
- Logout.
- Password management.
- User profile.
- Workspace creation.
- Workspace membership.

Roles:

```text
Owner
Admin
Editor
Viewer
```

## Frontend

Implement:

```text
/login
/register
/forgot-password
/reset-password
/onboarding
```

Create workspace selection and workspace context.

The frontend should send:

```http
X-Workspace-Id
```

for workspace-scoped requests where required.

## Security requirements

Every workspace-scoped endpoint must verify:

1. Authenticated user.
2. Workspace membership.
3. Required role.
4. Resource ownership where applicable.

## Deliverable

A user can register, create a workspace, authenticate, and access only authorized workspace data.

---

# 10. Phase 5 — Brand, Audience, and Strategy

## Objective

Give the AI enough structured context to understand what the user is trying to achieve.

## Brand Profile

Implement:

- Brand name.
- Description.
- Industry.
- Location.
- Products/services.
- Positioning.
- Brand values.
- Voice.
- Tone.
- Content preferences.
- Topics to avoid.

## Audience Profile

Implement:

- Audience description.
- Pain points.
- Goals.
- Interests.
- Knowledge level.
- Objections.
- Desired transformation.
- Platform behavior.

## Strategy

Implement:

- Primary goals.
- Content pillars.
- Content mix.
- Posting frequency.
- Preferred platforms.
- CTA preferences.

## Content pillars

Example:

```text
Education
Authority
Storytelling
Behind the Scenes
Case Studies
Opinion
Promotion
Community
```

The user should be able to create, edit, activate, deactivate, and reorder pillars.

## Deliverable

A workspace has a structured Brand Brain and Audience Brain that can be passed into AI generation.

---

# 11. Phase 6 — AI Foundation

## Objective

Build the core AI infrastructure before building advanced agents.

## Tasks

Create:

```text
IContentIntelligenceService
```

and provider abstractions.

Implement:

- LLM provider configuration.
- Model selection.
- Prompt templates.
- Structured outputs.
- AI run logging.
- Token usage tracking.
- Cost estimation.
- Error handling.
- Retry policies.
- AI request IDs.

## AI run lifecycle

Every meaningful AI operation should record:

```text
Workspace
User
Operation
Model
Prompt Version
Input Context
Output
Token Usage
Latency
Status
Error
Created At
```

Sensitive information should not be unnecessarily persisted.

## Prompt architecture

Use layered prompts:

```text
System Instructions
        ↓
Product Strategy
        ↓
Brand Context
        ↓
Audience Context
        ↓
Task Instructions
        ↓
Research Context
        ↓
Output Schema
```

## Deliverable

The system can execute a structured AI request and persist an auditable AI run.

---

# 12. Phase 7 — Content Intelligence and Generation

## Objective

Implement the first useful AI content workflow.

## Initial pipeline

```text
Brand Context
      ↓
Audience Context
      ↓
Strategy
      ↓
Research
      ↓
Opportunity Discovery
      ↓
Opportunity Scoring
      ↓
Content Strategy
      ↓
Content Generation
      ↓
Quality Evaluation
```

## Opportunity discovery

The system should identify potential topics based on:

- Brand relevance.
- Audience relevance.
- Timeliness.
- Authority.
- Discussion potential.
- Originality.
- Content goals.
- Historical performance when available.

## Content generation

Generate:

- Hook.
- Main content.
- CTA.
- Hashtags where appropriate.
- Suggested platform.
- Suggested content type.
- Suggested posting time.
- Reasoning/recommendation metadata.

## Storytelling

Support frameworks such as:

```text
Personal Story
Problem → Solution
Observation → Insight
Mistake → Lesson
Case Study
Contrarian Perspective
Build in Public
Educational Breakdown
```

## Quality evaluation

Evaluate:

- Strategic fit.
- Audience fit.
- Hook strength.
- Value.
- Clarity.
- Originality.
- Authenticity.
- Storytelling.
- Platform fit.
- Voice match.
- Factual reliability.
- AI-language risk.

Limit automatic AI revision cycles to prevent unnecessary model usage.

## Deliverable

A user can ask the system to generate content and receive a structured draft with an evaluation.

---

# 13. Phase 8 — Content Workflow

## Objective

Turn AI-generated output into a controlled publishing workflow.

## Content lifecycle

```text
IDEA
 ↓
DRAFT
 ↓
IN_REVIEW
 ↓
APPROVED
 ↓
SCHEDULED
 ↓
PUBLISHING
 ↓
PUBLISHED
```

Alternative states:

```text
FAILED
ARCHIVED
```

## Frontend

Implement:

- Idea list.
- Draft list.
- Content editor.
- AI generation panel.
- AI recommendations.
- Quality score breakdown.
- Edit history where required.
- Platform preview.
- Approval actions.

## Important UX rule

AI should never silently overwrite user-edited content.

Clearly distinguish:

```text
AI Generated
User Edited
User Created
System Generated
```

## Deliverable

A user can generate, edit, review, approve, and prepare content for publishing.

---

# 14. Phase 9 — Social Account Connections

## Objective

Connect the system to external social platforms.

Initial platforms:

```text
X
LinkedIn
```

## Architecture

Use:

```csharp
public interface ISocialPlatform
{
    string Platform { get; }

    Task<PlatformValidationResult> ValidateAsync(
        ContentVariant content,
        CancellationToken cancellationToken);

    Task<PublishResult> PublishAsync(
        ContentVariant content,
        SocialAccount account,
        CancellationToken cancellationToken);
}
```

Each platform gets its own adapter.

Example:

```text
Social/
├── X/
│   └── XSocialPlatform.cs
└── LinkedIn/
    └── LinkedInSocialPlatform.cs
```

## Security

OAuth tokens must:

- Never be returned to the frontend.
- Be encrypted at rest.
- Be protected from logs.
- Be refreshable where supported.
- Be revocable.

## Deliverable

A user can securely connect and disconnect supported social accounts.

---

# 15. Phase 10 — Scheduling and Publishing

## Objective

Build reliable automated publishing.

## Scheduling

Implement:

- One-time scheduling.
- Timezone-aware scheduling.
- Platform-specific schedules.
- Schedule editing.
- Schedule cancellation.

All timestamps should be stored in UTC.

The user's configured timezone should be used when displaying and creating schedules.

## Publishing validation

Before publishing, verify:

```text
Content exists
        ↓
Content is approved
        ↓
Variant exists
        ↓
Social account exists
        ↓
Token is valid
        ↓
Platform validation passes
        ↓
Schedule is valid
        ↓
Publication has not already succeeded
```

## Worker

The worker should process scheduled publications.

```text
Scheduler
   ↓
Publication Job
   ↓
Validation
   ↓
Platform Adapter
   ↓
External Platform
   ↓
Publication Result
```

## Idempotency

Publishing must be idempotent.

Use:

```http
Idempotency-Key
```

and database-level safeguards to prevent accidental duplicate posts.

## Retry policy

Retries should be used only for recoverable failures.

Examples:

```text
Network timeout       → Retry
Temporary platform error → Retry
Expired OAuth token   → Re-authentication required
Invalid content       → Do not retry automatically
Platform rejection    → Do not blindly retry
```

## Deliverable

An approved post can be scheduled and reliably published to a connected social account.

---

# 16. Phase 11 — Analytics

## Objective

Capture what happens after publication.

## Metrics

Normalize available platform metrics into:

```text
Impressions
Likes
Comments
Shares
Clicks
Profile Visits
Followers Gained
Engagement Rate
```

Not every platform will provide every metric.

The system must preserve the distinction between:

```text
Metric unavailable
Metric = 0
Metric not yet synchronized
```

These are not the same state.

## Analytics synchronization

Create background jobs for:

```text
AnalyticsSyncRequested
```

The worker fetches platform metrics and updates:

```text
publication_metrics
```

## Dashboard

Display:

- Total published posts.
- Engagement.
- Reach/impressions.
- Top-performing posts.
- Platform comparison.
- Content pillar performance.
- Recent trends.

Avoid claiming causation where the data only shows correlation.

## Deliverable

Users can see measurable performance of published content.

---

# 17. Phase 12 — Learning Loop

## Objective

Turn analytics into future content intelligence.

## Pipeline

```text
Published Content
      ↓
Performance Metrics
      ↓
Performance Analysis
      ↓
AI Insight
      ↓
Learning Signal
      ↓
Future Strategy
      ↓
Future Content
```

## Learning signals

Examples:

```text
Topic performed well
Hook pattern performed well
CTA performed well
Content pillar underperformed
Posting window showed stronger engagement
Storytelling format performed well
Platform-specific pattern identified
```

Learning signals should be evidence-based.

The system should not automatically make major strategic changes from a single post.

## Exploration vs exploitation

The AI should balance:

```text
Known successful patterns
+
New content experiments
```

The system should continue testing new ideas instead of repeating the same format indefinitely.

## Deliverable

Performance data influences future content recommendations.

---

# 18. Phase 13 — Notifications

## Objective

Keep users informed about important system activity.

Notifications may include:

- Content generation completed.
- Content needs review.
- Scheduled post published.
- Publishing failed.
- OAuth connection expired.
- Analytics synchronization completed.
- AI recommendation available.

Notifications should not become excessive.

Only meaningful events should generate notifications.

---

# 19. Phase 14 — Observability and Reliability

## Objective

Make the system diagnosable before production scale.

Implement:

### Application logging

Track:

- Request ID.
- Trace ID.
- User ID.
- Workspace ID.
- Operation.
- Duration.
- Result.

Never log:

- OAuth access tokens.
- Refresh tokens.
- Passwords.
- API keys.
- Sensitive personal data unnecessarily.

### Metrics

Track:

```text
API latency
API error rate
AI latency
AI failure rate
AI token usage
AI cost
Publication success rate
Publication failure rate
Queue depth
Worker failures
Analytics sync failures
```

### Health monitoring

Monitor:

```text
API
Database
Redis
Worker
AI provider
Social integrations
```

---

# 20. Phase 15 — Security Hardening

## Authentication

Implement:

- Secure password hashing.
- Short-lived access tokens.
- Refresh token rotation where appropriate.
- Secure token storage.
- Logout/revocation.

## Authorization

Enforce authorization server-side.

Never rely on frontend role checks for security.

## Workspace isolation

Every workspace-scoped query must enforce the workspace boundary.

Avoid relying on developers remembering to add:

```csharp
.Where(x => x.WorkspaceId == workspaceId)
```

to every query manually.

Prefer reusable authorization/query patterns where appropriate.

## External secrets

Store secrets using environment variables or a proper secret manager.

Never commit:

```text
API keys
OAuth secrets
JWT signing secrets
Database passwords
```

## API protection

Implement:

- Rate limiting.
- Input validation.
- Request size limits.
- Secure headers.
- CORS restrictions.
- Abuse protection.

---

# 21. Testing Strategy

Testing should cover both deterministic application behavior and probabilistic AI behavior.

## 21.1 Unit tests

Test:

- Domain logic.
- Validators.
- Authorization policies.
- Content lifecycle transitions.
- Scheduling logic.
- Timezone conversion.
- Platform validation.
- Publishing idempotency.
- Analytics calculations.
- AI output parsing.

Example:

```text
Approved content cannot return to Draft
unless an explicit edit workflow allows it.
```

---

## 21.2 Integration tests

Test:

- API + PostgreSQL.
- API + Redis.
- EF Core queries.
- Authentication.
- Workspace isolation.
- AI service integration.
- Worker + database.
- Publishing workflow.

Use isolated test environments.

---

## 21.3 End-to-end tests

Critical flows:

### Authentication

```text
Register
→ Login
→ Create Workspace
→ Access Dashboard
```

### Content generation

```text
Configure Brand
→ Configure Audience
→ Generate Opportunity
→ Generate Content
→ Evaluate
→ Edit
→ Approve
```

### Publishing

```text
Connect Account
→ Create Content
→ Approve
→ Schedule
→ Worker Executes
→ Publish
→ Record Result
```

### Analytics

```text
Published Content
→ Sync Metrics
→ Dashboard Updated
```

---

# 22. AI Evaluation Testing

AI requires a separate evaluation system.

Create a small golden dataset containing representative examples.

Test:

- Brand voice adherence.
- Audience relevance.
- Hook quality.
- Platform fit.
- Factual reliability.
- Output schema validity.
- Repetition.
- AI-language risk.

Track performance across prompt/model versions.

A prompt change should not be considered safe simply because one example looks good.

---

# 23. Environments

Use at least:

```text
Development
Staging
Production
```

## Development

Used for:

- Local development.
- Docker services.
- Mock or sandbox integrations.
- Rapid iteration.

## Staging

Used for:

- Integration testing.
- OAuth testing.
- Worker testing.
- Production-like configuration.
- Release validation.

## Production

Used for:

- Real users.
- Real social accounts.
- Real publishing.
- Production analytics.

Production should never be used as a development environment.

---

# 24. Configuration Strategy

Configuration should be environment-driven.

Examples:

```text
Database
Redis
JWT
AI providers
Embedding provider
X OAuth
LinkedIn OAuth
Application URLs
CORS
Rate limits
Worker configuration
```

Use strongly typed configuration objects in .NET.

Avoid scattering:

```csharp
Environment.GetEnvironmentVariable(...)
```

throughout the codebase.

---

# 25. Background Jobs

The worker should own asynchronous operations that do not need to block HTTP requests.

Initial jobs:

```text
ContentGenerationJob
ContentEvaluationJob
ResearchJob
PublicationJob
AnalyticsSyncJob
PerformanceAnalysisJob
LearningSignalJob
NotificationJob
```

Jobs should support:

- Retry.
- Failure tracking.
- Idempotency.
- Cancellation where appropriate.
- Logging.
- Dead-letter/failure handling.

---

# 26. Git and Branching Strategy

Use a simple feature-branch workflow.

```text
main
  ↑
feature/*
```

Recommended:

```text
feature/authentication
feature/workspace-management
feature/content-ideas
feature/ai-generation
feature/x-integration
```

Bug fixes:

```text
fix/publication-timezone
fix/content-status-transition
```

Refactoring:

```text
refactor/ai-provider
refactor/database-repositories
```

## Pull request expectations

Every PR should:

- Have a focused purpose.
- Pass automated tests.
- Avoid unrelated changes.
- Include database migrations when required.
- Update documentation when behavior changes.
- Include screenshots for significant UI changes.

---

# 27. Coding Standards

## Backend

Follow:

- Clear naming.
- Dependency injection.
- Async APIs.
- CancellationToken propagation.
- DTOs instead of exposing EF entities.
- Thin controllers.
- Business logic in services/domain layers.
- Explicit authorization.
- Strong validation.

Avoid:

- Fat controllers.
- Static service dependencies.
- Returning EF entities directly.
- Business logic inside controllers.
- Hidden database queries.
- Hard-coded secrets.

## Frontend

Follow:

- Reusable components.
- Clear feature-based organization.
- TanStack Query for server state.
- Zustand only for appropriate client state.
- Accessible components.
- Loading/error/empty states.
- Consistent form validation.
- No duplicated API logic.

## AI

Follow:

- Version prompts.
- Validate outputs.
- Log AI runs.
- Use schemas.
- Avoid unnecessary context.
- Control token usage.
- Never trust AI output blindly.

---

# 28. MVP Scope

The first production-ready MVP should include:

## Product

- User authentication.
- Workspace management.
- Brand profile.
- Audience profile.
- Content strategy.
- Content pillars.
- Content ideas.
- AI content generation.
- AI content evaluation.
- Content editing.
- Content approval.
- Platform variants.
- X connection.
- LinkedIn connection.
- Scheduling.
- Publishing.
- Basic analytics.
- Basic audit logs.

## Infrastructure

- PostgreSQL.
- Redis.
- Background worker.
- Docker development environment.
- CI/CD.
- Logging.
- Error tracking.
- Basic monitoring.

---

# 29. Post-MVP Scope

Do not block MVP development with these features.

Later phases may include:

- Advanced research engine.
- Persistent semantic memory.
- Story bank.
- Advanced RAG.
- Automated trend detection.
- Advanced performance learning.
- Posting-time intelligence.
- More social platforms.
- Advanced notifications.
- Team collaboration features.
- Content approval workflows with multiple stages.
- Advanced analytics.
- Content experiments.
- AI-generated content calendars.
- Automatic strategy evolution.
- Advanced recommendation systems.

---

# 30. Definition of Done

A feature is considered complete only when:

### Backend

- Business logic is implemented.
- Validation exists.
- Authorization exists.
- Database migration exists if required.
- Unit tests exist.
- Integration tests exist where appropriate.
- Logging exists for important failures.

### Frontend

- UI is implemented.
- Loading state exists.
- Empty state exists.
- Error state exists.
- Responsive behavior is handled.
- Accessibility has been considered.
- API errors are handled.

### AI

- Output schema exists.
- Output validation exists.
- Prompt version is recorded.
- AI run is logged.
- Failure behavior is defined.
- Evaluation criteria exist.

### Production features

- Retry behavior is defined.
- Idempotency is considered.
- Observability exists.
- Security has been reviewed.
- Documentation is updated.

---

# 31. Risk Management

## Risk: AI output quality

Mitigation:

- Structured prompts.
- Brand context.
- Audience context.
- Evaluation.
- Human approval.
- Golden dataset.
- Feedback loop.

## Risk: Duplicate publishing

Mitigation:

- Idempotency keys.
- Publication records.
- External platform IDs.
- Worker safeguards.

## Risk: OAuth failures

Mitigation:

- Token refresh.
- Connection status.
- Re-authentication flow.
- Clear user notifications.

## Risk: Platform API changes

Mitigation:

- Platform abstraction.
- Separate adapters.
- Provider-specific tests.
- API version monitoring.

## Risk: AI provider outage

Mitigation:

- Provider abstraction.
- Retry policy.
- Fallback provider where practical.
- Graceful degradation.

## Risk: Excessive AI cost

Mitigation:

- Model routing.
- Token limits.
- Caching.
- Prompt optimization.
- Avoid unnecessary regeneration.
- Usage tracking.

## Risk: Workspace data leakage

Mitigation:

- Centralized authorization.
- Workspace-scoped queries.
- Integration tests.
- Security review.
- Audit logs.

## Risk: Overengineering

Mitigation:

- Modular monolith.
- MVP scope control.
- Vertical slices.
- Delay advanced infrastructure until justified.

---

# 32. Recommended Milestones

## Milestone 1 — Foundation

Deliver:

```text
Repository
Docker
PostgreSQL
Redis
.NET API
Next.js
Design System
CI
```

---

## Milestone 2 — User and Workspace

Deliver:

```text
Authentication
Workspace
Roles
Authorization
Onboarding
```

---

## Milestone 3 — Content Intelligence

Deliver:

```text
Brand
Audience
Strategy
Content Pillars
AI Context
AI Generation
AI Evaluation
```

---

## Milestone 4 — Content Operating System

Deliver:

```text
Ideas
Drafts
Editor
Approval
Platform Variants
Content Calendar
```

---

## Milestone 5 — Publishing

Deliver:

```text
X OAuth
LinkedIn OAuth
Scheduling
Worker
Publishing
Retries
Idempotency
```

---

## Milestone 6 — Intelligence Loop

Deliver:

```text
Analytics
Performance Analysis
Learning Signals
AI Recommendations
```

---

## Milestone 7 — Production Hardening

Deliver:

```text
Security Review
Performance Testing
Monitoring
Error Tracking
Backup Strategy
CI/CD
Production Deployment
```

---

# 33. First Implementation Sprint

The first sprint should focus on making the project executable rather than building visible AI functionality immediately.

## Sprint Goal

A developer should be able to:

```text
Clone Repository
        ↓
Start Docker
        ↓
Start API
        ↓
Start Web
        ↓
Open Application
        ↓
Call API
        ↓
Read/Write PostgreSQL
```

## Sprint Tasks

### Day 1 — Repository

- Initialize Git repository.
- Create monorepo structure.
- Create README.
- Add `.gitignore`.
- Add environment examples.
- Add branch conventions.

### Day 2 — Infrastructure

- Create Docker Compose.
- Add PostgreSQL.
- Add Redis.
- Verify connectivity.

### Day 3 — Backend

- Create ASP.NET Core project.
- Configure DI.
- Configure PostgreSQL.
- Configure EF Core.
- Add health checks.
- Add global exception middleware.

### Day 4 — Database

- Create initial entities.
- Create DbContext.
- Configure relationships.
- Create initial migration.
- Run migration.
- Verify database.

### Day 5 — Frontend

- Create Next.js application.
- Configure Tailwind.
- Configure shadcn/ui.
- Build application shell.
- Connect frontend to API.

### Day 6 — API Contract

- Add `/api/v1`.
- Add standard responses.
- Add error model.
- Add API client.
- Add request handling.

### Day 7 — CI

Configure GitHub Actions for:

```text
Install
↓
Lint
↓
Test
↓
Build
```

---

# 34. Second Implementation Sprint

The second sprint should create the first meaningful user workflow.

## Sprint Goal

A user can:

```text
Register
 ↓
Create Workspace
 ↓
Configure Brand
 ↓
Configure Audience
 ↓
Create Content Pillar
 ↓
Create Content Idea
 ↓
View Idea
```

## Tasks

### Authentication

- Registration.
- Login.
- Refresh token.
- Logout.
- Protected routes.

### Workspace

- Create workspace.
- Get workspace.
- List workspace members.
- Workspace switching.

### Brand

- Create brand profile.
- Update brand profile.
- Read brand profile.

### Audience

- Create audience profile.
- Update audience profile.
- Read audience profile.

### Strategy

- Create content strategy.
- Create content pillars.
- Manage pillar ordering.

### Content

- Create content idea.
- List ideas.
- Update idea.
- Archive idea.

---

# 35. Third Implementation Sprint

The third sprint should introduce the first AI-powered workflow.

## Sprint Goal

A user can:

```text
Open Content Ideas
        ↓
Request AI Opportunities
        ↓
Review Recommendations
        ↓
Generate Draft
        ↓
Evaluate Draft
        ↓
Edit Draft
```

## Tasks

- AI provider abstraction.
- AI configuration.
- Prompt templates.
- Brand context builder.
- Audience context builder.
- Strategy context builder.
- Opportunity generation.
- Opportunity scoring.
- Draft generation.
- Quality evaluation.
- AI run logging.
- Error handling.
- AI generation UI.

This milestone is where the product begins demonstrating its core intelligence.

---

# 36. Vertical Slice Strategy

After the initial foundation, future development should generally follow:

```text
Feature Definition
        ↓
Database
        ↓
Backend API
        ↓
Service Logic
        ↓
AI Logic if Required
        ↓
Frontend UI
        ↓
Tests
        ↓
Observability
        ↓
Documentation
```

Example:

### Scheduling

Do not build only the scheduling database table first.

Instead build:

```text
Schedule UI
+
Schedule API
+
Schedule Service
+
Validation
+
Worker
+
Publication Job
+
Platform Adapter
+
Success/Failure UI
```

This produces a complete feature that can be tested in real usage.

---

# 37. Development Priorities

When deciding what to build next, prioritize:

### Priority 1 — Core user value

Does the feature help the user decide what to post, create content, or publish?

### Priority 2 — Reliability

Can the system perform the operation safely?

### Priority 3 — Intelligence

Can AI make the operation meaningfully better?

### Priority 4 — Learning

Can the system learn from the result?

### Priority 5 — Optimization

Can the system become faster, cheaper, or easier to use?

Avoid spending significant development time on optimization before the underlying workflow is proven.

---

# 38. What Not to Build Too Early

Do not initially build:

- Microservices.
- Complex event sourcing.
- Multiple databases.
- Custom vector infrastructure.
- Custom ML models.
- Complex autonomous agents.
- Dozens of social integrations.
- Advanced recommendation algorithms.
- Overly complex permissions.
- Massive analytics infrastructure.

The product should first prove:

```text
Understand → Recommend → Create → Review → Publish → Measure
```

---

# 39. Release Readiness Checklist

Before the first public release:

## Product

- [ ] Authentication works.
- [ ] Workspace isolation works.
- [ ] Brand configuration works.
- [ ] Audience configuration works.
- [ ] Strategy configuration works.
- [ ] AI generation works.
- [ ] AI evaluation works.
- [ ] Editing works.
- [ ] Approval works.
- [ ] X integration works.
- [ ] LinkedIn integration works.
- [ ] Scheduling works.
- [ ] Publishing works.
- [ ] Analytics works.

## Security

- [ ] Secrets are protected.
- [ ] OAuth tokens are encrypted.
- [ ] Authorization is enforced.
- [ ] Rate limits are active.
- [ ] CORS is restricted.
- [ ] Sensitive data is excluded from logs.

## Reliability

- [ ] Worker retries are tested.
- [ ] Publishing is idempotent.
- [ ] Failed jobs are visible.
- [ ] Health checks work.
- [ ] Database backups are configured.
- [ ] Monitoring is active.

## AI

- [ ] Structured outputs are validated.
- [ ] AI runs are logged.
- [ ] Prompt versions are tracked.
- [ ] Token usage is monitored.
- [ ] Evaluation dataset exists.
- [ ] AI failures degrade gracefully.

## UX

- [ ] Responsive layout works.
- [ ] Empty states exist.
- [ ] Loading states exist.
- [ ] Error states exist.
- [ ] Accessibility has been reviewed.
- [ ] AI-generated content is clearly identified.
- [ ] User edits are never silently overwritten.

---

# 40. Final Development Principle

The Mayorx Content Engine should be developed as a **content operating system with intelligence**, not as a collection of disconnected AI features.

Every major workflow should eventually follow:

```text
UNDERSTAND
    ↓
DECIDE
    ↓
CREATE
    ↓
REVIEW
    ↓
PUBLISH
    ↓
MEASURE
    ↓
LEARN
    ↺
```

The first version does not need to automate everything.

It needs to establish this loop reliably.

Once the core loop works, additional intelligence, platforms, automation, analytics, and personalization can be added without fundamentally changing the architecture.
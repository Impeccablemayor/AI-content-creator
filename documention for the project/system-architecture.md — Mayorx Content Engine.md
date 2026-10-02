# System Architecture

## Mayorx Content Engine

> **A modular AI-powered content management platform that discovers relevant content opportunities, generates platform-specific text, schedules posts, publishes to social platforms, and learns from performance.**

---

# 1. Architecture Goals

The system should be:

- Modular
- Scalable
- Secure
- AI-provider independent
- Platform-independent
- Observable
- Maintainable
- Cost-conscious
- Easy to develop locally
- Easy to deploy
- Ready for future automation

The architecture should support:

```text
Research
    ↓
Strategy
    ↓
Generation
    ↓
Review
    ↓
Scheduling
    ↓
Publishing
    ↓
Analytics
    ↓
Learning
```

---

# 2. Recommended V1 Architecture

V1 should use a:

> **Modular Monolith + Background Workers**

rather than microservices.

Conceptually:

```text
                    ┌──────────────────────┐
                    │      Web App         │
                    │      Frontend        │
                    └──────────┬───────────┘
                               │
                               ↓
                    ┌──────────────────────┐
                    │      Backend API     │
                    │   Modular Monolith   │
                    └──────────┬───────────┘
                               │
          ┌────────────────────┼────────────────────┐
          │                    │                    │
          ↓                    ↓                    ↓
   ┌─────────────┐      ┌─────────────┐      ┌─────────────┐
   │ PostgreSQL  │      │    Redis    │      │ Object      │
   │             │      │             │      │ Storage     │
   └─────────────┘      └─────────────┘      └─────────────┘
                               │
                               ↓
                    ┌──────────────────────┐
                    │   Background Worker  │
                    └──────────┬───────────┘
                               │
         ┌─────────────────────┼─────────────────────┐
         │                     │                     │
         ↓                     ↓                     ↓
    ┌──────────┐         ┌──────────┐         ┌──────────┐
    │ AI Layer │         │ Scheduler│         │ Analytics│
    └──────────┘         └──────────┘         └──────────┘
         │
         ├──────────────┐
         ↓              ↓
    ┌─────────┐    ┌──────────┐
    │   X     │    │ LinkedIn │
    │  API    │    │   API    │
    └─────────┘    └──────────┘
```

---

# 3. Why Modular Monolith?

Microservices would introduce unnecessary complexity at the beginning.

We would immediately need to manage:

- Service discovery
- Multiple deployments
- Distributed tracing
- Inter-service authentication
- Network failures
- Message brokers
- Multiple repositories
- Deployment coordination

For V1, this is unnecessary.

Instead:

```text
One application
+
Clear internal modules
+
Background workers
+
External integrations
```

This gives us clean architecture without premature infrastructure complexity.

---

# 4. High-Level Components

The system consists of:

```text
1. Frontend Application

2. Backend API

3. AI Orchestration Layer

4. Background Worker

5. Scheduler

6. PostgreSQL Database

7. Redis

8. Vector Storage

9. Social Platform Integrations

10. Analytics Engine

11. Notification System

12. Observability Layer
```

---

# 5. Frontend

The frontend is the primary interface for users.

Recommended stack:

```text
Next.js
React
Tailwind CSS
shadcn/ui
TanStack Query
Zustand
```

The frontend should communicate with the backend through REST APIs.

---

# 6. Frontend Responsibilities

The frontend should handle:

- Authentication UI
- Dashboard
- Content calendar
- Content creation
- AI recommendations
- Draft review
- Post editing
- Scheduling
- Social account connections
- Analytics
- Settings
- Brand configuration
- Audience configuration
- Content strategy configuration

The frontend should not contain business-critical AI logic.

---

# 7. Main Frontend Areas

```text
/dashboard

/content

/content/ideas

/content/drafts

/content/calendar

/content/published

/strategy

/analytics

/brand

/audience

/integrations

/settings
```

Exact routing can evolve during implementation.

---

# 8. Backend

The backend is the central application layer.

Recommended initial stack:

```text
.NET 9
ASP.NET Core
Entity Framework Core
PostgreSQL
Redis
Background Workers
```

The backend should expose APIs for:

- Authentication
- Users
- Workspaces
- Brands
- Audiences
- Content
- AI operations
- Scheduling
- Social integrations
- Publishing
- Analytics
- Settings

---

# 9. Backend Architecture

The backend should follow modular architecture.

```text
src/
│
├── Api/
│
├── Application/
│
├── Domain/
│
├── Infrastructure/
│
└── Worker/
```

Within the application:

```text
Modules/
│
├── Identity/
├── Brand/
├── Audience/
├── Content/
├── Strategy/
├── AI/
├── Publishing/
├── Scheduling/
├── Analytics/
└── Integrations/
```

---

# 10. Domain Layer

The domain layer contains core business concepts.

Examples:

```text
User
Workspace
Brand
Audience
ContentPillar
ContentIdea
ContentDraft
ScheduledPost
PublishedPost
SocialAccount
Campaign
Story
ContentPerformance
```

Domain logic should not depend directly on:

- HTTP
- PostgreSQL
- AI providers
- Redis
- External APIs

---

# 11. Application Layer

The application layer orchestrates business operations.

Examples:

```text
GenerateContent
DiscoverIdeas
CreateStrategy
SchedulePost
PublishPost
AnalyzePerformance
ConnectSocialAccount
UpdateBrandVoice
```

It coordinates domain objects and infrastructure.

---

# 12. Infrastructure Layer

Infrastructure handles external systems.

Examples:

```text
PostgreSQL
Redis
AI Providers
X API
LinkedIn API
Email Provider
Object Storage
Vector Database
```

This keeps external dependencies away from the core domain.

---

# 13. Worker System

Background workers are critical.

Some operations should not block an HTTP request.

Examples:

```text
AI generation
Research
Content analysis
Scheduling
Publishing
Analytics synchronization
Embedding generation
Performance analysis
```

Instead:

```text
API Request
    ↓
Create Job
    ↓
Queue
    ↓
Worker
    ↓
Process
    ↓
Save Result
```

---

# 14. Queue Architecture

Redis can initially serve as the queue infrastructure.

Conceptually:

```text
                 Redis
                   │
       ┌───────────┼────────────┐
       ↓           ↓            ↓
   AI Queue   Publish Queue  Analytics Queue
       │           │            │
       ↓           ↓            ↓
    Worker      Worker       Worker
```

Queue technology can later be replaced without changing the domain architecture.

---

# 15. Job Types

Initial job types:

```text
GenerateContentJob
ResearchTopicJob
EvaluateContentJob
GenerateEmbeddingJob
SchedulePostJob
PublishPostJob
SyncAnalyticsJob
AnalyzePerformanceJob
RefreshTokenJob
```

---

# 16. Job Lifecycle

Every job should have a lifecycle.

```text
Pending
   ↓
Processing
   ↓
Completed
```

Failure:

```text
Processing
   ↓
Failed
   ↓
Retry
   ↓
Processing
```

Permanent failure:

```text
Failed
   ↓
Dead Letter / Manual Review
```

---

# 17. Retry Strategy

Retries should use exponential backoff.

Example:

```text
Attempt 1
Immediate

Attempt 2
5 seconds

Attempt 3
30 seconds

Attempt 4
2 minutes
```

Exact values should be configurable.

Retries should not occur for permanent errors such as invalid credentials.

---

# 18. Scheduler

The scheduler determines when posts should be published.

It should support:

- User-selected time
- AI-recommended time
- Timezone
- Platform-specific schedules
- Recurring schedules
- Rescheduling
- Cancellation

The scheduler should create publishing jobs rather than directly publish.

---

# 19. Scheduling Flow

```text
User selects:

Tuesday
08:30
LinkedIn

        ↓

ScheduledPost created

        ↓

Scheduler monitors due posts

        ↓

PublishPostJob created

        ↓

Worker processes job

        ↓

LinkedIn API

        ↓

Published
```

---

# 20. Timezone Handling

All timestamps should be stored in UTC.

Example:

```text
Database:
2026-09-29T07:30:00Z
```

The user's timezone is stored separately.

For a user in Nigeria:

```text
Africa/Lagos
UTC+1
```

The UI converts UTC timestamps into the user's local timezone.

---

# 21. Content Lifecycle

A content item should move through controlled states.

```text
Idea
 ↓
Strategy
 ↓
Draft
 ↓
Review
 ↓
Approved
 ↓
Scheduled
 ↓
Published
```

Alternative:

```text
Draft
 ↓
Rejected
 ↓
Revision
 ↓
Review
```

---

# 22. Content State Machine

Suggested states:

```text
IDEA
DRAFT
IN_REVIEW
APPROVED
SCHEDULED
PUBLISHING
PUBLISHED
FAILED
ARCHIVED
```

State transitions should be validated by the backend.

---

# 23. Content Model

A content record should separate the strategic idea from the final platform post.

Example:

```text
ContentIdea
    ↓
ContentStrategy
    ↓
ContentDraft
    ↓
PlatformVariant
```

This is important for repurposing.

---

# 24. Platform Variants

One idea may produce:

```text
Content Idea
      │
      ├── LinkedIn Version
      │
      └── X Version
```

Each platform version should have its own:

- Text
- Hashtags
- CTA
- Metadata
- Status
- Schedule
- Performance

---

# 25. Social Integrations

The system initially targets:

```text
X
LinkedIn
```

Each platform should be implemented behind a common interface.

Conceptually:

```text
ISocialPublisher
       │
       ├── XPublisher
       └── LinkedInPublisher
```

---

# 26. Social Publisher Interface

Conceptually:

```csharp
public interface ISocialPublisher
{
    Task<PublishResult> PublishAsync(
        SocialPost post,
        CancellationToken cancellationToken);
}
```

The exact implementation will be defined during development.

---

# 27. OAuth Architecture

Users should connect social accounts using OAuth.

Flow:

```text
User
 ↓
Connect LinkedIn
 ↓
Backend generates OAuth URL
 ↓
User authorizes
 ↓
Provider redirects back
 ↓
Backend receives authorization code
 ↓
Exchange code for tokens
 ↓
Encrypt/store tokens
 ↓
Social account connected
```

The same architecture applies to X.

---

# 28. Token Security

Access and refresh tokens are sensitive.

They should:

- Never be returned unnecessarily to the frontend
- Never be logged
- Be encrypted at rest
- Be stored securely
- Be refreshed when necessary
- Be revoked when disconnected

The database should not store raw secrets if encryption is available.

---

# 29. Social Account Model

A social account should contain conceptually:

```text
Id
UserId
Platform
PlatformUserId
Username
AccessTokenEncrypted
RefreshTokenEncrypted
TokenExpiresAt
Scopes
ConnectedAt
LastSyncedAt
Status
```

---

# 30. Publishing Safety

Before publishing:

```text
1. Post exists
2. Post is approved
3. Platform account is connected
4. Token is valid
5. Platform requirements are satisfied
6. Content is not already published
7. Schedule is valid
```

Only then should the worker call the platform API.

**Implemented as follows.** Checks 1, 2, 6, and 7 collapse into one
requirement: only a `Scheduled` post whose time has come is a publish candidate,
and the worker's claim write requires that exact state. Check 3 becomes exactly
one connected account for the post's `(workspace, platform)`, and check 4 covers
both a known-expired token and a token the platform rejects. Check 5 is limited
to the text being non-empty and the platform being configured.

A post that fails any check is not published and is not retried immediately: it
keeps its claim and records a `PublishFailureReason`, so the next attempt waits
for the claim to expire.

---

# 31. Idempotency

Publishing must be idempotent.

If a worker crashes after successfully publishing but before saving the result, it must not publish twice.

The system should use:

```text
Idempotency Key
+
Publishing Status
+
Platform Post ID
```

Example:

```text
publish-job-7f92...
```

**Not implemented.** There is no idempotency key, no publication row, and no
stored platform post id, so a crash between a successful publish and the status
write can leave a post that is published externally but still `Scheduled`
locally. The worker will pick it up again once the claim expires and publish it
a second time.

What is implemented is narrower: the claim write is a single conditional
database update, so two workers cannot publish the same post at the same time,
and every outcome write requires the caller's exact claim, so a worker whose
claim expired cannot overwrite another worker's result. Both bound concurrent
duplication; neither bounds duplication across a crash.

Closing this needs either a stored external post id to reconcile against, or
platform-side idempotency where the provider supports it. Both are deferred.

---

# 32. Analytics Architecture

Analytics should be asynchronous.

```text
Published Post
      ↓
Analytics Worker
      ↓
Platform API
      ↓
Raw Metrics
      ↓
Normalized Metrics
      ↓
Performance Analysis
```

---

# 33. Normalized Metrics

Different platforms use different terminology.

The application should normalize metrics.

Example:

```text
Impressions
Likes
Comments
Shares
Clicks
ProfileVisits
FollowersGained
EngagementRate
```

Platform-specific metrics can still be preserved separately.

---

# 34. Analytics Sync

Analytics synchronization may happen:

```text
Shortly after publication
+
Daily
+
On-demand
```

Example:

```text
Published
 ↓
15 minutes
 ↓
First metrics

24 hours
 ↓
Performance snapshot

7 days
 ↓
Final analysis
```

Exact intervals should remain configurable.

---

# 35. Performance Analysis

The analytics engine should not simply display numbers.

It should also identify patterns.

Example:

```text
Post:
Personal story about debugging

Performance:
High comments
High shares

Analysis:
Personal experience + technical lesson
performed above the user's average.
```

---

# 36. AI Analytics Loop

```text
Published Content
       ↓
Metrics
       ↓
Performance Analysis
       ↓
AI Insight
       ↓
Strategy Update
       ↓
Future Content
```

This creates the system's learning loop.

---

# 37. Database

Primary database:

```text
PostgreSQL
```

PostgreSQL stores:

- Users
- Workspaces
- Brands
- Audiences
- Content
- Strategies
- Stories
- Social accounts
- Schedules
- Jobs
- Analytics
- AI operations
- Feedback

---

# 38. Redis

Redis should initially be used for:

- Job queues
- Caching
- Rate limiting
- Distributed locks
- Temporary state
- AI response caching where appropriate

Redis should not become the primary source of truth.

---

# 39. Vector Storage

The system will eventually require semantic search.

Options include:

```text
PostgreSQL + pgvector
```

or a dedicated vector database.

For V1:

> **PostgreSQL + pgvector is preferred.**

This reduces infrastructure complexity.

---

# 40. Embedding Architecture

Content:

```text
Post
 ↓
Embedding Model
 ↓
Vector
 ↓
PostgreSQL / pgvector
```

When a new idea arrives:

```text
Idea
 ↓
Embedding
 ↓
Similarity Search
 ↓
Related Content
```

---

# 41. Object Storage

V1 is text-only, so object storage is not a major requirement.

However, the architecture should remain compatible with:

- Images
- Videos
- Documents
- Brand assets

Future options:

```text
Cloudinary
S3-compatible storage
```

---

# 42. Notification System

The system should eventually support:

- Email
- In-app notifications
- Push notifications

Initial notification events:

```text
Content generated
Content needs approval
Post scheduled
Post published
Publishing failed
Social account disconnected
AI task failed
```

---

# 43. API Architecture

The backend should expose REST APIs.

Example:

```text
/api/v1/auth
/api/v1/brands
/api/v1/audiences
/api/v1/content
/api/v1/ideas
/api/v1/strategy
/api/v1/ai
/api/v1/social
/api/v1/schedules
/api/v1/analytics
```

API versioning should begin from V1.

---

# 44. API Authentication

Recommended:

```text
JWT / secure session authentication
```

For browser-based authentication, secure HTTP-only cookies should be considered to reduce token exposure.

Exact authentication architecture will be finalized during implementation.

---

# 45. Authorization

The system should support workspace-level authorization.

Initial roles:

```text
Owner
Admin
Editor
Viewer
```

For a single-user V1, this can be simplified while keeping the domain model extensible.

---

# 46. Workspace Architecture

The system should eventually support:

```text
User
  ↓
Workspace
  ├── Brand
  ├── Audience
  ├── Content
  ├── Social Accounts
  └── Team Members
```

This allows the product to evolve from:

> Personal content manager

to:

> Multi-brand social media platform.

---

# 47. Multi-Tenancy

Data should be scoped by:

```text
WorkspaceId
```

rather than relying only on:

```text
UserId
```

This prepares the architecture for teams and agencies.

---

# 48. Security Architecture

Security priorities:

```text
Authentication
Authorization
Encryption
Secret management
Input validation
Rate limiting
Audit logging
OAuth security
AI output validation
```

---

# 49. Secrets Management

Secrets should not be committed to Git.

Examples:

```text
Database connection string
AI API keys
OAuth secrets
Encryption keys
Redis credentials
```

Local development:

```text
.env / user secrets
```

Production:

```text
Managed secret store / platform secrets
```

---

# 50. Rate Limiting

Rate limiting should exist at multiple levels.

### API

Protect public endpoints.

### AI

Prevent accidental excessive AI usage.

### Social APIs

Respect platform limits.

### User

Prevent abuse.

---

# 51. Audit Logging

Important actions should be recorded.

Examples:

```text
Social account connected
Post created
Post approved
Post scheduled
Post published
Post deleted
Brand settings changed
AI strategy generated
```

Audit logs should include:

```text
Actor
Action
Resource
Timestamp
Result
```

---

# 52. Error Handling

The API should return consistent errors.

Example:

```json id="e9j6v2"
{
  "error": {
    "code": "SOCIAL_ACCOUNT_NOT_CONNECTED",
    "message": "Connect your LinkedIn account before publishing."
  }
}
```

Internal errors should not expose sensitive implementation details.

---

# 53. Observability

The system should monitor:

```text
API latency
Worker latency
AI latency
AI cost
Queue depth
Publishing failures
OAuth failures
Database performance
Redis health
External API failures
```

### What exists today

This list is the target. The only observability surfaces currently registered are:

| Capability | Status |
| --- | --- |
| Dependency health for the API host | Implemented. `GET /api/v1/health` runs the PostgreSQL, Redis, and AI-provider configuration checks; `GET /api/v1/health/live` runs none of them. See [System Health](#86-system-health). |
| Per-request log correlation | Implemented. `X-Correlation-Id` is accepted or generated, echoed in the response, set as `HttpContext.TraceIdentifier`, tagged on the current `Activity`, and added to a logging scope. See [Distributed Tracing](#55-distributed-tracing). |
| Metrics collection and export | Not implemented. No metrics package is registered and there is no metrics endpoint. |
| Distributed trace export | Not implemented. No OpenTelemetry or other exporter is registered. |
| Health endpoint on the worker host | Not implemented. The worker runs no HTTP listener. |

Until a metrics pipeline exists, latency and failure-rate monitoring reads log output
and the readiness endpoint, not a metrics backend.

---

# 54. Logging

Logs should be structured.

Example:

```json id="d7xv9b"
{
  "level": "error",
  "service": "publishing-worker",
  "jobId": "job_123",
  "platform": "linkedin",
  "errorCode": "TOKEN_EXPIRED",
  "timestamp": "..."
}
```

Sensitive values must never be logged.

### What exists today

Logs come from the default ASP.NET Core providers. The built-in request logging is
not reconfigured, and no logging framework is registered.

Every log line written during a request carries the values from the
`CorrelationIdMiddleware` scope: `X-Correlation-Id` and `TraceId`. That scope is the
only request-scoped logging enrichment in place; the user ID, workspace ID,
operation, duration, and result fields in the example above are not emitted.

---

# 55. Distributed Tracing

As the system grows, tracing should allow:

```text
API Request
 ↓
AI Job
 ↓
Worker
 ↓
AI Provider
 ↓
Database
```

to be traced through a shared correlation ID.

### What exists today

Only the correlation ID is implemented, and only inside the API host. There is no
trace exporter, no span hierarchy across processes, and no persisted correlation
store.

`CorrelationIdMiddleware` runs first in the API pipeline, before
`GlobalExceptionMiddleware`, and for each request it:

1. Reuses an inbound `X-Correlation-Id` when it is non-empty, at most 128 characters,
   and contains only ASCII letters, digits, `-`, `_`, `.`, or `:`; otherwise
2. Falls back to the current `Activity`'s trace id, and generates a GUID only when no
   ambient activity exists, and then
3. Sets the value as `HttpContext.TraceIdentifier`, as a tag on the current
   `Activity`, in the `X-Correlation-Id` response header, and in a logging scope
   alongside `TraceId`.

Because it reads the ambient activity rather than starting its own, it does not fork
the trace the runtime already maintains. Because it runs first, a failure logged by
the exception handler cites the same identifier the caller was handed.

Deliberately not implemented: propagating the id to the worker or to external
providers, storing it on posts, publications, jobs, or audit entries, and exporting
spans. The worker is a separate host with no HTTP surface, so a request-scoped header
does not reach it.

---

# 56. External API Failure Strategy

External platforms can fail.

Examples:

```text
LinkedIn unavailable
X API timeout
Rate limit
Expired token
Invalid request
```

The application should:

```text
Detect
 ↓
Classify
 ↓
Retry if appropriate
 ↓
Record failure
 ↓
Notify user if necessary
```

---

# 57. Architecture for Current Platform Scope

V1:

```text
X
LinkedIn
```

Future:

```text
Instagram
Facebook
Threads
TikTok
YouTube
```

The social integration interface should make adding platforms possible without rewriting the publishing system.

---

# 58. Environment Architecture

The project should support:

```text
Development
Staging
Production
```

Each environment should have separate:

- Database
- Redis
- OAuth credentials
- AI configuration
- Secrets
- Logging configuration

---

# 59. Local Development

Recommended local setup:

```text
Frontend
localhost:3000

Backend
localhost:5000

PostgreSQL
localhost:5432

Redis
localhost:6379
```

Docker Compose can simplify infrastructure setup.

---

# 60. Deployment Architecture

A practical initial production setup:

```text
                    Internet
                       │
                       ↓
                 ┌────────────┐
                 │   CDN      │
                 └─────┬──────┘
                       │
              ┌────────┴─────────┐
              ↓                  ↓
        Frontend Host       Backend Host
                              │
                    ┌─────────┼──────────┐
                    ↓         ↓          ↓
               PostgreSQL   Redis      Worker
                    │                    │
                    └────────┬───────────┘
                             ↓
                       AI Providers
                             │
                 ┌───────────┴───────────┐
                 ↓                       ↓
                 X                    LinkedIn
```

Exact hosting providers can be selected later.

---

# 61. CI/CD

The repository should use automated CI.

Pipeline:

```text
Push
 ↓
Lint
 ↓
Unit Tests
 ↓
Integration Tests
 ↓
Build
 ↓
Security Checks
 ↓
Deploy
```

Production deployment should require passing checks.

---

# 62. Testing Strategy

Testing should occur at several levels.

### Unit tests

Domain and application logic.

### Integration tests

Database and external service abstractions.

### API tests

HTTP endpoints.

### Worker tests

Background jobs.

### AI evaluation

Prompt/model quality.

### End-to-end

Critical user workflows.

---

# 63. Critical End-to-End Test

The system should eventually test:

```text
Create account
 ↓
Configure brand
 ↓
Connect LinkedIn
 ↓
Ask AI for content
 ↓
Review content
 ↓
Approve
 ↓
Schedule
 ↓
Publish
 ↓
Fetch analytics
 ↓
Display performance
```

---

# 64. Data Flow — Content Discovery

```text
Scheduler
    ↓
Research Job
    ↓
Research Sources
    ↓
Research Results
    ↓
Topic Discovery
    ↓
Opportunity Scoring
    ↓
Content Ideas
    ↓
Dashboard
```

---

# 65. Data Flow — Content Generation

```text
User
 ↓
Select Idea
 ↓
Strategy Engine
 ↓
Context Assembly
 ↓
AI Generator
 ↓
Quality Evaluator
 ↓
Revision if required
 ↓
Draft
```

---

# 66. Data Flow — Publishing

```text
Approved Draft
 ↓
Schedule
 ↓
ScheduledPost
 ↓
Scheduler
 ↓
Publish Job
 ↓
Worker
 ↓
Social Publisher
 ↓
Platform API
 ↓
Platform Post ID
 ↓
PublishedPost
```

---

# 67. Data Flow — Learning

```text
PublishedPost
 ↓
Analytics Worker
 ↓
Platform Metrics
 ↓
Normalized Metrics
 ↓
Performance Analysis
 ↓
AI Insight
 ↓
Learning Signals
 ↓
Strategy Engine
```

---

# 68. Critical Architecture Boundary

The most important boundary in the system is:

```text
AI
```

versus:

```text
Deterministic Application Logic
```

For example:

AI:

> "I recommend publishing this Tuesday at 8:30 AM."

Application:

> "The user approved the schedule. Create the publishing job."

AI:

> "This post is strong."

Application:

> "The post passed validation and is approved."

This separation makes the system predictable.

---

# 69. AI Architecture Boundary

The AI layer should be accessed through an internal interface.

Conceptually:

```csharp
public interface IContentIntelligenceService
{
    Task<ContentStrategyResult> CreateStrategyAsync(...);

    Task<ContentDraftResult> GenerateContentAsync(...);

    Task<ContentEvaluationResult> EvaluateContentAsync(...);
}
```

The rest of the application should not care which AI provider performs the work.

---

# 70. Social Architecture Boundary

Similarly:

```csharp
public interface ISocialPlatform
{
    Task PublishAsync(...);

    Task<MetricsResult> GetMetricsAsync(...);

    Task<AccountInfo> GetAccountAsync(...);
}
```

Implementations:

```text
XPlatform
LinkedInPlatform
```

---

# 71. Scheduler Architecture Boundary

Scheduling should be independent from publishing.

```text
Scheduler
    ↓
Publish Command
    ↓
Publishing System
```

This allows us to later support:

- Multiple platforms
- Recurring posts
- Campaigns
- Optimal-time publishing
- Bulk scheduling

---

# 72. Architecture Evolution

V1:

```text
Modular Monolith
+
Worker
+
PostgreSQL
+
Redis
```

V2:

```text
Modular Monolith
+
Multiple Workers
+
Advanced Queues
```

V3:

```text
Potential Service Extraction

AI Service
Publishing Service
Analytics Service
```

Services should only be extracted when there is a real operational reason.

---

# 73. Scalability Strategy

The first scaling mechanism should be:

```text
Horizontal worker scaling
```

Example:

```text
1 Worker
   ↓
3 Workers
   ↓
10 Workers
```

The API and worker processes should remain stateless where possible.

---

# 74. Database Scaling

Initial:

```text
Single PostgreSQL instance
```

Later:

```text
Read replicas
Connection pooling
Query optimization
Partitioning where necessary
```

Database scaling should be driven by actual usage.

---

# 75. Queue Scaling

Queue consumers should be horizontally scalable.

Example:

```text
AI Queue
 ├── Worker 1
 ├── Worker 2
 └── Worker 3
```

Publishing workers can scale independently.

---

# 76. Caching Strategy

Cache:

```text
Frequently requested settings
Brand context
AI research summaries
AI analysis
Platform metadata
```

Do not cache highly volatile data without a clear invalidation strategy.

---

# 77. Performance Principles

The system should prioritize:

```text
Fast UI
+
Async heavy operations
+
Efficient AI context
+
Database indexing
+
Caching
+
Background processing
```

The user should not stare at a loading screen while a long AI pipeline executes synchronously.

---

# 78. Real-Time Updates

The frontend should eventually receive updates when background tasks complete.

Potential technologies:

```text
Server-Sent Events
WebSockets
SignalR
```

Example:

```text
AI generation started

      ↓

Frontend:
"Generating..."

      ↓

Worker completes

      ↓

Frontend:
"Your content is ready."
```

---

# 79. Notifications vs Real-Time

Real-time updates are for active sessions.

Notifications are for events that occur while the user is away.

Example:

```text
Active:
"In-app content ready notification."

Away:
"Email: Your scheduled post failed."
```

---

# 80. System Security Principles

The architecture should follow:

```text
Least privilege
Defense in depth
Secure defaults
Zero trust between external integrations
Encrypted secrets
Validated inputs
Audited sensitive operations
```

---

# 81. AI Security

AI-specific security risks include:

- Prompt injection
- Malicious external content
- Data leakage
- Tool abuse
- Untrusted research
- Hallucinated actions

External content should never be treated as system instructions.

For example:

```text
Web page content
        ↓
UNTRUSTED DATA
        ↓
Research Context
```

It must not be allowed to override system instructions.

---

# 82. User Data Isolation

One user's:

- Brand
- Stories
- Content
- Analytics
- AI memory

must never appear in another user's AI context.

Every retrieval operation should enforce workspace/user authorization.

---

# 83. Backup Strategy

Critical data should be backed up.

Database backups should support:

```text
Automated backups
Point-in-time recovery
Retention policy
Restore testing
```

The backup strategy should be defined before production launch.

---

# 84. Disaster Recovery

The system should be able to recover from:

- Database failure
- Redis failure
- Worker failure
- AI provider outage
- Social platform outage

The system should degrade gracefully.

For example:

```text
AI provider unavailable

        ↓

Existing drafts remain available

        ↓

Publishing continues for already-approved content
```

---

# 85. Graceful Degradation

The system should not fail completely because one subsystem fails.

Examples:

```text
AI unavailable
→ Existing content/calendar still accessible.

LinkedIn unavailable
→ X publishing can continue.

Analytics unavailable
→ Publishing still works.

Redis unavailable
→ Application enters controlled degraded state.
```

---

# 86. System Health

The application should expose internal health checks.

Example:

```text
/health
/health/database
/health/redis
/health/ai
```

These should be protected appropriately where necessary.

### What exists today

The routes sketched above are not the ones that were built. The API host exposes two
endpoints instead, both anonymous and both under the versioned base path:

| Route | Kind | Dependency checks |
| --- | --- | --- |
| `GET /api/v1/health` | Readiness | `postgres`, `redis`, `ai-provider` |
| `GET /api/v1/health/live` | Liveness | None |

There is deliberately no per-dependency route. A single readiness response names every
dependency and its status, so an orchestrator gets one decision and an operator gets
one place to read, instead of three probes that can disagree.

Implementation:

```text
apps/api/
├── Infrastructure/Health/HealthResponseWriter.cs     shared envelope + status mapping
├── Infrastructure/Health/AiProviderReadinessCheck.cs
├── Infrastructure/Persistence/PostgresHealthCheck.cs
└── Infrastructure/Redis/RedisHealthCheck.cs
```

- `postgres` opens a short-lived connection and runs `SELECT 1`, deliberately not an
  application query. A missing table is a mis-migrated deployment, not a reason for an
  orchestrator to restart a process that is otherwise serving.
- `redis` pings the same `IConnectionMultiplexer` the distributed cache uses, so the
  probe and the OAuth state cache report the same server's state.
- `ai-provider` reads configuration only. A probe that called a model would make every
  readiness poll depend on a third party being reachable and fast, so an unconfigured
  or slow provider is reported as `degraded` instead of failing readiness.

Both endpoints share one response writer so an orchestrator and an API client parse
one envelope. Readiness answers `503` when the aggregate status is `unhealthy` and `200`
otherwise; `degraded` is still a `200`, because an instance missing an optional
capability can serve traffic. The envelope keeps the `data.status` and `data.service`
fields the previous hardcoded controller returned, with lowercase status values, so
existing clients are unaffected; `data.checks` and `data.totalDurationMs` are additive.

`AbortOnConnectFail` is set to `false` on the Redis multiplexer unless the connection
string overrides it. At the library default the first resolve during an outage throws,
which escaped the health check and answered `500` instead of `503`, turning a cache
outage into an error page.

Not implemented: a health endpoint on the worker host, and any protection on these two
routes. They are anonymous by design so an orchestrator or load balancer can poll them
without credentials. They expose no secrets — the AI check reports the provider and
model but never the API key — so the exposure is acceptable, but a deployment that
requires protected probes must put them behind its own gateway.

See section 52, Health and Observability, in [api-spec.md](api-spec.md) for the exact
request and response contract.

---

# 87. Architecture Documentation

The repository should maintain:

```text
/docs
    architecture/
    api/
    ai/
    deployment/
```

The architecture should evolve alongside the code.

---

# 88. Recommended Repository Structure

A potential monorepo:

```text
mayorx-content-engine/
│
├── apps/
│   ├── web/
│   ├── api/
│   └── worker/
│
├── packages/
│   ├── shared/
│   └── contracts/
│
├── docs/
│   ├── architecture/
│   ├── ai/
│   ├── api/
│   └── deployment/
│
├── tests/
│   ├── unit/
│   ├── integration/
│   └── e2e/
│
├── docker/
│
└── README.md
```

The exact structure may change depending on the final frontend/backend repository strategy.

---

# 89. Core Architecture Diagram

The overall system can be summarized as:

```text
                         USER
                          │
                          ↓
                  ┌───────────────┐
                  │   NEXT.JS     │
                  │   WEB APP     │
                  └───────┬───────┘
                          │
                          ↓
                  ┌───────────────┐
                  │ ASP.NET CORE  │
                  │      API      │
                  └───────┬───────┘
                          │
          ┌───────────────┼────────────────┐
          │               │                │
          ↓               ↓                ↓
     PostgreSQL         Redis          AI Layer
          │               │                │
          │               │        ┌───────┴────────┐
          │               │        │                │
          │               │    Research       Generation
          │               │        │                │
          │               │        └───────┬────────┘
          │               │                ↓
          │               │         Quality Evaluation
          │               │
          └───────────────┼────────────────┐
                          ↓                ↓
                     WORKER SYSTEM     SCHEDULER
                          │                │
                          └───────┬────────┘
                                  ↓
                         SOCIAL PUBLISHERS
                            │          │
                            ↓          ↓
                            X       LinkedIn
                            │          │
                            └────┬─────┘
                                 ↓
                             ANALYTICS
                                 │
                                 ↓
                             LEARNING
                                 │
                                 └──────────────→ AI
```

---

# 90. Final Architecture Principle

The system should be built around one fundamental separation:

```text
                    INTELLIGENCE
                         │
                         ↓
                       AI
                         │
                         ↓
                 RECOMMEND / GENERATE
                         │
                         ↓
                    APPLICATION
                         │
                         ↓
                  VALIDATE / CONTROL
                         │
                         ↓
                     EXECUTE
                         │
                         ↓
                    MEASURE
                         │
                         ↓
                      LEARN
                         │
                         └──────────→ AI
```

The AI should make the system **intelligent**.

The application architecture should make the system **reliable**.

Together they create the Mayorx Content Engine.
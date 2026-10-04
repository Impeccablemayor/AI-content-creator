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

### Implementation status

**Implemented, at different routes than the example above.** The API exposes:

| Route | Kind | Behaviour |
| --- | --- | --- |
| `GET /api/v1/health` | Readiness | Runs the `postgres`, `redis`, and `ai-provider` checks. `503` when any is unhealthy, `200` otherwise. |
| `GET /api/v1/health/live` | Liveness | Runs no check. Always `200`. |

Both are anonymous, both answer in the standard response envelope, and there is no
per-dependency route. Readiness keeps the `data.status` and `data.service` fields the
earlier hardcoded controller returned, so an existing client comparing
`data.status == "healthy"` still works.

The AI check reads configuration only and reports `degraded`, not `unhealthy`, when no
provider is configured: a readiness probe must not depend on a third party being
reachable, and local development and CI legitimately run without an API key. `degraded`
still answers `200`, so a missing optional capability does not take an instance out of
rotation.

`X-Correlation-Id` is handled by `CorrelationIdMiddleware`, which runs first in the
pipeline. It is accepted from the request when well formed and generated otherwise,
returned on the response, set as `HttpContext.TraceIdentifier`, tagged on the current
`Activity`, and added to a logging scope. Error bodies already report the same value as
`traceId`.

Not implemented: metrics endpoints, a trace exporter, and a health endpoint on the
worker.

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

## Implementation status

**Status: backend delivered.** The browser auth contract and the workspace
session contract are implemented and covered by integration tests.

| Capability | Status |
| --- | --- |
| Registration, creating the caller's first workspace as `Owner` | Implemented |
| Login, opening a session | Implemented |
| Refresh, with rotation, reuse detection, and workspace binding | Implemented |
| Logout, revoking the session and deleting the cookies | Implemented |
| `GET /users/me` | Implemented |
| `GET /workspaces` — the caller's own memberships only | Implemented |
| `POST /workspaces` — creates, owns, and switches to a new workspace | Implemented |
| `POST /workspaces/{workspaceId}/switch` — membership-checked, reissues the session | Implemented |
| `GET /workspaces/current`, `PATCH /workspaces/current` | Implemented |
| Session in `HttpOnly` cookies, no token in any response body | Implemented |
| Workspace selected by the session's `workspace_id` claim, not a request header | Implemented |
| No `GET /workspaces/{workspaceId}`, `PATCH`, or `DELETE` | Deliberate; see below |
| Password management (forgot/reset), user profile editing | Not implemented |
| Workspace member management endpoints | Not implemented |
| Frontend: `/login`, `/register`, `/onboarding`, workspace switcher | Not started |

### The session is the workspace

There is no workspace id on any request. Login picks the workspace
deterministically (oldest membership, lowest membership id on a tie), embeds it
as a `workspace_id` claim, and every read and write is scoped to that claim.
Switching is the only way to change it, and it checks membership first.

This is why there is no route taking a workspace id: such a route would accept
a workspace name from the caller, and the whole point is that the set of
workspaces a request may touch is decided by who the caller is, not by what
they typed.

### Why cookies

Both credentials are `HttpOnly` cookies and neither appears in a response body.
A token in a body is a token page script can read, so it can be exfiltrated by
any script that runs on the page and survives in `localStorage` where an XSS
read can take it. The `Authorization` header is still accepted for non-browser
clients, and is validated identically.

Defaults assume the web app and API share a site: `SameSite=Lax`, `Secure`
outside Development. A cross-site deployment sets `AuthCookies:SameSite=None`
with `AuthCookies:Secure=true`; the API refuses to start with `None` and
`Secure=false`, because browsers reject that combination silently.

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

The frontend does **not** send a workspace id on requests. The session carries
it: login returns the workspace the session is scoped to, the frontend shows a
switcher built from `GET /workspaces`, and choosing one calls
`POST /workspaces/{workspaceId}/switch` to open a session there.

The frontend also never sees a token. Authentication is two `HttpOnly` cookies,
so credentials cannot be read by page script, stored in `localStorage`, or
attached by hand. A refresh happens by posting `/auth/refresh` with no body and
letting the browser send its cookie.

## Security requirements

Every workspace-scoped endpoint must verify:

1. Authenticated user.
2. Workspace membership.
3. Required role.
4. Resource ownership where applicable.

Membership and role are not re-checked per request because there is no workspace
to check them against: the session already names one, and it was checked when
the session was issued. Resource ownership is checked on every read, so a post
or account belonging to another workspace reads as not found even though the
caller is authenticated.

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

**Status: implemented.** `content_pillars` is a real table with full CRUD at
`/strategy/pillars`, workspace scoped. Reordering is expressed as `weight`, a
percentage in `(0, 100]`, and the list is returned heaviest first, so "reorder"
means "change the weights" rather than storing a separate ordinal. Activate and
deactivate are the `isActive` flag. Names are unique per workspace, compared
case-insensitively.

`ContentStrategy.ContentPillars` still exists and is still free text. It is
not replaced by the new table; nothing reads the two together, and the
migration to drop the free-text column is separate work.

Posting frequency, preferred platforms and CTA preferences remain design
intent, as does the free-text content mix.

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

## Implementation status

**Connection management is implemented. Publishing is not.** The deliverable above
is met; the publishing half of this phase belongs to Phase 10.

The adapter shape actually built is a provider abstraction for the OAuth
exchange, not the `ISocialPlatform` interface sketched above. `PublishAsync` is
deliberately absent from this abstraction, because a connection and a publish
have different lifecycles: a workspace connects an account once and publishes
through it many times. Publishing is a separate abstraction in
`Application/Publishing`, added with Phase 10:

```text
Application/SocialAccounts/
├── ISocialPlatformProvider.cs          provider + resolver contracts
├── OAuthSocialPlatformProvider.cs      shared code exchange, PKCE S256
├── SocialPlatformProviders.cs          X and LinkedIn providers
├── SocialOAuthStateStore.cs            single-use state + PKCE verifier
└── SocialAccountService.cs             connect, callback, list, disconnect
```

| Capability | Status |
| --- | --- |
| OAuth authorization-code flow for X and LinkedIn | Implemented |
| PKCE (`S256`) for X | Implemented |
| Encrypted credential storage at rest | Implemented, separate purposes per token type |
| Credentials never returned by the API | Implemented |
| Credentials never logged | Implemented |
| Connect, reconnect, list, disconnect | Implemented |
| Disconnect destroys stored credentials | Implemented |
| Token refresh | Not implemented, no refresh flow yet |
| Revocation and `Revoked` state detection | Not implemented |
| Platform content validation | Not implemented, belongs to Phase 10 |
| Publishing | Implemented in Phase 10, see below |

The OAuth callback returns a JSON envelope rather than redirecting to a frontend.
It is browser-navigable, so a frontend redirect can be added later without
changing the flow.

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

**Implemented, with two checks collapsed.** The first two, the schedule, and the
last all reduce to a single condition: only a `Scheduled` post whose time has
come is a publish candidate, and the claim write requires that exact state.
There is no separate variant row — a post is already per-platform — so the
variant check does not exist.

"Social account exists" is stricter than the sketch above: exactly **one**
connected account must exist for the post's `(workspace, platform)`. Zero or more
than one is a failure, not a guess. "Token is valid" covers both a known-expired
token and a token the platform rejects, and there is no refresh: an expired token
fails and the account must be reconnected.

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

Implemented as `apps/worker`, which polls, claims due posts with a conditional
update, publishes, and records the outcome under that same claim.

The worker decrypts stored tokens, so it needs the same Data Protection key ring
and application name as the API. If they differ, publishing fails with
`CredentialsUnavailable` for every post. It also needs the same `Social` client
configuration, because a platform with no configuration cannot publish even when
a workspace is connected to it.

## Idempotency

Publishing must be idempotent.

Use:

```http
Idempotency-Key
```

and database-level safeguards to prevent accidental duplicate posts.

**Not implemented, with the safeguards partially in place.** There is no
`Idempotency-Key` and no publication row, so a crash between a successful
publish and the status write can publish twice. What is implemented is the
database-level safeguard: the claim is a single atomic conditional update, and
every outcome write requires the caller's exact claim, so two workers cannot
publish concurrently and a worker whose claim expired cannot overwrite a result.

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

**Not implemented as a policy.** There is no retry table, attempt counter, or
backoff schedule. Retries happen only because a failed publish keeps its claim,
so the existing claim expiry is the retry interval, and it applies uniformly
regardless of whether the failure was recoverable.

The distinction the policy calls for is recorded, though, in
`PublishFailureReason`: `ProviderUnavailable` covers the retryable transient
cases (timeout, rate limit, 5xx) separately from `CredentialsRejected` and
`ProviderResponseInvalid`, which need a reconnect or a content fix. Acting on
that difference is future work.

## Deliverable

An approved post can be scheduled and reliably published to a connected social account.

**Status: delivered**, with the limits above. Scheduling and publishing are
implemented end to end and covered by tests, except that concurrent-claim
atomicity and live platform behaviour are unverified: the test suite cannot
execute the conditional database writes at all, because EF InMemory does not
support `ExecuteUpdateAsync`, and no live LinkedIn or X credentials are
available. See [The in-memory provider cannot execute the conditional write
paths] in `tests/unit/ScheduledPostProcessorTests.cs`.

---

# 16. Phase 11 — Analytics

## Objective

Capture what happens after publication.

## Implementation status

**The read surface and the capture half are both implemented.** The endpoints,
the `content_post_metrics` table, the metric normalization rules, the workspace
scoping and the `AnalyticsSync` worker that populates the table are built and
tested. A metric reports `null` only when the platform does not expose it or has
not reported it yet.

| Capability | Status |
|---|---|
| `GET /analytics/overview` | Implemented |
| `GET /analytics/posts` | Implemented |
| `GET /analytics/posts/{contentPostId}` | Implemented |
| `platform`, `from` and `to` filters | Implemented |
| `pillarId` filter | Implemented, added with the `content_pillars` table |
| Workspace scoping | Implemented, enforced by a composite foreign key |
| Published-only counting | Implemented |
| Latest-snapshot-per-post selection | Implemented |
| `metric unavailable` / `= 0` / `not yet synchronized` kept distinct | Implemented via nullable columns and an attempt timestamp |
| `AnalyticsSyncRequested` job and metric-fetching worker | Implemented, as a poll loop in `apps/worker/AnalyticsSyncWorkerService.cs` |
| Storing the platform's post id so metrics can be fetched later | Implemented, `content_posts.published_external_post_id` |
| Trend, platform-comparison and pillar breakdowns | Not implemented. The `pillarId` filter scopes existing totals to one pillar; it is not a grouped per-pillar comparison. |

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

**Implemented.** Each metric is a nullable column, and the three states below
survive a round trip through the API unchanged.

```text
Metric unavailable        → null
Metric = 0                → 0
Metric not yet synchronized → null, with recordedAt also null
```

The last one is now distinguishable from the other two. A successful read always
writes a snapshot row, even when every value is `null`, so a non-null
`recordedAt` with null metrics means "measured, platform reported nothing",
while a null `recordedAt` means "no snapshot yet". A failed read writes no
snapshot at all, so it never looks like a measurement of zero.

`EngagementRate` is derived on read rather than stored, so a workspace-level
rate can never disagree with its own inputs:

```text
(likes + comments + shares) / impressions * 100
```

Clicks, profile visits and followers are excluded. It is `null` when
impressions are `null` or `0`.

## Analytics synchronization

Create background jobs for:

```text
AnalyticsSyncRequested
```

The worker fetches platform metrics and updates:

```text
publication_metrics
```

**Implemented.** `AnalyticsSyncProcessor` scans published posts whose
`metrics_last_attempted_at` is null or older than `AnalyticsSync:ResyncIntervalHours`,
reads their metrics through the same connected account that published them, and
inserts a `content_post_metrics` snapshot. The worker polls on
`AnalyticsSync:PollIntervalSeconds` with a batch of `AnalyticsSync:BatchSize`.

Fetching a post's metrics requires the id the platform assigned it at publish
time, so `content_posts.published_external_post_id` is written by the same
conditional update that marks a post `Published`. Posts published before that
column existed are never back-filled, because the platform ids cannot be
recovered after the fact.

The attempt timestamp is recorded before the platform is called, so a provider
outage, a misconfiguration and a permanent "no data" answer all wait for the next
resync interval instead of being retried on every poll. A post is deliberately
not claimed: two overlapping workers could each write a snapshot, and the newest
snapshot wins on read, so the duplicate is wasted work rather than wrong data.

Platform coverage:

- **X** — one request per post (`tweet.fields=public_metrics,non_public_metrics`)
  returns impressions, likes, comments, shares and clicks. Private metrics need a
  user-context token, which the connection flow already stores. Shares counts
  reposts and quotes together because there is one shares column. X exposes no
  per-post follower gain, so that metric stays null.
- **LinkedIn** — `memberCreatorPostAnalytics` exposes one metric per request, so
  a post costs seven calls. These metrics require LinkedIn's Community Management
  API product, which is not part of the default connection scopes. Without it the
  request is rejected and surfaces as a provider failure, leaving the post's
  metrics empty rather than inventing zeros. Enabling it is a product-scope
  decision, not a code change.

The table this job writes is named `content_post_metrics`, keyed by
`ContentPostId` rather than `PublicationId`, since there is no publication
entity.

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

**Partially available.** Total published posts, engagement, reach and
per-post performance are readable today, and metric ingestion now populates them.
Platform comparison, content pillar performance and recent trends are not built.
LinkedIn figures additionally require the Community Management API product; X
figures work with the existing connection scopes.

## Deliverable

Users can see measurable performance of published content.

**Status: delivered**, with the platform-coverage caveat above. The read surface,
the metric capture pipeline and the published-post identifier are implemented end
to end and covered by tests. Live platform behaviour is unverified: no LinkedIn
or X credentials are available in this environment, and the conditional database
writes are verified against PostgreSQL rather than by the unit suite.

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

### What is built

One dimension is compared per finding, and a finding is only produced when the
evidence supports it:

```text
PLATFORM_ENGAGEMENT
CONTENT_TYPE_ENGAGEMENT
CONTENT_PILLAR_ENGAGEMENT
POSTING_DAY_ENGAGEMENT
```

The rest of the list above is the target vocabulary, not delivered behaviour.
Topic, hook, CTA, and storytelling comparisons need text and structure the system
does not measure yet, and a signal derived from a proxy would be stored and read
back as though it were a finding. They stay unwired until there is something real
behind them.

### How a signal is decided

The system says less than it could, on purpose:

- only posts with a real metric snapshot count as measured; a post nobody has
  read metrics for contributes no evidence, and is reported in the run's counts
  rather than quietly dropped;
- a group needs at least two posts and a derivable engagement rate, so a group of
  one is never compared and a group with no reported impressions is never ranked
  as zero;
- a dimension is reported only when its strongest group clears a configured
  margin over its weakest, so a two-percent difference costs no AI call;
- the observation sentence is written by code from the metric totals, so it can
  only state what was measured, and the model's answer is stored beside it as an
  inference rather than merged into it;
- nothing claims a cause. Posts differ in many ways at once, so a comparison is a
  correlation, and the prompt tells the model to recommend something to try
  rather than to assert a cause.

The last point is the operational form of "the system should not automatically
make major strategic changes from a single post": nothing here changes strategy
at all. Signals are recorded and readable, and no generation path consumes them
yet.

### Insufficient data is a result, not an error

A workspace with too little measured history gets a completed run that reports
`hasSufficientData: false`, the counts that produced that answer, and an empty
list. No AI call is made and no row is written. Returning a fabricated finding
for two posts would be the failure mode this threshold exists to prevent.

### The run is triggered by a request, not the worker

`POST /api/v1/learning/insights` runs the loop; `GET` reads the results. A run
makes AI calls, and every other AI operation in the system is request-triggered.
An AI run row is also recorded against the workspace of the request that caused
it, and the worker has no request and therefore no workspace to record against.
Running it in the worker would mean inventing a workspace-explicit run recorder
and a background AI configuration to serve a loop the publisher asks for anyway.

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

Status: the first half is delivered and the second is not. Performance data is
analysed and stored as learning signals, and the signals are readable. No
recommendation path consumes them yet, so a measured difference does not yet
change what gets generated. That is the remaining work in this phase, and it is
deliberately separate: wiring generation to weak signals would let a handful of
posts quietly steer output, which is the failure this phase is meant to avoid.

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

## Implementation status

**Health monitoring for the API host and metrics collection and export are
implemented. A worker health endpoint, distributed tracing and an error tracking
service are not.**

| Capability | Status |
| --- | --- |
| `GET /api/v1/health` reporting PostgreSQL, Redis, and AI provider state | Implemented |
| `GET /api/v1/health/live` as a dependency-free probe | Implemented |
| `X-Correlation-Id` accepted or generated, echoed, and propagated to logs | Implemented |
| Request ID and trace ID in log output | Implemented via the correlation middleware's logging scope |
| User ID, workspace ID, operation, duration, and result in log output | Implemented by `RequestLoggingMiddleware`, one line per request, after authentication and before authorization |
| Metrics for API latency and error rate | Implemented as `mayorx_http_server_requests_total` and `mayorx_http_server_request_duration_seconds`, labelled by method, status class and route template |
| Metrics for AI latency and failure rate | Implemented as `mayorx_ai_operations_total` and `mayorx_ai_operation_duration_seconds`, labelled by provider, run type and outcome |
| Metrics for publication success and failure rate | Implemented as `mayorx_publish_attempts_total`, labelled by platform, outcome and reason, one per candidate rather than per run |
| Metrics for worker failures | Implemented as `mayorx_worker_poll_runs_total`, labelled by operation and outcome |
| Metrics for analytics sync failures | Implemented as `mayorx_analytics_sync_attempts_total`, plus `mayorx_external_provider_failures_total` for the provider failures behind them |
| Metrics for AI token usage and AI cost | Not implemented. The AI provider interface returns generated text only, so there is no measured usage to report and a series would have to be guessed |
| Metrics for queue depth | Not implemented. Nothing in the codebase holds a queue whose length can be read |
| Social platform failure classification in metrics | Implemented as `mayorx_external_provider_failures_total`, with `failure_class` distinguishing authentication, unavailable and request failures |
| Worker and social integration health endpoints | Not implemented. Only the API host exposes health endpoints. |
| AI provider monitoring | Configuration only. Readiness reports whether a provider and model are configured, and makes no call to the provider. |

### How the metrics are collected

`System.Diagnostics.Metrics`, with no third-party exporter. One meter,
`Mayorx.ContentEngine`, declares every instrument in `ApplicationMetrics`, which is
the only supported way to record them. `PrometheusMetricsCollector` holds one
`MeterListener` for the process lifetime and renders the snapshot as Prometheus text
exposition at `GET /api/v1/metrics`, content type
`text/plain; version=0.0.4; charset=utf-8`. `Metrics:Enabled` defaults to `true`;
`false` removes both the route and the listener, so nothing is collected for an
endpoint nobody reads.

Three decisions are worth stating because they are not the obvious ones.

**Counters are `Counter<long>` and histograms are `Histogram<double>`.** The BCL's
`Instrument` does not report which kind it is, so the collector tells them apart by
the value type it is handed. `mayorx_scheduled_post_scan_due_posts` is a histogram
even though its value is a count, for exactly this reason.

**The collector is resolved while the host is built, not on the first scrape.** A
listener only reports measurements made after it starts, so a lazily created
collector would lose everything the process did before the first scrape.

**Every label is bounded at the recording site.** `RecordPublishAttempt` takes a
`ContentPlatform`, a `PublishOutcome` and a nullable `PublishFailureReason`; there is
no parameter that could accept a workspace id, a content post id, a path or an
exception message. Provider failures are counted inside the one function that
classifies them, so a new provider path cannot be added and left uncounted.

The `Never log` list above holds for the implemented logging, and the same
constraint is what keeps the scrape free of secrets: the AI readiness check reports
the provider and model and never the API key, the correlation header is validated
before it reaches a log line or a response header, and the request log carries the
method, the route template, the status, the duration and the caller's ids, never a
path, a query string, a body, a header or a token.

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

### Implementation status

A separate integration test project now exists alongside the unit tests. It hosts the
real composition root through `WebApplicationFactory<Program>` against the
repository's own PostgreSQL and Redis from `docker-compose.yml`.

Each run creates its own database, applies the real migrations, and drops it
afterwards, so a run cannot collide with a developer's data and the schema under test is
the schema a deployment applies. The database is never mocked, because the database is
the part under test.

Only the two edges that need the internet are substituted: the social platform and the
model. The scheduler, publish service, credential protector, metrics store, reporting
service, analyser, and persistence are the production types.

| Coverage | Status |
| --- | --- |
| API + PostgreSQL, real migrations | Implemented |
| API + Redis | Implemented |
| Publishing loop: scheduled post through publish, platform identity, metrics ingestion, reporting, persisted learning signal | Implemented |
| Workspace isolation and direct read across the boundary by identifier | Implemented |
| Readiness healthy, and readiness unhealthy with both dependencies unreachable | Implemented |
| Liveness independent of dependency state | Implemented |
| `X-Correlation-Id` generated and echoed | Implemented |
| Unauthenticated request rejected, still correlatable | Implemented |
| Authentication and authorization success paths | Not covered by this project; covered by the unit test project |

CI starts PostgreSQL and Redis with the project's own compose file before running these
tests, and tears them down afterwards.

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

`PerformanceAnalysisJob` and `LearningSignalJob` are not built, and the list
above is the target rather than the current state. The worker owns jobs that must
run whether or not anyone asks, and the learning loop does not: it is triggered by
`POST /api/v1/learning/insights`, because a run makes AI calls and its AI run row
is recorded against the workspace of the request that caused it — neither of which
a poll has. A scheduled learning job becomes worth building when signals need to
appear without being asked for; at that point it needs a workspace-explicit run
recorder, which is the real work, not the scheduling.

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

### Implementation status

| Deliverable | Status |
| --- | --- |
| Monitoring | Delivered. Dependency health, per-request log correlation, one request log line per request, and Prometheus metrics collection with an anonymous scrape endpoint all exist. A worker health endpoint, dashboards and alerts do not. |
| Error Tracking | Not started. Errors are logged and returned in the standard error envelope; nothing is forwarded to a tracking service. |
| CI/CD | Delivered. A workflow restores, builds with warnings as errors, runs the unit tests, then starts PostgreSQL and Redis with the project's compose file and runs the integration tests. |
| Security Review | Not started |
| Performance Testing | Not started |
| Backup Strategy | Not started |
| Production Deployment | Not started |

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
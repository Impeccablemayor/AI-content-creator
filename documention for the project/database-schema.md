# Mayorx Content Engine — Database Schema

## 1. Overview

The Mayorx Content Engine uses **PostgreSQL** as its primary persistent database.

The schema is designed around:

- Multi-workspace architecture
- Brand and audience intelligence
- Content strategy and planning
- AI-generated content
- Human review and approval
- Social account connections
- Scheduling and publishing
- Post analytics
- AI learning and feedback
- Research and evidence
- Auditability
- AI usage and cost tracking

### Primary database technologies

- PostgreSQL
- Entity Framework Core
- pgvector
- Redis for temporary state, queues, locks, caching, and rate limiting

PostgreSQL remains the source of truth for business data.

Redis must not be treated as the permanent source of truth for content, publishing history, analytics, or user configuration.

---

# 2. Database Principles

## 2.1 Multi-tenancy

All workspace-owned entities must be scoped through:

```text
WorkspaceId
```

A user may belong to one or multiple workspaces.

Workspace data must never be accessible across workspace boundaries.

---

## 2.2 UUID Primary Keys

All major entities use UUIDs.

Example:

```text
Id UUID PRIMARY KEY
```

UUIDs are preferred because:

- They work well across distributed workers.
- They avoid predictable sequential IDs.
- They simplify future scaling.
- They are suitable for external references.

---

## 2.3 Timestamps

All timestamps are stored in UTC.

Standard fields:

```text
CreatedAt
UpdatedAt
```

Where applicable:

```text
DeletedAt
PublishedAt
ScheduledAt
CompletedAt
```

User/workspace timezone preferences are stored separately.

---

## 2.4 Soft Deletion

Soft deletion should be used for important user-owned entities where recovery or historical integrity matters.

Example:

```text
DeletedAt TIMESTAMPTZ NULL
```

Not every table needs soft deletion.

Logs, analytics, publishing attempts, and audit records should generally remain immutable.

---

# 3. High-Level Entity Relationship

```text
User
 │
 ├──< WorkspaceMember >── Workspace
 │                            │
 │                            ├── BrandProfile
 │                            ├── AudienceProfile
 │                            ├── ContentStrategy
 │                            ├── ContentPillar
 │                            ├── ContentIdea
 │                            ├── ContentDraft
 │                            ├── ContentVariant
 │                            ├── SocialAccount
 │                            ├── Schedule
 │                            ├── Publication
 │                            ├── Analytics
 │                            ├── ResearchSource
 │                            ├── Story
 │                            ├── Feedback
 │                            ├── LearningSignal
 │                            └── AuditLog
 │
 └──< AI Runs / Feedback / Notifications
```

---

# 4. Core Identity Tables

## 4.1 Users

Stores application users.

### Table

```text
users
```

### Fields

| Field | Type | Description |
|---|---|---|
| Id | UUID | Primary key |
| Email | VARCHAR(320) | User email |
| PasswordHash | TEXT | Password hash if local authentication is used |
| FirstName | VARCHAR(100) | First name |
| LastName | VARCHAR(100) | Last name |
| AvatarUrl | TEXT | Profile image |
| Timezone | VARCHAR(100) | User timezone |
| IsActive | BOOLEAN | Account status |
| EmailVerified | BOOLEAN | Email verification state |
| LastLoginAt | TIMESTAMPTZ | Last login |
| CreatedAt | TIMESTAMPTZ | Creation time |
| UpdatedAt | TIMESTAMPTZ | Last update |
| DeletedAt | TIMESTAMPTZ | Soft deletion |

### Constraints

```text
Email UNIQUE
```

Emails should be normalized before storage.

---

# 5. Workspace Tables

## 5.1 Workspaces

Represents a brand, business, creator, or organization using the platform.

### Table

```text
workspaces
```

### Fields

| Field | Type | Description |
|---|---|---|
| Id | UUID | Primary key |
| Name | VARCHAR(200) | Workspace name |
| Slug | VARCHAR(100) | URL-safe identifier |
| Description | TEXT | Workspace description |
| Timezone | VARCHAR(100) | Default timezone |
| DefaultLanguage | VARCHAR(20) | Default content language |
| IsActive | BOOLEAN | Workspace status |
| CreatedAt | TIMESTAMPTZ | Creation time |
| UpdatedAt | TIMESTAMPTZ | Last update |
| DeletedAt | TIMESTAMPTZ | Soft deletion |

### Constraints

```text
Slug UNIQUE
```

---

## 5.2 Workspace Members

Connects users to workspaces.

### Table

```text
workspace_members
```

### Fields

| Field | Type |
|---|---|
| Id | UUID |
| WorkspaceId | UUID |
| UserId | UUID |
| Role | VARCHAR(30) |
| JoinedAt | TIMESTAMPTZ |

### Roles

```text
OWNER
ADMIN
EDITOR
VIEWER
```

### Constraints

```text
UNIQUE(WorkspaceId, UserId)
```

### Relationships

```text
Workspace 1 ───< WorkspaceMember >─── 1 User
```

---

# 6. Brand Intelligence

## 6.1 Brand Profiles

Stores structured brand intelligence used by the AI.

### Table

```text
brand_profiles
```

### Fields

| Field | Type |
|---|---|
| Id | UUID |
| WorkspaceId | UUID |
| BrandName | VARCHAR(200) |
| Industry | VARCHAR(150) |
| Description | TEXT |
| Mission | TEXT |
| Positioning | TEXT |
| ExpertiseAreas | JSONB |
| CoreValues | JSONB |
| BrandPersonality | JSONB |
| VoiceProfile | JSONB |
| ContentBoundaries | JSONB |
| PreferredTopics | JSONB |
| AvoidedTopics | JSONB |
| CTAStyle | VARCHAR(100) |
| EmojiUsage | VARCHAR(30) |
| TechnicalDepth | VARCHAR(30) |
| Formality | VARCHAR(30) |
| HumorLevel | VARCHAR(30) |
| IsActive | BOOLEAN |
| CreatedAt | TIMESTAMPTZ |
| UpdatedAt | TIMESTAMPTZ |

A workspace may have multiple historical brand profiles, but only one active profile should normally be used for generation.

---

# 7. Audience Intelligence

## 7.1 Audience Profiles

Defines who the content is intended to reach.

### Table

```text
audience_profiles
```

### Fields

| Field | Type |
|---|---|
| Id | UUID |
| WorkspaceId | UUID |
| Name | VARCHAR(150) |
| Description | TEXT |
| Demographics | JSONB |
| Roles | JSONB |
| Industries | JSONB |
| Interests | JSONB |
| PainPoints | JSONB |
| Goals | JSONB |
| KnowledgeLevel | VARCHAR(50) |
| BuyingStage | VARCHAR(50) |
| PreferredContentTypes | JSONB |
| CommonQuestions | JSONB |
| Objections | JSONB |
| Active | BOOLEAN |
| CreatedAt | TIMESTAMPTZ |
| UpdatedAt | TIMESTAMPTZ |

A workspace may define multiple audience segments.

---

# 8. Content Strategy

## 8.1 Content Strategies

Stores the strategic direction of content.

### Table

```text
content_strategies
```

### Fields

| Field | Type |
|---|---|
| Id | UUID |
| WorkspaceId | UUID |
| Name | VARCHAR(150) |
| Description | TEXT |
| PrimaryGoal | VARCHAR(50) |
| SecondaryGoals | JSONB |
| PostingFrequency | INTEGER |
| StrategyData | JSONB |
| StartDate | DATE |
| EndDate | DATE |
| IsActive | BOOLEAN |
| CreatedAt | TIMESTAMPTZ |
| UpdatedAt | TIMESTAMPTZ |

### Example goals

```text
AUTHORITY
AWARENESS
ENGAGEMENT
LEADS
EDUCATION
COMMUNITY
PRODUCT_AWARENESS
PERSONAL_BRAND
```

---

## 8.2 Content Pillars

Defines recurring content themes.

### Table

```text
content_pillars
```

### Fields

| Field | Type |
|---|---|
| Id | UUID |
| WorkspaceId | UUID |
| Name | VARCHAR(150) |
| Description | TEXT |
| Objective | TEXT |
| TargetAudienceId | UUID |
| Weight | DECIMAL(5,2) |
| IsActive | BOOLEAN |
| CreatedAt | TIMESTAMPTZ |
| UpdatedAt | TIMESTAMPTZ |

`Weight` determines how frequently a pillar should appear relative to others.
It is constrained to the range `(0, 100]` by the service, which is the only
thing that writes it.

`Name` is unique per workspace (`IX_ContentPillars_WorkspaceId_Name`). The
service compares case-insensitively, and the unique index is the final
guarantee, so two pillars in one workspace can never both be called
"Education" regardless of case.

The table also carries an alternate key `AK_ContentPillars_WorkspaceId_Id`,
which exists so `content_ideas` can reference a pillar by
`(WorkspaceId, ContentPillarId)`. That makes a cross-workspace attribution
impossible at the database level, not merely in application code.

### Deleting a pillar

`content_ideas.ContentPillarId` is nullable and its foreign key is
`ON DELETE RESTRICT`. Deleting a pillar must never delete the ideas built from
it, so `DELETE /api/v1/strategy/pillars/{pillarId}` runs in a single
transaction that first sets `ContentPillarId` to `NULL` on the affected ideas
and then deletes the pillar. The idea survives and simply becomes
unattributed.

`SetNull` is not used here. Because the foreign key is composite, `SET NULL`
would also try to set the non-nullable `WorkspaceId` column to `NULL` and
abort the whole delete with a not-null violation. `Restrict` plus the
explicit detach step gives the same outcome, and additionally makes any
future code path that deletes a pillar without detaching fail loudly at the
database instead of silently orphaning content.

---

# 9. Content Ideas

## 9.1 Content Ideas

Represents ideas before they become drafts.

### Table

```text
content_ideas
```

### Fields

| Field | Type |
|---|---|
| Id | UUID |
| WorkspaceId | UUID |
| ContentPillarId | UUID |
| AudienceProfileId | UUID |
| Title | VARCHAR(300) |
| Description | TEXT |
| SourceType | VARCHAR(50) |
| SourceReference | TEXT |
| OpportunityScore | DECIMAL(5,2) |
| TimelinessScore | DECIMAL(5,2) |
| AudienceFitScore | DECIMAL(5,2) |
| AuthorityFitScore | DECIMAL(5,2) |
| OriginalityScore | DECIMAL(5,2) |
| DiscussionPotentialScore | DECIMAL(5,2) |
| Status | VARCHAR(30) |
| CreatedBy | UUID |
| CreatedAt | TIMESTAMPTZ |
| UpdatedAt | TIMESTAMPTZ |

### Status

```text
IDEA
SELECTED
REJECTED
CONVERTED
ARCHIVED
```

---

# 10. Stories

## 10.1 Story Bank

Stores reusable experiences, lessons, observations, examples, and narratives.

### Table

```text
stories
```

### Fields

| Field | Type |
|---|---|
| Id | UUID |
| WorkspaceId | UUID |
| Title | VARCHAR(300) |
| Summary | TEXT |
| StoryContent | TEXT |
| StoryType | VARCHAR(50) |
| Lesson | TEXT |
| ApplicableTopics | JSONB |
| ApplicableAudiences | JSONB |
| EmotionalTone | VARCHAR(50) |
| AuthenticityLevel | VARCHAR(30) |
| UsageCount | INTEGER |
| LastUsedAt | TIMESTAMPTZ |
| CreatedAt | TIMESTAMPTZ |
| UpdatedAt | TIMESTAMPTZ |

### Story types

```text
PERSONAL
CASE_STUDY
OBSERVATION
MISTAKE
LESSON
CLIENT_RESULT
BUILD_IN_PUBLIC
EXPERIENCE
```

---

# 11. Content Drafts

## 11.1 Content Drafts

Represents the main content object.

### Table

```text
content_drafts
```

### Fields

| Field | Type |
|---|---|
| Id | UUID |
| WorkspaceId | UUID |
| ContentIdeaId | UUID |
| StrategyId | UUID |
| ContentPillarId | UUID |
| AudienceProfileId | UUID |
| StoryId | UUID |
| Title | VARCHAR(300) |
| CoreMessage | TEXT |
| Hook | TEXT |
| Body | TEXT |
| CTA | TEXT |
| Status | VARCHAR(30) |
| Source | VARCHAR(30) |
| CreatedBy | UUID |
| ApprovedBy | UUID |
| ApprovedAt | TIMESTAMPTZ |
| CreatedAt | TIMESTAMPTZ |
| UpdatedAt | TIMESTAMPTZ |
| DeletedAt | TIMESTAMPTZ |

### Source values

```text
AI
USER
AI_EDITED
IMPORTED
```

### Lifecycle

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

## 11.2 Platform-Specific Content Posts

The current API persists each generated platform post as a draft linked to its
source idea and workspace. This first implementation supports LinkedIn and X;
scheduling and publishing remain later lifecycle steps.

### Table

```text
content_posts
```

### Fields

| Field | Type | Description |
|---|---|---|
| Id | UUID | Primary key |
| WorkspaceId | UUID | Owning workspace |
| ContentIdeaId | UUID | Approved source idea |
| Platform | VARCHAR(50) | LinkedIn or X |
| Status | VARCHAR(50) | Draft, Approved, Scheduled, Published, or Rejected |
| Content | VARCHAR(3000) | Complete platform-specific post text |
| Hook | VARCHAR(300) | Opening hook |
| CallToAction | VARCHAR(300) | Optional CTA |
| Hashtags | TEXT[] | Normalized, relevant hashtags |
| ContentType | VARCHAR(50) | Educational, Story, Opinion, HowTo, CaseStudy, Insight, or Question |
| StorytellingStructure | VARCHAR(300) | Concise applied structure |
| ScheduledAt | TIMESTAMPTZ | Intended publication time; set when a post is scheduled |
| PublishClaimedAt | TIMESTAMPTZ | When a worker claimed the post for publishing; internal lease |
| PublishedAt | TIMESTAMPTZ | When the platform accepted the post; set only on success |
| PublishedExternalPostId | VARCHAR(255) | The platform's own id for the published post; `NULL` until published |
| MetricsLastAttemptedAt | TIMESTAMPTZ | When metrics were last requested for this post, successful or not |
| PublishFailureReason | VARCHAR(50) | Why the last attempt failed; `NULL` when none |
| CreatedAt | TIMESTAMPTZ | Creation time |
| UpdatedAt | TIMESTAMPTZ | Last update |

### Constraints

```text
UNIQUE (ContentIdeaId, Platform)
FOREIGN KEY (WorkspaceId, ContentIdeaId) REFERENCES content_ideas (WorkspaceId, Id)
INDEX (WorkspaceId, Status, CreatedAt)
INDEX (Status, ScheduledAt)
INDEX (Status, MetricsLastAttemptedAt)
```

Generating an existing draft for the same idea and platform returns it without
another AI request. The composite foreign key ensures the post and source idea
belong to the same workspace.

`ScheduledAt` is set when an approved post is scheduled and is what the worker
scans. `PublishClaimedAt` is worker bookkeeping: it records a lease so two
workers cannot pick up the same due post, and an expired claim becomes claimable
again so a crashed worker cannot strand a post. The same lease is the retry
backoff, because a failed publish keeps its claim.

`PublishedAt` and `PublishFailureReason` are set only by the worker, and only
through a conditional update that requires the caller's exact claim. A worker
whose claim has expired can therefore never write a result, which is what stops
two workers from both recording an outcome for the same post.

There is no `publications` table. A publish leaves no per-attempt row: the
outcome is the two columns above, and the platform's own post id is stored on
`PublishedExternalPostId` so the same post can be asked about later. Recording
history per attempt, and the `Idempotency-Key` handling that would make retries
safe, is deferred.

`PublishedExternalPostId` is written by the same conditional update that sets
`Status = 'Published'`, so it can never be recorded for a post this worker no
longer owns. Posts published before the column existed keep it `NULL` and are
never back-filled, because the platform ids cannot be recovered after the fact;
they are simply never measured.

On success the worker sets `Status = 'Published'`, `PublishedAt` and
`PublishedExternalPostId`, and clears `PublishClaimedAt` and
`PublishFailureReason`. On failure it changes nothing except
`PublishFailureReason`: the post stays `Scheduled` with its claim intact, which
is the only signal that explains a post that is overdue but not published.

`PublishFailureReason` is stored as a string, not an integer, so the stored value
stays readable and the API can return it as-is:

```text
PlatformNotSupported, PlatformNotConfigured, SocialAccountNotFound,
SocialAccountNotConnected, SocialAccountAmbiguous, CredentialsUnavailable,
CredentialsExpired, CredentialsRejected, ProviderUnavailable,
ProviderResponseInvalid, PublishClaimLost, ContentPostNotScheduled
```

A `content_post` has no `SocialAccountId`. The account is resolved at publish
time from `(WorkspaceId, Platform)`, and a workspace with anything other than
exactly one connected account fails the attempt rather than choosing one. This
keeps the post row independent of connection lifecycle, but it also means a
workspace with two connected LinkedIn accounts cannot publish until one is
disconnected. Storing the intended account per post is deferred.

---

# 12. Platform Content Variants

A single content draft can produce different platform-specific versions.

## 12.1 Content Variants

### Table

```text
content_variants
```

### Fields

| Field | Type |
|---|---|
| Id | UUID |
| WorkspaceId | UUID |
| ContentDraftId | UUID |
| Platform | VARCHAR(30) |
| Text | TEXT |
| Hook | TEXT |
| CTA | TEXT |
| Hashtags | JSONB |
| CharacterCount | INTEGER |
| PlatformMetadata | JSONB |
| OptimizationScore | DECIMAL(5,2) |
| Status | VARCHAR(30) |
| CreatedAt | TIMESTAMPTZ |
| UpdatedAt | TIMESTAMPTZ |

### Platforms

```text
X
LINKEDIN
```

The same idea can therefore produce:

```text
Content Draft
    │
    ├── X Variant
    └── LinkedIn Variant
```

---

# 13. Research

## 13.1 Research Sources

Stores external sources used by the AI.

### Table

```text
research_sources
```

### Fields

| Field | Type |
|---|---|
| Id | UUID |
| WorkspaceId | UUID |
| Url | TEXT |
| Title | TEXT |
| Publisher | VARCHAR(200) |
| PublishedAt | TIMESTAMPTZ |
| RetrievedAt | TIMESTAMPTZ |
| SourceType | VARCHAR(50) |
| ContentHash | VARCHAR(128) |
| Metadata | JSONB |
| CreatedAt | TIMESTAMPTZ |

### Source types

```text
ARTICLE
NEWS
REPORT
DOCUMENTATION
SOCIAL_POST
WEBSITE
RESEARCH
USER_PROVIDED
```

---

## 13.2 Research Evidence

Stores specific evidence extracted from sources.

### Table

```text
research_evidence
```

### Fields

| Field | Type |
|---|---|
| Id | UUID |
| ResearchSourceId | UUID |
| ContentIdeaId | UUID |
| Claim | TEXT |
| Evidence | TEXT |
| Confidence | DECIMAL(5,2) |
| CreatedAt | TIMESTAMPTZ |

This allows the system to distinguish:

```text
Claim
↓
Evidence
↓
Source
```

instead of storing unsupported AI statements as facts.

---

# 14. Semantic Memory

## 14.1 Memory Items

Stores information useful for semantic retrieval.

### Table

```text
memory_items
```

### Fields

| Field | Type |
|---|---|
| Id | UUID |
| WorkspaceId | UUID |
| MemoryType | VARCHAR(50) |
| Content | TEXT |
| Metadata | JSONB |
| Embedding | VECTOR |
| Importance | DECIMAL(5,2) |
| Confidence | DECIMAL(5,2) |
| CreatedAt | TIMESTAMPTZ |
| UpdatedAt | TIMESTAMPTZ |
| LastRetrievedAt | TIMESTAMPTZ |

### Memory types

```text
BRAND
AUDIENCE
STORY
CONTENT
FEEDBACK
LESSON
PERFORMANCE
RESEARCH
```

PostgreSQL `pgvector` is used for semantic search.

---

# 15. Social Accounts

## 15.1 Social Accounts

Represents connected X and LinkedIn accounts.

Implemented in migration `AddSocialAccounts`. One platform account may be
connected by more than one workspace, so the unique constraint includes
`WorkspaceId`.

### Table

```text
social_accounts
```

### Fields

| Field | Type | Notes |
|---|---|---|
| Id | UUID | Primary key |
| WorkspaceId | UUID | Required. Cascade delete with the workspace |
| Platform | VARCHAR(50) | `X` or `LinkedIn` |
| PlatformAccountId | VARCHAR(200) | The platform's own account identifier |
| Username | VARCHAR(200) | Nullable |
| DisplayName | VARCHAR(200) | Nullable |
| ProfileUrl | VARCHAR(2000) | Nullable |
| AccessTokenEncrypted | VARCHAR(8000) | Nullable. Encrypted, never plaintext |
| RefreshTokenEncrypted | VARCHAR(8000) | Nullable. Encrypted, never plaintext |
| TokenExpiresAt | TIMESTAMPTZ | Nullable |
| Scopes | TEXT[] | Granted scopes |
| Status | VARCHAR(50) | See below |
| ConnectedAt | TIMESTAMPTZ | Last successful authorization |
| CreatedAt | TIMESTAMPTZ | |
| UpdatedAt | TIMESTAMPTZ | |

### Constraints and indexes

| Name | Kind | Columns |
|---|---|---|
| `PK_SocialAccounts` | Primary key | `Id` |
| `FK_SocialAccounts_Workspaces_WorkspaceId` | Foreign key | `WorkspaceId` → `Workspaces.Id`, cascade delete |
| `IX_SocialAccounts_WorkspaceId_Platform_PlatformAccountId` | Unique | `WorkspaceId`, `Platform`, `PlatformAccountId` |
| `IX_SocialAccounts_WorkspaceId_Status` | Index | `WorkspaceId`, `Status` |

The unique index is what makes reconnect idempotent: re-authorizing an account
already connected to the same workspace updates the existing row instead of
creating a duplicate.

### Platforms

```text
X
LinkedIn
```

### Status

```text
Connected
Expired
Revoked
Error
Disconnected
```

### Credential handling

OAuth credentials must never be stored as plaintext. Encryption occurs before
persistence, through `ISocialCredentialProtector`, and access and refresh tokens
are protected under separate Data Protection purposes so neither can be
decrypted as the other.

`AccessTokenEncrypted` and `RefreshTokenEncrypted` are set to `NULL` on
disconnect, which destroys the stored credential. The row itself is retained as
connection history rather than deleted, so a previously disconnected
`PlatformAccountId` cannot be silently reconnected under a fresh id.

The encrypted columns must never be selected into an API response. The public
`SocialAccountResponse` contract has no credential property at all, and
`tokenExpiresAt` is the only token-related field a client ever sees.

> **Not implemented:** `AccountMetadata` and `LastSyncedAt` from earlier drafts of
> this schema. They are absent from the entity and the migration. Nothing in the
> current milestone reads provider profile metadata back from the platform, so
> there is nothing to record or sync.

---

# 16. Scheduling

## 16.1 Schedules

Stores planned publishing events.

### Table

```text
schedules
```

### Fields

| Field | Type |
|---|---|
| Id | UUID |
| WorkspaceId | UUID |
| ContentVariantId | UUID |
| SocialAccountId | UUID |
| ScheduledAt | TIMESTAMPTZ |
| Timezone | VARCHAR(100) |
| Status | VARCHAR(30) |
| CreatedBy | UUID |
| CreatedAt | TIMESTAMPTZ |
| UpdatedAt | TIMESTAMPTZ |

### Status

```text
SCHEDULED
PROCESSING
PUBLISHED
FAILED
CANCELLED
```

The stored `ScheduledAt` value must represent the exact UTC execution time.

---

# 17. Publishing

## 17.1 Publications

Represents an actual publishing event.

### Table

```text
publications
```

### Fields

| Field | Type |
|---|---|
| Id | UUID |
| WorkspaceId | UUID |
| ContentVariantId | UUID |
| SocialAccountId | UUID |
| ScheduleId | UUID |
| PlatformPostId | VARCHAR(300) |
| PlatformPostUrl | TEXT |
| IdempotencyKey | VARCHAR(200) |
| Status | VARCHAR(30) |
| PublishedAt | TIMESTAMPTZ |
| ErrorCode | VARCHAR(100) |
| ErrorMessage | TEXT |
| AttemptCount | INTEGER |
| CreatedAt | TIMESTAMPTZ |
| UpdatedAt | TIMESTAMPTZ |

### Status

```text
PENDING
PUBLISHING
PUBLISHED
FAILED
```

### Publishing safety

The database should enforce uniqueness around:

```text
SocialAccountId + IdempotencyKey
```

This prevents accidental duplicate publication when a worker retries.

---

# 18. Analytics

## 18.1 Publication Metrics

Stores normalized platform performance data.

> **Implementation note.** The table is named `content_post_metrics`, not
> `publication_metrics`, and it references `ContentPostId` rather than a
> `PublicationId`. There is no publication entity: publishing records its
> outcome on `content_posts` and deliberately does not keep publication history.
>
> The table, the read endpoints and the sync worker that fetches metrics from
> the platforms are implemented. A metric reads as `null` only when the platform
> does not expose it or has not reported it yet.
>
> The worker reads through the account that published each post, so it needs the
> platform's own post id. That is stored on
> `content_posts.published_external_post_id`, written by the same conditional
> update that marks the post `Published`.
>
> `content_posts.metrics_last_attempted_at` is the worker's resync signal. It is
> written before the platform is called and is not part of a snapshot, so a read
> that fails still waits for the next resync interval instead of being retried on
> every poll.
>
> `EngagementRate` is **not** stored. It is derived on read as
> `(likes + comments + shares) / impressions * 100`, so a workspace-level rate
> can never disagree with its own inputs.

### Table

```text
content_post_metrics
```

### Fields

| Field | Type | Notes |
|---|---|---|
| Id | UUID | PK |
| WorkspaceId | UUID | Part of the foreign key, not a bare reference |
| ContentPostId | UUID | FK to `content_posts (WorkspaceId, Id)` |
| Impressions | BIGINT NULL | |
| Likes | BIGINT NULL | |
| Comments | BIGINT NULL | |
| Shares | BIGINT NULL | |
| Clicks | BIGINT NULL | |
| ProfileVisits | BIGINT NULL | |
| FollowersGained | BIGINT NULL | |
| RecordedAt | TIMESTAMPTZ | When the snapshot was measured |
| CreatedAt | TIMESTAMPTZ | Row insert time |

### Why every metric is nullable

The design requires three states to stay distinct, and a `NOT NULL BIGINT`
cannot hold them:

| Stored | Reported as | Meaning |
|---|---|---|
| `NULL` | `null` | The platform has not reported this metric (never fetched, or not supported) |
| `0` | `0` | The platform reported a real zero |
| `n` | `n` | The platform reported a real value |

A successful read always writes a row, even when every value is `NULL`. That is
what keeps "not yet synchronized" (no row, so `recordedAt` is `null` in the
response) apart from "measured, and the platform reported nothing" (a row whose
`recordedAt` is set). A failed read writes no row at all, so it never looks like
a measurement of zero.

Collapsing `NULL` to `0` would make "LinkedIn does not expose profile visits"
indistinguishable from "this post got no profile visits".

Totals follow the same rule: summing skips `NULL`, and if no snapshot in the
period reported a metric, the total is `null` rather than `0`.

`engagementRate` is `null` when impressions are `null` or `0`, because no rate
is defined over an unknown or empty denominator.

### Cardinality

A post may have several snapshots, because platform statistics keep changing
after publication. `content_post_metrics` is **not** unique per post.

```text
ContentPost
    │
    ├── Snapshot @ 1 hour
    ├── Snapshot @ 6 hours
    ├── Snapshot @ 24 hours
    └── Snapshot @ 7 days
```

Reads take the **latest** `RecordedAt` per post, with the higher `Id` as the
tie-break when two snapshots share a timestamp. Without that rule a post that
was measured four times would be counted four times over.

### Tenant isolation

The foreign key is composite:

```text
FOREIGN KEY (WorkspaceId, ContentPostId)
    REFERENCES content_posts (WorkspaceId, Id)
```

`content_posts` therefore carries a unique constraint on `(WorkspaceId, Id)`.
Because the reference includes the workspace, the database itself rejects a
metric row that points at another workspace's post — isolation is not left to
the read path alone. This was verified against a real PostgreSQL instance; EF
InMemory does not enforce foreign keys and cannot demonstrate it.

### Indexes

| Index | Serves |
|---|---|
| `(ContentPostId, RecordedAt)` | Latest-snapshot lookup per post |
| `(WorkspaceId, ContentPostId)` | Foreign key support |
| `(WorkspaceId, RecordedAt)` | Workspace-scoped scans |

---

# 19. AI Runs

## 19.1 AI Runs

Tracks every significant AI operation.

### Table

```text
ai_runs
```

### Fields

| Field | Type |
|---|---|
| Id | UUID |
| WorkspaceId | UUID |
| RunType | VARCHAR(50) |
| Provider | VARCHAR(50) |
| Model | VARCHAR(100) |
| PromptVersion | VARCHAR(50) |
| InputTokens | INTEGER |
| OutputTokens | INTEGER |
| TotalTokens | INTEGER |
| EstimatedCost | DECIMAL(12,6) |
| DurationMs | INTEGER |
| Status | VARCHAR(30) |
| ErrorMessage | TEXT |
| Metadata | JSONB |
| CreatedAt | TIMESTAMPTZ |
| CompletedAt | TIMESTAMPTZ |

### Run types

```text
RESEARCH
IDEA_GENERATION
OPPORTUNITY_SCORING
STRATEGY
CONTENT_GENERATION
PLATFORM_OPTIMIZATION
QUALITY_EVALUATION
FACT_VERIFICATION
PERFORMANCE_ANALYSIS
LEARNING
EMBEDDING
```

This table allows the system to understand AI usage and cost.

---

# 20. AI Evaluations

## 20.1 Content Evaluations

Stores AI quality evaluation results.

### Table

```text
content_evaluations
```

### Fields

| Field | Type |
|---|---|
| Id | UUID |
| WorkspaceId | UUID |
| ContentDraftId | UUID |
| ContentVariantId | UUID |
| AIRunId | UUID |
| StrategicFit | DECIMAL(5,2) |
| AudienceFit | DECIMAL(5,2) |
| HookStrength | DECIMAL(5,2) |
| ValueScore | DECIMAL(5,2) |
| ClarityScore | DECIMAL(5,2) |
| OriginalityScore | DECIMAL(5,2) |
| AuthenticityScore | DECIMAL(5,2) |
| StorytellingScore | DECIMAL(5,2) |
| PlatformFit | DECIMAL(5,2) |
| VoiceMatch | DECIMAL(5,2) |
| FactualReliability | DECIMAL(5,2) |
| AILanguageRisk | DECIMAL(5,2) |
| OverallScore | DECIMAL(5,2) |
| Passed | BOOLEAN |
| Feedback | JSONB |
| CreatedAt | TIMESTAMPTZ |

The `OverallScore` is an internal quality signal, not a public ranking of content.

---

# 21. User Feedback

## 21.1 Content Feedback

Captures human decisions that can improve future generation.

### Table

```text
content_feedback
```

### Fields

| Field | Type |
|---|---|
| Id | UUID |
| WorkspaceId | UUID |
| ContentDraftId | UUID |
| ContentVariantId | UUID |
| UserId | UUID |
| Action | VARCHAR(30) |
| Reason | TEXT |
| FeedbackData | JSONB |
| CreatedAt | TIMESTAMPTZ |

### Actions

```text
APPROVED
REJECTED
EDITED
REGENERATED
SAVED
ARCHIVED
```

Human edits are particularly valuable because they reveal the difference between AI output and the user's preferred output.

---

# 22. Learning Signals

## 22.1 Learning Signals

Stores structured observations generated from content performance and feedback.

### Table

```text
learning_signals
```

Implemented as `LearningSignals` in the database, matching the casing of every
other table in this schema.

### Fields

| Field | Type |
|---|---|
| Id | UUID |
| WorkspaceId | UUID |
| SourceType | VARCHAR(50) |
| SourceId | UUID (nullable) |
| SignalType | VARCHAR(100) |
| Signal | TEXT |
| Confidence | DECIMAL(5,2) |
| Metadata | JSONB |
| CreatedAt | TIMESTAMPTZ |

### Indexes

```text
IX_LearningSignals_WorkspaceId_CreatedAt  (WorkspaceId, CreatedAt)
```

The newest-first workspace read depends on this index, so it is part of the
table rather than an optimisation added later.

### Relationships

```text
WorkspaceId -> Workspaces.Id  ON DELETE RESTRICT
```

Restrict rather than cascade: signals are a durable record of what was measured
and what was concluded, and a workspace deletion that silently took them with it
would erase the evidence trail.

### The observation and the inference are stored apart

`Signal` is the observation, and it is written by code from real metric totals —
never by the model. The interpretation lives in `Metadata` under its own
`recommendation` key, alongside the evidence it was drawn from:

```json
{
  "dimension": "Platform",
  "window": { "from": "2026-06-27T18:30:00Z", "to": "2026-09-25T18:30:00Z" },
  "measuredPosts": 9,
  "publishedPostsInWindow": 12,
  "evidenceCount": 5,
  "best":   { "key": "LinkedIn", "label": "LinkedIn", "posts": 3, "impressions": 3000, "engagementRate": 6.0 },
  "worst":  { "key": "X",        "label": "X",        "posts": 2, "impressions": 1000, "engagementRate": 2.0 },
  "groups": [ "..." ],
  "recommendation": "Worth trying one more LinkedIn-only post next week."
}
```

A single merged sentence could not carry that distinction once it was read back,
which is why the two halves are never written into one field. Everything needed
to recompute the observation is in the metadata, so the sentence does not have to
be trusted.

`SourceId` is null for performance-analysis signals. A platform or pillar
comparison is measured over every post in the window, so pointing at a single
record would misstate where the evidence came from.

### Confidence

`Confidence` is a strength-of-evidence indicator from 0 to 100, computed as a
deterministic function of the smaller of the two compared group sizes relative to
the minimum a group needs to be compared at all. It exists to rank signals
against each other. It is not a probability, and no significance test or
confidence interval is computed anywhere in this feature.

### Examples

The signal types the current analysis produces:

```text
PLATFORM_ENGAGEMENT
CONTENT_TYPE_ENGAGEMENT
CONTENT_PILLAR_ENGAGEMENT
POSTING_DAY_ENGAGEMENT
```

The examples below are from the wider design and are not produced by the
implemented analysis. They stay in this list as the target vocabulary, and each
would need its own measurement before it could be stored.

```text
HOOK_PERFORMED_WELL
SHORT_POST_OUTPERFORMED_LONG_POST
AUDIENCE_RESPONDED_TO_CASE_STUDY
CTA_GENERATED_COMMENTS
TOPIC_LOW_ENGAGEMENT
POSTING_TIME_PERFORMED_WELL
```

These signals feed future strategy and generation decisions.

### Rows accumulate

Nothing here is updated in place. A later run over a longer history is a new
observation, not a correction of an earlier one, so repeated runs add rows and
the history of what was believed when stays readable.

---

# 23. Posting-Time Intelligence

## 23.1 Posting Time Insights

Stores learned posting-time patterns.

### Table

```text
posting_time_insights
```

### Fields

| Field | Type |
|---|---|
| Id | UUID |
| WorkspaceId | UUID |
| Platform | VARCHAR(30) |
| DayOfWeek | SMALLINT |
| Hour | SMALLINT |
| SampleSize | INTEGER |
| AverageEngagementRate | DECIMAL(8,4) |
| AverageImpressions | DECIMAL(12,2) |
| Confidence | DECIMAL(5,2) |
| UpdatedAt | TIMESTAMPTZ |

Initial recommendations can be based on configured audience timezone and platform patterns.

As sufficient historical data becomes available, this table can represent workspace-specific observations.

---

# 24. Notifications

## 24.1 Notifications

Stores user-facing system notifications.

### Table

```text
notifications
```

### Fields

| Field | Type |
|---|---|
| Id | UUID |
| UserId | UUID |
| WorkspaceId | UUID |
| Type | VARCHAR(50) |
| Title | VARCHAR(200) |
| Message | TEXT |
| Data | JSONB |
| ReadAt | TIMESTAMPTZ |
| CreatedAt | TIMESTAMPTZ |

### Types

```text
CONTENT_READY
CONTENT_REQUIRES_REVIEW
POST_PUBLISHED
PUBLISH_FAILED
ACCOUNT_EXPIRED
AI_JOB_FAILED
WEEKLY_REPORT
```

---

# 25. Audit Logs

## 25.1 Audit Logs

Tracks important system actions.

### Table

```text
audit_logs
```

### Fields

| Field | Type |
|---|---|
| Id | UUID |
| WorkspaceId | UUID |
| UserId | UUID |
| Action | VARCHAR(100) |
| EntityType | VARCHAR(100) |
| EntityId | UUID |
| OldValues | JSONB |
| NewValues | JSONB |
| IpAddress | INET |
| UserAgent | TEXT |
| CreatedAt | TIMESTAMPTZ |

Examples:

```text
ContentApproved
ContentRejected
ContentDeleted
AccountConnected
AccountDisconnected
ScheduleCreated
ScheduleCancelled
Published
```

Audit logs should generally be immutable.

### Implementation notes

The physical table created by the migration is `AuditLogs`, following the
PascalCase table naming the schema already uses in PostgreSQL. The logical name
in this document stays `audit_logs` to match the rest of the document.

- **Action casing.** The examples above originally read `CONTENT_APPROVED`, but
  the API specification's own response example shows `ContentApproved`, and the
  other enum columns in this schema are stored as PascalCase. The action values
  are therefore stored PascalCase so the two documents agree. The `Action`
  column is a plain `VARCHAR(100)`, so the C# enum in
  `apps/api/Domain/Enums/AuditAction.cs` is the only guard on the value.
- **`UserId` is nullable.** Every action written today has an authenticated
  user, but the documented `Published` action is performed by the background
  worker, which is system-scoped. `AccountConnected` is the one user-initiated
  action already recorded with a null `UserId`, because the social OAuth
  callback is anonymous and takes its workspace from signed state.
- **`EntityId` is nullable** so a system-wide action that is not about one
  record can still be logged.
- **Immutability** is enforced by the absence of a write path, not by a database
  rule. There is no update method, no delete method, and the read controller
  exposes no verb other than `GET`.
- The workspace foreign key is `RESTRICT` and the user foreign key is
  `SET NULL`, so deleting a workspace cannot silently discard audit history and
  deleting a user cannot erase the record of what they did.

---

# 26. Persistent Job Records

Redis will handle most queue operations, but important long-running jobs should have persistent records.

## 26.1 Jobs

### Table

```text
jobs
```

### Fields

| Field | Type |
|---|---|
| Id | UUID |
| WorkspaceId | UUID |
| JobType | VARCHAR(100) |
| Status | VARCHAR(30) |
| Payload | JSONB |
| Attempts | INTEGER |
| MaxAttempts | INTEGER |
| AvailableAt | TIMESTAMPTZ |
| StartedAt | TIMESTAMPTZ |
| CompletedAt | TIMESTAMPTZ |
| ErrorMessage | TEXT |
| CreatedAt | TIMESTAMPTZ |
| UpdatedAt | TIMESTAMPTZ |

### Status

```text
PENDING
PROCESSING
COMPLETED
FAILED
CANCELLED
```

Redis handles fast queue operations while PostgreSQL provides durable job history where required.

---

# 27. Recommended Indexes

Indexes should focus on common application queries.

## Users

```text
UNIQUE INDEX users_email
```

## Workspace Members

```text
UNIQUE INDEX workspace_members_workspace_user
INDEX workspace_members_user
```

## Content Ideas

```text
INDEX content_ideas_workspace_status
INDEX content_ideas_workspace_created_at
INDEX content_ideas_pillar
```

## Content Drafts

```text
INDEX content_drafts_workspace_status
INDEX content_drafts_workspace_created_at
INDEX content_drafts_workspace_pillar
```

## Content Variants

```text
INDEX content_variants_draft
INDEX content_variants_workspace_platform
```

## Social Accounts

```text
UNIQUE INDEX social_accounts_workspace_platform_account  (WorkspaceId, Platform, PlatformAccountId)
INDEX social_accounts_workspace_status                  (WorkspaceId, Status)
```

## Schedules

```text
INDEX schedules_status_scheduled_at
INDEX schedules_workspace_scheduled_at
```

This is particularly important for workers searching for due posts.

## Publications

```text
INDEX publications_workspace_created_at
INDEX publications_social_account
UNIQUE INDEX publications_social_account_idempotency
```

## Analytics

```text
INDEX content_post_metrics_content_post_recorded_at
INDEX content_post_metrics_workspace_content_post
INDEX content_post_metrics_workspace_recorded_at
```

## AI Runs

```text
INDEX ai_runs_workspace_created_at
INDEX ai_runs_workspace_run_type
```

## Notifications

```text
INDEX notifications_user_read_at
INDEX notifications_workspace_created_at
```

## Audit Logs

```text
INDEX audit_logs_workspace_created_at
INDEX audit_logs_entity
```

---

# 28. Vector Index

For `memory_items.embedding`, pgvector should be enabled.

Example:

```sql
CREATE EXTENSION IF NOT EXISTS vector;
```

The exact vector dimension depends on the embedding model selected.

Example:

```text
embedding VECTOR(1536)
```

should **not** be hardcoded until the embedding provider/model is finalized.

For larger datasets, use an appropriate pgvector index such as HNSW.

---

# 29. JSONB Usage

JSONB should be used for flexible AI-related metadata, not for core relational data.

### Good JSONB candidates

```text
BrandPersonality
VoiceProfile
ExpertiseAreas
Demographics
StrategyData
PlatformMetadata
ResearchMetadata
FeedbackData
LearningSignalMetadata
AI Run Metadata
```

### Avoid JSONB for

```text
UserId
WorkspaceId
ContentDraftId
Status
Platform
ScheduledAt
CreatedAt
PublishedAt
```

Core relationships and frequently queried fields should remain relational columns.

---

# 30. Foreign-Key Relationships

Core relationships:

```text
users
  │
  └── workspace_members
          │
          └── workspaces
                │
                ├── brand_profiles
                ├── audience_profiles
                ├── content_strategies
                ├── content_pillars
                ├── content_ideas
                ├── stories
                ├── content_drafts
                │      │
                │      └── content_variants
                │
                ├── social_accounts
                │      │
                │      └── publications
                │
                ├── schedules
                ├── research_sources
                ├── memory_items
                ├── ai_runs
                ├── learning_signals
                └── audit_logs
```

---

# 31. Delete Behavior

Foreign-key deletion rules must protect historical data.

### Example

Deleting a workspace should not automatically delete:

```text
Publications
Analytics
AuditLogs
AI Runs
```

unless the product explicitly implements permanent workspace deletion.

For dependent configuration entities, cascading may be acceptable.

For historical entities, prefer:

```text
RESTRICT
```

or soft deletion.

---

# 32. Data Integrity Rules

The application and database should enforce the following.

### Content

A content variant must belong to the same workspace as its parent draft.

### Scheduling

A schedule cannot exist without:

```text
ContentVariant
SocialAccount
ScheduledAt
```

### Publishing

A publication must reference a valid social account.

### Approval

Only approved content may enter the publishing pipeline.

### Workspace isolation

Every workspace-owned query must include:

```text
WorkspaceId
```

### OAuth

Encrypted credentials must never be exposed through normal API responses.

### Analytics

Analytics must always reference a real publication.

### AI

AI runs should reference the workspace responsible for the operation.

---

# 33. EF Core Entity Organization

The .NET backend should organize database entities by domain rather than putting every entity into one folder.

Recommended structure:

```text
src/
└── Infrastructure/
    └── Persistence/
        ├── AppDbContext.cs
        ├── Configurations/
        │   ├── UserConfiguration.cs
        │   ├── WorkspaceConfiguration.cs
        │   ├── BrandProfileConfiguration.cs
        │   ├── AudienceProfileConfiguration.cs
        │   ├── ContentStrategyConfiguration.cs
        │   ├── ContentPillarConfiguration.cs
        │   ├── ContentIdeaConfiguration.cs
        │   ├── StoryConfiguration.cs
        │   ├── ContentDraftConfiguration.cs
        │   ├── ContentVariantConfiguration.cs
        │   ├── SocialAccountConfiguration.cs
        │   ├── ScheduleConfiguration.cs
        │   ├── PublicationConfiguration.cs
        │   ├── PublicationMetricsConfiguration.cs
        │   ├── AIRunConfiguration.cs
        │   ├── ContentEvaluationConfiguration.cs
        │   ├── FeedbackConfiguration.cs
        │   ├── LearningSignalConfiguration.cs
        │   └── AuditLogConfiguration.cs
        │
        └── Migrations/
```

---

# 34. EF Core Configuration Rules

Entity configuration should be handled with:

```csharp
IEntityTypeConfiguration<T>
```

rather than putting extensive configuration inside entity classes.

Example:

```csharp
public class WorkspaceConfiguration
    : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Slug)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(x => x.Slug)
            .IsUnique();
    }
}
```

This keeps domain models clean and database configuration centralized.

---

# 35. Enumerations

Enums should exist in the application domain for predictable values.

Important enums include:

```text
WorkspaceRole
ContentIdeaStatus
ContentStatus
ContentSource
Platform
SocialAccountStatus
ScheduleStatus
PublicationStatus
AIRunType
AIRunStatus
FeedbackAction
MemoryType
JobStatus
```

For PostgreSQL storage, the initial implementation may use strings rather than native PostgreSQL enums.

This makes future enum changes easier to manage through EF Core migrations.

---

# 36. Token Security

Social OAuth credentials are sensitive.

The system should:

1. Receive credentials through OAuth.
2. Encrypt them in the application layer.
3. Store only encrypted values.
4. Decrypt only when required for API calls.
5. Never return tokens through API responses.
6. Never log access or refresh tokens.
7. Rotate credentials when supported.
8. Remove credentials when an account is disconnected.

Example:

```text
OAuth Provider
      ↓
API
      ↓
Encryption Service
      ↓
PostgreSQL
```

### Implementation status

Implemented as described, with one addition. Access and refresh tokens are
protected under **separate Data Protection purposes**:

```text
Mayorx.ContentEngine.SocialCredentials.AccessToken.v1
Mayorx.ContentEngine.SocialCredentials.RefreshToken.v1
```

Separating the purposes means a protected access token cannot be decrypted as a
refresh token, so a bug that swaps the two fields fails loudly instead of
sending the wrong credential to the provider.

| Requirement | Status |
| --- | --- |
| Encrypt in the application layer | Implemented, before persistence |
| Store only encrypted values | Implemented |
| Never return tokens through API responses | Implemented, enforced by the response contract |
| Never log access or refresh tokens | Implemented, only ids and platform are logged |
| Remove credentials on disconnect | Implemented, encrypted columns set to `NULL` |
| Decrypt only when required for API calls | Not yet needed, nothing calls a platform API yet |
| Rotate credentials when supported | Not implemented, no refresh flow yet |

The Data Protection key ring is as sensitive as the credentials it protects.
Losing it makes every stored token unrecoverable; leaking it makes every stored
token readable. It is git-ignored locally and must be an explicitly configured,
protected, persistent location in production, shared by all API instances.

---

# 37. Data Retention

Not all data needs the same retention period.

### Long-term

```text
Users
Workspaces
Brand Profiles
Audience Profiles
Content
Publications
Stories
Strategies
```

### Medium/long-term

```text
Analytics
Learning Signals
Feedback
AI Runs
```

### Potentially short-term

```text
Temporary AI research cache
Temporary job payloads
Transient processing metadata
```

Redis data should have explicit TTLs where appropriate.

---

# 38. MVP Database Scope

The initial implementation does **not** need every table above.

> **Status.** This list is the original design intent and was never a record of
> what exists. The tables actually created are `users`, `workspaces`,
> `workspace_members`, `brand_profiles`, `audience_profiles`,
> `content_strategies`, `content_ideas`, `content_posts`, `social_accounts` and
> `content_post_metrics`.
>
> The following entries were implemented differently rather than as separate
> tables: `content_drafts`, `content_variants` and `schedules` all live on
> `content_posts`; `publications` has no table, because publishing records its
> outcome on `content_posts`; and `publication_metrics` is named
> `content_post_metrics` and references `content_posts` directly.
>
> `content_pillars` is now implemented as its own table
> (`20260930154134_AddContentPillars`). It was previously listed here as
> non-existent because `ContentStrategy.ContentPillars` is a free-text string.
> That field still exists and is still free text; the new table is what
> `content_ideas.ContentPillarId` actually references. `TargetAudienceId` is
> deliberately not a foreign key — `audience_profiles` allows only one profile
> per workspace — so the service verifies the referenced profile belongs to the
> caller's workspace before storing it.

### Phase 1 — Required

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

### Phase 2

```text
stories
research_sources
research_evidence
memory_items

content_feedback
posting_time_insights

notifications
jobs
```

`learning_signals` has moved out of this phase. It is not a Phase 2 table: its
evidence is content performance, and there is no performance to learn from until
publishing and metrics exist. It was built in the same change as the performance
analysis that fills it, once the measured posts it reads were real — which is
section 22.1 above and migration `20261001174057_AddLearningSignals`.

### Phase 3

Additional tables should only be introduced when the corresponding product capability actually exists.

---

# 39. Initial Migration Order

The initial EF Core migration should create tables approximately in this order:

```text
1. Users

2. Workspaces
3. WorkspaceMembers

4. BrandProfiles
5. AudienceProfiles

6. ContentStrategies
7. ContentPillars

8. ContentIdeas
9. Stories

10. ContentDrafts
11. ContentVariants

12. SocialAccounts

13. Schedules
14. Publications
15. PublicationMetrics

16. AIRuns
17. ContentEvaluations

18. AuditLogs
```

This minimizes foreign-key dependency problems.

`Publications` is not created. Publishing is implemented, but it records its
outcome on `content_posts` (`PublishedAt`, `PublishFailureReason`) rather than in
a table of its own, so there is no publication history to store yet.
`Schedules` is likewise not a table: scheduling is `content_posts.ScheduledAt`.
`content_post_metrics` **is** created, and references its post directly by
`(WorkspaceId, ContentPostId)` instead of going through a publication.

---

# 40. Database Architecture Summary

The database is intentionally designed around the lifecycle:

```text
Workspace
    ↓
Brand + Audience
    ↓
Strategy
    ↓
Content Pillars
    ↓
Content Ideas
    ↓
Content Draft
    ↓
Platform Variants
    ↓
Schedule
    ↓
Publication
    ↓
Analytics
    ↓
Learning Signals
    ↓
Improved Strategy
    ↓
Future Content
```

The database therefore does more than store posts.

It stores the **context, decisions, content, publishing history, performance, and learning data** required for the Mayorx Content Engine to become progressively more useful over time.

---

# 41. Core Design Principle

The database should preserve the distinction between:

```text
What the user configured
        ↓
What the AI recommended
        ↓
What the user approved/edited
        ↓
What the system published
        ↓
What happened after publication
        ↓
What the system learned
```

This distinction is critical.

The system should never overwrite the original user configuration or historical publishing data simply because the AI generated a new recommendation.

The database is therefore designed to support **traceability, learning, reliability, and future automation** without turning the system into an opaque AI pipeline.
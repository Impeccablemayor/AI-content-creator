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

### Table

```text
social_accounts
```

### Fields

| Field | Type |
|---|---|
| Id | UUID |
| WorkspaceId | UUID |
| Platform | VARCHAR(30) |
| PlatformAccountId | VARCHAR(200) |
| Username | VARCHAR(200) |
| DisplayName | VARCHAR(200) |
| ProfileUrl | TEXT |
| AccessTokenEncrypted | TEXT |
| RefreshTokenEncrypted | TEXT |
| TokenExpiresAt | TIMESTAMPTZ |
| Scopes | JSONB |
| AccountMetadata | JSONB |
| Status | VARCHAR(30) |
| LastSyncedAt | TIMESTAMPTZ |
| CreatedAt | TIMESTAMPTZ |
| UpdatedAt | TIMESTAMPTZ |

### Platforms

```text
X
LINKEDIN
```

### Status

```text
CONNECTED
EXPIRED
REVOKED
ERROR
DISCONNECTED
```

OAuth credentials must never be stored as plaintext.

Encryption should occur before persistence.

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

### Table

```text
publication_metrics
```

### Fields

| Field | Type |
|---|---|
| Id | UUID |
| WorkspaceId | UUID |
| PublicationId | UUID |
| Impressions | BIGINT |
| Likes | BIGINT |
| Comments | BIGINT |
| Shares | BIGINT |
| Clicks | BIGINT |
| ProfileVisits | BIGINT |
| FollowersGained | BIGINT |
| EngagementRate | DECIMAL(8,4) |
| RecordedAt | TIMESTAMPTZ |
| CreatedAt | TIMESTAMPTZ |

Metrics may be recorded multiple times because platform statistics can change over time.

Example:

```text
Publication
    │
    ├── Metrics @ 1 hour
    ├── Metrics @ 6 hours
    ├── Metrics @ 24 hours
    └── Metrics @ 7 days
```

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

### Fields

| Field | Type |
|---|---|
| Id | UUID |
| WorkspaceId | UUID |
| SourceType | VARCHAR(50) |
| SourceId | UUID |
| SignalType | VARCHAR(100) |
| Signal | TEXT |
| Confidence | DECIMAL(5,2) |
| Metadata | JSONB |
| CreatedAt | TIMESTAMPTZ |

### Examples

```text
HOOK_PERFORMED_WELL
SHORT_POST_OUTPERFORMED_LONG_POST
AUDIENCE_RESPONDED_TO_CASE_STUDY
CTA_GENERATED_COMMENTS
TOPIC_LOW_ENGAGEMENT
POSTING_TIME_PERFORMED_WELL
```

These signals feed future strategy and generation decisions.

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
CONTENT_APPROVED
CONTENT_REJECTED
CONTENT_DELETED
ACCOUNT_CONNECTED
ACCOUNT_DISCONNECTED
SCHEDULE_CREATED
SCHEDULE_CANCELLED
PUBLISHED
```

Audit logs should generally be immutable.

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
UNIQUE INDEX social_accounts_platform_account
INDEX social_accounts_workspace
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
INDEX publication_metrics_publication_recorded_at
INDEX publication_metrics_workspace_recorded_at
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
learning_signals
posting_time_insights

notifications
jobs
```

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
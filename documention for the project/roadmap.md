# Mayorx Content Engine — Product Roadmap

## 1. Purpose

This roadmap defines the planned evolution of the Mayorx Content Engine from its initial foundation into a mature AI-powered content management and intelligence platform.

The roadmap focuses on:

- Product evolution
- Major capabilities
- AI maturity
- Platform expansion
- Analytics and learning
- Collaboration
- Infrastructure maturity
- Release priorities
- Long-term product direction

It intentionally avoids prescribing every engineering task. Those details belong in `development-plan.md`.

---

# 2. Product North Star

The long-term goal of Mayorx Content Engine is to become an **AI-powered content operating system** that helps individuals and teams:

> **Understand their audience → Decide what to say → Create the content → Publish it → Measure the result → Learn from it → Improve the next decision.**

The product should move beyond being an AI writing assistant.

It should understand:

- Who the user is
- What the brand represents
- Who the audience is
- What the audience cares about
- What topics are relevant
- What content has already been published
- What is currently performing
- What the user is trying to achieve
- What has historically worked
- What should be created next

The system should progressively move from:

**Content Generation → Content Intelligence → Content Decision System**

---

# 3. Roadmap Principles

## 3.1 Build the Core Before the Complexity

The product should first become excellent at:

1. Understanding the brand
2. Understanding the audience
3. Generating useful content
4. Reviewing content
5. Publishing reliably
6. Measuring performance

Advanced AI capabilities should be built on top of this foundation.

---

## 3.2 Intelligence Must Be Grounded in Data

AI recommendations should increasingly use:

- Brand information
- Audience information
- Content history
- User feedback
- Research
- Platform data
- Historical performance
- Publishing patterns

The system should not become more autonomous simply by adding larger prompts.

---

## 3.3 Reliability Before Automation

Publishing is an external side effect.

The product should prioritize:

- Approval
- Validation
- Idempotency
- Authentication
- Scheduling reliability
- Error handling
- Auditability

before allowing increasingly autonomous behavior.

---

## 3.4 Human Control Remains Important

The system should help users make better content decisions without making the product feel uncontrollable.

Users should always be able to:

- Review AI-generated content
- Edit content
- Reject recommendations
- Approve publishing
- Modify schedules
- Disconnect accounts
- Override AI recommendations

---

## 3.5 Expand Platforms Gradually

X and LinkedIn form the initial platform foundation.

Additional platforms should be added only after the underlying publishing architecture is stable.

---

# 4. Roadmap Overview

| Phase | Product Stage | Primary Objective |
|---|---|---|
| Phase 0 | Foundation | Establish technical and product foundation |
| Phase 1 | MVP | Create the first usable content workflow |
| Phase 2 | V1 | Deliver reliable AI-powered content management |
| Phase 3 | V1.5 | Improve intelligence, analytics and automation |
| Phase 4 | V2 | Build a stronger learning and decision system |
| Phase 5 | Long Term | Evolve into a broader AI content operating system |

---

# 5. Phase 0 — Foundation

## Objective

Build the underlying platform architecture required for the product.

## Core Capabilities

### Product foundation

- Workspace model
- User authentication
- Role-based permissions
- Brand profile
- Audience profile
- Content strategy
- Content pillars

### Technical foundation

- Next.js frontend
- ASP.NET Core API
- PostgreSQL
- Redis
- Background worker
- AI provider abstraction
- Social platform abstraction
- Docker development environment
- CI/CD foundation

### Initial integrations

- X
- LinkedIn

### Core infrastructure

- Structured logging
- Error handling
- Configuration management
- Environment separation
- Database migrations
- Basic observability

### Health and observability status

The observability foundation listed above is built; the monitoring stack around it is
not.

| Capability | Status |
| --- | --- |
| `GET /api/v1/health` | Implemented. Checks PostgreSQL, Redis, and AI provider configuration, and answers `503` when a dependency is down |
| `GET /api/v1/health/live` | Implemented. Runs no dependency check, so an outage does not trigger a restart loop |
| `X-Correlation-Id` | Implemented. Accepted or generated, echoed on the response, and applied to tracing and a logging scope |
| Metrics collection and export | Not implemented |
| Distributed trace export | Not implemented |
| Error tracking service | Not implemented |
| Worker health endpoint | Not implemented, the worker runs no HTTP listener |

The reliability of that foundation is now partly proven rather than asserted. A
dedicated integration test project runs the real composition root against the
repository's own PostgreSQL and Redis, applies the real migrations, and substitutes
only the social platform and the model. See the Engineering Gate below.

## Outcome

At the end of this phase, the product should have a stable foundation capable of supporting the MVP.

---

# 6. Phase 1 — MVP

## Objective

Allow a user to go from an empty workspace to creating and preparing content for publication.

## Core User Journey

```text
Create Workspace
      ↓
Configure Brand
      ↓
Define Audience
      ↓
Define Content Strategy
      ↓
Generate Content Ideas
      ↓
Generate Draft
      ↓
Review/Edit
      ↓
Approve
      ↓
Schedule
      ↓
Publish
```

## MVP Features

### Authentication

- Registration
- Login
- Logout
- Refresh tokens
- Workspace creation
- Workspace membership

### Brand Intelligence

- Brand profile
- Voice configuration
- Tone configuration
- Topics
- Goals
- Content boundaries

### Audience Intelligence

- Audience profile
- Audience pain points
- Interests
- Desired outcomes
- Awareness level

### Strategy

- Content pillars
- Content goals
- Posting preferences
- Content mix

**Status: content pillars implemented.** The `content_pillars` table and the
full CRUD surface at `/strategy/pillars` exist, workspace scoped, with a
name unique per workspace, a `(0, 100]` weight, an optional objective and
target audience profile, and an active flag. Ideas can be attributed to a
pillar, which is what makes the pillar filters on `/content/ideas` and
`/analytics/posts` and `/analytics/overview` possible.

Deleting a pillar keeps the ideas built from it and clears their attribution
rather than deleting content.

Content goals, posting preferences and content mix remain design intent, as
does AI-driven pillar *recommendation* below.

### Content

- Content ideas
- AI-generated ideas
- Draft generation
- Editing
- Platform variants
- Approval workflow

### Publishing

- Connect X
- Connect LinkedIn
- Schedule posts
- Publish posts
- Publication status
- Publishing history

### Basic Analytics

- Impressions
- Likes
- Comments
- Shares
- Clicks
- Engagement rate

**Status: implemented.** The workspace overview and per-post endpoints exist,
along with the `content_post_metrics` table they read, including the
`platform` / `from` / `to` / `pillarId` filters and workspace scoping. The
`AnalyticsSync` worker populates the table through the connected account that
published each post, so the metrics above are collected rather than permanently
unavailable.

Fetching a post's metrics needs the id the platform assigned it at publish time,
so `content_posts.published_external_post_id` is written by the same conditional
update that marks a post `Published`. Posts published before that column existed
are never back-filled, because the platform ids cannot be recovered after the
fact.

X exposes every metric above except per-post follower gain, in one request.
LinkedIn exposes them through `memberCreatorPostAnalytics`, which requires the
Community Management API product and is therefore optional per deployment.

The `pillarId` filter was originally omitted because there was no content
pillar entity to reference. It is now implemented, since `content_pillars` and
`content_ideas.ContentPillarId` exist.

One documented item is still omitted rather than approximated: the
`{publicationId}` route parameter, because there is no publication entity —
the route is keyed by content post id.

## Outcome

The user can manage the basic content lifecycle inside one application.

---

# 7. Phase 2 — V1

## Objective

Turn the MVP into a reliable AI-powered content management product.

The key transition is:

> From "AI can generate posts" to "AI understands what content should be created."

## AI Capabilities

### Content Opportunity Engine

The system begins identifying opportunities based on:

- Brand relevance
- Audience relevance
- Content goals
- Timeliness
- Originality
- Discussion potential
- Historical performance
- Content diversity

### Strategy Engine

AI can recommend:

- Topics
- Content pillars
- Content formats
- Story angles
- Content goals
- Posting cadence

### Story Engine

Introduce structured storytelling frameworks such as:

- Personal Story
- Problem → Solution
- Mistake → Lesson
- Observation → Insight
- Case Study
- Educational Breakdown
- Build in Public
- Contrarian Perspective

### Platform Optimization

Generate platform-specific versions rather than simply copying the same post.

For example:

```text
Core Idea
   ├── LinkedIn Version
   └── X Version
```

### Quality Evaluation

AI evaluates generated content for:

- Hook strength
- Audience fit
- Brand fit
- Value
- Clarity
- Originality
- Storytelling
- Platform fit
- Voice match
- Factual reliability
- AI-language risk

## Product Improvements

- Better content editor
- Content calendar
- AI recommendation panels
- Platform previews
- Content search
- Content filtering
- Draft history
- Better publishing controls
- Better error recovery

## Outcome

The product becomes a complete AI-assisted content workflow rather than a basic scheduler with AI writing.

---

# 8. Phase 3 — V1.5

## Objective

Use actual content performance and user behavior to make the system increasingly intelligent.

This phase introduces the first meaningful learning loop.

## Analytics Expansion

Track:

- Historical performance
- Engagement trends
- Content pillar performance
- Topic performance
- Format performance
- Posting-time performance
- Platform performance
- Audience response

## Performance Analysis

After publication, the system should analyze:

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
Future Recommendations
```

The implemented run is `POST /api/v1/learning/insights`. It reads the workspace's
published posts and their current metric snapshots, groups the measured posts by
one dimension at a time, and reports a difference only where the strongest group
clears a configured margin over the weakest. The observation is written by code
from the real totals; the model is asked only what to try next, and its answer is
stored beside the measurement rather than merged into it. A run with too little
measured history returns `hasSufficientData: false` and writes nothing.

## Learning Signals

Examples:

- Certain topics generate more discussion
- Certain hooks receive higher engagement
- Some posting windows consistently perform better
- Certain content formats underperform
- Certain storytelling patterns generate stronger responses

These signals should influence future recommendations.

The first four signal types are implemented, one per compared dimension:

```text
PLATFORM_ENGAGEMENT
CONTENT_TYPE_ENGAGEMENT
CONTENT_PILLAR_ENGAGEMENT
POSTING_DAY_ENGAGEMENT
```

Topic, hook, format, and storytelling comparisons are listed here as the target
vocabulary rather than as delivered behaviour. Each needs text the system does not
yet measure, and producing one from a proxy would store an inference as though it
were a finding. They stay unwired until there is a measurement behind them.

Influencing future recommendations is also not done. Signals are stored and
readable, and nothing yet consumes them; wiring generation to them is a separate
change with its own risk of a model taking a weak signal as an instruction.

## User Feedback Learning

The system should learn from:

- Approved content
- Edited content
- Rejected content
- Regenerated content
- User feedback
- Manual strategy changes

## Content Memory

Introduce structured memory for:

- Previous posts
- Recurring themes
- Stories
- Brand knowledge
- Audience insights
- Successful content patterns

## Outcome

The system begins adapting to the specific user rather than behaving like a generic AI content generator.

---

# 9. Phase 4 — V2

## Objective

Transform the platform from an AI-assisted publishing tool into a genuine content intelligence system.

## Advanced AI

### Research Engine

The system can identify relevant current information from trusted sources when research is necessary.

Research should support:

- Industry trends
- News-driven content
- Emerging topics
- Market developments
- New technologies
- Relevant conversations

Research evidence should be stored with source metadata.

---

## Advanced Opportunity Detection

The system can continuously identify potential content opportunities.

Examples:

```text
Trending Topic
      ↓
Brand Relevance
      ↓
Audience Relevance
      ↓
Content Opportunity
```

The system may recommend:

> "This topic is gaining attention and connects strongly with your audience's recurring problem around X."

---

## Advanced Content Memory

The system maintains semantic memory across:

- Previous content
- Brand knowledge
- Audience insights
- Research
- Stories
- Performance results

Vector search can retrieve relevant context when generating new content.

---

## Adaptive Content Strategy

The strategy engine can recommend adjustments based on performance.

For example:

```text
Historical Data
      ↓
Performance Analysis
      ↓
Strategy Insight
      ↓
Recommended Strategy Adjustment
      ↓
User Approval
```

The system should recommend strategy changes rather than silently changing them.

---

## Intelligent Posting-Time Recommendations

Posting-time recommendations can evolve from generic platform patterns toward personalized recommendations based on:

- User timezone
- Audience timezone
- Platform
- Historical performance
- Content type
- Content pillar
- Audience activity

---

## Outcome

The product becomes capable of answering:

> "What should I post next, why should I post it, and when should I post it?"

rather than simply:

> "Write me a post."

---

# 10. Phase 5 — Long-Term Vision

## Objective

Build a broader AI content operating system capable of managing the complete content intelligence lifecycle.

## Potential Capabilities

### Continuous Content Intelligence

The system continuously monitors:

- Brand context
- Audience interests
- Industry developments
- Content performance
- Platform changes
- Historical patterns

and turns relevant information into actionable recommendations.

---

## Autonomous Content Planning

The system can propose:

- Weekly content plans
- Monthly content calendars
- Content campaigns
- Content series
- Topic clusters
- Platform-specific publishing plans

Users remain able to approve or modify plans.

---

## Cross-Platform Content Systems

A single idea can become a coordinated content system.

Example:

```text
Core Topic
│
├── LinkedIn Post
├── X Thread
├── Short-form Script
├── Newsletter Idea
├── Educational Post
└── Follow-up Content
```

The platform should preserve the relationship between these pieces of content.

---

## Advanced Personalization

The system may eventually learn:

- User writing patterns
- Preferred storytelling styles
- Recurring phrases
- Preferred hooks
- Editing behavior
- Audience response patterns

Personalization should remain controllable and transparent.

---

# 11. AI Maturity Roadmap

The AI system should evolve through several maturity levels.

## Level 1 — Generator

```text
User Request
    ↓
AI
    ↓
Content
```

The AI mainly generates content.

---

## Level 2 — Context-Aware Generator

```text
Brand
Audience
Strategy
    ↓
AI
    ↓
Content
```

The AI understands the user's context.

---

## Level 3 — Intelligent Strategist

```text
Brand
Audience
Strategy
Performance
Research
    ↓
AI
    ↓
Recommendations
    ↓
Content
```

The AI begins deciding what content opportunities matter.

---

## Level 4 — Learning System

```text
Content
    ↓
Publication
    ↓
Performance
    ↓
Learning
    ↓
Improved Strategy
    ↓
Better Content
```

The system learns from outcomes.

---

## Level 5 — Content Operating System

```text
Understand
    ↓
Monitor
    ↓
Identify Opportunities
    ↓
Plan
    ↓
Create
    ↓
Review
    ↓
Publish
    ↓
Measure
    ↓
Learn
    ↺
```

The system continuously improves the content operation.

---

# 12. Social Platform Expansion

## Initial Platforms

### X

Initial capabilities:

- OAuth
- Text publishing
- Scheduling
- Publication status
- Analytics synchronization

### LinkedIn

Initial capabilities:

- OAuth
- Text publishing
- Scheduling
- Publication status
- Analytics synchronization

---

## Future Platforms

Potential future integrations include:

- Instagram
- Facebook
- Threads
- TikTok
- YouTube
- Other professional or creator platforms

Platform expansion should happen only when there is sufficient demand and the platform's API capabilities support reliable integration.

---

# 13. Analytics Evolution

Analytics should mature in stages.

## Stage 1 — Reporting

Show:

- Impressions
- Likes
- Comments
- Shares
- Clicks
- Engagement rate

**Reporting stage delivered.** The read surface and the ingestion that fills it
are both in place. See "Basic Analytics" above for the per-platform coverage.

---

## Stage 2 — Performance Analysis

Explain:

- What performed well
- What performed poorly
- Which topics performed best
- Which content pillars performed best
- Which platforms performed best
- Which posting periods performed best

---

## Stage 3 — Recommendations

Convert performance into recommendations.

Example:

```text
Performance
    ↓
Insight
    ↓
Recommendation
```

---

## Stage 4 — Adaptive Strategy

Performance data influences future:

- Topic selection
- Content mix
- Storytelling
- Platform strategy
- Posting times
- Content frequency

with user oversight.

---

# 14. Collaboration Roadmap

## Initial

Single user per workspace.

## V1

Basic roles:

- Owner
- Admin
- Editor
- Viewer

## Future

Potential capabilities:

- Content approval workflows
- Team comments
- Editorial assignments
- Review queues
- Client workspaces
- Agency workspaces
- Multiple brands
- Approval permissions
- Team activity history

This allows the product to eventually support:

- Individual creators
- Freelancers
- Businesses
- Marketing teams
- Agencies

---

# 15. Infrastructure Evolution

## Stage 1 — Modular Monolith

The initial system should remain a modular monolith.

Benefits:

- Faster development
- Simpler deployment
- Easier debugging
- Lower infrastructure cost
- Clear domain boundaries

---

## Stage 2 — Worker Scaling

As workloads increase:

- Scale background workers
- Separate AI workloads
- Scale publishing workers
- Improve queue management
- Introduce stronger job prioritization

---

## Stage 3 — Selective Service Extraction

Only extract services when there is a demonstrated need.

Potential candidates:

- AI orchestration
- Publishing service
- Analytics ingestion
- Research service

Microservices should not be introduced simply because the product has grown.

---

# 16. Security Evolution

Security requirements should increase alongside product capabilities.

## Initial

- Authentication
- Authorization
- OAuth
- Encrypted tokens
- Input validation
- Rate limiting
- Audit logs
- Secure secrets

### Audit log status

Basic audit logging is implemented. Seven user-initiated actions are recorded
in the same transaction as the change they describe: idea approve and reject,
post approve, reject and schedule, and social account connect and disconnect.
The log is readable at `GET /api/v1/audit-logs`, scoped to the caller's
workspace, and has no write path.

Not yet recorded, because no code path produces them: `ContentDeleted` and
`ScheduleCancelled` (no delete or unschedule endpoint exists), and `Published`
(the background worker does not write audit rows, because doing so would mean
modifying Publishing).

The endpoint is not paginated and caps a single response at 200 rows, so
"More detailed audit trails" below is not yet reachable for a workspace with
long history.

## Future

- Advanced workspace permissions
- Security monitoring
- Session management
- Token rotation
- More detailed audit trails
- Compliance controls where commercially required

---

# 17. Product Metrics

Roadmap success should be measured through product outcomes rather than feature count.

## Activation

Measure:

- Users completing onboarding
- Users configuring brand information
- Users configuring audience information
- Users generating their first idea
- Users generating their first draft
- Users connecting a social account

---

## Engagement

Measure:

- Weekly active workspaces
- Content ideas generated
- Drafts created
- Posts approved
- Posts published
- Returning users

---

## Publishing

Measure:

- Scheduled posts
- Successfully published posts
- Failed publications
- Publishing reliability
- Average scheduling-to-publication success rate

---

## AI Quality

Measure:

- Generation acceptance rate
- Regeneration rate
- User edit distance
- AI evaluation scores
- Rejection rate
- User feedback
- Hallucination/factual error rate

---

## Learning

Measure:

- Performance insights generated
- Recommendations accepted
- Strategy recommendations adopted
- Improvement in content performance over time

These metrics should be interpreted carefully because social-platform performance is affected by factors outside the product's control.

---

# 18. Release Gates

A phase should not advance simply because its feature list is complete.

Each major phase should meet a release gate.

## Product Gate

- Core user journey works
- UX is understandable
- No critical workflow blockers

## Engineering Gate

- Automated tests pass
- No unresolved critical defects
- Database migrations are reliable
- Background jobs are recoverable
- Readiness reports real dependency state, and liveness does not

## AI Gate

- Structured outputs are reliable
- AI evaluation is functioning
- Hallucination controls exist where required
- Prompt versions are tracked
- Important recommendations can be explained

## Publishing Gate

- OAuth works
- Tokens are protected
- Duplicate publishing is prevented
- Failed publications are recoverable
- Publication status is traceable

## Security Gate

- Authorization is enforced
- Secrets are protected
- Sensitive data is encrypted appropriately
- Audit logging is operational

---

# 19. What Is Intentionally Deferred

To prevent unnecessary complexity, the following should not be prioritized during the early product stages.

## Deferred

- Full autonomous publishing without safeguards
- Large microservice architecture
- Dozens of social integrations
- Advanced autonomous research
- Complex agent-to-agent systems
- Fully autonomous strategy changes
- Enterprise billing infrastructure
- Advanced white-labeling
- Large-scale real-time collaboration
- Complex predictive analytics
- Fully autonomous content campaigns

These may become relevant later based on product demand.

---

# 20. 90-Day Product Direction

The first major product cycle should focus on establishing a complete vertical slice.

## Stage 1 — Foundation

Deliver:

- Repository
- Backend
- Frontend
- PostgreSQL
- Redis
- Authentication
- Workspace
- Brand configuration
- Audience configuration
- Strategy configuration

---

## Stage 2 — Intelligence

Deliver:

- AI provider abstraction
- Context building
- Content opportunity generation
- Content idea generation
- Draft generation
- AI evaluation
- Content editing
- AI run tracking

### AI run tracking status

AI run tracking is implemented for the three AI operations that exist today:
idea generation, content generation, and quality evaluation. Each records one
`ai_runs` row per attempt with the provider, model, prompt version, status,
duration, timestamps, and operation-specific metadata, scoped to the calling
workspace. Failures record the exception type rather than its message, because a
provider exception can quote the prompt or the raw response back.

`InputTokens`, `OutputTokens`, `TotalTokens` and `EstimatedCost` are always
null. The provider abstraction returns only the generated text, so there is no
measured usage to record, and a guess written to a cost column reads back later
as though it had been measured. They become meaningful when the provider
abstraction exposes reported usage.

Recording is diagnostic and cannot fail the operation it describes: a run row
that cannot be written is logged and dropped, and the row is detached from the
change tracker so a later save does not retry it. The remaining eight run types
in the schema are declared but unwritten, because no code path produces them.

There is no read endpoint. The table is recorded for the cost picture the schema
calls for, and is not yet surfaced in the API.

---

## Stage 3 — Publishing

Deliver:

- X integration
- LinkedIn integration
- Account connections
- Content approval
- Scheduling
- Publishing workers
- Publication history

---

## Stage 4 — Learning Foundation

Deliver:

- Basic analytics
- Performance metrics
- Performance analysis
- Initial learning signals
- Content performance dashboard

Status: the first four are delivered. Analytics and metrics give the raw
measurement, performance analysis compares it, and learning signals store the
result and can be read back.

The dashboard is not built, and is deliberately not the same thing as the rest of
this stage. A dashboard is a display decision — which panels, which ranges, whose
workspace, and what happens when a number is missing — and none of it was needed
to prove the loop works. What exists instead is two endpoints: one that reads
stored signals, and one that runs the analysis. A dashboard can be built on top
of them without changing the loop.

---

# 21. Roadmap Dependencies

Some capabilities depend on earlier foundations.

```text
Authentication
      ↓
Workspace
      ↓
Brand + Audience
      ↓
Strategy
      ↓
AI Context
      ↓
Content Generation
      ↓
Review
      ↓
Social Connections
      ↓
Scheduling
      ↓
Publishing
      ↓
Analytics
      ↓
Learning
      ↓
Adaptive Intelligence
```

The roadmap should respect these dependencies.

---

# 22. Roadmap Governance

The roadmap is a directional product document, not a permanent promise.

Priorities may change based on:

- User feedback
- Product usage
- Technical constraints
- Platform API changes
- AI capability changes
- Infrastructure costs
- Security requirements
- Business opportunities
- Market demand

When priorities change, the roadmap should be updated rather than allowing undocumented scope to accumulate.

Each major roadmap revision should answer:

1. What changed?
2. Why did it change?
3. What capability moved?
4. What dependency caused the change?
5. What user outcome is now prioritized?

---

# 23. Product Evolution Summary

The product should evolve through this progression:

```text
Phase 0
FOUNDATION
    ↓
Phase 1
CONTENT MANAGEMENT
    ↓
Phase 2
AI-POWERED CONTENT
    ↓
Phase 3
PERFORMANCE LEARNING
    ↓
Phase 4
CONTENT INTELLIGENCE
    ↓
Phase 5
CONTENT OPERATING SYSTEM
```

The long-term product loop is:

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
     ↓
UNDERSTAND BETTER
     ↺
```

---

# 24. Final Roadmap Principle

Mayorx Content Engine should not compete simply by generating better AI-written posts.

Its long-term advantage should come from **understanding the relationship between the brand, audience, strategy, content, publishing behavior, and performance over time.**

The product should progressively become better at answering one central question:

> **"Given everything we know about this brand, its audience, its goals, its content history, and its performance, what should happen next?"**

That is the foundation of the Mayorx Content Engine vision.
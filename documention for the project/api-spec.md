# Mayorx Content Engine — API Specification

**Version:** 1.0  
**Status:** Draft  
**API Style:** REST  
**Base Path:** `/api/v1`  
**Backend:** ASP.NET Core 9  
**Primary Database:** PostgreSQL  
**Cache/Queue:** Redis  

---

## 1. Purpose

This document defines the HTTP API contract for Mayorx Content Engine.

The API is responsible for:

- Authentication and authorization
- Workspace management
- Brand and audience configuration
- Content strategy
- Content ideas and drafts
- AI content generation and evaluation
- Social account connections
- Scheduling
- Publishing
- Analytics
- Feedback and learning
- Notifications
- Audit logging

The API is the primary boundary between the web application and backend services.

---

# 2. API Principles

The API follows these principles:

1. **REST-first**
2. **Versioned endpoints**
3. **JSON request and response bodies**
4. **JWT-based authentication**
5. **Workspace-aware authorization**
6. **Consistent response structures**
7. **Predictable HTTP status codes**
8. **Idempotent operations where required**
9. **Pagination for collection endpoints**
10. **AI operations return structured data**
11. **Long-running operations are handled asynchronously**
12. **The API validates all AI-generated output before execution**

The AI layer can recommend or generate content, but it does not bypass application authorization, validation, scheduling, or publishing rules.

---

# 3. Base URL

Production:

```text
https://api.example.com/api/v1
```

Development:

```text
http://localhost:5000/api/v1
```

All endpoints described in this document are relative to:

```text
/api/v1
```

Example:

```http
GET /api/v1/content/ideas
```

---

# 4. Content Type

Requests containing JSON must use:

```http
Content-Type: application/json
```

Responses use:

```http
Content-Type: application/json
```

Dates and timestamps use ISO 8601 UTC format:

```text
2026-09-25T18:30:00Z
```

---

# 5. Authentication

## 5.1 Authentication Model

The API uses:

- Short-lived JWT access tokens
- Refresh tokens
- Secure token rotation
- Role-based authorization

Typical authentication flow:

```text
Login
  ↓
Access Token + Refresh Token
  ↓
API Requests
  ↓
Access Token Expires
  ↓
Refresh Token
  ↓
New Access Token
```

---

## 5.2 Authorization Header

Authenticated requests use:

```http
Authorization: Bearer <access_token>
```

Example:

```http
GET /api/v1/workspaces
Authorization: Bearer eyJ...
```

---

# 6. Authentication Endpoints

## POST `/auth/register`

Creates a user account.

### Request

```json
{
  "email": "user@example.com",
  "password": "SecurePassword123!",
  "firstName": "Mayowa",
  "lastName": "Binuyo"
}
```

### Response

```json
{
  "data": {
    "user": {
      "id": "uuid",
      "email": "user@example.com",
      "firstName": "Mayowa",
      "lastName": "Binuyo"
    }
  },
  "message": "Account created successfully"
}
```

**Status:** `201 Created`

---

## POST `/auth/login`

Authenticates a user.

### Request

```json
{
  "email": "user@example.com",
  "password": "SecurePassword123!"
}
```

### Response

```json
{
  "data": {
    "accessToken": "jwt-token",
    "refreshToken": "refresh-token",
    "expiresIn": 900,
    "user": {
      "id": "uuid",
      "email": "user@example.com",
      "firstName": "Mayowa",
      "lastName": "Binuyo"
    }
  },
  "message": "Login successful"
}
```

**Status:** `200 OK`

---

## POST `/auth/refresh`

Generates a new access token.

### Request

```json
{
  "refreshToken": "refresh-token"
}
```

### Response

```json
{
  "data": {
    "accessToken": "new-jwt-token",
    "refreshToken": "new-refresh-token",
    "expiresIn": 900
  },
  "message": "Token refreshed successfully"
}
```

---

## POST `/auth/logout`

Invalidates the current refresh session.

**Status:** `204 No Content`

---

## GET `/auth/me`

Returns the currently authenticated user.

### Response

```json
{
  "data": {
    "id": "uuid",
    "email": "user@example.com",
    "firstName": "Mayowa",
    "lastName": "Binuyo"
  }
}
```

---

# 7. Workspace Context

Mayorx Content Engine is designed to support multiple workspaces.

Most authenticated resources belong to a workspace.

The workspace can be supplied through:

```http
X-Workspace-Id: <workspace_uuid>
```

Example:

```http
X-Workspace-Id: 4d4c0b7c-...
```

The backend must verify that the authenticated user is a member of the requested workspace.

---

# 8. Workspace Roles

Supported roles:

| Role | Description |
|---|---|
| `Owner` | Full workspace control |
| `Admin` | Workspace and member management |
| `Editor` | Create and manage content |
| `Viewer` | Read-only access |

Authorization must be enforced server-side.

---

# 9. Common Response Format

Successful responses generally use:

```json
{
  "data": {},
  "message": "Operation completed successfully"
}
```

For collection responses:

```json
{
  "data": [],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalItems": 100,
    "totalPages": 5
  }
}
```

---

# 10. Error Response Format

All expected API errors should follow a consistent structure.

```json
{
  "error": {
    "code": "CONTENT_NOT_FOUND",
    "message": "The requested content could not be found",
    "details": {}
  },
  "traceId": "00-abc123..."
}
```

Validation example:

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "One or more validation errors occurred",
    "details": {
      "title": [
        "Title is required"
      ],
      "content": [
        "Content must not exceed the platform limit"
      ]
    }
  },
  "traceId": "00-abc123..."
}
```

---

# 11. HTTP Status Codes

| Status | Meaning |
|---|---|
| `200` | Successful request |
| `201` | Resource created |
| `202` | Request accepted for asynchronous processing |
| `204` | Successful request with no response body |
| `400` | Invalid request |
| `401` | Authentication required/invalid |
| `403` | Insufficient permissions |
| `404` | Resource not found |
| `409` | Conflict |
| `422` | Validation/business-rule failure |
| `429` | Rate limit exceeded |
| `500` | Internal server error |
| `502` | External provider failure |
| `503` | Service temporarily unavailable |

---

# 12. Pagination

Collection endpoints use:

```text
?page=1&pageSize=20
```

Example:

```http
GET /api/v1/content/ideas?page=1&pageSize=20
```

Default:

```text
page = 1
pageSize = 20
```

Maximum:

```text
pageSize = 100
```

The backend must enforce the maximum regardless of client input.

---

# 13. Filtering

Filtering uses query parameters.

Example:

```http
GET /api/v1/content/ideas?status=New&pillarId=uuid
```

Multiple filters may be combined.

---

# 14. Sorting

Collection endpoints may support:

```text
?sortBy=createdAt&sortOrder=desc
```

Example:

```http
GET /api/v1/content/drafts?sortBy=updatedAt&sortOrder=desc
```

Default ordering should be documented per endpoint.

---

# 15. Idempotency

Publishing and other operations that could accidentally execute twice should support:

```http
Idempotency-Key: <unique-key>
```

Example:

```http
POST /api/v1/publications
Idempotency-Key: 7c8e2...
```

The backend must prevent duplicate execution when the same key is reused for the same operation.

**Not implemented.** No current endpoint takes an `Idempotency-Key`, and
`POST /publications` is deferred. Scheduled publishing relies on the worker's
claim lease for single-writer behaviour instead, which prevents two workers from
publishing concurrently but does not make publishing exactly-once across a
crash.

---

# 16. Users

## GET `/users/me`

Returns the current user's profile.

## PATCH `/users/me`

Updates the current user's profile.

### Request

```json
{
  "firstName": "Mayowa",
  "lastName": "Binuyo",
  "timezone": "Africa/Lagos"
}
```

---

# 17. Workspaces

## GET `/workspaces`

Returns workspaces accessible to the authenticated user.

---

## POST `/workspaces`

Creates a workspace.

### Request

```json
{
  "name": "Mayorx Tech",
  "slug": "mayorx-tech"
}
```

**Status:** `201 Created`

---

## GET `/workspaces/{workspaceId}`

Returns workspace details.

---

## PATCH `/workspaces/{workspaceId}`

Updates workspace details.

---

## DELETE `/workspaces/{workspaceId}`

Deletes or archives a workspace according to retention rules.

**Required role:** `Owner`

---

# 18. Workspace Members

## GET `/workspaces/{workspaceId}/members`

Returns workspace members.

---

## POST `/workspaces/{workspaceId}/members`

Adds/invites a member.

### Request

```json
{
  "email": "editor@example.com",
  "role": "Editor"
}
```

---

## PATCH `/workspaces/{workspaceId}/members/{memberId}`

Updates a member's role.

---

## DELETE `/workspaces/{workspaceId}/members/{memberId}`

Removes a member.

---

# 19. Brand Profile

The brand profile defines how the AI understands the brand.

## GET `/brand`

Returns the workspace brand profile.

---

## PUT `/brand`

Creates or replaces the brand profile.

### Request

```json
{
  "name": "Mayorx Tech",
  "description": "A technology company building software and educating developers.",
  "mission": "Build useful technology and help people use it effectively.",
  "industry": "Technology",
  "location": "Nigeria",
  "websiteUrl": "https://example.com",
  "tone": "Direct, educational, practical",
  "formality": "CasualProfessional",
  "humorLevel": "Low",
  "emojiUsage": "Minimal",
  "technicalDepth": "Intermediate",
  "ctaStyle": "Conversational"
}
```

---

# 20. Audience Profiles

## GET `/audiences`

Returns audience profiles.

---

## POST `/audiences`

Creates an audience profile.

### Request

```json
{
  "name": "Early Career Developers",
  "description": "Developers building professional software skills.",
  "painPoints": [
    "Lack of practical experience",
    "Difficulty finding opportunities"
  ],
  "goals": [
    "Improve technical skills",
    "Build a portfolio",
    "Get hired"
  ],
  "interests": [
    "Software engineering",
    "AI",
    "Career development"
  ]
}
```

---

## GET `/audiences/{audienceId}`

Returns an audience profile.

---

## PATCH `/audiences/{audienceId}`

Updates an audience profile.

---

## DELETE `/audiences/{audienceId}`

Deletes an audience profile.

---

# 21. Content Strategy

## GET `/strategy`

Returns the current workspace content strategy.

---

## PUT `/strategy`

Creates or updates the workspace content strategy.

### Request

```json
{
  "primaryGoal": "AudienceGrowth",
  "secondaryGoals": [
    "Authority",
    "LeadGeneration"
  ],
  "postingFrequency": 5,
  "contentMix": {
    "educational": 40,
    "storytelling": 25,
    "opinion": 15,
    "caseStudy": 10,
    "promotional": 10
  }
}
```

---

# 22. Content Pillars

## GET `/strategy/pillars`

Returns content pillars, heaviest `weight` first and then by name so the order
is stable across identical calls.

### Filters

```text
isActive
```

### Response

```json
{
  "id": "…",
  "name": "AI for Business",
  "description": "Practical ways businesses can use AI.",
  "objective": "Establish credibility with operations leaders.",
  "audienceProfileId": "…",
  "weight": 30.00,
  "isActive": true,
  "ideaCount": 12,
  "createdAt": "2026-09-30T00:00:00Z",
  "updatedAt": "2026-09-30T00:00:00Z"
}
```

`ideaCount` is how many content ideas are currently attributed to the pillar.
Pillars belonging to other workspaces are never returned.

---

## GET `/strategy/pillars/{pillarId}`

Returns one content pillar. Returns `404 CONTENT_PILLAR_NOT_FOUND` when the
pillar does not exist in the caller's workspace.

---

## POST `/strategy/pillars`

Creates a content pillar. Returns `201` with the created pillar.

### Request

```json
{
  "name": "AI for Business",
  "description": "Practical ways businesses can use AI.",
  "objective": "Establish credibility with operations leaders.",
  "audienceProfileId": "…",
  "weight": 30.00,
  "isActive": true
}
```

`name` and `weight` are required. `weight` is a percentage in `(0, 100]`;
`objective`, `audienceProfileId` and `isActive` are optional, and `isActive`
defaults to `true`. Omitted `description` becomes an empty string rather than
`null`.

> **Deviation from the original sketch.** That sketch sent `"priority": 1`, an
> integer ordering key. The database schema specifies `Weight DECIMAL(5,2)` —
> "how frequently a pillar should appear relative to others" — which is a
> proportion, not an ordering. An ordinal `priority` would also be redundant:
> response order is derived from `weight`. The implemented field is therefore
> `weight`, matching the schema, rather than adding a second field that would
> disagree with it.

### Errors

| Status | Code | Cause |
|---|---|---|
| 400 | `INVALID_CONTENT_PILLAR_REQUEST` | Missing or out-of-range `name` or `weight` |
| 400 | `CONTENT_PILLAR_AUDIENCE_NOT_FOUND` | `audienceProfileId` is not in this workspace |
| 409 | `DUPLICATE_CONTENT_PILLAR_NAME` | A pillar with this name already exists in this workspace |

Names are compared case-insensitively, so "education" and "Education" are the
same name. `audienceProfileId` must belong to the caller's workspace; the
database cannot enforce this because `audience_profiles` allows only one
profile per workspace, so it is checked in the service.

---

## PATCH `/strategy/pillars/{pillarId}`

Updates a content pillar. Every field is optional and only the fields that are
supplied are changed, so a caller can reweight a pillar without resending its
name.

### Request

```json
{
  "name": "AI for Business",
  "weight": 40.00,
  "isActive": false
}
```

### Errors

| Status | Code | Cause |
|---|---|---|
| 400 | `INVALID_CONTENT_PILLAR_REQUEST` | Out-of-range `name` or `weight` |
| 400 | `CONTENT_PILLAR_AUDIENCE_NOT_FOUND` | `audienceProfileId` is not in this workspace |
| 404 | `CONTENT_PILLAR_NOT_FOUND` | No such pillar in this workspace |
| 409 | `DUPLICATE_CONTENT_PILLAR_NAME` | Renamed onto an existing pillar's name |

---

## DELETE `/strategy/pillars/{pillarId}`

Deletes a content pillar. Returns `204`, or `404 CONTENT_PILLAR_NOT_FOUND` when
the pillar does not exist in the caller's workspace.

Deleting a pillar never deletes content. Ideas attributed to it are kept and
have their `contentPillarId` set to `null`, in the same transaction as the
delete. A pillar with ideas is therefore deleted in one call — no need to
reassign the ideas first.

---

# 23. Content Ideas

## GET `/content/ideas`

Returns content ideas.

Supported filters:

```text
status
pillarId
source
createdFrom
createdTo
```

Example:

```http
GET /api/v1/content/ideas?status=New&pillarId=uuid
```

> `status`, `pillarId` and `platform` are implemented. `source`,
> `createdFrom` and `createdTo` are not: ideas have no `source` column, and
> there is no created-at range filter. Each response item reports the
> `contentPillarId` it is attributed to, which is `null` when the idea has not
> been attributed to a pillar. Passing a `pillarId` that does not exist in the
> caller's workspace returns an empty list rather than an error.

---

## POST `/content/ideas`

Creates a content idea manually.

### Request

```json
{
  "title": "How businesses can use AI without replacing their team",
  "description": "Explain practical AI automation opportunities.",
  "pillarId": "uuid",
  "source": "Manual"
}
```

---

## GET `/content/ideas/{ideaId}`

Returns an idea.

---

## PATCH `/content/ideas/{ideaId}`

Updates an idea.

---

## DELETE `/content/ideas/{ideaId}`

Deletes an idea.

---

# 24. AI Opportunity Discovery

## POST `/ai/opportunities/generate`

Generates content opportunities based on:

- Brand profile
- Audience profiles
- Content strategy
- Content pillars
- Existing content
- Recent research
- Historical performance

### Request

```json
{
  "count": 10,
  "platforms": [
    "LinkedIn",
    "X"
  ]
}
```

### Response

```json
{
  "data": {
    "opportunities": [
      {
        "title": "Why most businesses use AI backwards",
        "angle": "EducationalContrarian",
        "pillarId": "uuid",
        "audienceId": "uuid",
        "reason": "Strong alignment with current audience interests",
        "score": 87
      }
    ]
  }
}
```

**Status:** `200 OK`

The score is an internal prioritization signal, not a user-facing quality rating.

---

# 25. Content Drafts

## GET `/content/drafts`

Returns content drafts.

Filters:

```text
status
platform
pillarId
createdFrom
createdTo
```

---

## POST `/content/drafts`

Creates a draft manually.

### Request

```json
{
  "ideaId": "uuid",
  "platform": "LinkedIn",
  "content": "Your post content goes here."
}
```

---

## GET `/content/drafts/{draftId}`

Returns a draft and its metadata.

---

## PATCH `/content/drafts/{draftId}`

Updates a draft.

---

## DELETE `/content/drafts/{draftId}`

Deletes a draft.

---

# 26. AI Content Generation

## POST `/ai/content/generate`

Generates a draft from an existing content idea.

### Request

```json
{
  "ideaId": "uuid",
  "platform": "LinkedIn",
  "objective": "AudienceGrowth",
  "includeHashtags": true
}
```

### Response

```json
{
  "data": {
    "draftId": "uuid",
    "content": "Generated post...",
    "platform": "LinkedIn",
    "hashtags": [
      "#ArtificialIntelligence",
      "#Business"
    ],
    "reasoning": {
      "angle": "ProblemToSolution",
      "storyFramework": "ObservationInsight"
    }
  }
}
```

The API should not expose hidden chain-of-thought or private model reasoning. Any returned explanation must be a concise, user-safe rationale.

### Current Content-Idea Post Generation Endpoint

The current API exposes post generation from an approved, workspace-owned
content idea:

```http
POST /api/v1/content-ideas/{id}/generate-post
Authorization: Bearer <access_token>
Content-Type: application/json
```

Request:

```json
{
  "platform": "LinkedIn"
}
```

Only `LinkedIn` and `X` are supported. A draft is unique per content idea and
platform. If one already exists, the API returns it without another provider
request. Suggested or rejected ideas cannot generate posts; only approved ideas
can. Idea lookup, context loading, duplicate checks, and persistence are scoped
to the authenticated user's workspace.

Successful responses use the standard API envelope and include the generated
content, hook, CTA, hashtags, content type, storytelling structure, status, and
creation time. Invalid idea status returns `409 CONTENT_IDEA_INVALID_STATUS`;
an idea outside the current workspace returns `404 CONTENT_IDEA_NOT_FOUND`.

### Current Content Post Review and Editing Endpoints

The current API exposes a dedicated content post resource under
`/api/v1/content-posts` for listing, reading, editing, regenerating, approving,
and rejecting generated posts:

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/v1/content-posts` | List workspace posts, optionally filtered |
| `GET` | `/api/v1/content-posts/{id}` | Read one workspace post |
| `PATCH` | `/api/v1/content-posts/{id}` | Edit a draft's content |
| `POST` | `/api/v1/content-posts/{id}/regenerate` | Rerun generation on the same post |
| `POST` | `/api/v1/content-posts/{id}/approve` | Approve a draft |
| `POST` | `/api/v1/content-posts/{id}/reject` | Reject a draft |
| `POST` | `/api/v1/content-posts/{id}/schedule` | Schedule or reschedule an approved post |
| `POST` | `/api/v1/content-posts/{id}/evaluate` | Request a quality evaluation |

All routes require authentication and are scoped to the authenticated user's
workspace. A post belonging to another workspace is treated as not found.

`GET /api/v1/content-posts` accepts optional `status` and `platform` query
parameters and returns posts newest first.

#### Review lifecycle

The persisted lifecycle is `Draft`, `Approved`, `Scheduled`, `Published`, and
`Rejected`. The implemented review workflow is deliberately narrow and is
enforced server-side; the client cannot set `status` directly.

| Current status | Edit | Regenerate | Approve | Reject |
| --- | --- | --- | --- | --- |
| `Draft` | yes | yes | yes | yes |
| `Rejected` | no | yes, returns to `Draft` | no | no |
| `Approved` | no | no | no | no |
| `Scheduled` | no | no | no | no |
| `Published` | no | no | no | no |

Rationale:

- Only drafts are reviewable, so an approved post is never silently changed.
  Approved content cannot return to `Draft`.
- Scheduled and published posts are protected from every review mutation.
- Regenerating a rejected post is the explicit revision step: it reuses the same
  post identity, keeps the same content idea and platform, and moves the post
  back to `Draft` for another review pass. It never moves a post to `Published`.
- Any disallowed mutation returns `409 INVALID_CONTENT_POST_TRANSITION`.

#### Editable fields

`PATCH /api/v1/content-posts/{id}` replaces the full editable content set:

```json
{
  "content": "A hand written post",
  "hook": "A hand written hook",
  "callToAction": "What would you add?",
  "hashtags": ["AI", "SoftwareEngineering"],
  "contentType": "Insight",
  "storytellingStructure": "Hook → Insight → Takeaway → CTA"
}
```

Ids, workspace, content idea relation, platform, status, `createdAt`, and
`updatedAt` are system-controlled and are never accepted from the client.
Validation is shared with generation, so an edit is held to the same rules:
platform content limits measured in Unicode code points (280 for `X`, 3000
otherwise), hashtag limits (3 for `X`, 5 otherwise), hashtag trimming,
normalization, de-duplication, and generic-tag removal, required and length-
bounded hook, CTA, and storytelling structure, and a valid content type. An edit
that fails validation returns `400 INVALID_CONTENT_POST_CONTENT` and leaves the
stored post unchanged.

#### Regeneration

`POST /api/v1/content-posts/{id}/regenerate` takes no request body. It reuses
the existing content idea, platform, and post id, re-runs generation with the
configured provider and the content post response schema, and sets `updatedAt`.
It does not create a second post for the same idea and platform. Generation
requires the workspace brand profile, audience profile, and content strategy, so
an incomplete workspace returns `400 CONTENT_CONTEXT_INCOMPLETE`. Provider
failures surface as the existing `AI_*` and `AI_RESPONSE_INVALID` errors.

#### Responses

Every response uses the standard API envelope. A post payload includes `id`,
`contentIdeaId`, `platform`, `status`, `content`, `hook`, `callToAction`,
`hashtags`, `contentType`, `storytellingStructure`, `createdAt`, and
`updatedAt`. Successful edits, regenerations, approvals, and rejections advance
`updatedAt`; `createdAt` is never changed.

Responses also include two nullable fields describing the publish outcome, both
maintained by the worker rather than by any request: `publishedAt` and
`publishFailureReason`. See [Scheduled publishing](#scheduled-publishing).

| Code | Condition |
| --- | --- |
| `404 CONTENT_POST_NOT_FOUND` | Unknown id, or a post in another workspace |
| `409 INVALID_CONTENT_POST_TRANSITION` | The current status forbids the operation |
| `400 INVALID_CONTENT_POST_CONTENT` | Edited content failed shared validation |

Variants and approval history are not part of this workflow and are not yet
implemented.

#### Scheduling

`POST /api/v1/content-posts/{id}/schedule` records when a post is intended to
be published.

```json
{
  "scheduledAt": "2026-06-01T09:30:00Z"
}
```

Scheduling and scheduled publishing are implemented. There is no request that
publishes on demand; a post goes out only through its schedule.

| Code | Condition |
| --- | --- |
| `400 INVALID_CONTENT_POST_SCHEDULE_TIME` | `scheduledAt` is not in the future |
| `404 CONTENT_POST_NOT_FOUND` | Unknown id, or a post in another workspace |
| `409 INVALID_CONTENT_POST_TRANSITION` | The post status does not allow scheduling |

On success the post moves to `Scheduled`, `scheduledAt` is stored, `updatedAt`
advances, and the post content is preserved byte for byte. A successful
schedule also clears any previous publish claim so a rescheduled post can be
claimed again when it becomes due.

| Current status | Schedule |
| --- | --- |
| `Approved` | yes, moves to `Scheduled` |
| `Scheduled` | yes, reschedules and stays `Scheduled` |
| `Draft` | no, must be approved first |
| `Rejected` | no, must be approved again first |
| `Published` | no |

`scheduledAt` must be strictly in the future. A client cannot set `status` or
`scheduledAt` through `PATCH`; the update contract exposes only content fields.

Responses now include a nullable `scheduledAt`, a nullable `publishedAt`, and a
nullable `publishFailureReason`. `publishClaimedAt` is internal worker bookkeeping
and is never exposed.

#### Scheduled post worker

`apps/worker` polls for due scheduled posts, claims them, and publishes them. It
is orchestration only; all scheduling and publishing rules live in the
application layer.

Each poll:

1. Selects posts that are `Scheduled` and whose `scheduledAt` is at or before
   now, oldest first, bounded by the configured batch size.
2. Claims each one with a conditional update, so two workers cannot both win the
   same post.
3. Logs the claim with the post, workspace, idea, and platform.
4. Publishes the claimed post through the workspace's connected account, then
   records the outcome under that same claim.

A post that throws unexpectedly does not abandon the rest of the batch. It is
counted as not published, and because its claim is already recorded it becomes
eligible again when that claim expires.

The worker is system-scoped, not user-scoped: it scans across workspaces,
because a background job is not acting on behalf of one request. Each post is
claimed individually and logged with its own workspace, and no workspace's
content is ever attributed to another.

Configuration:

```json
{
  "Scheduling": {
    "PollIntervalSeconds": 30,
    "BatchSize": 25,
    "ClaimExpiryMinutes": 15
  }
}
```

A claim is a lease. If a worker dies mid-claim, the post becomes claimable
again once the claim is older than `ClaimExpiryMinutes`. This keeps a crashed
worker from stranding a post forever, and it doubles as the retry backoff: a
failed publish keeps its claim, so it is not attempted again until the claim
expires.

Multi-instance safety comes from the atomic conditional update in the database,
not from Redis. Redis exists in this solution only as a health check, and adding
a distributed lock layer before a publishing subsystem exists would be
premature.

Deliberately not implemented: manual publish, unscheduling a post, publication
history or a per-attempt record, calendar or drag-to-reschedule UX, token
refresh, and content variants or A/B testing.

#### Quality evaluation

`POST /api/v1/content-posts/{id}/evaluate` takes no request body. It is
**on-demand and advisory**: the generation path never calls it, and it never
changes the post's status, content, or timestamps. The user remains responsible
for approving or rejecting.

Evaluation asks whether a post that already passed hard validation is actually
worth recommending. Hard validation and quality evaluation are deliberately
separate: hard validation answers whether the output is structurally and
technically valid (platform character limits, content type, hashtag format and
limits, required fields, platform consistency), while quality evaluation answers
whether the content is specific, grounded, and on-strategy.

The evaluator receives the workspace `BrandProfile`, `AudienceProfile`,
`ContentStrategy`, the source `ContentIdea`, the post's platform and content, and
uses a dedicated response schema. The provider layer remains task-agnostic.

Evaluated dimensions:

| Dimension | Question |
| --- | --- |
| `strategicFit` | Does it deliver the intended idea and match the content strategy? |
| `audienceFit` | Does it actually address the target audience? |
| `hookStrength` | Is the opening strong enough to earn attention? |
| `value` | Is it useful and specific rather than generic filler? |
| `clarity` | Is the point clear to a specific reader? |
| `originality` | Is it specific to this idea rather than reusable filler? |
| `storytelling` | Does the structure suit the declared content type? |
| `platformFit` | Does it match the platform's format and length expectations? |
| `voiceMatch` | Does it match the brand positioning and tone? |
| `factualReliability` | Does it avoid invented claims, statistics, and experience? |

Response:

```json
{
  "data": {
    "contentPostId": "uuid",
    "overallScore": 88.00,
    "recommendation": "MinorEdit",
    "passed": true,
    "passThreshold": 70,
    "dimensions": {
      "strategicFit": 90,
      "audienceFit": 86,
      "hookStrength": 82,
      "value": 91,
      "clarity": 94,
      "originality": 80,
      "storytelling": 84,
      "platformFit": 89,
      "voiceMatch": 93,
      "factualReliability": 96
    },
    "strengths": ["Specific to the audience"],
    "issues": ["CTA is weak"],
    "repetition": {
      "isRepetitive": false,
      "similarity": 0.12,
      "similarToContentPostId": null,
      "threshold": 0.75
    }
  },
  "message": "Content post evaluated successfully."
}
```

Scoring is computed by the application, not asserted by the model.
`overallScore` is the mean of the ten dimensions, rounded to two decimal places.
`recommendation` is derived from the documented content quality bands:

| Overall score | Recommendation |
| --- | --- |
| 90-100 | `Accept` |
| 80-89 | `MinorEdit` |
| 70-79 | `ConsiderImprovement` |
| 60-69 | `Revise` |
| below 60 | `Rewrite` |

`passed` compares `overallScore` against the configurable `PassThreshold`, which
defaults to `70` and is returned in the response so the boundary is never
implicit. Both thresholds are configurable under `ContentQuality`:

```json
{
  "ContentQuality": {
    "PassThreshold": 70,
    "RepetitionSimilarityThreshold": 0.75
  }
}
```

`passed` is a recommendation, not a gate. It never approves a post, never blocks
a save, and never changes a status. A post can pass validation and still fail
evaluation, and the user can approve it anyway.

Repetition is a deterministic token-set Jaccard comparison against the
workspace's other posts on the same platform, excluding the post itself. It
requires no embeddings. Semantic similarity via vector search is a later
capability, not part of this layer.

Evaluation results are **not persisted**. They are returned to the caller for
the current request only.

| Code | Condition |
| --- | --- |
| `404 CONTENT_POST_NOT_FOUND` | Unknown id, or a post in another workspace |
| `400 CONTENT_CONTEXT_INCOMPLETE` | Workspace brand, audience, or strategy is missing |
| `502 CONTENT_EVALUATION_FAILED` | The provider returned an unusable evaluation |

`CONTENT_EVALUATION_FAILED` is deliberately distinct from the `AI_RESPONSE_INVALID`
code used during generation, so an evaluator failure is never mistaken for a
generation failure. Provider-level errors continue to surface as the existing
`AI_*` codes.

Deliberately deferred: persisted evaluation history, the `AI-language-risk`
dimension, `content_feedback`, embedding-based similarity, and any automatic
quality gate.

---

# 27. Content Evaluation

## POST `/ai/content/evaluate`

Evaluates a draft.

### Request

```json
{
  "draftId": "uuid"
}
```

### Response

```json
{
  "data": {
    "evaluationId": "uuid",
    "dimensions": {
      "strategicFit": 90,
      "audienceFit": 86,
      "hookStrength": 82,
      "value": 91,
      "clarity": 94,
      "originality": 80,
      "storytelling": 84,
      "platformFit": 89,
      "voiceMatch": 93,
      "factualReliability": 96
    },
    "issues": [
      "CTA could encourage more discussion."
    ],
    "recommendation": "Revise"
  }
}
```

Internal evaluation values may be stored for model improvement, but should not be treated as objective measures of content quality.

---

# 28. AI Content Revision

## POST `/ai/content/{draftId}/revise`

Requests an AI revision based on evaluation or user feedback.

### Request

```json
{
  "instructions": "Make the opening more conversational and reduce the technical language."
}
```

### Response

```json
{
  "data": {
    "draftId": "uuid",
    "content": "Revised content..."
  },
  "message": "Draft revised successfully"
}
```

Revision limits should be enforced to control cost and prevent endless AI loops.

---

# 29. Content Variants

A single draft may have platform-specific variants.

## GET `/content/drafts/{draftId}/variants`

Returns variants.

---

## POST `/content/drafts/{draftId}/variants`

Creates a platform variant.

### Request

```json
{
  "platform": "X",
  "content": "Platform-specific version..."
}
```

---

## PATCH `/content/variants/{variantId}`

Updates a variant.

---

# 30. Content Approval

## POST `/content/drafts/{draftId}/approve`

Approves a draft for scheduling/publishing.

### Response

```json
{
  "data": {
    "draftId": "uuid",
    "status": "Approved"
  },
  "message": "Content approved successfully"
}
```

Only approved content can enter the publishing pipeline.

---

# 31. Research

Research is optional and should only be triggered when useful.

## POST `/ai/research`

Starts research for a topic.

### Request

```json
{
  "query": "AI automation trends for small businesses",
  "purpose": "ContentResearch"
}
```

For long-running research, the API may return:

```json
{
  "data": {
    "jobId": "uuid",
    "status": "Queued"
  }
}
```

**Status:** `202 Accepted`

---

## GET `/ai/research/{jobId}`

Returns research job status and results when completed.

---

## GET `/research/sources`

Returns stored research sources.

---
# 32. Social Accounts

Social accounts represent external accounts connected to the workspace.

Supported V1 platforms:

- X
- LinkedIn

**Connection management is implemented. Publishing is not.** A connected account
stores an authorized, encrypted credential so the system can act as that account
later, but nothing is posted to the platform yet.

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/v1/social/accounts` | List the workspace's connected accounts |
| `GET` | `/api/v1/social/{platform}/connect` | Start the OAuth flow |
| `GET` | `/api/v1/social/{platform}/callback` | OAuth redirect target |
| `DELETE` | `/api/v1/social/accounts/{id}` | Disconnect and destroy credentials |

`{platform}` accepts `x` or `linkedin`, case-insensitively. Any other value is
rejected with `400 SOCIAL_PLATFORM_NOT_SUPPORTED`. Platform matching is not
fuzzy, so `twitter` and `facebook` are not accepted as aliases.

Account status values:

| Status | Meaning |
| --- | --- |
| `Connected` | Authorized and usable |
| `Expired` | The stored credential is past its expiry |
| `Revoked` | The platform withdrew access |
| `Error` | The last connection attempt failed |
| `Disconnected` | Credentials were destroyed; the row is kept as history |

---

## GET `/api/v1/social/accounts`

Returns the calling workspace's connected social accounts. Requires
authentication.

Example response:

```json
{
  "data": [
    {
      "id": "uuid",
      "platform": "LinkedIn",
      "username": "mayorx-tech",
      "displayName": "Mayorx Tech",
      "profileUrl": "https://www.linkedin.com/company/mayorx-tech",
      "status": "Connected",
      "tokenExpiresAt": "2026-10-02T18:30:00Z",
      "connectedAt": "2026-09-25T18:30:00Z",
      "updatedAt": "2026-09-25T18:30:00Z"
    }
  ],
  "message": "Social accounts retrieved successfully."
}
```

**Sensitive OAuth tokens must never be returned.** The response contract has no
access token, refresh token, or encrypted-credential property of any kind, and
`tokenExpiresAt` is the only token-related field exposed.

Accounts are scoped to the caller's workspace. An account belonging to another
workspace is never listed, readable, or disconnectable.

---

## GET `/api/v1/social/{platform}/connect`

Starts the OAuth flow and returns the provider authorization URL as JSON. A
client is expected to redirect the browser to `authorizationUrl`.

Requires authentication. The redirect URI sent to the provider is read from
server configuration only; it is never taken from the request.

```json
{
  "data": {
    "platform": "LinkedIn",
    "authorizationUrl": "https://www.linkedin.com/oauth/v2/authorization?...",
    "expiresAt": "2026-09-30T12:10:00Z"
  },
  "message": "Social authorization URL created."
}
```

X uses PKCE with `S256`; the verifier is stored with the state and never leaves
the server. LinkedIn's authorization-code flow does not use PKCE.

| Code | Condition |
| --- | --- |
| `400 SOCIAL_PLATFORM_NOT_SUPPORTED` | The platform is not X or LinkedIn |
| `503 SOCIAL_PLATFORM_NOT_CONFIGURED` | The platform has no client credentials |
| `502 SOCIAL_PROVIDER_ERROR` | The provider rejected the request |

A platform with no credentials is reported as not configured rather than failing
mid-flow. Partially configured platforms are rejected at startup.

---

## GET `/api/v1/social/{platform}/callback`

The OAuth redirect target. **This endpoint is anonymous by design**: the
provider redirects the browser here without an `Authorization` header.

The workspace is therefore taken from the stored, single-use OAuth state and
never from the caller. Requesting another workspace's account is not possible
without that workspace's state value.

Query parameters: `code`, `state`, and `error`.

The callback:

1. Rejects a provider-reported `error` with `400 SOCIAL_OAUTH_DENIED`
2. Validates the single-use `state` and reads the workspace and platform from it
3. Exchanges the authorization code
4. Retrieves the provider profile
5. Encrypts the tokens
6. Creates or updates the social account in the state's workspace

The callback currently returns a JSON success envelope rather than redirecting to
a frontend. It is designed to be browser-navigable, so a frontend redirect can
be added without changing the flow.

| Code | Condition |
| --- | --- |
| `400 SOCIAL_OAUTH_DENIED` | The provider returned an error |
| `400 SOCIAL_STATE_INVALID` | The state is missing, unknown, expired, or already used |
| `400 SOCIAL_AUTHORIZATION_CODE_MISSING` | No `code` was supplied |
| `400 SOCIAL_PLATFORM_MISMATCH` | The state was issued for a different platform |
| `503 SOCIAL_PLATFORM_NOT_CONFIGURED` | The platform has no client credentials |
| `502 SOCIAL_PROVIDER_ERROR` | Token exchange or profile lookup failed |

The state is consumed before it is used, so a replayed callback fails. Provider
error details, including token endpoint response bodies, are never logged or
returned.

Reconnecting an account that is already connected re-authorizes it in place
instead of creating a duplicate.

---

## DELETE `/api/v1/social/accounts/{id}`

Disconnects a social account. Requires authentication.

On success the account moves to `Disconnected` and its stored access and refresh
tokens are destroyed. The row is kept as connection history rather than deleted,
so `platformAccountId` cannot silently be reconnected under a new account id.

| Code | Condition |
| --- | --- |
| `404 SOCIAL_ACCOUNT_NOT_FOUND` | Unknown id, or an account in another workspace |
| `409 SOCIAL_ACCOUNT_NOT_DISCONNECTABLE` | The account is already disconnected |

A disconnected account can be reconnected, which creates a fresh authorization
and a new credential. Scheduled content that referenced a disconnected account
still needs to be handled safely when publishing is implemented; that handling is
not part of this milestone.

---

# 34. Schedules

## GET `/schedules`

Returns scheduled posts.

Filters:

```text
from
to
platform
status
```

---

## POST `/schedules`

Schedules approved content.

### Request

```json
{
  "contentVariantId": "uuid",
  "socialAccountId": "uuid",
  "scheduledFor": "2026-09-28T17:30:00Z"
}
```

The backend must verify:

- Content exists
- Content is approved
- Social account is connected
- User has permission
- Scheduled time is valid
- Content has not already been published
- Platform requirements are satisfied

---

## GET `/schedules/{scheduleId}`

Returns schedule details.

---

## PATCH `/schedules/{scheduleId}`

Reschedules a post.

---

## DELETE `/schedules/{scheduleId}`

Cancels a scheduled publication.

---

# 35. Publishing

Publishing itself is implemented, but only as part of the scheduled post worker.
There is **no publication HTTP API**. A post is published by being scheduled and
becoming due; nothing triggers a publish from a request.

What a content post exposes is its outcome, on the post itself:

| Field | Meaning |
| --- | --- |
| `status` | `Published` once the platform accepted the post |
| `publishedAt` | When that happened |
| `publishFailureReason` | Why the last attempt failed, or `null` |

A failed publish does **not** change `status`. The post stays `Scheduled` with a
recorded `publishFailureReason`, and it is attempted again once its claim
expires. This is what distinguishes "nothing is wrong" from "this keeps failing
and here is why".

`publishFailureReason` is one of:

| Value | Meaning |
| --- | --- |
| `PlatformNotSupported` | No publisher exists for the platform |
| `PlatformNotConfigured` | The platform has no client configuration |
| `SocialAccountNotFound` | The workspace has no account for the platform |
| `SocialAccountNotConnected` | Accounts exist, but none is connected |
| `SocialAccountAmbiguous` | More than one connected account, so the target is unclear |
| `CredentialsUnavailable` | No stored token, or it could not be decrypted |
| `CredentialsExpired` | The token is past its expiry, or the account is expired |
| `CredentialsRejected` | The platform refused the stored credentials |
| `ProviderUnavailable` | Timeout, rate limit, or provider error; retrying may work |
| `ProviderResponseInvalid` | The platform rejected the publish or replied unreadably |
| `PublishClaimLost` | Another worker owned the post when the outcome was written |
| `ContentPostNotScheduled` | The post was not in a publishable state when the attempt ran |

Account selection is by `(workspace, platform)`. Exactly one connected account
must exist: none, or more than one, is a failure rather than a guess, because
picking one of several accounts would publish to an account nobody chose.

The published text is composed deterministically from the post's `content`, its
optional `callToAction`, and its `hashtags`, joined by blank lines. A missing
part is omitted rather than leaving a gap, so there are no variants and nothing
is randomised between attempts.

## Deferred

The following remain design intent and are **not implemented**:

- `POST /publications` — on-demand publishing with an `Idempotency-Key`.
- `GET /publications` and `GET /publications/{id}` — publication history.
  There is no `publications` table, so there is no per-attempt record. The
  platform's post id for the successful publish is stored on
  `content_posts.published_external_post_id`, so metrics can be fetched for it,
  but the publish attempts themselves are not kept.
- Token refresh. A publish uses the stored access token as-is; an expired token
  fails rather than being renewed.
- Exactly-once publishing. If a worker dies after the platform accepted a post
  but before the status is written, the post stays `Scheduled` and may be
  published again.

---

# 36. Analytics

> **Implementation note.** Analytics is implemented as a **read-only reporting
> surface** over a `content_post_metrics` table, and the metric **ingestion**
> pipeline described in the roadmap ("Analytics synchronization", a worker that
> fetches platform metrics) is implemented as well. A metric reports `null` only
> when the platform does not expose it or has not reported it yet.
>
> Metrics are read with the same access token that published the post, so the
> platform's own post id is stored at publish time on
> `content_posts.published_external_post_id`. Posts published before that column
> existed are never back-filled and stay unmeasured.
>
> X exposes every metric below except per-post follower gain, in one request per
> post. LinkedIn exposes them through `memberCreatorPostAnalytics`, which
> requires the Community Management API product and is therefore optional per
> deployment; without it the post's metrics stay `null` rather than being
> reported as zero.
## GET `/analytics/overview`

Returns high-level workspace analytics.

```http
GET /api/v1/analytics/overview?platform=LinkedIn&from=2026-09-01T00:00:00Z&to=2026-09-25T23:59:59Z&pillarId=…
```

All four query parameters are optional.

| Parameter | Type | Notes |
|---|---|---|
| `platform` | `LinkedIn` \| `X` | Restricts to one platform |
| `from` | ISO-8601 | Inclusive lower bound on `PublishedAt` |
| `to` | ISO-8601 | Inclusive upper bound on `PublishedAt` |
| `pillarId` | UUID | Restricts to posts whose originating idea is attributed to this pillar |

A post has no pillar of its own, so `pillarId` reaches through the idea the post
was generated from. Ideas that were never attributed to a pillar, and pillars
from other workspaces, are excluded. This was previously documented as
deferred because no pillar table existed to reference; it is now available
because `content_pillars` is implemented.

Omitting `from` and `to` means **all time**, not a default window; the `period`
in the response then reports `null` bounds so the caller can see which window
was actually used.

Example (no metric data recorded yet, so every metric is `null`):

```json
{
  "data": {
    "period": {
      "from": "2026-09-01T00:00:00Z",
      "to": "2026-09-25T23:59:59Z"
    },
    "postsPublished": 18,
    "impressions": null,
    "likes": null,
    "comments": null,
    "shares": null,
    "clicks": null,
    "profileVisits": null,
    "followersGained": null,
    "engagementRate": null
  }
}
```

`profileVisits` is not in the original sketch of this response but is listed as
a normalized metric by the development plan and as a column by the database
schema, so it is included.

### What `null` means

Every metric is nullable, and that is deliberate. The design requires three
states to stay distinct, and a single number cannot carry them:

| Reported value | Meaning |
|---|---|
| `null` | Unknown. No published post in the period has reported this metric. |
| `0` | Measured. A platform reported that the value really is zero. |
| a number | Measured. |

`postsPublished` is always a number, because the count comes from the posts
table rather than from any platform.

### `engagementRate`

Derived, never stored:

```text
engagementRate = (likes + comments + shares) / impressions * 100
```

Two consequences worth knowing:

- It is `null` when `impressions` is `null` or `0`, because no rate is defined
  then — even if every other metric was genuinely measured.
- Clicks, profile visits and followers are **excluded**. They describe reach and
  audience growth rather than engagement with the post.
- It is computed from the summed totals, not as an average of per-post rates.
  Averaging would weight a post with 3 impressions the same as one with 30,000.

---

## GET `/analytics/posts`

Returns post-level performance.

```http
GET /api/v1/analytics/posts?platform=X&from=2026-09-01T00:00:00Z&to=2026-09-30T00:00:00Z&pillarId=…
```

Takes the same four parameters as `/analytics/overview`.

```json
{
  "data": [
    {
      "contentPostId": "8f14e45f-ceea-467a-9ba3-1f2b3c4d5e6f",
      "platform": "X",
      "contentPillarId": "3b7c1d2e-4a5f-4c6d-8e9f-0a1b2c3d4e5f",
      "contentPillarName": "AI for Business",
      "publishedAt": "2026-09-12T09:00:00Z",
      "recordedAt": "2026-09-12T15:00:00Z",
      "impressions": 1200,
      "likes": 84,
      "comments": 9,
      "shares": 3,
      "clicks": 41,
      "profileVisits": 12,
      "followersGained": 2,
      "engagementRate": 8.0
    }
  ]
}
```

`contentPillarId` and `contentPillarName` are both `null` when the originating
idea was never attributed to a pillar. `GET /analytics/posts/{id}` returns
these two fields too.

`recordedAt` is `null` when the post has no metric snapshot at all, which is
how a caller distinguishes "no data yet" from "zero".

### Ordering

`publishedAt` descending, with `contentPostId` ascending as the tie-break, so
repeated calls over unchanged data always return the same order.

---

## GET `/analytics/posts/{contentPostId}`

Returns metrics for a single published post.

```http
GET /api/v1/analytics/posts/8f14e45f-ceea-467a-9ba3-1f2b3c4d5e6f
```

The original sketch named this parameter `{publicationId}`. There is no
publication entity — publishing records its outcome on the content post — so
the route is keyed by content post id.

Returns `404 ANALYTICS_POST_NOT_FOUND` when the post does not exist, belongs to
another workspace, **or was never published**. A post that exists but has no
performance data of its own is still `200` with `null` metrics, because "published
with no data yet" and "not a published post" are different answers.

---

## Errors

| Code | Status | Cause |
|---|---|---|
| `INVALID_ANALYTICS_PERIOD` | 400 | `from` is later than `to` |

---

## Analytics rules that are not in the parameter list

- Only posts whose status is `Published` are counted. Draft, approved, scheduled
  and rejected posts are invisible here, including a post whose publish failed —
  a failed publish leaves the status `Scheduled` with a `PublishFailureReason`, so
  it must not be reported as published.
- A post may have several metric snapshots, because platform statistics keep
  moving. Reads use the **latest** `recordedAt`, with the higher `id` as the
  tie-break when two snapshots share a timestamp.
- The period filters on `PublishedAt`, not `CreatedAt`. A post is part of a
  period by when it went out, not by when it was written.
- Everything is scoped to the caller's workspace. The database enforces this
  too: `content_post_metrics` references its post by `(WorkspaceId, ContentPostId)`,
  so a metric row cannot point at another workspace's post even by mistake.

---

## Deferred

- **`pillarId` filter.** Previously omitted because no content pillar entity
  existed. It is implemented on both `/content/ideas` and `/analytics/overview`
  and `/analytics/posts`, since `content_pillars` and
  `content_ideas.ContentPillarId` both exist.
- **Trend, platform-comparison and pillar-performance breakdowns.** Not built.
  The `pillarId` filter is a way to *scope* existing totals to one pillar, not
  a grouped per-pillar comparison.
- **LinkedIn member post metrics.** Supported in code through
  `memberCreatorPostAnalytics`, but that endpoint requires LinkedIn's Community
  Management API product, which is not part of the default connection scopes.
  Without it the read is rejected and the post's metrics stay `null`.
- **Live platform verification.** The sync pipeline is covered by unit tests and
  the conditional database writes are verified against PostgreSQL, but no live
  LinkedIn or X credentials are available, so real provider responses are
  unverified.

---

# 37. Performance Learning

Performance learning is the loop that turns measured performance into something
a publisher can act on. It has two halves that are deliberately not the same
thing:

- the **observation** is computed in code from real metric totals, and
- the **recommendation** is the model's reading of that evidence.

They are stored and returned in separate fields, so a reader can accept one
without the other and can tell at a glance which half is a measurement.

## GET `/learning/insights`

Returns the workspace's stored learning signals, newest first.

Query parameters:

- `limit` — optional, default and maximum 200. Signals accumulate, so this is a
  recent-history read rather than a full history export.

Example:

```json
{
  "data": [
    {
      "id": "1f0c2c1e-9a1b-4d4e-8a0b-2f2f0f5b1a44",
      "type": "PLATFORM_ENGAGEMENT",
      "dimension": "Platform",
      "observation": "Across 5 measured posts, Platform LinkedIn averaged 6% engagement over 3 posts, compared with 2% for X over 2 posts.",
      "recommendation": "Worth trying one more LinkedIn-only post next week before changing anything else.",
      "evidenceCount": 5,
      "confidence": 50.0,
      "createdAt": "2026-09-25T18:30:00Z"
    }
  ],
  "message": "Learning insights retrieved successfully."
}
```

`observation` is written by code from the metric totals and can be recomputed
from them at any time. `recommendation` is inference. `confidence` is a
strength-of-evidence indicator from 0 to 100, derived from the size of the
smaller compared group; it is not a probability and no significance test is
computed anywhere in this feature.

## POST `/learning/insights`

Runs the loop once over the workspace's measured posts. The run compares the
posts, asks the model what each difference might mean, and stores one signal per
finding. There is no request body: the window is the configured lookback, so a
caller cannot narrow it to a smaller and more flattering sample.

Example:

```json
{
  "data": {
    "from": "2026-06-27T18:30:00Z",
    "to": "2026-09-25T18:30:00Z",
    "postsInWindow": 12,
    "measuredPosts": 9,
    "minimumMeasuredPosts": 5,
    "hasSufficientData": true,
    "insights": [
      {
        "id": "1f0c2c1e-9a1b-4d4e-8a0b-2f2f0f5b1a44",
        "type": "PLATFORM_ENGAGEMENT",
        "dimension": "Platform",
        "observation": "Across 5 measured posts, Platform LinkedIn averaged 6% engagement over 3 posts, compared with 2% for X over 2 posts.",
        "recommendation": "Worth trying one more LinkedIn-only post next week before changing anything else.",
        "evidenceCount": 5,
        "confidence": 50.0,
        "createdAt": "2026-09-25T18:30:00Z"
      }
    ]
  },
  "message": "Learning insights generated successfully."
}
```

### What a run does

1. Reads the workspace's published posts inside the lookback window, each with
   its current metric snapshot, through the same reader the analytics endpoints
   use. Posts that were never measured are counted in `postsInWindow` and
   contribute no evidence.
2. Groups the measured posts by one dimension at a time — platform, content
   type, content pillar, and day of week in UTC — and keeps only groups that can
   support a comparison: at least `MinimumPostsPerGroup` posts, and an
   engagement rate, which needs impressions.
3. Reports a difference for a dimension only when the strongest group is at
   least `MinimumEngagementRateGap` above the weakest. Otherwise the dimension
   is silent.
4. Asks the model for one recommendation per finding, and stores the finding.

Hour of day is not analysed. Twenty-four buckets would leave almost every bucket
holding a single post, which cannot support a comparison at any threshold. Day of
week is reported in UTC because the audience timezone is not stored anywhere the
publisher can read it.

### Insufficient data

When there are fewer than `MinimumMeasuredPosts` measured posts, or no dimension
has two comparable groups, the run completes with `hasSufficientData: false`, an
empty `insights` array, and a message saying why. No AI call is made and no
signal is written. This is a successful run that had nothing to say, not an
error.

```json
{
  "data": {
    "from": "2026-06-27T18:30:00Z",
    "to": "2026-09-25T18:30:00Z",
    "postsInWindow": 5,
    "measuredPosts": 2,
    "minimumMeasuredPosts": 5,
    "hasSufficientData": false,
    "insights": []
  },
  "message": "Not enough measured performance to generate learning insights yet."
}
```

### Why this runs in the request, not the worker

The run is API-triggered rather than a background poll, and the reason is
structural rather than a matter of preference:

- a run makes AI calls, and every other AI operation in the system is
  request-triggered;
- an AI run row is recorded against the workspace of the request that caused it,
  and the worker has no request and therefore no workspace to record against.

Adding AI to the worker would mean inventing a workspace-explicit run recorder
and a background AI configuration, both of which are outside what this loop
needs. The publisher asking for a run is also the only moment the result is
wanted. A scheduled run can be added when there is a demonstrated need for
signals to appear without being asked for.

### Configuration

```json
{
  "LearningLoop": {
    "LookbackDays": 90,
    "MinimumMeasuredPosts": 5,
    "MinimumPostsPerGroup": 2,
    "MinimumEngagementRateGap": 0.25
  }
}
```

Every value exists to make the loop say less. A workspace with two published
posts has no pattern, only two data points, and a comparison drawn from it would
read as a finding while being noise.

### Scope notes

- **A run is a series of model calls.** One call per finding, made one at a
  time. A provider failure fails the run: no partial set of signals is stored
  against an analysis that was not completed. A workspace with enough measured
  posts to produce findings needs a configured AI provider.
- **Correlation, not causation.** Posts differ in many ways at once. The
  observation says what was measured and never why, and the prompt instructs the
  model to recommend something to try rather than to assert a cause.
- **Signals are append-only.** A later run over a longer history is a new
  observation, not a correction of an earlier one, so repeated runs accumulate
  rows and none is updated in place.
- **No live provider verification.** The loop is covered by unit tests and its
  database behaviour is verified against PostgreSQL, but no live AI provider
  call has been made, so a real model response is unverified.

---

# 38. Content Feedback

## POST `/content/{contentId}/feedback`

Records user feedback.

### Request

```json
{
  "type": "Edited",
  "comment": "Make this sound more like how I normally speak."
}
```

Supported feedback types may include:

```text
Approved
Edited
Rejected
Regenerated
Commented
```

---

# 39. Notifications

## GET `/notifications`

Returns user notifications.

---

## PATCH `/notifications/{notificationId}/read`

Marks a notification as read.

---

## POST `/notifications/read-all`

Marks all notifications as read.

---

# 40. Audit Logs

## GET `/audit-logs`

Returns workspace audit events.

Example:

```json
{
  "data": [
    {
      "id": "uuid",
      "action": "ContentApproved",
      "resourceType": "ContentPost",
      "resourceId": "uuid",
      "performedBy": "uuid",
      "ipAddress": "203.0.113.5",
      "userAgent": "string",
      "oldValues": "{\"status\":\"Draft\"}",
      "newValues": "{\"status\":\"Approved\"}",
      "createdAt": "2026-09-25T18:30:00Z"
    }
  ]
}
```

Audit logs are read-only through the API.

Notes on the implemented endpoint:

- `resourceType` records the table that was acted on. The example above says
  `ContentPost` rather than `ContentDraft`, because there is no draft table:
  drafts and platform variants both live on `content_posts`.
- `action` values are PascalCase, matching the enum names in
  `apps/api/Domain/Enums/AuditAction.cs`.
- `oldValues` and `newValues` are serialized JSON objects, returned as strings
  because the `audit_logs` columns are `jsonb`.
- `performedBy` is null for a system-scoped action. This currently applies to
  `AccountConnected` only: the social OAuth callback is anonymous and resolves
  its workspace from signed state rather than from a token, so there is no
  authenticated user to attribute the connect to.
- Optional query filters: `action`, `resourceType`, and `resourceId`. The last
  two match the `audit_logs_entity` index.
- Results are newest first. A single call returns at most 200 rows; the
  endpoint is not paginated yet, so a workspace with more history than that
  needs pagination added before the cap can be lifted.

---

# 41. Jobs

Long-running operations use background jobs.

Examples:

- AI generation
- Research
- Analytics synchronization
- Social publishing
- Performance analysis

## GET `/jobs/{jobId}`

Returns job status.

### Response

```json
{
  "data": {
    "id": "uuid",
    "type": "ContentGeneration",
    "status": "Completed",
    "progress": 100,
    "createdAt": "2026-09-25T18:30:00Z",
    "completedAt": "2026-09-25T18:31:12Z"
  }
}
```

Supported statuses:

```text
Queued
Running
Completed
Failed
Cancelled
```

---

# 42. Asynchronous Operations

Operations expected to take longer than a normal HTTP request should return:

```http
202 Accepted
```

Example:

```json
{
  "data": {
    "jobId": "uuid",
    "status": "Queued"
  }
}
```

The client can poll:

```http
GET /api/v1/jobs/{jobId}
```

The system may later introduce WebSockets or Server-Sent Events for real-time job updates.

---

# 43. Publishing Pipeline

Publishing follows:

```text
Approved Content
       ↓
Schedule / Immediate Publish
       ↓
Validation
       ↓
Create Publication
       ↓
Queue Job
       ↓
Worker
       ↓
Social Platform Adapter
       ↓
External Platform
       ↓
Publication Result
       ↓
Analytics Sync
```

The HTTP API does not directly perform all external publishing work inside the request lifecycle.

---

# 44. Internal Events

The worker system communicates through internal jobs/events.

Examples:

```text
ContentGenerationRequested
ContentEvaluationRequested
ResearchRequested
PublicationScheduled
PublicationRequested
PublicationSucceeded
PublicationFailed
AnalyticsSyncRequested
```

`PerformanceAnalysisRequested` and `LearningSignalCreated` are not event types.
The learning loop is request-triggered, so a run has no job to publish and a
stored signal has no subscriber to notify. Adding them to this list would
describe a messaging path the system does not have.

Internal event payloads should contain IDs and necessary metadata rather than large duplicated objects.

Example:

```json
{
  "eventId": "uuid",
  "eventType": "PublicationRequested",
  "workspaceId": "uuid",
  "publicationId": "uuid",
  "occurredAt": "2026-09-25T18:30:00Z"
}
```

---

# 45. API ↔ Worker Boundary

The API should enqueue background work rather than directly executing long-running tasks.

Example:

```text
POST /ai/content/generate
        ↓
Create AI Run
        ↓
Create Job
        ↓
Queue Job in Redis
        ↓
Worker processes job
        ↓
Persist result in PostgreSQL
        ↓
Frontend retrieves result
```

---

# 46. Security Requirements

## Authentication

- JWT access tokens
- Secure refresh token handling
- Refresh token rotation
- Token expiration
- Revocation support

## Authorization

Every workspace resource must verify:

```text
Authenticated User
        ↓
Workspace Membership
        ↓
Required Role
        ↓
Resource Access
```

Never rely on frontend role checks for security.

---

## OAuth Security

Social OAuth credentials must:

- Never be returned through API responses
- Never be logged
- Be encrypted at rest
- Use secure OAuth state validation
- Use appropriate redirect URI validation
- Support token refresh where available

### How this is implemented

Credential encryption uses ASP.NET Core Data Protection through
`ISocialCredentialProtector`, so the algorithm can be replaced without touching
callers. Access and refresh tokens are protected under **separate purposes**, so a
protected access token can never be decrypted as a refresh token or the reverse.

| Purpose string | Protects |
| --- | --- |
| `Mayorx.ContentEngine.SocialCredentials.AccessToken.v1` | Access tokens |
| `Mayorx.ContentEngine.SocialCredentials.RefreshToken.v1` | Refresh tokens |

The `ISocialCredentialProtector` abstraction exists so a different backing store
can be introduced later without changing the social account service. It is not
intended as a general-purpose secret store.

Data Protection keys are persisted to a local key ring. **The key ring is as
sensitive as the credentials it protects**: losing it makes every stored token
unrecoverable, and leaking it makes every stored token readable.

| Environment | Key ring |
| --- | --- |
| Development | `.data-protection-keys/` at the repository root, git-ignored |
| Production | Must be an explicitly configured, protected, persistent location |

Production must not rely on the default per-machine profile location. Multiple
API instances must share one key ring, or a token encrypted by one instance will
be undecryptable by another. A production deployment that cannot provide a
protected persistent key ring must not enable social connections.

> **Not yet implemented:** rotating or revoking Data Protection keys. There is
> currently no supported way to re-encrypt existing tokens after a key rotation
> without reconnecting the affected accounts. Disconnect and reconnect is the
> recovery path.

---

## Rate Limiting

Rate limits should exist at multiple levels:

```text
IP
User
Workspace
Endpoint
External provider
```

AI endpoints should have stricter limits because they incur provider costs.

---

# 47. Validation

The API validates:

- Required fields
- String lengths
- Enum values
- UUID formats
- Date ranges
- Workspace ownership
- Resource existence
- Platform constraints
- Content length
- Publishing eligibility

AI-generated content receives the same validation as human-generated content.

---

# 48. Platform-Specific Validation

The API should use a platform abstraction rather than hardcoding publishing rules throughout controllers.

Example:

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

Possible implementations:

```text
XSocialPlatform
LinkedInSocialPlatform
```

This allows additional platforms to be added later without redesigning the publishing API.

---

# 49. AI Service Boundary

AI functionality should also use an abstraction.

Example:

```csharp
public interface IContentIntelligenceService
{
    Task<ContentOpportunityResult> GenerateOpportunitiesAsync(
        OpportunityRequest request,
        CancellationToken cancellationToken);

    Task<ContentGenerationResult> GenerateContentAsync(
        ContentGenerationRequest request,
        CancellationToken cancellationToken);

    Task<ContentEvaluationResult> EvaluateContentAsync(
        ContentEvaluationRequest request,
        CancellationToken cancellationToken);
}
```

Controllers should not contain prompts or provider-specific AI logic.

---

# 50. API Layer Structure

Recommended ASP.NET Core structure:

```text
apps/api/
├── Controllers/
│   ├── AuthController.cs
│   ├── UsersController.cs
│   ├── WorkspacesController.cs
│   ├── BrandController.cs
│   ├── AudiencesController.cs
│   ├── StrategyController.cs
│   ├── ContentController.cs
│   ├── AiController.cs
│   ├── SocialController.cs
│   ├── SchedulesController.cs
│   ├── PublicationsController.cs
│   ├── AnalyticsController.cs
│   ├── LearningController.cs
│   ├── NotificationsController.cs
│   └── AuditLogsController.cs
│
├── Contracts/
│   ├── Auth/
│   ├── Workspaces/
│   ├── Content/
│   ├── AI/
│   ├── Social/
│   ├── Publishing/
│   └── Analytics/
│
├── Services/
├── Validators/
├── Middleware/
├── Authorization/
└── Program.cs
```

---

# 51. API Contract Rules

The following rules should remain consistent throughout development.

### Rule 1 — Never expose database entities directly

Use DTOs/contracts.

```text
Database Entity
      ↓
Service
      ↓
DTO
      ↓
API Response
```

---

### Rule 2 — Controllers stay thin

Controllers should coordinate requests rather than contain business logic.

Bad:

```text
Controller
 ├── database queries
 ├── AI prompts
 ├── publishing logic
 └── business rules
```

Preferred:

```text
Controller
    ↓
Application Service
    ↓
Domain/Infrastructure Services
    ↓
Database / AI / External APIs
```

---

### Rule 3 — Never trust client state

The server must independently verify:

- User identity
- Workspace membership
- Permissions
- Resource ownership
- Content status
- Social account status
- Publishing eligibility

---

### Rule 4 — Never expose secrets

Responses must never contain:

- OAuth client secrets
- Access tokens
- Refresh tokens
- API keys
- Password hashes
- Internal credentials

---

### Rule 5 — AI does not control execution

AI may produce:

```text
Ideas
Strategies
Drafts
Recommendations
Evaluations
Insights
```

The application controls:

```text
Authorization
Validation
Approval
Scheduling
Publishing
Persistence
```

---

# 52. MVP Endpoint Set

The first implementation should focus on the smallest useful API.

## Authentication

```text
POST /auth/register
POST /auth/login
POST /auth/refresh
POST /auth/logout
GET  /auth/me
```

## Workspace

```text
GET  /workspaces
POST /workspaces
GET  /workspaces/{id}
PATCH /workspaces/{id}
```

## Brand & Audience

```text
GET /brand
PUT /brand

GET /audiences
POST /audiences
PATCH /audiences/{id}
DELETE /audiences/{id}
```

## Strategy

```text
GET /strategy
PUT /strategy

GET /strategy/pillars
POST /strategy/pillars
PATCH /strategy/pillars/{id}
DELETE /strategy/pillars/{id}
```

## Content

```text
GET  /content/ideas
POST /content/ideas
GET  /content/ideas/{id}
PATCH /content/ideas/{id}
DELETE /content/ideas/{id}

GET  /content/drafts
POST /content/drafts
GET  /content/drafts/{id}
PATCH /content/drafts/{id}
DELETE /content/drafts/{id}

POST /content/drafts/{id}/approve
```

## AI

```text
POST /ai/opportunities/generate
POST /ai/content/generate
POST /ai/content/evaluate
POST /ai/content/{id}/revise
```

## Social

```text
GET    /api/v1/social/accounts
GET    /api/v1/social/{platform}/connect
GET    /api/v1/social/{platform}/callback
DELETE /api/v1/social/accounts/{id}
```

Implemented in `SocialAccountsController`. The callback is the only anonymous
action; every other social action requires authentication.

## Scheduling & Publishing

```text
GET    /schedules
POST   /schedules
PATCH  /schedules/{id}
DELETE /schedules/{id}
```

Publishing has no endpoints. It runs in the scheduled post worker, and its
outcome is reported on the content post as `status`, `publishedAt`, and
`publishFailureReason`. `GET /publications*` is deferred; see
[Publishing](#35-publishing).

## Analytics

```text
GET /analytics/overview
GET /analytics/posts
GET /analytics/posts/{contentPostId}
```

---

# 53. Example End-to-End Flow

A normal automated content workflow should look like:

```text
1. User configures Brand
        ↓
2. User defines Audience
        ↓
3. User defines Content Strategy
        ↓
4. AI discovers opportunities
        ↓
5. User or automation selects opportunity
        ↓
6. AI generates draft
        ↓
7. AI evaluates draft
        ↓
8. User reviews/edits
        ↓
9. User approves
        ↓
10. Platform variant created
        ↓
11. User schedules content
        ↓
12. Worker publishes content
        ↓
13. Metrics are collected
        ↓
14. AI analyzes performance
        ↓
15. Learning signals are stored
        ↓
16. Future content strategy improves
```

---

# 54. Future API Extensions

These are intentionally outside the initial MVP:

- Instagram publishing
- Facebook publishing
- Threads publishing
- TikTok publishing
- Advanced content calendar
- WebSocket job updates
- Real-time collaboration
- Advanced team permissions
- AI autonomous publishing modes
- Advanced competitor intelligence
- Automated trend monitoring
- A/B testing
- Multi-language content generation
- Advanced reporting exports
- Webhooks for third-party integrations

These should only be introduced when the core publishing and learning loop is stable.

---

# 55. API Design Principle

The API should remain a **stable application contract**, even when internal implementation changes.

The frontend should not need to know:

- Which AI provider is being used
- Which database query retrieves the data
- Which queue implementation processes a job
- How OAuth tokens are stored
- Which social platform SDK is being used internally

The API exposes **business capabilities**, not infrastructure details.

The central contract is:

```text
User
  ↓
Workspace
  ↓
Brand + Audience + Strategy
  ↓
Content Intelligence
  ↓
Content
  ↓
Approval
  ↓
Schedule
  ↓
Publication
  ↓
Analytics
  ↓
Learning
  ↓
Better Future Content
```

This keeps the API aligned with the core product vision: **Mayorx Content Engine is a content intelligence and execution system, not simply an AI text generator.**
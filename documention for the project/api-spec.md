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

Returns content pillars.

---

## POST `/strategy/pillars`

Creates a content pillar.

### Request

```json
{
  "name": "AI for Business",
  "description": "Practical ways businesses can use AI.",
  "priority": 1
}
```

---

## PATCH `/strategy/pillars/{pillarId}`

Updates a content pillar.

---

## DELETE `/strategy/pillars/{pillarId}`

Deletes a content pillar.

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

| Code | Condition |
| --- | --- |
| `404 CONTENT_POST_NOT_FOUND` | Unknown id, or a post in another workspace |
| `409 INVALID_CONTENT_POST_TRANSITION` | The current status forbids the operation |
| `400 INVALID_CONTENT_POST_CONTENT` | Edited content failed shared validation |

Scheduling, publishing, variants, and approval history are not part of this
workflow and are not yet implemented.

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

---

## GET `/social/accounts`

Returns connected social accounts.

Example response:

```json
{
  "data": [
    {
      "id": "uuid",
      "platform": "LinkedIn",
      "accountName": "Mayorx Tech",
      "status": "Connected",
      "connectedAt": "2026-09-25T18:30:00Z"
    }
  ]
}
```

Sensitive OAuth tokens must never be returned.

---

# 33. OAuth Connection

## GET `/social/{platform}/connect`

Returns or redirects to the OAuth authorization flow.

Supported values:

```text
x
linkedin
```

---

## GET `/social/{platform}/callback`

Handles the OAuth callback.

The callback must:

1. Validate state
2. Exchange authorization code
3. Retrieve account information
4. Encrypt tokens
5. Store/update the social account
6. Redirect the user back to the application

---

## DELETE `/social/accounts/{accountId}`

Disconnects a social account.

Before deletion, scheduled content targeting that account should be identified and handled safely.

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

## POST `/publications`

Immediately publishes approved content.

### Request

```json
{
  "contentVariantId": "uuid",
  "socialAccountId": "uuid"
}
```

Headers:

```http
Idempotency-Key: unique-operation-key
```

### Response

```json
{
  "data": {
    "publicationId": "uuid",
    "status": "Publishing"
  },
  "message": "Publication started"
}
```

**Status:** `202 Accepted`

Publishing is asynchronous because external platform APIs may take time or fail.

---

## GET `/publications`

Returns publication history.

Filters:

```text
platform
status
from
to
```

---

## GET `/publications/{publicationId}`

Returns publication details.

Example:

```json
{
  "data": {
    "id": "uuid",
    "platform": "LinkedIn",
    "status": "Published",
    "publishedAt": "2026-09-25T18:30:00Z",
    "externalPostId": "external-id"
  }
}
```

---

# 36. Analytics

## GET `/analytics/overview`

Returns high-level workspace analytics.

Example:

```json
{
  "data": {
    "period": {
      "from": "2026-09-01T00:00:00Z",
      "to": "2026-09-25T23:59:59Z"
    },
    "postsPublished": 18,
    "impressions": 45200,
    "likes": 1800,
    "comments": 320,
    "shares": 210,
    "clicks": 540,
    "followersGained": 380,
    "engagementRate": 5.1
  }
}
```

---

## GET `/analytics/posts`

Returns post-level performance.

Supported filters:

```text
platform
from
to
pillarId
```

---

## GET `/analytics/posts/{publicationId}`

Returns metrics for a specific publication.

---

# 37. Performance Learning

## GET `/learning/insights`

Returns AI-generated learning insights from historical content performance.

Example:

```json
{
  "data": [
    {
      "type": "HookPattern",
      "insight": "Posts beginning with a specific problem statement have generated stronger discussion.",
      "evidenceCount": 8,
      "createdAt": "2026-09-25T18:30:00Z"
    }
  ]
}
```

Learning insights should be evidence-based and distinguish observations from recommendations.

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
      "resourceType": "ContentDraft",
      "resourceId": "uuid",
      "performedBy": "uuid",
      "createdAt": "2026-09-25T18:30:00Z"
    }
  ]
}
```

Audit logs are read-only through the API.

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
PerformanceAnalysisRequested
LearningSignalCreated
```

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
GET    /social/accounts
GET    /social/{platform}/connect
GET    /social/{platform}/callback
DELETE /social/accounts/{id}
```

## Scheduling & Publishing

```text
GET    /schedules
POST   /schedules
PATCH  /schedules/{id}
DELETE /schedules/{id}

GET  /publications
POST /publications
GET  /publications/{id}
```

## Analytics

```text
GET /analytics/overview
GET /analytics/posts
GET /analytics/posts/{publicationId}
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

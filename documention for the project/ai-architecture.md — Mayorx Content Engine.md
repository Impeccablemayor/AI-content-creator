# AI Architecture

## Mayorx Content Engine

> **The AI architecture transforms brand context, audience intelligence, external information, historical content, and performance data into strategic, platform-specific content decisions.**

---

# 1. Purpose

This document defines the architecture of the AI layer powering Mayorx Content Engine.

It describes:

- AI responsibilities
- AI agents
- Model routing
- Context management
- Memory
- Retrieval
- Prompt architecture
- Structured outputs
- Content strategy pipeline
- Research
- Content generation
- Quality evaluation
- Feedback
- Learning
- Guardrails
- Cost control
- Human-in-the-loop workflows

The AI architecture should support the product vision without making the entire application dependent on one AI model.

---

# 2. Core Architectural Principle

The system should not use:

```text
User
  ↓
One giant prompt
  ↓
LLM
  ↓
Post
```

Instead:

```text
User Context
     ↓
Context Engine
     ↓
Research Engine
     ↓
Opportunity Engine
     ↓
Strategy Engine
     ↓
Story Engine
     ↓
Content Generator
     ↓
Platform Optimizer
     ↓
Quality Evaluator
     ↓
Human Review
     ↓
Publisher
     ↓
Analytics
     ↓
Learning Engine
```

Each component should have a clear responsibility.

---

# 3. AI System Goals

The AI system should:

1. Understand the user's brand.
2. Understand the user's audience.
3. Understand the user's goals.
4. Discover relevant topics.
5. Evaluate content opportunities.
6. Generate content angles.
7. Select storytelling structures.
8. Generate platform-specific content.
9. Preserve the user's voice.
10. Recommend hashtags.
11. Recommend posting times.
12. Evaluate generated content.
13. Detect potential factual problems.
14. Learn from user feedback.
15. Learn from performance.
16. Avoid repetitive content.
17. Reduce unnecessary AI generation costs.

---

# 4. AI vs Application Logic

Not everything should be handled by AI.

This distinction is critical.

## AI should handle

- Topic interpretation
- Topic relevance
- Content opportunity analysis
- Angle generation
- Storytelling decisions
- Writing
- Voice adaptation
- Semantic similarity
- Content classification
- Qualitative performance analysis
- Strategy recommendations

## Application logic should handle

- Authentication
- Authorization
- Database operations
- OAuth
- Token storage
- Scheduling
- Queue processing
- Publishing
- Retry logic
- Rate limiting
- Notifications
- Billing
- Permissions
- Audit logs
- API validation

The AI should **recommend and reason**.

The application should **control and execute**.

---

# 5. AI Architecture Overview

```text
                         ┌──────────────────────┐
                         │       USER           │
                         └──────────┬───────────┘
                                    │
                                    ↓
                         ┌──────────────────────┐
                         │   APPLICATION API   │
                         └──────────┬───────────┘
                                    │
                                    ↓
                         ┌──────────────────────┐
                         │   AI ORCHESTRATOR    │
                         └──────────┬───────────┘
                                    │
              ┌─────────────────────┼──────────────────────┐
              │                     │                      │
              ↓                     ↓                      ↓
      ┌──────────────┐      ┌──────────────┐      ┌──────────────┐
      │ Brand Brain  │      │Audience Brain│      │Content Memory│
      └──────┬───────┘      └──────┬───────┘      └──────┬───────┘
             │                     │                      │
             └─────────────────────┼──────────────────────┘
                                   ↓
                         ┌──────────────────────┐
                         │  Context Assembler   │
                         └──────────┬───────────┘
                                    ↓
                         ┌──────────────────────┐
                         │  Research Engine     │
                         └──────────┬───────────┘
                                    ↓
                         ┌──────────────────────┐
                         │ Opportunity Engine   │
                         └──────────┬───────────┘
                                    ↓
                         ┌──────────────────────┐
                         │ Strategy Engine      │
                         └──────────┬───────────┘
                                    ↓
                         ┌──────────────────────┐
                         │ Story Engine         │
                         └──────────┬───────────┘
                                    ↓
                         ┌──────────────────────┐
                         │ Content Generator    │
                         └──────────┬───────────┘
                                    ↓
                         ┌──────────────────────┐
                         │ Platform Optimizer   │
                         └──────────┬───────────┘
                                    ↓
                         ┌──────────────────────┐
                         │ Quality Evaluator    │
                         └──────────┬───────────┘
                                    ↓
                         ┌──────────────────────┐
                         │ Human Approval       │
                         └──────────┬───────────┘
                                    ↓
                         ┌──────────────────────┐
                         │ Scheduler/Publisher  │
                         └──────────┬───────────┘
                                    ↓
                         ┌──────────────────────┐
                         │ Analytics Engine     │
                         └──────────┬───────────┘
                                    ↓
                         ┌──────────────────────┐
                         │ Learning Engine      │
                         └──────────────────────┘
```

---

# 6. AI Orchestrator

The AI Orchestrator is responsible for coordinating AI operations.

It should determine:

- Which AI capability is needed
- Which context is required
- Which model should be used
- Which tools should be called
- What order operations should happen in
- Whether an operation needs human approval
- Whether the output should be regenerated

The orchestrator should not itself contain all intelligence.

It coordinates specialized components.

---

# 7. AI Agents

The initial system should conceptually contain the following AI agents/services.

```text
Brand Intelligence Agent
Audience Intelligence Agent
Research Agent
Topic Discovery Agent
Opportunity Scoring Agent
Strategy Agent
Angle Agent
Storytelling Agent
Content Generation Agent
Platform Optimization Agent
Hashtag Agent
Quality Evaluation Agent
Fact Verification Agent
Performance Analysis Agent
Learning Agent
```

These do not necessarily need to be separate deployed services in V1.

They can initially be implemented as modular application components using shared infrastructure.

---

# 8. Brand Intelligence Agent

## Responsibility

Understand the user's identity and positioning.

Inputs:

- Brand profile
- Bio
- Profession
- Expertise
- Goals
- User-provided information
- Approved content
- Rejected content

Outputs:

```json id="t2x3d6"
{
  "positioning": "...",
  "expertise": [],
  "topics": [],
  "brandValues": [],
  "tone": {},
  "differentiators": [],
  "authorityAreas": []
}
```

---

# 9. Audience Intelligence Agent

## Responsibility

Understand who the user wants to reach.

Inputs:

- Audience profile
- User goals
- Historical engagement
- Content performance
- Audience feedback

Outputs:

```json id="xjv9mq"
{
  "audienceSegments": [],
  "painPoints": [],
  "goals": [],
  "interests": [],
  "questions": [],
  "objections": [],
  "contentPreferences": []
}
```

---

# 10. Brand Brain

The Brand Brain is persistent structured context representing the user's brand.

It should contain:

```text
Identity
Positioning
Expertise
Values
Opinions
Goals
Content pillars
Tone
Voice
Audience
Stories
Projects
Preferences
Restrictions
```

The Brand Brain should not simply be one large text blob.

It should be structured so individual pieces can be retrieved when needed.

---

# 11. Audience Brain

The Audience Brain stores structured information about target audiences.

Example:

```json id="r5s3ql"
{
  "name": "Small Business Owners",
  "problems": [
    "Manual processes",
    "Low online visibility",
    "Limited technical knowledge"
  ],
  "goals": [
    "Increase revenue",
    "Automate operations",
    "Attract customers"
  ],
  "interests": [
    "AI",
    "Automation",
    "Digital marketing"
  ]
}
```

---

# 12. Content Memory

Content memory stores the user's content history.

The system should remember:

- Generated posts
- Approved posts
- Published posts
- Rejected posts
- Topics
- Angles
- Hooks
- Content pillars
- Story structures
- Performance

This prevents repetition.

---

# 13. Semantic Content Memory

Simple keyword search is insufficient.

The system should eventually use embeddings/vector search to identify semantically similar content.

Example:

Previous post:

> "AI coding tools are changing how developers work."

New idea:

> "AI assistants are transforming software development workflows."

Keyword matching may treat these as different.

Semantic search should recognize their similarity.

The system can then warn:

> "You've recently published a similar idea."

---

# 14. Story Bank

The AI architecture should support persistent stories.

A story record may contain:

```json id="7drm6g"
{
  "title": "Production API timeout",
  "context": "...",
  "problem": "...",
  "action": "...",
  "result": "...",
  "lesson": "...",
  "topics": [],
  "contentUses": []
}
```

Stories become reusable content assets.

---

# 15. Research Agent

The Research Agent is responsible for finding external information when current context is required.

Potential research sources:

- Search engines
- News
- Industry websites
- Documentation
- RSS feeds
- Social platforms
- Public datasets

The research layer should be separate from content generation.

The generator should receive researched information rather than independently inventing current facts.

---

# 16. Research Rules

Research should only happen when necessary.

Examples requiring research:

- Current events
- Breaking news
- Current trends
- Product updates
- New technology releases
- Recent statistics
- Industry developments

Examples that may not require research:

- Personal experience
- User's own project
- Evergreen technical lessons
- Personal opinions

This reduces cost and unnecessary complexity.

---

# 17. Research Evidence

Research results should preserve source metadata.

Example:

```json id="yqqrml"
{
  "claim": "Example claim",
  "source": "source-id",
  "publishedAt": "2026-09-25",
  "confidence": 0.91
}
```

Generated content should be grounded in available evidence when factual claims are involved.

---

# 18. Topic Discovery Agent

The Topic Discovery Agent transforms research and brand context into potential content topics.

Input:

```text
Brand
Audience
Goals
Pillars
Research
Historical content
```

Output:

```json id="0pn5ba"
{
  "topic": "...",
  "reason": "...",
  "relevance": 92,
  "timeliness": 88
}
```

---

# 19. Opportunity Scoring Agent

This component determines whether a topic is worth pursuing.

The score should consider:

```text
Brand Relevance
Audience Fit
Authority Fit
Timeliness
Originality
Discussion Potential
Goal Alignment
Historical Performance
Content Diversity
```

The result should be explainable.

Example:

> "This opportunity scored highly because it is directly related to your AI-for-business pillar and is currently gaining attention among your target audience."

---

# 20. Strategy Agent

The Strategy Agent determines:

- Content objective
- Audience
- Topic
- Angle
- Content category
- Platform
- Storytelling framework
- Desired outcome

Example:

```json id="xps3jr"
{
  "objective": "authority",
  "category": "industry_insight",
  "angle": "...",
  "storyFramework": "observation_insight",
  "platforms": [
    "linkedin",
    "x"
  ]
}
```

---

# 21. Angle Agent

One topic can have many angles.

The Angle Agent should generate multiple options.

Example:

```text
Topic:
AI agents for business

Angles:

1. Educational
How AI agents actually work.

2. Contrarian
Most businesses don't need an AI agent yet.

3. Practical
Three workflows businesses can automate today.

4. Personal
What building with AI agents taught me.

5. Business
Where AI agents can produce measurable ROI.
```

The Strategy Agent selects the most appropriate angle.

---

# 22. Storytelling Agent

The Storytelling Agent selects the structure that best fits the content.

Inputs:

```text
Topic
Angle
Objective
Platform
Available personal story
Audience
```

Possible outputs:

```text
Personal Story
Observation → Insight
Problem → Solution
Mistake → Lesson
Case Study
Contrarian
Build in Public
Educational Breakdown
```

---

# 23. Content Generation Agent

This agent produces the actual draft.

It should receive structured context rather than a raw database dump.

Example:

```text
Brand Context
Audience Context
Strategy
Angle
Story Framework
Voice Profile
Research
Relevant Previous Content
Platform Requirements
```

---

# 24. Context Assembly

The Context Assembler is responsible for selecting the minimum relevant context.

This is important because:

```text
More context ≠ better output
```

Too much context can:

- Increase cost
- Increase latency
- Confuse the model
- Dilute important information

The system should retrieve only what is relevant.

---

# 25. Context Priority

Context should generally be prioritized:

```text
1. Current task
2. Brand constraints
3. Audience
4. Strategy
5. User voice
6. Relevant research
7. Relevant content history
8. General knowledge
```

---

# 26. Voice Engine

The Voice Engine should maintain a structured representation of writing style.

Potential attributes:

```json id="v8u8qz"
{
  "tone": [
    "direct",
    "professional",
    "conversational"
  ],
  "formality": 0.55,
  "humor": 0.25,
  "technicalDepth": 0.75,
  "emojiUsage": 0.1,
  "sentenceLength": "medium",
  "ctaStyle": "conversational"
}
```

These values should be learned and manually configurable.

---

# 27. Voice Learning

The system should learn from:

### Approved posts

What the user accepts.

### Edited posts

What the user changes.

### Rejected posts

What the user dislikes.

For example:

If the user repeatedly removes:

> "Here are 5 ways..."

the system should reduce the probability of generating that structure.

---

# 28. Platform Optimization Agent

This agent adapts content to the target platform.

Input:

```text
Core idea
+
Strategy
+
Voice
+
Platform
```

Output:

```text
Platform-specific content
```

The core idea should remain consistent.

---

# 29. LinkedIn Optimization

The AI should optimize for:

- Strong first lines
- Professional relevance
- Readability
- Storytelling
- Insight
- Appropriate spacing
- Discussion

The system should avoid excessive:

- Corporate jargon
- Fake authority
- Motivational clichés
- Emoji spam
- Artificial hooks

---

# 30. X Optimization

The AI should optimize for:

- Concision
- Strong opening
- Clear thought
- Opinion
- Conversation
- Shareability

Where appropriate, the system may transform an idea into a thread.

---

# 31. Hashtag Agent

The Hashtag Agent determines:

```text
Should hashtags be used?
Which hashtags?
How many?
```

It should consider:

- Platform
- Topic
- Audience
- Niche
- Relevance
- Historical performance

The system should be allowed to return:

```json id="e1e04p"
{
  "useHashtags": false,
  "hashtags": []
}
```

---

# 32. Posting-Time Intelligence

Posting-time recommendations should initially use:

```text
Platform-level patterns
+
Audience timezone
+
User configuration
```

Later:

```text
Platform patterns
+
Audience data
+
User history
+
Historical performance
```

The system should calculate confidence.

---

# 33. Quality Evaluation Agent

Every AI-generated post should pass through an evaluator.

Dimensions:

```text
Strategic Fit
Audience Fit
Hook Strength
Value
Clarity
Originality
Authenticity
Storytelling
Platform Fit
Voice Match
Factual Reliability
AI-Language Risk
```

---

# 34. Quality Evaluation Output

Example:

```json id="6p7a0h"
{
  "overallScore": 89,
  "scores": {
    "strategicFit": 94,
    "audienceFit": 91,
    "hook": 87,
    "value": 92,
    "authenticity": 95,
    "platformFit": 88
  },
  "issues": [
    "The conclusion could be stronger."
  ],
  "recommendation": "minor_revision"
}
```

---

# 35. AI-as-Judge

The system may use a separate model call for evaluation.

The generation model should not be the only judge of its own work.

A useful pattern is:

```text
Generator
   ↓
Evaluator
   ↓
Revision
   ↓
Evaluator
```

A maximum number of revision cycles should be enforced to control cost.

Suggested initial maximum:

```text
2 revision cycles
```

---

# 36. Factual Verification

If content contains factual claims, the system should determine whether verification is required.

Example:

> "LinkedIn's algorithm now does X."

This requires verification.

But:

> "I personally found X easier than Y."

does not require external verification.

---

# 37. Hallucination Control

The AI system should minimize hallucinations through:

### Grounding

Use retrieved evidence.

### Structured inputs

Separate facts from generated interpretation.

### Source tracking

Keep source references.

### Confidence

Represent uncertainty.

### Verification

Verify important claims.

### Human approval

Require review before publication for high-risk content.

---

# 38. AI Confidence

AI outputs should support confidence where useful.

Example:

```text
Topic relevance:
94%

Trend confidence:
81%

Recommended posting time:
78%

Hashtag confidence:
65%
```

Low-confidence recommendations should not be presented as certainty.

---

# 39. Prompt Architecture

Prompts should be modular.

Instead of one huge prompt:

```text
SYSTEM
+
BRAND
+
AUDIENCE
+
STRATEGY
+
RESEARCH
+
VOICE
+
PLATFORM
+
TASK
```

The application should construct the appropriate context dynamically.

---

# 40. Prompt Layers

## Layer 1 — System Instructions

Defines:

- AI role
- Safety
- Output rules
- General behavior

---

## Layer 2 — Product Strategy

Defines:

- Mayorx Content Engine philosophy
- Content principles
- Strategy rules

---

## Layer 3 — Brand Context

Defines:

- User identity
- Positioning
- Voice
- Expertise

---

## Layer 4 — Audience Context

Defines:

- Target audience
- Problems
- Goals
- Interests

---

## Layer 5 — Task Context

Defines:

- Current topic
- Objective
- Angle
- Platform
- Story framework

---

## Layer 6 — Research Context

Defines:

- Relevant facts
- Sources
- Current events

---

## Layer 7 — Output Schema

Defines exactly what the model must return.

---

# 41. Structured Outputs

AI outputs should use structured schemas wherever possible.

Do not rely on parsing arbitrary prose.

Example:

```json id="d9xj1k"
{
  "topic": "...",
  "objective": "...",
  "angle": "...",
  "storyFramework": "...",
  "platform": "...",
  "content": "...",
  "hashtags": [],
  "qualityScore": 0
}
```

Structured output makes AI behavior easier to validate and debug.

---

# 42. Model Routing

The system should not use the most expensive model for every task.

Conceptually:

```text
Simple classification
        ↓
Fast / inexpensive model

Topic analysis
        ↓
Medium model

Strategy
        ↓
Strong reasoning model

Final content
        ↓
Strong generation model

Quality evaluation
        ↓
Medium / strong evaluator
```

Exact providers and models should remain configurable.

---

# 43. Model Abstraction

The application should use a provider abstraction.

Conceptually:

```text
IAIProvider
   │
   ├── Provider A
   ├── Provider B
   └── Provider C
```

This prevents the application from becoming tightly coupled to a single AI provider.

The provider interface should support:

- Text generation
- Structured output
- Embeddings
- Tool calling
- Model selection

---

# 44. Embeddings

Embeddings should eventually be used for:

- Content similarity
- Story retrieval
- Brand memory retrieval
- Audience memory retrieval
- Semantic search
- Duplicate detection
- Topic clustering

Example:

```text
New idea
   ↓
Embedding
   ↓
Vector search
   ↓
Similar previous content
```

---

# 45. Retrieval-Augmented Generation

RAG should be used when the AI needs user-specific information.

Example:

```text
User asks:
"Create a post about my recent project."

RAG retrieves:
- Project information
- Previous posts
- User voice
- Relevant stories
```

Then the model generates the content.

---

# 46. Memory Architecture

Memory should be separated into categories.

```text
                    MEMORY
                       │
        ┌──────────────┼──────────────┐
        ↓              ↓              ↓
   Structured       Semantic       Historical
    Memory           Memory          Memory
        │              │              │
     Profile        Embeddings      Events
     Settings       Similarity      Performance
     Goals          Retrieval       Content
```

---

# 47. Short-Term AI Context

Short-term context is information relevant to the current operation.

Example:

```text
Current topic
Current strategy
Current platform
Current research
Current user instruction
```

This should not automatically become permanent memory.

---

# 48. Long-Term Memory

Long-term memory includes:

- Brand profile
- Voice preferences
- Content pillars
- Audience
- Stories
- Published content
- Performance patterns
- User preferences

---

# 49. Memory Write Rules

AI should not automatically store every generated statement as memory.

Memory should be written when:

1. The user explicitly provides important information.
2. The user approves a preference.
3. A stable pattern emerges.
4. The application records an actual event.

For example:

> User repeatedly rejects overly promotional content.

This may eventually become a preference.

But:

> AI generated a post using a certain phrase.

should not automatically become permanent memory.

---

# 50. Feedback Loop

The core learning loop:

```text
Generate
   ↓
User edits
   ↓
User approves
   ↓
Publish
   ↓
Performance
   ↓
Analysis
   ↓
Learn
   ↓
Future strategy
```

---

# 51. Explicit Feedback

Users may directly provide feedback.

Examples:

> "Too formal."

> "Make it more Nigerian."

> "Don't use emojis."

> "Too long."

> "This doesn't sound like me."

These should be captured as preference signals where appropriate.

---

# 52. Implicit Feedback

The system may infer signals from:

- Approval
- Rejection
- Editing
- Regeneration
- Scheduling
- Publishing
- Deletion
- Engagement

However, implicit signals should be treated as probabilistic rather than absolute.

---

# 53. Performance Learning

The system should learn patterns such as:

```text
Personal stories
→ High comments

Technical posts
→ High saves/shares

Promotional posts
→ Low engagement

Tuesday mornings
→ Strong LinkedIn performance
```

These should become strategy signals.

---

# 54. Learning Engine

The Learning Engine transforms events into strategic insights.

Example:

```text
Raw Data:
Post A received 120 comments.

Analysis:
Post used personal story + controversial opinion.

Insight:
Personal stories combined with strong opinions
perform well for this audience.

Strategy update:
Increase probability of this content combination.
```

---

# 55. Avoiding Bad Learning

The system should not make decisions from small sample sizes.

For example:

One successful post does not mean:

> "Always post at 9:00 AM."

The system should consider:

- Sample size
- Statistical confidence
- Consistency
- Content type
- Audience differences

---

# 56. Exploration vs Exploitation

The AI should balance:

### Exploitation

Create content based on what has already worked.

### Exploration

Try new:

- Topics
- Formats
- Angles
- Posting times
- Story structures

Otherwise the system risks becoming repetitive.

Conceptually:

```text
70–80%
Known effective patterns

20–30%
Experimentation
```

These values should be configurable.

---

# 57. AI Cost Management

AI operations can become expensive.

The system should optimize cost using:

### Caching

Cache reusable AI results.

### Context minimization

Retrieve only relevant context.

### Model routing

Use cheaper models for simple tasks.

### Batch processing

Batch background analysis.

### Embedding reuse

Do not repeatedly embed unchanged content.

### Asynchronous processing

Move expensive operations to background workers.

---

# 58. AI Caching

Potential cache candidates:

- Brand analysis
- Voice analysis
- Topic classification
- Content embeddings
- Research summaries
- Hashtag analysis

Cache invalidation should occur when underlying information changes.

---

# 59. AI Observability

Every AI operation should be traceable.

Store:

```text
Request ID
User ID
Agent
Model
Prompt version
Input tokens
Output tokens
Latency
Cost
Result
Validation status
Error
```

This allows the development team to understand:

- Why outputs changed
- Which prompts perform well
- Which models are expensive
- Where failures happen

---

# 60. Prompt Versioning

Prompts must be version-controlled.

Example:

```text
content-generator-v1
content-generator-v2
content-generator-v3
```

The system should know which prompt version generated a piece of content.

This is critical for debugging and evaluation.

---

# 61. AI Evaluation Framework

Before deploying significant AI changes, evaluate against a dataset.

Evaluation categories:

```text
Brand Fit
Audience Fit
Accuracy
Voice
Storytelling
Platform Fit
Originality
Quality
Safety
```

Example test:

```text
Input:
Developer brand
Target audience:
Startup founders

Expected:
Business-oriented software insight

Actual:
Generic programming tutorial

Result:
FAIL
```

---

# 62. Golden Dataset

The project should maintain a curated set of examples.

Examples should include:

- Excellent posts
- Poor posts
- User-edited posts
- Rejected posts
- Different content objectives
- Different platforms
- Different storytelling formats

This dataset becomes the benchmark for AI improvements.

---

# 63. Regression Testing

Whenever prompts or models change:

```text
New AI version
      ↓
Run evaluation dataset
      ↓
Compare scores
      ↓
Approve / reject release
```

The system should avoid improving one capability while silently damaging another.

---

# 64. AI Failure Handling

If AI generation fails:

```text
Attempt 1
   ↓
Retry with same strategy
   ↓
Failure
   ↓
Retry with fallback model
   ↓
Failure
   ↓
Return controlled error
```

The application should never publish an invalid or incomplete AI response.

---

# 65. AI Output Validation

Every structured AI response should be validated before use.

Validation should check:

- Required fields
- Allowed enums
- Length limits
- Platform limits
- Valid JSON
- No forbidden content
- No missing context

AI output is considered **untrusted input**.

---

# 66. Autonomous Actions

AI should not directly execute sensitive actions.

For example:

```text
AI:
"I recommend publishing this."

Application:
"Is this approved?"

User:
"Yes."

Application:
"Publish."
```

The AI recommends.

The application executes.

---

# 67. Publishing Safety

For V1:

```text
AI generates
      ↓
AI evaluates
      ↓
User approves
      ↓
Application schedules
      ↓
Application publishes
```

Autonomous publishing can be added later behind explicit user settings.

---

# 68. AI Agent Communication

Agents should communicate using structured objects.

Example:

```text
Research Agent
      ↓
ResearchResult

Opportunity Agent
      ↓
OpportunityResult

Strategy Agent
      ↓
StrategyResult

Generation Agent
      ↓
DraftResult

Evaluation Agent
      ↓
EvaluationResult
```

This prevents loosely coupled prompt chains.

---

# 69. Example End-to-End AI Flow

User requests:

> "Find something worth posting today."

### Step 1

Load Brand Brain.

### Step 2

Load Audience Brain.

### Step 3

Load recent content.

### Step 4

Research relevant current topics.

### Step 5

Generate topic candidates.

### Step 6

Score opportunities.

### Step 7

Check content diversity.

### Step 8

Select strongest opportunities.

### Step 9

Generate angles.

### Step 10

Select strategy.

### Step 11

Select storytelling framework.

### Step 12

Generate LinkedIn version.

### Step 13

Generate X version.

### Step 14

Evaluate both.

### Step 15

Recommend hashtags.

### Step 16

Recommend publishing time.

### Step 17

Present to user.

---

# 70. Example AI Decision

```json id="6t3tqz"
{
  "opportunity": {
    "topic": "AI automation for small businesses",
    "score": 92
  },
  "strategy": {
    "objective": "authority",
    "angle": "Most businesses don't need another AI tool; they need better workflows.",
    "framework": "observation_insight"
  },
  "distribution": {
    "platforms": [
      "linkedin",
      "x"
    ],
    "linkedinTime": "08:30",
    "xTime": "12:15"
  }
}
```

---

# 71. Multi-Agent vs Single-Agent Architecture

The initial implementation should not over-engineer the system.

We should distinguish between:

### Logical agents

Separate responsibilities in the architecture.

and:

### Physical agents

Separate autonomous processes/services.

V1 should primarily use **logical agents inside one AI orchestration layer**.

We can split services later if scale requires it.

---

# 72. Recommended V1 AI Pipeline

The first production version should focus on:

```text
Context Engine
       ↓
Research
       ↓
Opportunity Scoring
       ↓
Strategy
       ↓
Generation
       ↓
Evaluation
```

Everything else can evolve around this pipeline.

---

# 73. Recommended V1 AI Components

Minimum components:

```text
1. Brand Context Service

2. Audience Context Service

3. Content Memory Service

4. Research Service

5. Strategy Engine

6. Content Generator

7. Platform Adapter

8. Quality Evaluator

9. AI Provider Abstraction

10. AI Observability
```

---

# 74. Future AI Components

Later:

```text
Trend Prediction
Advanced Audience Intelligence
Competitor Intelligence
Social Listening
Autonomous Strategy
Lead Intelligence
Campaign Intelligence
Cross-platform Repurposing
AI Community Manager
```

---

# 75. AI Architecture Principles

The following principles should guide implementation.

### 1. Modular

AI capabilities should be independently replaceable.

### 2. Structured

Use schemas rather than free-form agent communication.

### 3. Grounded

Use real information when factual accuracy matters.

### 4. Observable

Track model behavior.

### 5. Evaluated

Continuously test quality.

### 6. Cost-conscious

Do not use expensive reasoning for trivial operations.

### 7. User-specific

Prefer user context over generic advice.

### 8. Human-controlled

Do not allow AI to make irreversible decisions without permission.

### 9. Provider-independent

Avoid unnecessary lock-in.

### 10. Learning-oriented

Use feedback and outcomes to improve recommendations.

---

# 76. Long-Term AI Vision

Eventually the system should behave like an experienced social media strategist.

A user should be able to say:

> "I want to grow my authority among startup founders."

The system should understand that this means:

```text
Positioning
+
Audience
+
Content pillars
+
Content objectives
+
Topic strategy
+
Storytelling
+
Distribution
+
Measurement
```

It should then continuously operate the content intelligence loop:

```text
Research
   ↓
Think
   ↓
Plan
   ↓
Create
   ↓
Publish
   ↓
Measure
   ↓
Learn
   ↓
Think better
```

---

# 77. Final AI Principle

The AI layer should never be treated as:

> **"A chatbot that writes social media posts."**

It should be treated as:

> **"A decision and intelligence system for content."**

Writing is only one component.

The real intelligence lies in deciding:

> **What should be said, why it matters, who should hear it, how it should be expressed, when it should be distributed, and what the system should learn from the result.**

That is the foundation of the Mayorx Content Engine AI architecture.
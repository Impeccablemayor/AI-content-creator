# Product Requirements Document (PRD)

## Product Name

**Mayorx Content Engine**

### Working Description

An AI-powered social media content management system that researches relevant topics, develops content ideas, creates platform-specific text content, recommends hashtags and posting times, schedules posts, publishes them to social platforms, and learns from performance data.

---

# 1. Product Overview

Mayorx Content Engine is an AI-powered social media management platform designed to help individuals, professionals, creators, founders, businesses, and personal brands consistently publish relevant, high-quality content without manually handling the entire content planning process.

The system combines:

- Personal brand intelligence
- Audience intelligence
- Topic and trend discovery
- Content strategy
- Storytelling
- AI-assisted writing
- Platform-specific optimization
- Hashtag strategy
- Posting-time optimization
- Scheduling
- Publishing
- Performance analytics
- Continuous learning

The initial version will focus exclusively on **text-based content** for:

- X (Twitter)
- LinkedIn

The system should eventually evolve from a content-generation tool into an **AI social media manager** capable of managing a significant portion of a user's content workflow.

---

# 2. Problem Statement

Creating consistent social media content is difficult for many professionals and businesses.

The problem is not simply writing a post.

A good content strategy requires answering several questions:

- What should I post about?
- Is this topic relevant to my audience?
- Why should I talk about it?
- Is this topic timely?
- What angle should I take?
- What type of story should I tell?
- How should I communicate the idea?
- Which platform is better for this content?
- When should I publish it?
- Should I use hashtags?
- Will this sound like me?
- Have I already posted something similar?
- What content has worked for me previously?
- How can I improve my next post?

Most existing AI writing tools primarily solve:

> "Write me a post."

Mayorx Content Engine aims to solve the larger problem:

> **"Understand my brand, understand my audience, determine what is worth talking about, develop the right angle, create the content, decide where and when it should be published, and learn from the results."**

---

# 3. Product Vision

Build an AI-powered social media manager that can understand a person's brand and audience deeply enough to make intelligent content decisions on their behalf.

The long-term vision is for the system to behave more like a professional social media strategist than a generic AI writing assistant.

---

# 4. Product Goals

## 4.1 Primary Goals

The system should:

1. Understand the user's personal or business brand.
2. Understand the user's target audience.
3. Understand the user's niche and areas of expertise.
4. Maintain configurable content pillars.
5. Discover relevant content opportunities.
6. Generate useful and relevant content ideas.
7. Apply appropriate storytelling frameworks.
8. Generate platform-specific content.
9. Preserve the user's writing style and voice.
10. Recommend relevant hashtags when appropriate.
11. Recommend optimal publishing times.
12. Allow users to review and edit generated content.
13. Schedule posts.
14. Publish approved posts to supported platforms.
15. Track content performance.
16. Use performance data to improve future recommendations.

---

# 5. Non-Goals for V1

The following features are intentionally outside the first version:

- Instagram publishing
- Facebook publishing
- TikTok publishing
- YouTube publishing
- Image generation
- Video generation
- Video editing
- AI avatar generation
- Automated direct messages
- Automated comments
- Automated replies
- Social listening at large scale
- Influencer management
- Paid advertising management
- Full CRM functionality
- Team collaboration
- Enterprise permissions
- Advanced competitor intelligence
- Autonomous posting without configurable safeguards

These may be considered in future versions.

---

# 6. Target Users

## 6.1 Primary User

### Professionals / Personal Brands

Examples:

- Software engineers
- Developers
- Designers
- Product managers
- Consultants
- Freelancers
- Creators
- Founders
- Career professionals

They want to build authority and visibility within their industry.

---

## 6.2 Secondary User

### Small Businesses

Businesses that want to:

- Build an online presence
- Educate their audience
- Promote their expertise
- Generate conversations
- Increase brand awareness
- Generate leads

---

## 6.3 Future Users

Potential future segments include:

- Marketing agencies
- Social media managers
- Content teams
- Startups
- Enterprises
- Influencers
- Educational organizations

---

# 7. Core Product Principles

## 7.1 Strategy Before Generation

The system must not immediately generate content simply because the user asks for a post.

It should first understand:

- The user
- The audience
- The objective
- The topic
- The platform
- The desired outcome

---

## 7.2 Relevance Over Volume

The goal is not to generate as many posts as possible.

The goal is to generate content that is:

- Relevant
- Useful
- Timely
- Authentic
- Platform appropriate
- Consistent with the user's brand

---

## 7.3 Authenticity Over Generic AI Writing

Generated content should avoid sounding like generic AI-generated social media content.

The system should avoid excessive:

- Corporate jargon
- Empty motivational language
- Buzzwords
- Clichés
- Repetitive structures
- Excessive emojis
- Artificial enthusiasm
- Generic hooks

The user's personal experiences, opinions, projects, observations and expertise should be prioritized whenever available.

---

## 7.4 Platform-Specific Content

The system should not simply publish identical text to every platform.

A single content idea can produce multiple platform-specific versions.

Example:

```text
                    Content Idea
                         │
             ┌───────────┴───────────┐
             │                       │
         LinkedIn                    X
             │                       │
      Professional                Concise
       storytelling               opinion
```

---

## 7.5 Human Control

AI should assist the user rather than completely remove human judgment.

The default workflow should be:

```text
AI generates
      ↓
User reviews
      ↓
User approves
      ↓
System schedules
      ↓
System publishes
```

Autonomous publishing can be introduced later with configurable rules.

---

# 8. V1 Scope

## 8.1 Platforms

V1 supports:

- X
- LinkedIn

---

## 8.2 Content Type

V1 supports:

- Text posts

Future versions may support:

- Images
- Carousels
- Videos
- Threads
- Articles
- Short-form video

---

# 9. Core Features

## 9.1 Brand Profile

Users can define their brand identity.

### Information

- Name
- Professional title
- Industry
- Niche
- Expertise
- Bio
- Brand description
- Goals
- Tone
- Writing preferences
- Topics of interest
- Topics to avoid

### Example

```text
Name:
Mayowa Binuyo

Profession:
Full-Stack Engineer

Primary positioning:
Software engineering + AI + business technology

Primary audience:
Developers, founders and businesses

Tone:
Professional, conversational, direct

Goals:
Build authority
Attract opportunities
Generate leads
Grow audience
```

---

# 10. Audience Profile

Users should define their target audiences.

Each audience profile may contain:

- Audience name
- Description
- Industry
- Experience level
- Goals
- Problems
- Pain points
- Questions
- Interests
- Objections
- Desired outcomes

The AI uses this information when evaluating content opportunities.

---

# 11. Content Pillars

Users can define content pillars.

Example:

```text
Software Engineering
AI for Business
Building in Public
Career Lessons
Technology
Projects
Developer Education
Industry Opinions
```

Each pillar can have:

- Name
- Description
- Priority
- Target audience
- Recommended frequency
- Associated topics

---

# 12. Content Idea Engine

The system should generate content opportunities based on:

- User expertise
- Content pillars
- Audience interests
- Current events
- Industry trends
- Questions
- Problems
- Previous content
- User experiences
- Projects
- Opinions

Each content idea should have an opportunity score.

Example:

```text
Topic:
AI automation for small businesses

Relevance: 92
Audience Fit: 95
Timeliness: 88
Authority Fit: 91
Discussion Potential: 89

Opportunity Score: 91
```

---

# 13. Storytelling Engine

The system should support multiple storytelling frameworks.

Initial frameworks:

### Experience → Problem → Insight → Lesson

### Problem → Tension → Discovery → Solution

### Before → After

### Mistake → Lesson

### Build in Public

### Contrarian Opinion

### Case Study

### Personal Story

### Educational Breakdown

### Observation → Insight → Action

The AI should select a suitable framework based on:

- Topic
- Content objective
- Audience
- Platform
- User experience availability

---

# 14. Content Generation

The system should generate content using structured context rather than a single generic prompt.

Generation context should include:

```text
Brand
+
Audience
+
Content pillar
+
Topic
+
Objective
+
Story framework
+
Platform
+
Voice profile
+
Relevant research
+
Previous content
```

---

# 15. Platform Adaptation

## LinkedIn

The system should optimize for:

- Strong opening
- Storytelling
- Professional insight
- Readability
- Educational value
- Discussion
- Personal experience

---

## X

The system should optimize for:

- Strong hook
- Concision
- Opinion
- Conversation
- Clear ideas
- Shareability

The system may recommend:

- Single post
- Thread

when appropriate.

---

# 16. Hashtag Strategy

Hashtags should be intelligently selected.

The system should consider:

- Platform
- Topic
- Audience
- Industry
- Relevance
- Historical performance
- Hashtag saturation

The system must also be able to determine that **no hashtags are appropriate**.

Hashtags should never be added merely because they are expected in every post.

---

# 17. Posting-Time Recommendations

The system should recommend publishing times based on:

### Initial stage

General platform and audience behavior.

### Later stage

The user's own historical performance.

The system should eventually learn:

```text
Platform
Day
Time
Audience
Content type
Performance
```

and produce personalized recommendations.

---

# 18. Content Calendar

The system should provide a calendar containing:

- Draft posts
- Approved posts
- Scheduled posts
- Published posts
- Failed posts

Users should be able to:

- Create
- Edit
- Reschedule
- Delete
- Approve
- Reject
- Duplicate

content.

---

# 19. Social Account Integration

Users should be able to connect:

### X

Using OAuth and the supported X API capabilities.

### LinkedIn

Using LinkedIn OAuth and supported publishing APIs.

The system must securely store access credentials/tokens.

---

# 20. Scheduling

Users should be able to schedule:

- Individual posts
- Multiple posts
- Recurring content schedules

The system should account for:

- User timezone
- Platform
- Recommended posting time
- Scheduling conflicts
- Failed publishing
- Retry behavior

---

# 21. Publishing

Publishing states should include:

```text
DRAFT
APPROVED
SCHEDULED
PUBLISHING
PUBLISHED
FAILED
CANCELLED
```

The system should maintain publishing logs.

---

# 22. Analytics

The system should track available performance metrics.

Potential metrics include:

- Impressions
- Likes
- Comments
- Shares/reposts
- Clicks
- Engagement rate
- Follower growth
- Post reach

The exact metrics depend on the capabilities and permissions provided by each platform's API.

---

# 23. AI Performance Analysis

The AI should eventually analyze historical performance.

Example:

```text
Your educational posts receive
32% more engagement than promotional posts.

Personal experience posts generate
more comments than purely educational posts.

Your strongest posting window appears to be
Tuesday–Thursday mornings.
```

The system should use these insights to improve future recommendations.

---

# 24. AI Quality Evaluation

Before a post becomes available for publishing, it should pass an AI quality evaluation.

Evaluation dimensions:

```text
Relevance
Audience Fit
Clarity
Hook Strength
Value
Originality
Authenticity
Storytelling
Platform Fit
Brand Alignment
Factual Risk
AI-like Language
```

The system can produce an overall quality score.

Example:

```text
Overall: 88/100

Hook:          92
Value:         91
Authenticity:  86
Clarity:       94
Platform Fit:  87
Brand Fit:     89
```

---

# 25. Content Memory

The system should remember previously published content to reduce repetition.

It should identify:

- Similar topics
- Repeated hooks
- Repeated arguments
- Repeated stories
- Repeated phrases
- Previously successful angles

Before generating content, the AI should check relevant historical content.

---

# 26. Voice Profile

The system should develop a structured representation of the user's writing style.

Possible attributes:

```text
Tone
Sentence length
Vocabulary
Formality
Humor
Storytelling preference
CTA style
Emoji usage
Formatting
Hook style
```

Users should also be able to explicitly define writing preferences.

---

# 27. User Workflow

The primary workflow:

```text
Connect social accounts
        ↓
Create brand profile
        ↓
Define audience
        ↓
Define content pillars
        ↓
Set goals
        ↓
AI generates content opportunities
        ↓
User selects opportunity
        ↓
AI develops content strategy
        ↓
AI generates post
        ↓
AI evaluates post
        ↓
User reviews
        ↓
User approves
        ↓
AI recommends time
        ↓
User schedules
        ↓
System publishes
        ↓
System collects performance
        ↓
AI analyzes results
        ↓
Future content improves
```

---

# 28. Functional Requirements

## FR-001 — User Authentication

The system must allow users to securely create accounts and authenticate.

---

## FR-002 — Brand Configuration

The system must allow users to create and update their brand profile.

---

## FR-003 — Audience Configuration

The system must allow users to define one or more target audiences.

---

## FR-004 — Content Pillars

The system must allow users to create, edit, reorder and deactivate content pillars.

---

## FR-005 — Content Ideas

The system must generate and store content opportunities.

---

## FR-006 — AI Content Generation

The system must generate platform-specific content using configured brand and audience context.

---

## FR-007 — Storytelling

The system must support multiple storytelling frameworks.

---

## FR-008 — Voice Adaptation

The system must adapt generated content to the user's configured writing style.

---

## FR-009 — Hashtag Recommendations

The system must recommend relevant hashtags when appropriate.

---

## FR-010 — Scheduling

The system must allow approved posts to be scheduled.

---

## FR-011 — Publishing

The system must publish scheduled posts through supported social APIs.

---

## FR-012 — Performance Tracking

The system must store available post-performance data.

---

## FR-013 — AI Analysis

The system must analyze historical performance and generate actionable insights.

---

## FR-014 — Content History

The system must maintain a searchable history of generated and published content.

---

# 29. Non-Functional Requirements

## Performance

Normal dashboard operations should feel responsive.

Target:

```text
API response:
< 500ms for common database operations
```

AI operations may take longer and should provide appropriate loading/progress states.

---

## Reliability

Scheduled publishing should be resilient to temporary API failures.

The system should support:

- Retries
- Idempotency
- Publishing logs
- Failure states

---

## Security

The system must:

- Encrypt sensitive credentials
- Secure OAuth tokens
- Protect API endpoints
- Validate user input
- Implement authorization
- Avoid exposing provider credentials
- Maintain audit logs for sensitive operations

---

## Scalability

The architecture should allow the system to evolve from:

```text
1 user
```

to:

```text
10,000+ users
```

without requiring a complete architectural rewrite.

---

# 30. AI Safety and Reliability

AI-generated content should not automatically be treated as factual.

The system should identify potentially risky content involving:

- Statistics
- News
- Claims about people
- Financial information
- Legal claims
- Medical information
- Breaking events

For these categories, the system should encourage verification before publication.

---

# 31. Success Metrics

Product success should be measured by:

### Content productivity

- Posts created per user
- Posts scheduled
- Posts published

### Engagement

- Average engagement
- Engagement rate
- Comments
- Shares
- Impressions

### Growth

- Follower growth
- Profile visits
- Click-through rate

### AI quality

- User approval rate
- AI regeneration rate
- User editing rate
- Content rejection rate

### Retention

- Weekly active users
- Monthly active users
- Returning users
- Posts published per active user

---

# 32. MVP Acceptance Criteria

V1 should be considered functional when a user can:

1. Create an account.
2. Configure their brand.
3. Define their audience.
4. Define content pillars.
5. Connect X.
6. Connect LinkedIn.
7. Generate content ideas.
8. Select an idea.
9. Generate a LinkedIn post.
10. Generate an X post.
11. Apply the user's voice.
12. Select a storytelling framework.
13. Receive hashtag recommendations.
14. Receive a posting-time recommendation.
15. Edit the generated content.
16. Approve content.
17. Schedule content.
18. Publish content.
19. View publishing status.
20. View basic performance data.

---

# 33. Future Vision

Future versions may introduce:

- Instagram
- Facebook
- TikTok
- YouTube
- Image generation
- Video generation
- AI-generated creatives
- Automated comments
- Social listening
- Competitor analysis
- Trend prediction
- Lead generation
- AI community management
- Team collaboration
- Agency mode
- Multiple brand management
- Advanced analytics
- Autonomous content management

The long-term product should move toward:

> **An AI social media manager that can independently manage a brand's content strategy while keeping the human in control.**

---

# 34. V1 Product Philosophy

Mayorx Content Engine should not compete by being the AI that writes the most posts.

It should compete by being the AI that makes **better content decisions**.

The central product question is therefore not:

> "What can AI write?"

It is:

> **"What should this brand say, to whom, why now, in what format, and where?"**

That principle should guide the architecture, AI system, UX and future development of the product.
# Mayorx Content Engine — Design System

## 1. Purpose

This document defines the visual language, interaction patterns, component standards, and UX principles for Mayorx Content Engine.

The design system exists to ensure that the product feels like one coherent professional SaaS application rather than a collection of independently designed screens.

It is designed for a modern AI-powered content management platform used by creators, professionals, marketers, businesses, and teams to plan, generate, review, schedule, publish, and learn from social content.

The design system should remain:

- Professional
- Clear
- Modern
- AI-native without feeling gimmicky
- Fast to navigate
- Information-dense without being overwhelming
- Accessible
- Responsive
- Consistent
- Easy to implement with Tailwind CSS and shadcn/ui

---

# 2. Design Philosophy

Mayorx Content Engine should feel like a serious productivity and intelligence tool.

It should not feel like:

- An AI chatbot wrapper
- A futuristic sci-fi dashboard
- A generic marketing template
- An overly colorful creator platform
- A complicated enterprise administration panel

The interface should communicate:

> **"This system understands my content and helps me make better decisions."**

### Core principles

#### 2.1 Clarity over decoration

Every visual element should have a purpose.

Avoid excessive:

- Gradients
- Glows
- Glassmorphism
- Decorative illustrations
- Animations
- Shadows
- Borders
- Floating elements

Visual hierarchy should come primarily from spacing, typography, grouping, and contrast.

---

#### 2.2 Information first

The application manages a large amount of structured information.

Users should be able to quickly answer:

- What should I post?
- What am I currently working on?
- What is scheduled?
- What has been published?
- How did it perform?
- What does the AI recommend next?
- What requires my attention?

---

#### 2.3 AI should feel integrated

AI should not feel like a separate chatbot sitting beside the product.

AI recommendations should appear naturally inside existing workflows.

Examples:

- Content opportunity recommendations
- Suggested hooks
- Suggested content angles
- Content evaluations
- Best-time recommendations
- Performance insights
- Draft improvements

AI-generated information must remain visually distinguishable from user-created or system-generated information.

---

#### 2.4 Human control remains visible

The product assists the user rather than silently making important decisions.

The interface should make it clear when:

- AI generated something
- AI recommended something
- The user edited something
- The user approved something
- The system scheduled something
- The system published something

---

# 3. Visual Direction

## 3.1 Overall Style

The visual language should be:

**Modern SaaS + Editorial Intelligence + Productivity**

Reference characteristics:

- Clean layouts
- Strong typography
- Subtle borders
- Moderate corner radius
- Neutral backgrounds
- One restrained brand accent
- Compact but comfortable spacing
- High information density
- Minimal visual noise

---

## 3.2 Visual Hierarchy

Every page should have a clear hierarchy:

1. Page title
2. Page-level context
3. Primary action
4. Main content
5. Secondary information
6. Supporting actions

Primary actions should be visually obvious without dominating the screen.

---

# 4. Color System

The color system should use neutral surfaces with a restrained purple brand accent.

The system must support both light and dark themes.

## 4.1 Semantic Colors

Use semantic tokens instead of hardcoding colors throughout components.

Recommended semantic tokens:

```text
background
foreground

card
card-foreground

popover
popover-foreground

primary
primary-foreground

secondary
secondary-foreground

muted
muted-foreground

accent
accent-foreground

destructive
destructive-foreground

border
input
ring
```

These should map to CSS variables so the theme can be changed without modifying component styles.

---

## 4.2 Brand Color

The primary brand direction is a restrained purple.

Purple should communicate:

- Intelligence
- Creativity
- Technology
- Strategy

It should not be used excessively.

Use the brand color primarily for:

- Primary buttons
- Active navigation
- Important links
- Selected states
- AI indicators
- Focus states
- Important data highlights

Do not make every card or section purple.

---

## 4.3 Semantic Status Colors

Use semantic colors consistently.

### Success

Used for:

- Published
- Successful connection
- Successful operation
- Positive metric changes

### Warning

Used for:

- Attention required
- Expiring connection
- Scheduled content approaching
- Content requiring review

### Destructive

Used for:

- Failed publication
- Removed content
- Disconnect actions
- Irreversible actions
- Validation failures

### Informational

Used for:

- AI insights
- System information
- Research findings
- Educational guidance

Semantic colors should never be the only indicator of meaning.

Pair color with:

- Text
- Icons
- Labels
- Status indicators

---

# 5. Typography

Use a modern sans-serif typeface.

Preferred hierarchy:

```text
Display
Heading 1
Heading 2
Heading 3
Heading 4
Body
Small
Caption
```

Recommended characteristics:

- High readability
- Strong numerical clarity
- Good support for technical terminology
- Good rendering at small sizes

The typography should feel similar to modern productivity tools rather than editorial magazines.

---

## 5.1 Suggested Scale

```text
text-xs   → 12px
text-sm   → 14px
text-base → 16px
text-lg   → 18px
text-xl   → 20px
text-2xl  → 24px
text-3xl  → 30px
text-4xl  → 36px
```

Most application UI should remain within:

```text
14px–24px
```

Large headings should be used sparingly.

---

## 5.2 Font Weight

Use:

```text
400 → Body
500 → Labels / secondary emphasis
600 → Buttons / headings
700 → Major headings
```

Avoid excessive use of bold text.

---

# 6. Spacing

Use Tailwind's spacing scale consistently.

Primary spacing rhythm:

```text
4px
8px
12px
16px
20px
24px
32px
40px
48px
64px
```

Prefer consistent spacing over arbitrary pixel values.

### Common patterns

Page padding:

```text
24px desktop
16px mobile
```

Card padding:

```text
20px–24px
```

Section spacing:

```text
32px–48px
```

Form field spacing:

```text
16px
```

---

# 7. Border Radius

Use moderate rounding.

Recommended:

```text
sm  → 6px
md  → 8px
lg  → 12px
xl  → 16px
```

Default application radius:

```text
8px–12px
```

Avoid excessive pill-shaped components unless the component represents:

- Status
- Tags
- Categories
- Filters

---

# 8. Borders

Borders should be subtle.

Primary uses:

- Card separation
- Input boundaries
- Table rows
- Navigation separators
- Dialog boundaries
- Section divisions

Avoid using heavy borders around every element.

A combination of:

```text
background
spacing
typography
subtle border
```

should establish hierarchy.

---

# 9. Shadows

Shadows should be subtle and functional.

Use shadows primarily for elevated elements:

- Dropdowns
- Dialogs
- Popovers
- Floating panels
- Menus

Cards should generally rely on borders and surface contrast instead of large shadows.

---

# 10. Layout System

## 10.1 Application Shell

The main application layout consists of:

```text
┌──────────────────────────────────────────────┐
│ Sidebar │ Top Bar                            │
│         ├────────────────────────────────────┤
│         │                                    │
│         │ Main Content                       │
│         │                                    │
│         │                                    │
│         │                                    │
└─────────┴────────────────────────────────────┘
```

Desktop:

- Persistent sidebar
- Top-level workspace context
- Main content area
- Optional contextual panel

Mobile:

- Collapsible sidebar
- Mobile navigation trigger
- Full-width content
- Bottom actions where appropriate

---

# 11. Navigation

Primary navigation should remain short.

Recommended navigation:

```text
Dashboard

Content
  Ideas
  Drafts
  Calendar

Analytics

Strategy

AI Insights

Connections

Settings
```

Navigation should prioritize the user's core workflow.

---

## 11.1 Active Navigation

The active item should use:

- Subtle background
- Brand accent
- Stronger text weight
- Optional icon emphasis

Avoid extremely bright active states.

---

## 11.2 Sidebar

The sidebar should contain:

### Top

- Mayorx Content Engine logo
- Workspace selector

### Middle

- Main navigation

### Bottom

- Notifications
- Help
- User profile

The sidebar should support collapsed mode on desktop.

---

# 12. Dashboard

The dashboard is the primary overview screen.

Its purpose is not to display every piece of information.

It should answer:

> "What should I know and do right now?"

Recommended structure:

```text
Page Header
    ↓
Quick Overview
    ↓
AI Recommendations
    ↓
Content Pipeline
    ↓
Upcoming Schedule
    ↓
Performance Snapshot
```

---

## 12.1 Dashboard Header

Example:

```text
Good morning, Mayowa

Here's what needs your attention today.

[Create Content]
```

The greeting should remain optional and should not dominate the interface.

---

## 12.2 KPI Cards

Potential metrics:

- Posts published
- Scheduled posts
- Engagement rate
- Audience growth
- Content opportunities

Cards should include:

```text
Metric
Current value
Change
Comparison period
```

Example:

```text
Engagement Rate

7.8%

↑ 1.4%
vs previous 30 days
```

Do not display a change without defining the comparison period.

---

# 13. Content Components

## 13.1 Content Idea Card

Used for AI-generated or manually created opportunities.

Structure:

```text
Content Pillar
Title / Hook
Short Description
Why it matters
Suggested platform
Suggested timing

[Generate Draft]
[Save]
```

AI-generated ideas should include a subtle AI indicator.

---

## 13.2 Draft Card

Displays content currently being developed.

Show:

- Title/hook
- Platform
- Content pillar
- Status
- Last updated
- Author
- AI evaluation score if available

Actions:

```text
Open
Edit
Duplicate
Schedule
Archive
```

---

# 14. Content Editor

The content editor is one of the most important screens.

It should prioritize writing.

Recommended layout:

```text
┌───────────────────────────────────────────────┐
│ Back       Draft title              Save      │
├───────────────────────────┬───────────────────┤
│                           │ AI Assistant       │
│ Content Editor            │                   │
│                           │ Hook suggestions  │
│                           │ Improvements      │
│                           │ Evaluation        │
│                           │                   │
└───────────────────────────┴───────────────────┘
```

On smaller screens, the AI panel becomes a drawer or collapsible section.

---

## 14.1 Editor Principles

The editor should:

- Keep writing space large
- Minimize distractions
- Autosave
- Clearly show platform
- Show character/word limits where relevant
- Preserve user edits
- Make AI suggestions optional

Never automatically overwrite user-written content.

---

# 15. Platform Tabs

Platform-specific content should use tabs when comparing:

```text
LinkedIn
X
```

Each platform preview should display:

- Platform name
- Character count
- Content preview
- Hashtags
- Media requirements if applicable
- Validation state

The platform preview should resemble the platform enough to provide useful context without attempting to perfectly clone its UI.

---

# 16. AI Generation Panel

AI generation should be contextual.

Possible controls:

```text
Content goal
Audience
Tone
Platform
Content angle
Length
CTA
```

Primary action:

```text
Generate Draft
```

After generation:

```text
Generated by AI

[Accept]
[Edit]
[Regenerate]
```

The interface must never imply that AI output is automatically correct.

---

# 17. AI Evaluation Panel

The evaluation panel should explain *why* content received a particular evaluation.

Example:

```text
Content Evaluation

Hook Strength       Strong
Audience Fit        Strong
Clarity             Good
Originality         Moderate
Platform Fit        Strong

Suggestions

• Make the opening more specific.
• Add a concrete example.
• Reduce the final paragraph.
```

Avoid presenting an unexplained single "AI score" as the truth.

If an overall score is displayed, show the underlying dimensions.

---

# 18. AI Recommendation UX

AI recommendations should answer:

```text
What is being recommended?
Why is it being recommended?
What evidence supports it?
What can I do next?
```

Example:

```text
AI Insight

Your recent technical posts generated more discussion
than your promotional posts.

Recommendation

Consider publishing another educational breakdown
this week.

[Create Draft]
```

When based on historical performance, indicate the relevant time period.

---

# 19. Content Calendar

The calendar should support:

- Day
- Week
- Month views

Each scheduled item should display:

- Platform
- Content title
- Status
- Scheduled time

Use platform icons and status indicators.

Avoid filling calendar cells with excessive information.

---

# 20. Publishing Workflow

The publishing workflow should be explicit.

Recommended sequence:

```text
Draft
  ↓
Review
  ↓
Approve
  ↓
Schedule
  ↓
Publish
```

Important actions should require confirmation where appropriate.

Before publishing, show:

```text
Platform
Account
Content
Scheduled time
Validation status
```

---

# 21. Status System

Content status should remain consistent throughout the application.

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

Statuses should have:

- Label
- Semantic color
- Optional icon

Never rely solely on color.

---

# 22. Analytics UI

Analytics should focus on decisions rather than vanity metrics.

Recommended sections:

```text
Performance Overview
    ↓
Platform Comparison
    ↓
Content Performance
    ↓
Audience Growth
    ↓
AI Performance Insights
```

Metrics may include:

- Impressions
- Likes
- Comments
- Shares
- Clicks
- Profile visits
- Followers gained
- Engagement rate

Charts should remain simple and readable.

---

# 23. Tables

Tables should be used when users need to compare or manage many records.

Recommended features:

- Column sorting
- Filtering
- Pagination
- Row actions
- Bulk actions where appropriate
- Responsive overflow

Avoid putting every possible field into one table.

Prioritize the information required for the task.

---

# 24. Forms

Forms should follow:

```text
Label
Helper text
Input
Validation message
```

Example:

```text
Content Goal

What do you want this content to achieve?

[Build authority ▼]

Choose the primary objective for this content.
```

Validation should happen as early as practical without becoming distracting.

---

# 25. Buttons

Button hierarchy:

### Primary

For the main action.

Examples:

```text
Create Content
Generate Draft
Publish
Save Changes
```

### Secondary

For supporting actions.

Examples:

```text
Preview
Edit
Schedule
```

### Ghost

For low-emphasis actions.

Examples:

```text
Cancel
Back
View Details
```

### Destructive

For dangerous actions.

Examples:

```text
Delete
Disconnect
Remove
```

Avoid multiple visually competing primary buttons in the same section.

---

# 26. Inputs and Controls

Use shadcn/ui primitives wherever possible.

Core controls:

- Input
- Textarea
- Select
- Combobox
- Checkbox
- Radio Group
- Switch
- Slider
- Date Picker
- Time Picker

All controls must have:

- Visible focus state
- Accessible label
- Clear error state
- Appropriate disabled state

---

# 27. Dialogs and Drawers

Use dialogs for focused actions.

Examples:

- Delete confirmation
- Connect social account
- Schedule post
- Create workspace

Use drawers for contextual workflows where preserving the underlying page is useful.

Examples:

- AI recommendations
- Draft details
- Content preview

Avoid deeply nested dialogs.

---

# 28. Toasts and Notifications

Toasts should communicate completed or failed actions.

Examples:

```text
Draft saved.
Post scheduled successfully.
LinkedIn account connected.
Failed to publish post.
```

Do not use toasts for information that users need to read carefully.

Use inline alerts for important information.

---

# 29. Empty States

Empty states should explain:

1. What is missing
2. Why it matters
3. What the user can do next

Example:

```text
No content ideas yet

Your content strategy will help generate relevant
ideas for your audience.

[Create Your First Idea]
```

Avoid empty states that simply say:

```text
No data.
```

---

# 30. Loading States

Use skeletons for content-heavy pages.

Examples:

- Dashboard cards
- Tables
- Content lists
- Analytics
- AI recommendations

Use spinners for short actions:

```text
Generating...
Saving...
Publishing...
```

Do not block the entire application unnecessarily.

---

# 31. Error States

Errors should be understandable and actionable.

Bad:

```text
Error 500
```

Better:

```text
We couldn't load your content.

Please try again.

[Retry]
```

For platform failures:

```text
LinkedIn couldn't publish this post.

Your connection may have expired.

[Reconnect LinkedIn]
```

Technical details such as trace IDs may be available behind an advanced/details section.

---

# 32. Responsive Design

The product must be usable across:

- Desktop
- Laptop
- Tablet
- Mobile

Primary design target:

```text
Desktop
```

Secondary:

```text
Tablet
Mobile
```

---

## 32.1 Desktop

Use multi-column layouts where they improve productivity.

Examples:

```text
Editor + AI panel
Analytics cards
Calendar
Content tables
```

---

## 32.2 Tablet

Collapse secondary panels.

For example:

```text
Editor
↓
AI panel as drawer
```

---

## 32.3 Mobile

Prioritize:

- Reading
- Reviewing
- Editing
- Approving
- Scheduling
- Monitoring

Complex workflows should become sequential rather than compressed into tiny columns.

---

# 33. Accessibility

Target:

**WCAG 2.2 AA**

Requirements include:

- Keyboard navigation
- Visible focus states
- Semantic HTML
- Proper form labels
- Accessible dialogs
- Screen-reader-friendly controls
- Sufficient contrast
- Reduced motion support
- No color-only communication
- Accessible tooltips
- Accessible error messages

Interactive components must be usable without a mouse.

---

# 34. Motion

Motion should communicate state changes rather than decorate the interface.

Use subtle transitions for:

- Hover
- Focus
- Expand/collapse
- Dialogs
- Sidebar transitions
- Loading states
- Content status changes

Recommended duration:

```text
100ms–200ms
```

Long animations should be rare.

Respect:

```text
prefers-reduced-motion
```

---

# 35. AI-Specific Visual Language

AI-related UI should have a consistent visual identity.

Use a subtle brand-accent treatment for:

- AI-generated content
- AI recommendations
- AI insights
- AI evaluations

However, avoid creating a separate "AI world" inside the application.

AI should feel like a capability of the product.

---

## 35.1 Generated vs User Content

The interface should distinguish:

```text
AI Generated
User Edited
User Created
System Generated
```

Example:

```text
AI Generated

This draft was generated from your content strategy.
```

If the user edits the content:

```text
AI Generated · Edited by you
```

---

## 35.2 AI Confidence

Avoid presenting uncertain AI outputs as facts.

Prefer:

```text
Likely
Recommended
Potential opportunity
Based on recent performance
AI estimate
```

Avoid:

```text
This will perform well.
This is the best time to post.
This topic will go viral.
```

---

## 35.3 Explain Recommendations

Where practical, recommendations should include a reason.

Example:

```text
Why this recommendation?

Your recent posts about backend engineering
received 32% more comments than your average
technical posts over the last 30 days.
```

---

# 36. Content Preview

Platform previews should provide useful context without becoming pixel-perfect clones.

Each preview should communicate:

- Platform
- Author/account
- Content
- Hashtags
- Timestamp/scheduled time
- Media placeholder if applicable
- Validation state

Preview components should be reusable.

Example:

```text
<ContentPreview platform="linkedin" />
<ContentPreview platform="x" />
```

---

# 37. Component Architecture

The frontend should use a layered component architecture.

```text
components/
├── ui/
├── layout/
├── navigation/
├── content/
├── ai/
├── analytics/
├── calendar/
├── social/
├── forms/
└── shared/
```

---

## 37.1 UI Components

Primitive components should generally come from shadcn/ui.

Examples:

```text
Button
Input
Textarea
Dialog
DropdownMenu
Popover
Tabs
Tooltip
Badge
Card
Table
Select
```

---

## 37.2 Feature Components

Feature components combine primitives into product-specific experiences.

Examples:

```text
ContentIdeaCard
ContentEditor
ContentEvaluationPanel
AIRecommendationCard
ContentCalendar
PlatformPreview
PublishingStatus
AnalyticsSummary
```

---

# 38. Naming Conventions

React components:

```text
PascalCase
```

Examples:

```text
ContentCard
AiInsight
ScheduleDialog
PlatformPreview
```

Hooks:

```text
useSomething
```

Examples:

```text
useContent
useWorkspace
useSocialAccounts
useAiGeneration
```

Utility functions:

```text
camelCase
```

Examples:

```text
formatMetric()
getContentStatus()
calculateEngagementRate()
```

---

# 39. Tailwind Conventions

Prefer semantic utility classes and reusable component variants.

Avoid large amounts of duplicated class strings.

Use:

```text
cn()
```

for conditional class composition.

Component variants should be defined through a consistent variant system where appropriate.

Avoid arbitrary values unless there is a real design requirement.

Bad:

```text
mt-[13px]
```

Prefer:

```text
mt-3
```

unless the design system specifically requires the custom value.

---

# 40. Theme System

The application should support:

```text
Light
Dark
System
```

Theme selection should be persisted.

All components must use semantic theme variables rather than direct color values.

Dark mode should not simply invert the light theme.

It should be designed intentionally with:

- Dark surfaces
- Appropriate contrast
- Muted borders
- Reduced shadow dependence
- Controlled brand color intensity

---

# 41. Design Tokens

Centralize tokens for:

```text
Colors
Typography
Spacing
Radius
Shadows
Motion
Breakpoints
Z-index
```

Example conceptual token structure:

```text
tokens/
├── colors
├── typography
├── spacing
├── radius
├── shadows
└── motion
```

The implementation may use CSS variables and Tailwind configuration depending on the final Next.js/Tailwind setup.

---

# 42. Z-Index Layers

Maintain a predictable stacking system.

Example:

```text
Base content       0
Sticky elements    10
Dropdowns          20
Popovers           30
Dialogs            40
Toast              50
Critical overlay   60
```

Avoid arbitrary z-index values throughout the application.

---

# 43. Interaction Principles

Every important action should have clear feedback.

Examples:

```text
Click Generate
→ Loading state
→ Generated content
→ Evaluation
→ User decision
```

```text
Click Publish
→ Validation
→ Confirmation
→ Publishing state
→ Success / Failure
```

Users should never have to guess whether an action worked.

---

# 44. Destructive Actions

Destructive actions should:

- Use destructive styling
- Clearly describe the action
- Require confirmation where necessary
- Avoid ambiguous wording

Example:

```text
Delete draft?

This will permanently remove this draft.

[Cancel] [Delete Draft]
```

For reversible actions, prefer:

```text
Archive
```

over:

```text
Delete
```

when appropriate.

---

# 45. Search and Filtering

Search should be available where content volume makes it useful.

Examples:

```text
Search content...
Search ideas...
Search published posts...
```

Filters should support relevant dimensions such as:

- Platform
- Status
- Content pillar
- Date
- Author
- Performance
- Content type

Filters should be easy to clear.

---

# 46. Date and Time

All backend timestamps are stored in UTC.

The UI should display dates and times using the user's configured timezone.

Where relevant, display timezone context.

Example:

```text
Sep 25, 2026 · 7:30 PM WAT
```

Relative timestamps may be used for recent events:

```text
2 hours ago
```

but the exact timestamp should remain accessible.

---

# 47. Notifications

Notifications should be grouped by importance.

Examples:

### Action Required

```text
LinkedIn connection expired.
```

### Publishing

```text
Your post was published successfully.
```

### AI Insight

```text
New performance insight available.
```

Avoid generating excessive notifications for routine AI activity.

---

# 48. Settings

Settings should be organized into logical groups:

```text
Profile
Workspace
Brand
Audience
Content Strategy
AI Preferences
Social Accounts
Notifications
Security
Billing
```

The settings interface should prioritize discoverability over visual complexity.

---

# 49. Onboarding

Onboarding should progressively collect the information required for useful AI recommendations.

Suggested sequence:

```text
Create Workspace
      ↓
Define Brand
      ↓
Define Audience
      ↓
Select Content Goals
      ↓
Create Content Pillars
      ↓
Connect Social Accounts
      ↓
Generate First Opportunities
```

Do not ask for every configuration value upfront.

The system should allow users to improve their profile over time.

---

# 50. UX for First Content

The first successful user experience should be:

```text
User configures brand
        ↓
User configures audience
        ↓
AI identifies opportunities
        ↓
User selects opportunity
        ↓
AI generates draft
        ↓
User edits/approves
        ↓
User schedules
        ↓
System publishes
```

This should be the primary product loop.

---

# 51. Empty-to-Value Principle

Every major empty state should move the user toward value.

For example:

```text
No content strategy
        ↓
Create strategy
        ↓
Generate opportunities
        ↓
Create content
```

The UI should always make the next meaningful action obvious.

---

# 52. Performance

The design system must support a fast application.

Avoid unnecessary:

- Large client-side bundles
- Heavy animations
- Excessive chart libraries
- Automatically loaded components
- Large image assets

Use:

- Server Components where appropriate
- Dynamic imports for heavy client components
- Skeleton loading
- Optimized images
- Virtualized lists for large datasets where necessary

---

# 53. Design System Principles Summary

The Mayorx Content Engine interface should consistently follow these principles:

1. **Clarity over decoration**
2. **Information over visual noise**
3. **AI integrated into workflows**
4. **Human control over important decisions**
5. **Explain recommendations**
6. **Use semantic design tokens**
7. **Keep interaction patterns consistent**
8. **Design responsive layouts from the beginning**
9. **Accessibility is a requirement**
10. **Motion should communicate, not distract**
11. **Use shadcn/ui primitives before inventing new ones**
12. **Prefer reusable feature components**
13. **Never hide important system states**
14. **Do not make AI output look unquestionably authoritative**
15. **Maintain a professional SaaS aesthetic**

---

# 54. Initial Component Inventory

The first implementation should prioritize these components.

### Foundation

```text
Button
Input
Textarea
Select
Combobox
Checkbox
Switch
Badge
Card
Separator
Tooltip
Popover
DropdownMenu
Dialog
Drawer
Tabs
Alert
Toast
Skeleton
```

### Layout

```text
AppShell
Sidebar
TopBar
PageHeader
Section
Container
ResponsiveGrid
```

### Content

```text
ContentIdeaCard
ContentCard
ContentEditor
ContentStatus
ContentFilters
ContentPreview
PlatformTabs
DraftActions
```

### AI

```text
AiBadge
AiRecommendationCard
AiGenerationPanel
AiEvaluationPanel
AiInsightCard
AiSuggestion
```

### Publishing

```text
ScheduleDialog
PublishDialog
PublishingStatus
PlatformSelector
SocialAccountSelector
```

### Analytics

```text
MetricCard
MetricChange
AnalyticsChart
PerformanceTable
PerformanceInsight
```

### Calendar

```text
ContentCalendar
CalendarEvent
CalendarToolbar
CalendarFilters
```

---

# 55. Final Design Principle

Mayorx Content Engine should feel like a **professional content operating system with intelligence built into it**.

The product should not compete for attention with its own interface.

The interface exists to help the user:

```text
Understand
    ↓
Decide
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
```

The design system should make this loop feel natural, predictable, and fast.
# Mayorx Content Engine — Frontend Design Specification

This document describes the visual and interaction language of the implemented Mayorx Content Engine web application. It is a practical reference for future product design and frontend work, not a request to redesign existing screens.

Use the running application and its shared styles/components as the source of truth for details not covered here. Preserve the product's real API-backed behavior: a visual treatment must never imply data, actions, or certainty that the application does not have.

## 1. Product visual philosophy

Mayorx is a content operating system for deciding what to say, creating it, publishing it, and learning from its performance. The primary interface is a working editorial environment, not an executive dashboard or an AI chat surface.

Prioritize:

- **Clarity before decoration.** Structure comes from typography, whitespace, alignment, and thin rules.
- **The work before its metadata.** Ideas and post copy are the main objects; scores, status, dates, and controls support decisions.
- **Human agency.** Make generation, evaluation, approval, scheduling, and publication distinct actions. AI proposes; people review; the application enforces real workflow rules.
- **Truthful states.** Show what the API returned, what is still loading, what failed, and what is unknown. Do not disguise an unavailable value as zero or an unsupported action as available.
- **Quiet confidence.** The interface should be composed, useful, and readable without being sterile or visually loud.

## 2. Overall brand and design direction

The current frontend combines editorial software with premium professional tooling: restrained, tactile, precise, and warm. It takes inspiration from the simplicity and finish of high-quality consumer software without copying another product's layouts or branding.

The visual identity is built from:

- A warm, near-black application frame and matte charcoal navigation.
- A bone/off-white working canvas with dark ink typography.
- A restrained brass/bronze accent used sparingly.
- Geist sans for interface and editorial text; Geist Mono for compact technical metadata where useful.
- Fine rules, calm surfaces, intentional whitespace, and subtle interaction feedback.

Do not reintroduce a generic purple AI palette or an alternate light/dark theme without an explicit product decision. For the implemented web product, the warm charcoal/bone/brass palette below is the reference.

## 3. Color and surface usage

The current CSS tokens are the palette reference:

| Role | Current token/value | Use |
|---|---|---|
| Outer background | `--background: #171715` | Application frame and dark surroundings |
| Main canvas | `--canvas: #f3f0e9` | Authenticated page content |
| Main text on dark | `--foreground: #eeeae2` | Brand and navigation on charcoal |
| Main text on canvas | `--canvas-foreground: #1c1b18` | Primary text on the light working surface |
| Raised dark surfaces | `--surface: #201f1c`, `--surface-raised: #292822` | Dark menus and raised application elements |
| Secondary text | `--muted: #9d998f`, `--muted-foreground: #706d66` | Supporting copy and metadata |
| Dark rule | `--border: #393832` | Shell and dark-surface dividers |
| Canvas rule | `--canvas-border: #ded9cf` | Page divisions, fields, and list rows |
| Accent | `--accent: #c5a36d` | Focus, selected emphasis, and limited brass accents |
| Strong accent | `--accent-strong: #a98248` | Stronger accent states where needed |
| Destructive | `--danger: #c87568` | Error and destructive-action semantics |
| Positive / warning | `--positive: #8ca38c`, `--warning: #c59b5e` | Semantic status support |

Prefer existing CSS variables and semantic component variants over adding one-off colors. Canvas components may use the established muted bone and pale stone shades for subtle surface contrast. Use high text contrast; muted text must remain readable at its actual size.

Dark charcoal belongs mainly to the shell; the editorial work area remains bone. Do not turn every section into a dark card. Use a surface change only when it clarifies hierarchy or interaction. Reserve blur for layered shell/navigation elements and subtle backdrops; it is not a substitute for layout.

## 4. Typography hierarchy

Use Geist as the default typeface and Geist Mono selectively for identifiers, small codes, ordered step numbers, and similarly technical metadata. Do not use display fonts, decorative serif styling, or monospaced text for long-form copy.

Use typography structurally:

1. **Page title:** one clear editorial statement, usually 34–58 px on desktop with tight leading and tracking; scale down on mobile without clipping.
2. **Section heading:** around 17–21 px, medium weight, tight but readable tracking.
3. **Primary content:** usually 13–16 px, with comfortable line-height. Draft copy should be larger and more open than its metadata.
4. **Labels and metadata:** generally 9–12 px. Keep these concise and secondary.
5. **Eyebrows:** small uppercase labels with deliberate letter spacing, such as `WORKSPACE / ANALYTICS` or `EDITORIAL REVIEW`.

Use weight sparingly. Avoid bolding every label or using oversized headings where the content does not warrant them. Keep titles and long text from becoming excessively wide; use readable line lengths and allow generated/user copy to wrap.

## 5. Spacing and density

Use a consistent rhythm based on 4, 8, 12, 16, 20, 24, 28, 32, 40, and 48 px. Current pages typically have 42 px top padding on desktop and 26 px on mobile; page gutters are fluid on desktop and approximately 18 px on mobile, tightening to 14 px at very narrow widths.

Separate major sections with 24–32 px of space and a subtle rule where it clarifies the transition. Forms use compact, consistent field gaps; editorial content needs more vertical breathing room. Prefer a clean, information-dense list over a sparse grid of oversized cards.

Do not solve density by shrinking text or controls below comfortable reading and touch sizes. Remove repeated decoration and redundant labels before compressing meaningful content.

## 6. Layout and grid principles

- Keep page content fluid and centered, with a practical maximum width near 1520 px.
- Use `min-width: 0` and flexible grid tracks so long titles, hashtags, and API text cannot force horizontal overflow.
- Use asymmetric columns when they clarify roles, such as a narrower idea/draft queue beside a larger review/editor pane.
- Prefer explicit page sections, lists, and definition lists to arbitrary dashboard-card grids.
- Use desktop side-by-side layouts only when both panes remain comfortably readable.
- At mobile widths, convert complex split workspaces into an intentional list → detail experience instead of squeezing both columns.
- Keep the main editorial copy visually dominant; metadata and action areas follow it.

## 7. Application shell and navigation

The desktop shell uses a fixed-feeling, sticky 252 px charcoal rail with brand lockup, workspace switcher, grouped primary navigation, and account/sign-out controls. The light canvas occupies the working area. A restrained top bar identifies the current destination and account context.

Navigation groups reflect the product's work: Workspace, Content, Strategy, Insights, Distribution, and System. Keep destinations canonical and use the most specific current route for active state. Mark the active destination semantically (for example, `aria-current="page"`), not by color alone.

At widths below about 920 px, replace the rail with a compact sticky charcoal top bar, accessible menu trigger, and workspace switcher. Put the full grouped navigation and account actions in a left-side modal drawer. Selecting a destination closes the drawer. Preserve a visible workspace identity/switch control on mobile.

## 8. Page headers

Use a small contextual eyebrow, one descriptive page title, and a short sentence that explains the page's purpose or data boundary. A primary action may sit alongside the title on wide screens, but should wrap or stack cleanly on small screens.

The header normally ends with a thin divider before the main workflow. Avoid generic greetings, decorative hero imagery, oversized calls to action, and title copy that says nothing about the work. Put filters or foundation context in a subordinate band below the title rather than competing with it.

## 9. Buttons and actions

Use the shared button hierarchy:

- **Primary:** one main forward action in a local context (for example, Generate, Save, Approve, or Schedule).
- **Secondary:** a useful but non-dominant action.
- **Quiet:** navigation, dismissal, or low-emphasis controls.
- **Danger:** rejection, disconnect, or destructive operations; pair with clear language and confirmation where appropriate.

Buttons are compact, mostly square-cornered controls (current default minimum height is about 38 px). Do not turn every action into a pill. Use explicit action labels, visible keyboard focus, disabled states, and an in-place busy indicator. Avoid multiple competing primary actions in the same small region.

Do not show a control for an unsupported backend operation. If an operation cannot be undone, explain this before confirmation. A successful mutation should give concise feedback and update the visible data without requiring a page reload.

## 10. Forms and inputs

Use a visible, programmatically associated label for every input. Generate unique IDs when multiple instances/forms can coexist. Use hint text for constraints and actionable validation messages connected to the relevant control.

Inputs use a clear neutral boundary, bone surface, dark text, and a restrained brass focus ring. Current standard controls are about 42 px high; textareas should allow comfortable writing and retain vertical resize where appropriate. Keep labels compact but readable, and group related fields rather than enclosing every field in a card.

Use native semantic controls for dates, times, selects, and numbers when they fit the task. Explain local-time behavior for scheduling. Preserve unsaved edits visibly; do not silently discard them or allow actions that require saved content while the editor is dirty.

## 11. Cards and surfaces

The default separator is whitespace plus a fine rule, not a floating card. Use a contained surface for a genuinely discrete interactive object, menu, dialog, form group, or focused status area. Elevation and shadows are reserved for overlays and should remain subtle.

Avoid excessive corner radii. The current working interface favors square controls and restrained corners; use a compact badge shape only for short statuses or categories. Do not wrap every section, metric, or row in a rounded rectangle.

## 12. Lists and tables

Lists are first-class product surfaces. Use semantic ordered/unordered lists and buttons or links for selectable rows. A selectable row should expose its selected state (for example, `aria-pressed`), provide a quiet hover/selected surface, and show useful item text before secondary metadata.

Use thin row dividers and consistent vertical padding. Keep status, platform, and dates compact and aligned. Truncate only secondary previews; let the detail view show the full content. Use a horizontally scrollable wrapper for genuinely tabular data on narrow screens rather than allowing the page itself to overflow.

Do not use a table for editorial copy or an arbitrary list of cards for a simple queue.

## 13. Editorial and content interfaces

The content itself is the primary object. In Ideas, present title, hook, and angle clearly; metadata and scores support the review decision. In Drafts, the opening hook and full editable post copy should dominate the detail view. Provide a writing area with comfortable line-height, adequate height, and no distracting chrome.

Keep source idea, content type, platform, timestamps, hashtags, evaluation, and workflow controls visible but secondary to the draft. Distinguish editable Draft status from read-only states. Make save, evaluation, approval, and schedule prerequisites explicit.

In Publishing, prioritize chronological order, status, platform, scheduled date/time, and a readable preview. Let the user inspect a selected post and its actual scheduling state. Do not imply a manual publish or cancel action exists when the API does not provide one.

## 14. AI-generated content presentation

AI output is product content, not decoration. Present it in the same editorial typographic system as human-authored content, with a quiet, explicit provenance cue where the distinction matters (such as generated idea, model recommendation, or evaluation).

Keep the user in control: show the generated result for inspection, offer supported edit/review actions, and distinguish recommendation from measured evidence. Never use sparkles, robot imagery, glowing panels, purple gradients, or a separate chatbot visual language to signal AI.

Never insert optimistic/fabricated output as though the backend returned it. On a failed generation request, show a useful human-readable error, restore the action to a usable state, keep the dialog/context usable, and do not add an unpersisted item to the list.

## 15. Status indicators

Use concise text badges with consistent semantic tones; color is supplementary and must never be the only status signal. Current shared badge tones are neutral, accent, positive, warning, and negative.

Map the actual backend status to its label and tone. Examples in current content workflows include Draft (accent), Approved and Published (positive), Scheduled (warning), and Rejected (negative). Do not infer a state from appearance, a missing field, a social account's presence, or elapsed time. Distinguish an operational note such as “Awaiting publishing worker” from the backend status itself.

## 16. Dialogs and drawers

Use a semantic modal dialog for focused generation, confirmation, and navigation tasks. Give it a clear title, close button with an accessible name, labelled fields, and a predictable action row. Keep the dialog surface bone, with a subtle border and a restrained dark translucent backdrop.

Use a side drawer for navigation or a genuinely adjacent workflow, not as a decorative floating panel. On mobile, constrain dialog width to the viewport and preserve usable padding. Ensure Escape/close behavior, focus containment/return, and sensible initial focus. Do not put a long page's entire workflow inside a modal.

## 17. Empty states

An empty state should explain what is absent and give the next useful action when one exists. Distinguish:

- A genuinely empty workspace.
- No results matching current filters.
- Missing required foundations.
- No measured history yet.

Use a short eyebrow only when it adds context, a direct title, one explanatory sentence, and an optional relevant action. Do not fill emptiness with sample posts, fake activity, placeholder scores, or a decorative illustration.

## 18. Loading states

Use concise status text and restrained skeletons that resemble the shape of the content being loaded. Mark meaningful loading regions with `role="status"` or an appropriate live announcement. Preserve page structure while data loads when practical.

Use button busy/disabled state for an in-flight mutation and prevent duplicate submission. Do not display stale content as current after a workspace transition. Skeleton shimmer and spinner motion must honor reduced-motion preferences.

## 19. Error states

Errors should name the operation or resource that failed, provide a retry action when retry is safe, and retain relevant user-entered context where possible. Use an alert semantic for actionable failures and a clear destructive accent/rule; do not rely on red color alone.

Render sanitized, meaningful API messages. Do not expose stack traces, raw implementation details, credentials, or opaque error payloads. If a trace/reference ID is provided and useful for support, show it as a secondary reference. Separate an unavailable service from an empty successful result.

## 20. Notifications and feedback

Use small, local feedback beside the action or section that caused it. Success messages should confirm the result plainly; errors should explain what failed and what the user can do next. Announce dynamic success/error messages with `role="status"` or `role="alert"` as appropriate.

Prefer persistent inline feedback for important workflow outcomes over disappearing toast-only messages. Do not show success before the API confirms it, and do not make a failed operation look complete.

## 21. Motion and transitions

Motion is restrained and functional: brief color/surface feedback, a small busy spinner, and subtle loading skeletons. Existing interaction transitions are typically around 130–140 ms. Avoid page choreography, parallax, bouncing, spring effects, and animation that delays access to content.

Honor `prefers-reduced-motion: reduce` for transitions, scroll behavior, shimmer, and spinners. Focus changes during mobile list/detail transitions should be deliberate: focus the meaningful detail heading on entry, and restore focus to the triggering row or a sensible list item on return. Do not alter desktop focus behavior unnecessarily.

## 22. Responsive and mobile behavior

Treat mobile as a purposeful workflow, not a compressed desktop screenshot. Preserve content priority and use a list → selected detail pattern for Ideas, Drafts, and Publishing. Hide the inactive pane only when the corresponding selection transition is explicit and reversible.

At approximately 375 px:

- Keep page-level horizontal overflow at zero.
- Allow titles, metadata, and long generated text to wrap.
- Stack schedule controls and secondary form fields when needed.
- Let filters wrap or become a simple vertical group.
- Keep primary actions visible and touchable.
- Put Back to list/queue near the top of detail.
- Preserve visible focus and meaningful focus restoration.

Test around 380 px as well as standard desktop/tablet breakpoints. Do not force a desktop calendar grid onto a narrow viewport.

## 23. Accessibility expectations

- Use semantic landmarks, a logical heading hierarchy, and list/article/form elements appropriate to their content.
- Give every control an accessible name. Associate labels and hints with controls; ensure IDs are unique across simultaneously mounted views.
- Preserve visible `:focus-visible` treatment and keyboard operation for navigation, filters, dialogs, lists, and actions.
- Expose selected, disabled, busy, and current states semantically.
- Move focus predictably when mobile content panes change; do not add arbitrary `tabIndex` values unless needed to focus a meaningful non-interactive heading.
- Pair status color with text; provide sufficient contrast for small metadata and controls.
- Respect reduced-motion settings.
- Use accessible date/time labels and identify which timezone a scheduling field represents.

## 24. Data visualization rules

Only visualize measurements returned by the backend. Do not invent trends, comparisons, totals, percentages, confidence, or engagement numbers to fill space. A measured zero is different from null/unreported; render null as “Not reported” (or similarly explicit language), never as zero.

Label units, period, and source context. Aggregated values must use the backend's actual supported filters and dimensions. If the backend has no time series or grouping, use a truthful table/list or summary rather than a fake chart. Avoid charts unless they answer a real user question and the returned data supports them.

## 25. Page composition rules

A typical authenticated page follows this order:

1. Context eyebrow and specific title.
2. One-sentence explanation or relevant workspace context.
3. Primary action or filters, when the task requires them.
4. Main working object: list, editor, queue, configuration, or measured report.
5. Supporting metadata, provenance, limitations, and secondary actions.

Group content by meaning rather than putting every group in a card. Use an editorial asymmetry only when it clarifies hierarchy. Keep section headings, content edges, and dividers aligned. Ensure the page still makes sense if every decorative treatment is removed.

## 26. Workflow-specific design principles

### Dashboard / workspace

Orient the user in the active workspace and the strategy → ideas → drafts → publishing flow. Foundation readiness, real content counts, and the next scheduled items may help users decide where to work next. Counts must be derived from real workspace responses. Avoid greetings, generic KPI grids, fabricated activity, and widget overload.

### Content overview

Provide a useful index into the lifecycle and a truthful snapshot of current work. Link to the dedicated workflow surfaces; do not duplicate complex workflow actions in a summary view.

### Strategy

Treat brand profile, audience profile, content strategy, and pillars as connected foundations for all downstream content. Show configuration state explicitly (configured, not configured, checking, unavailable). Make forms readable and save feedback local. Keep pillar actions and their real attributes together without turning every item into a floating card.

### Ideas

Make discovery/editorial judgment primary. The list should make idea title/hook, status, platform, and useful score glanceable; selected detail should lead with title, hook, angle, and rationale. Actual scoring signals support review but do not replace the idea itself. Clearly show when brand, audience, or strategy context is missing and block generation when the backend requires it. Approval, rejection, and draft generation must reflect real status and API outcomes.

### Drafts

Make writing the dominant experience. The editor should feel calm and comfortable, with primary post copy much larger/taller than metadata fields. Keep evaluation and scoring subordinate but easy to inspect. Make dirty/unsaved state and action prerequisites explicit. Only show edit, regenerate, evaluate, approve, or schedule actions in states supported by the backend.

### Publishing

Make the queue operational and time-oriented. Prioritize real status, platform, scheduled local date/time, copy preview, and the selected post's next supported action. Sort and filter only on available data. Explain worker behavior and account readiness honestly. Never imply that a scheduled post can be cancelled or manually published unless an API operation exists.

### Analytics

Keep the page information-dense but calm: clear reporting period and filters, measured summary values, then real post-level records. Preserve “not reported” versus zero. Do not create chart trends, platform comparisons, or performance claims without supporting API data.

### AI Insights / Learning

Visually and verbally distinguish **measured observation** from **model recommendation**. Show evidence count, confidence only if returned, and the measured period where available. Explain insufficient sample sizes plainly; a learning run with no adequate evidence must not produce a fabricated finding. Keep AI recommendations supportive rather than dominant.

### Connections

Make authorization state explicit for each supported platform. Show only account metadata actually returned; never reveal tokens. Explain that connection state and publishing readiness are distinct when that is true. A failed account request is an error state, not an empty list or disconnected account.

### Settings

Keep settings simple, purposeful, and constrained to supported controls. Present role and permission boundaries clearly. Do not add toggles, billing, team management, notifications, or security controls that the application does not implement.

## 27. Design anti-patterns

Do not use:

- Neon or purple/blue gradients, glowing blobs, random orbs, or “AI sparkles.”
- Generic AI chatbot styling or template-dashboard conventions.
- Excessive glassmorphism, blur, shadows, or rounded containers.
- Giant card grids, dashboard-card overload, or many decorative statistics.
- Fake metrics, fake charts, sample production content, fabricated status, or placeholder analytics.
- Decoration with no navigational, informational, or interaction purpose.
- Generic centered hero layouts inside working pages.
- Arbitrary content density, tiny low-contrast labels, or oversized empty whitespace.
- Multiple competing primary actions, status expressed only by color, or success-shaped fallback UI after an error.
- Unavailable product controls presented as disabled “coming soon” features unless a product requirement explicitly asks for them.

## 28. Frontend quality checklist

Before considering a page or feature complete, verify:

- [ ] The screen supports a real step in the content operating system and has one obvious content/action hierarchy.
- [ ] Its layout, palette, type, rules, and controls match this specification and the shared frontend primitives.
- [ ] The primary content is more prominent than supporting metadata.
- [ ] All displayed product data and status are sourced from the real application state/API; null, empty, loading, and error are distinct.
- [ ] Every action is actually supported, state-appropriate, and gives confirmed feedback.
- [ ] Forms have unique IDs, associated labels, useful hints/validation, and preserved edits on failure.
- [ ] Lists, status, dialogs, loading states, and errors are accessible by keyboard and assistive technology.
- [ ] Mobile layout at 375 px has no horizontal overflow and keeps the workflow usable.
- [ ] Mobile selection transitions have sensible focus entry/return; desktop behavior remains stable.
- [ ] Reduced-motion preferences are respected.
- [ ] Workspace-specific information cannot visually leak across workspace switches.
- [ ] No unsupported API behavior, fake product data, fabricated metrics, or misleading success state was added.

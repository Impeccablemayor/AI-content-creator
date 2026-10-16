"use client";

import Link from "next/link";
import { ApiClientError } from "@/lib/api/client";
import { useSession } from "@/components/session/session-provider";
import { Badge, EmptyState, ErrorState } from "@/components/ui/primitives";
import {
  useAudienceProfile,
  useBrandProfile,
  useContentIdeas,
  useContentPosts,
  useContentStrategy,
  useCurrentWorkspace,
} from "@/lib/queries/hooks";
import type { ContentPostStatus } from "@/lib/api/models";

const workflow = [
  { title: "Strategy", description: "Define the brand, audience, and content direction.", href: "/strategy", step: "01" },
  { title: "Ideas", description: "Generate and review opportunities grounded in that context.", href: "/content/ideas", step: "02" },
  { title: "Drafts", description: "Write, evaluate, and approve platform-ready content.", href: "/content/drafts", step: "03" },
  { title: "Publishing", description: "Schedule approved work and follow its backend status.", href: "/content/calendar", step: "04" },
];

function countStatus(status: ContentPostStatus, posts: Array<{ status: ContentPostStatus }>): number {
  return posts.filter((post) => post.status === status).length;
}

function foundationState(
  query: { isPending: boolean; isSuccess: boolean; isError: boolean; error: unknown },
  notFoundCode: string,
): { label: string; tone: "positive" | "warning" | "negative" } {
  if (query.isPending) return { label: "Checking", tone: "warning" };
  if (query.isSuccess) return { label: "Configured", tone: "positive" };
  if (
    query.isError &&
    query.error instanceof ApiClientError &&
    query.error.status === 404 &&
    query.error.code === notFoundCode
  ) {
    return { label: "Needs setup", tone: "warning" };
  }
  return { label: "Unavailable", tone: "negative" };
}

function formatDate(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? "Date unavailable"
    : new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" }).format(date);
}

export default function HomePage() {
  const { session } = useSession();
  const workspaceId = session?.workspaceId ?? "";
  const workspace = useCurrentWorkspace(workspaceId);
  const posts = useContentPosts(workspaceId);
  const ideas = useContentIdeas(workspaceId);
  const brand = useBrandProfile(workspaceId);
  const audience = useAudienceProfile(workspaceId);
  const strategy = useContentStrategy(workspaceId);
  const postItems = posts.data ?? [];
  const ideasPending = ideas.isPending;
  const foundations = [
    { label: "Brand", state: foundationState(brand, "BRAND_PROFILE_NOT_FOUND") },
    { label: "Audience", state: foundationState(audience, "AUDIENCE_PROFILE_NOT_FOUND") },
    { label: "Strategy", state: foundationState(strategy, "CONTENT_STRATEGY_NOT_FOUND") },
  ];
  const scheduled = postItems
    .filter((post) => post.status === "Scheduled" && post.scheduledAt)
    .sort((left, right) => (left.scheduledAt ?? "").localeCompare(right.scheduledAt ?? ""))
    .slice(0, 5);

  return (
    <main className="ideas-page">
      <header className="ideas-page-heading">
        <div className="flex flex-wrap items-end justify-between gap-5">
          <div>
            <p className="eyebrow">MAYORX / WORKSPACE</p>
            <h1 className="mt-3 text-[clamp(34px,4.1vw,58px)] font-medium leading-[1.02] tracking-[-0.065em] text-[#22211d]">
              A clear line from strategy to publish.
            </h1>
            <p className="ideas-page-intro">
              {workspace.data
                ? `Active workspace · ${workspace.data.name}`
                : "Your content work, in one place."}
            </p>
          </div>
        </div>
        <div className="grid gap-0 border-t border-[#e1ddd4] sm:grid-cols-3">
          {foundations.map(({ label, state }) => (
            <div key={label} className="flex items-center justify-between border-b border-[#e1ddd4] py-3 sm:mr-5 sm:border-b-0 sm:border-r sm:pr-5">
              <span className="text-xs text-[#625e56]">{label} foundation</span>
              <Badge tone={state.tone}>{state.label}</Badge>
            </div>
          ))}
        </div>
      </header>

      <section className="grid gap-8 border-b border-[#d7d2c8] py-7 md:grid-cols-[0.75fr_1.25fr]" aria-labelledby="content-at-a-glance">
        <div>
          <p className="eyebrow">LIVE WORKSPACE STATE</p>
          <h2 id="content-at-a-glance" className="mt-2 text-xl font-medium tracking-[-0.04em] text-[#2d2b26]">
            Content at a glance
          </h2>
          <p className="mt-2 max-w-sm text-sm leading-6 text-[#77736a]">
            Counts are derived from the current workspace&apos;s real idea and post responses.
          </p>
        </div>
        {posts.isPending ? (
          <p className="text-sm text-[#77736a]" role="status">Loading workspace content…</p>
        ) : posts.isError ? (
          <ErrorState message={posts.error.message} onRetry={() => void posts.refetch()} />
        ) : (
          <dl className="grid grid-cols-2 gap-x-6 gap-y-4 sm:grid-cols-3">
            {(["Draft", "Approved", "Scheduled", "Published", "Rejected"] as ContentPostStatus[]).map((status) => (
              <div className="border-t border-[#d7d2c8] pt-3" key={status}>
                <dt className="text-xs text-[#77736a]">{status}</dt>
                <dd className="mt-1 text-2xl font-medium tracking-[-0.04em] text-[#302e29]">
                  {countStatus(status, postItems)}
                </dd>
              </div>
            ))}
            <div className="border-t border-[#d7d2c8] pt-3">
              <dt className="text-xs text-[#77736a]">Ideas to review</dt>
              <dd className="mt-1 text-2xl font-medium tracking-[-0.04em] text-[#302e29]">
                {ideasPending ? "…" : ideas.isError ? "—" : ideas.data.filter((idea) => idea.status === "Suggested").length}
              </dd>
            </div>
          </dl>
        )}
      </section>

      {ideas.isError ? (
        <p className="border-b border-[#d7d2c8] py-3 text-xs text-[#914a42]" role="status">
          Idea counts are unavailable: {ideas.error.message}
          <button type="button" className="ml-2 underline" onClick={() => void ideas.refetch()}>Retry</button>
        </p>
      ) : null}

      <section className="grid gap-7 border-b border-[#d7d2c8] py-7 lg:grid-cols-[0.72fr_1.28fr]" aria-labelledby="workflow-heading">
        <div>
          <p className="eyebrow">THE WORKFLOW</p>
          <h2 id="workflow-heading" className="mt-2 text-xl font-medium tracking-[-0.04em] text-[#2d2b26]">
            Move the work forward
          </h2>
        </div>
        <ol className="m-0 grid list-none gap-0 p-0 sm:grid-cols-2">
          {workflow.map((step) => (
            <li key={step.href} className="border-t border-[#d7d2c8] sm:mr-5">
              <Link href={step.href} className="block py-4 text-inherit no-underline transition-colors hover:bg-[#ebe8e0]">
                <span className="font-mono text-[10px] text-[#8d887e]">{step.step}</span>
                <span className="ml-3 text-base font-medium text-[#302e29]">{step.title}</span>
                <span className="mt-2 block text-sm leading-6 text-[#77736a]">{step.description}</span>
              </Link>
            </li>
          ))}
        </ol>
      </section>

      <section className="pt-7" aria-labelledby="scheduled-content">
        <div className="flex flex-wrap items-end justify-between gap-3 border-b border-[#d7d2c8] pb-4">
          <div>
            <p className="eyebrow">PUBLISHING QUEUE</p>
            <h2 id="scheduled-content" className="mt-2 text-xl font-medium tracking-[-0.04em] text-[#2d2b26]">
              Next scheduled
            </h2>
          </div>
          <Link className="text-xs font-medium text-[#685437] underline underline-offset-4" href="/content/calendar">
            Open publishing
          </Link>
        </div>
        {posts.isSuccess && scheduled.length === 0 ? (
          <EmptyState
            title="Nothing is scheduled yet"
            description="Once a draft is approved, schedule it in the publishing workspace."
            action={<Link className="button button-secondary" href="/content/drafts">Review drafts</Link>}
          />
        ) : posts.isSuccess ? (
          <ul className="m-0 list-none p-0">
            {scheduled.map((post) => (
              <li key={post.id} className="grid min-w-0 gap-2 border-b border-[#e3ded4] py-4 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center">
                <p className="line-clamp-2 break-words text-sm leading-6 text-[#302e29]">
                  {post.content || post.hook || "Content text unavailable"}
                </p>
                <p className="text-xs text-[#77736a]">{post.platform} · {formatDate(post.scheduledAt ?? post.createdAt)}</p>
              </li>
            ))}
          </ul>
        ) : null}
      </section>
    </main>
  );
}

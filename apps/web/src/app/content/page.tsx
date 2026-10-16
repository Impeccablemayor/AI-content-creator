"use client";

import Link from "next/link";
import { useSession } from "@/components/session/session-provider";
import { Badge, EmptyState, ErrorState } from "@/components/ui/primitives";
import {
  useContentIdeas,
  useContentPosts,
} from "@/lib/queries/hooks";
import type { ContentPostStatus } from "@/lib/api/models";

function statusTone(status: ContentPostStatus): "neutral" | "accent" | "positive" | "warning" | "negative" {
  switch (status) {
    case "Draft":
      return "accent";
    case "Approved":
    case "Published":
      return "positive";
    case "Scheduled":
      return "warning";
    case "Rejected":
      return "negative";
  }
}

function formatDate(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? "Not available"
    : new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" }).format(date);
}

export default function ContentPage() {
  const { session } = useSession();
  const workspaceId = session?.workspaceId ?? "";
  const posts = useContentPosts(workspaceId);
  const ideas = useContentIdeas(workspaceId);
  const postItems = posts.data ?? [];
  const ideaItems = ideas.data ?? [];
  const postStatuses: ContentPostStatus[] = ["Draft", "Approved", "Scheduled", "Published", "Rejected"];

  return (
    <main className="ideas-page">
      <header className="ideas-page-heading">
        <div>
          <p className="eyebrow">WORKSPACE / CONTENT</p>
          <h1 className="mt-3 text-[clamp(34px,4.1vw,58px)] font-medium leading-[1.02] tracking-[-0.065em] text-[#22211d]">
            From a good idea to a post in the world.
          </h1>
          <p className="ideas-page-intro">
            One working view of content in this workspace. Counts and recent posts below come
            from the content API; the workflow actions live in their dedicated spaces.
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Link className="button button-secondary" href="/content/ideas">Explore ideas</Link>
          <Link className="button button-primary" href="/content/drafts">Open drafts</Link>
        </div>
      </header>

      <nav className="grid gap-0 border-b border-[#d7d2c8] sm:grid-cols-3" aria-label="Content workflow">
        {[
          { step: "01", title: "Ideas", detail: "Discover and review", href: "/content/ideas" },
          { step: "02", title: "Drafts", detail: "Edit and evaluate", href: "/content/drafts" },
          { step: "03", title: "Publishing", detail: "Schedule and monitor", href: "/content/calendar" },
        ].map((item) => (
          <Link
            key={item.href}
            href={item.href}
            className="group border-b border-[#d7d2c8] py-5 pr-5 text-inherit no-underline transition-colors hover:bg-[#ebe8e0] sm:border-r sm:px-5 sm:last:border-r-0"
          >
            <span className="font-mono text-[10px] text-[#8d887e]">{item.step}</span>
            <span className="mt-2 block text-lg font-medium tracking-[-0.03em] text-[#302e29]">{item.title}</span>
            <span className="mt-1 block text-xs text-[#77736a]">{item.detail}</span>
          </Link>
        ))}
      </nav>

      <section className="grid gap-8 border-b border-[#d7d2c8] py-7 md:grid-cols-[0.7fr_1.3fr]" aria-labelledby="workflow-state">
        <div>
          <p className="eyebrow">CURRENT WORK</p>
          <h2 id="workflow-state" className="mt-2 text-xl font-medium tracking-[-0.04em] text-[#2d2b26]">
            Content by status
          </h2>
        </div>
        {posts.isPending ? (
          <p className="text-sm text-[#77736a]" role="status">Loading workspace content…</p>
        ) : posts.isError ? (
          <ErrorState message={posts.error.message} onRetry={() => void posts.refetch()} />
        ) : (
          <dl className="grid grid-cols-2 gap-x-6 gap-y-4 sm:grid-cols-3">
            {postStatuses.map((status) => (
              <div className="border-t border-[#d7d2c8] pt-3" key={status}>
                <dt className="text-xs text-[#77736a]">{status}</dt>
                <dd className="mt-1 text-2xl font-medium tracking-[-0.04em] text-[#302e29]">
                  {postItems.filter((post) => post.status === status).length}
                </dd>
              </div>
            ))}
            <div className="border-t border-[#d7d2c8] pt-3">
              <dt className="text-xs text-[#77736a]">Ideas awaiting review</dt>
              <dd className="mt-1 text-2xl font-medium tracking-[-0.04em] text-[#302e29]">
                {ideas.isSuccess ? ideaItems.filter((idea) => idea.status === "Suggested").length : "—"}
              </dd>
            </div>
          </dl>
        )}
        {ideas.isError ? (
          <p className="text-xs text-[#914a42]" role="alert">
            Idea status is unavailable: {ideas.error.message}
            <button className="ml-2 underline" type="button" onClick={() => void ideas.refetch()}>Retry</button>
          </p>
        ) : null}
      </section>

      <section className="pt-7" aria-labelledby="recent-posts-title">
        <div className="flex flex-wrap items-end justify-between gap-3 border-b border-[#d7d2c8] pb-4">
          <div>
            <p className="eyebrow">CONTENT POSTS</p>
            <h2 id="recent-posts-title" className="mt-2 text-xl font-medium tracking-[-0.04em] text-[#2d2b26]">
              Recent work
            </h2>
          </div>
          {posts.isSuccess ? (
            <span className="text-xs text-[#77736a]">{postItems.length} returned by the API</span>
          ) : null}
        </div>
        {posts.isPending ? (
          <p className="py-7 text-sm text-[#77736a]" role="status">Loading content posts…</p>
        ) : posts.isError ? (
          <div className="py-6">
            <ErrorState message={posts.error.message} onRetry={() => void posts.refetch()} />
          </div>
        ) : postItems.length === 0 ? (
          <EmptyState
            title="This workspace has no content posts yet"
            description="Start with an idea, review it, and generate a draft from the Ideas workspace."
            action={
              <Link className="button button-primary" href="/content/ideas">Go to ideas</Link>
            }
          />
        ) : (
          <ul className="m-0 list-none p-0">
            {postItems.slice(0, 12).map((post) => (
              <li key={post.id} className="grid min-w-0 gap-3 border-b border-[#e3ded4] py-5 md:grid-cols-[minmax(0,1fr)_auto_auto] md:items-center">
                <div className="min-w-0">
                  <p className="line-clamp-3 break-words text-sm leading-6 text-[#302e29]">
                    {post.content || post.hook || "Post copy unavailable"}
                  </p>
                  <p className="mt-2 text-xs text-[#77736a]">
                    {post.platform} · Updated {formatDate(post.updatedAt)}
                  </p>
                </div>
                <Badge tone={statusTone(post.status)}>{post.status}</Badge>
                <Link className="text-xs font-medium text-[#685437] underline underline-offset-4" href="/content/drafts">
                  Open drafts
                </Link>
              </li>
            ))}
          </ul>
        )}
      </section>
    </main>
  );
}

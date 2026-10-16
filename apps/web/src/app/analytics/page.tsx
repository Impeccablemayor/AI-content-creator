"use client";

import { useId, useMemo, useState } from "react";
import { ApiClientError } from "@/lib/api/client";
import type { AnalyticsFilters } from "@/lib/api/analytics";
import { useSession } from "@/components/session/session-provider";
import { Badge, EmptyState, ErrorState, Field, Input } from "@/components/ui/primitives";
import {
  useAnalyticsOverview,
  useContentPillars,
  useContentPosts,
  usePostAnalyticsList,
} from "@/lib/queries/hooks";
import type { ContentPlatform } from "@/lib/api/models";

function errorMessage(error: unknown): string {
  if (error instanceof ApiClientError) {
    return error.traceId ? `${error.message} Reference: ${error.traceId}` : error.message;
  }
  return error instanceof Error
    ? error.message
    : "The request could not be completed. Try again.";
}

function numberText(value: number | null, suffix = ""): string {
  return value === null
    ? "Not reported"
    : `${new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 }).format(value)}${suffix}`;
}

function dateLabel(value: string | null): string {
  if (!value) return "Not recorded";
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? "Not recorded"
    : new Intl.DateTimeFormat(undefined, { dateStyle: "medium" }).format(date);
}

function isoBoundary(date: string, endOfDay: boolean): string {
  return new Date(`${date}T${endOfDay ? "23:59:59.999" : "00:00:00"}`).toISOString();
}

function Metric({
  label,
  value,
  detail,
}: {
  label: string;
  value: string;
  detail: string;
}) {
  return (
    <div className="border-t border-[#d7d2c8] py-4">
      <p className="eyebrow">{label}</p>
      <p className="mt-2 break-words text-2xl font-medium tracking-[-0.045em] text-[#27251f]">
        {value}
      </p>
      <p className="mt-1 text-xs leading-5 text-[#77736a]">{detail}</p>
    </div>
  );
}

export default function AnalyticsPage() {
  const { session } = useSession();
  const workspaceId = session?.workspaceId ?? "";
  const platformId = useId();
  const fromId = useId();
  const toId = useId();
  const pillarId = useId();
  const [platform, setPlatform] = useState<ContentPlatform | "">("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [selectedPillar, setSelectedPillar] = useState("");
  const filters = useMemo<AnalyticsFilters>(
    () => ({
      ...(platform ? { platform } : {}),
      ...(from ? { from: isoBoundary(from, false) } : {}),
      ...(to ? { to: isoBoundary(to, true) } : {}),
      ...(selectedPillar ? { pillarId: selectedPillar } : {}),
    }),
    [from, platform, selectedPillar, to],
  );

  const overview = useAnalyticsOverview(workspaceId, filters);
  const report = usePostAnalyticsList(workspaceId, filters);
  const pillars = useContentPillars(workspaceId);
  const publishedPosts = useContentPosts(
    workspaceId,
    { status: "Published", ...(platform ? { platform } : {}) },
  );
  const contentById = useMemo(
    () => new Map((publishedPosts.data ?? []).map((post) => [post.id, post])),
    [publishedPosts.data],
  );
  const hasInvalidRange = Boolean(from && to && from > to);

  return (
    <main className="ideas-page">
      <header className="ideas-page-heading">
        <div>
          <p className="eyebrow">WORKSPACE / ANALYTICS</p>
          <h1 className="mt-3 text-[clamp(34px,4.1vw,58px)] font-medium leading-[1.02] tracking-[-0.065em] text-[#22211d]">
            Measure what went out.
          </h1>
          <p className="ideas-page-intro">
            Published-post metrics from the connected platforms. Unreported values remain
            distinct from a measured zero.
          </p>
        </div>
      </header>

      <section className="grid gap-4 border-b border-[#d7d2c8] py-6 sm:grid-cols-2 lg:grid-cols-4" aria-label="Report filters">
        <div className="field">
          <label className="field-label" htmlFor={platformId}>Platform</label>
          <select
            className="field-input"
            id={platformId}
            value={platform}
            onChange={(event) => setPlatform(event.target.value as ContentPlatform | "")}
          >
            <option value="">All supported platforms</option>
            <option value="LinkedIn">LinkedIn</option>
            <option value="X">X</option>
          </select>
        </div>
        <Field label="Published from" htmlFor={fromId}>
          <Input id={fromId} type="date" value={from} max={to || undefined} onChange={(event) => setFrom(event.target.value)} />
        </Field>
        <Field label="Published through" htmlFor={toId}>
          <Input id={toId} type="date" value={to} min={from || undefined} onChange={(event) => setTo(event.target.value)} />
        </Field>
        <div className="field">
          <label className="field-label" htmlFor={pillarId}>Content pillar</label>
          <select
            className="field-input"
            id={pillarId}
            value={selectedPillar}
            onChange={(event) => setSelectedPillar(event.target.value)}
          >
            <option value="">All pillars</option>
            {(pillars.data ?? []).map((pillar) => (
              <option key={pillar.id} value={pillar.id}>{pillar.name}</option>
            ))}
          </select>
        </div>
      </section>

      {hasInvalidRange ? (
        <p className="ideas-page-error" role="alert">The start date must be on or before the end date.</p>
      ) : null}
      {pillars.isError ? (
        <p className="ideas-page-error" role="alert">
          Pillar filters could not be loaded: {errorMessage(pillars.error)}
          <button className="ml-2 underline" type="button" onClick={() => void pillars.refetch()}>Retry</button>
        </p>
      ) : null}

      <section className="grid gap-x-8 gap-y-2 pt-7 sm:grid-cols-2 lg:grid-cols-4" aria-labelledby="analytics-summary">
        <div className="sm:col-span-2 lg:col-span-4">
          <p className="eyebrow">PERFORMANCE SUMMARY</p>
          <h2 id="analytics-summary" className="mt-2 text-xl font-medium tracking-[-0.04em] text-[#2d2b26]">
            {from || to ? "Selected publishing period" : "All-time reporting"}
          </h2>
        </div>
        {overview.isPending ? (
          <p className="py-5 text-sm text-[#77736a]" role="status">Loading performance summary…</p>
        ) : overview.isError ? (
          <div className="sm:col-span-2 lg:col-span-4">
            <ErrorState message={errorMessage(overview.error)} onRetry={() => void overview.refetch()} />
          </div>
        ) : (
          <>
            <Metric label="Posts published" value={numberText(overview.data.postsPublished)} detail="Published posts in this workspace and period." />
            <Metric label="Impressions" value={numberText(overview.data.impressions)} detail="Platform-reported impressions." />
            <Metric label="Engagement rate" value={numberText(overview.data.engagementRate, "%")} detail="Likes, comments, and shares divided by impressions." />
            <Metric label="Followers gained" value={numberText(overview.data.followersGained)} detail="Not every platform reports this per post." />
            <Metric label="Likes" value={numberText(overview.data.likes)} detail="Null means not reported; zero means measured as zero." />
            <Metric label="Comments" value={numberText(overview.data.comments)} detail="Platform-reported comments." />
            <Metric label="Shares" value={numberText(overview.data.shares)} detail="Platform-reported shares or reposts." />
            <Metric label="Clicks" value={numberText(overview.data.clicks)} detail="Platform-reported clicks." />
            <Metric label="Profile visits" value={numberText(overview.data.profileVisits)} detail="Platform-reported profile visits." />
          </>
        )}
      </section>

      <section className="mt-8 border-t border-[#d7d2c8] pt-7" aria-labelledby="post-performance-title">
        <div className="flex flex-wrap items-end justify-between gap-3 border-b border-[#d7d2c8] pb-4">
          <div>
            <p className="eyebrow">POST LEVEL</p>
            <h2 id="post-performance-title" className="mt-2 text-xl font-medium tracking-[-0.04em] text-[#2d2b26]">
              Published content
            </h2>
          </div>
          {report.isSuccess ? <Badge>{report.data.length} {report.data.length === 1 ? "record" : "records"}</Badge> : null}
        </div>
        {report.isPending ? (
          <p className="py-6 text-sm text-[#77736a]" role="status">Loading post performance…</p>
        ) : report.isError ? (
          <div className="py-6">
            <ErrorState message={errorMessage(report.error)} onRetry={() => void report.refetch()} />
          </div>
        ) : report.data.length === 0 ? (
          <EmptyState
            title="No published posts in this report"
            description="Try another platform or date range. Drafts and scheduled posts are intentionally excluded from analytics."
          />
        ) : (
          <ul className="m-0 list-none p-0">
            {report.data.map((row) => {
              const post = contentById.get(row.contentPostId);
              return (
                <li key={row.contentPostId} className="grid min-w-0 gap-3 border-b border-[#e3ded4] py-5 md:grid-cols-[minmax(0,1fr)_130px_150px] md:items-center">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <Badge tone="accent">{row.platform}</Badge>
                      {row.contentPillarName ? <Badge>{row.contentPillarName}</Badge> : null}
                    </div>
                    <p className="mt-3 break-words text-sm leading-6 text-[#302e29]">
                      {post?.content
                        ? post.content
                        : post?.hook || "Post content unavailable"}
                    </p>
                    <p className="mt-2 text-xs text-[#77736a]">
                      Published {dateLabel(row.publishedAt)} · metrics recorded {dateLabel(row.recordedAt)}
                    </p>
                  </div>
                  <div className="text-sm text-[#625e56]">
                    <span className="block text-[10px] uppercase tracking-[0.12em] text-[#89847a]">Impressions</span>
                    <span className="mt-1 block font-medium text-[#302e29]">{numberText(row.impressions)}</span>
                  </div>
                  <div className="text-sm text-[#625e56]">
                    <span className="block text-[10px] uppercase tracking-[0.12em] text-[#89847a]">Engagement</span>
                    <span className="mt-1 block font-medium text-[#302e29]">{numberText(row.engagementRate, "%")}</span>
                  </div>
                </li>
              );
            })}
          </ul>
        )}
        {publishedPosts.isError ? (
          <p className="mt-3 text-xs text-[#914a42]" role="status">
            Post text is unavailable: {errorMessage(publishedPosts.error)}
          </p>
        ) : null}
      </section>

      <p className="mt-8 border-t border-[#d7d2c8] pt-4 text-xs leading-5 text-[#77736a]">
        The reporting API provides period totals and per-post snapshots, not time-series or grouped platform/pillar comparisons. No trend line or comparison is inferred here.
      </p>
    </main>
  );
}

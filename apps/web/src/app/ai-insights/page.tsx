"use client";

import { useState } from "react";
import { ApiClientError } from "@/lib/api/client";
import type { LearningInsightsRun } from "@/lib/api/models";
import { useSession } from "@/components/session/session-provider";
import { Badge, Button, EmptyState, ErrorState } from "@/components/ui/primitives";
import {
  useGenerateLearningInsights,
  useLearningInsights,
} from "@/lib/queries/hooks";

function errorMessage(error: unknown): string {
  if (error instanceof ApiClientError) {
    return error.traceId ? `${error.message} Reference: ${error.traceId}` : error.message;
  }
  return error instanceof Error
    ? error.message
    : "The request could not be completed. Try again.";
}

function dateRange(run: LearningInsightsRun): string {
  const from = new Date(run.from);
  const to = new Date(run.to);
  if (Number.isNaN(from.getTime()) || Number.isNaN(to.getTime())) return "Reported period unavailable";
  const format = new Intl.DateTimeFormat(undefined, { dateStyle: "medium" });
  return `${format.format(from)} – ${format.format(to)}`;
}

function AiInsightsWorkspace({ workspaceId }: { workspaceId: string }) {
  const insights = useLearningInsights(workspaceId);
  const generate = useGenerateLearningInsights(workspaceId);
  const [lastRun, setLastRun] = useState<LearningInsightsRun | null>(null);

  function runLearning() {
    generate.mutate(undefined, { onSuccess: setLastRun });
  }

  return (
    <main className="ideas-page">
      <header className="ideas-page-heading">
        <div className="flex flex-wrap items-end justify-between gap-5">
          <div>
            <p className="eyebrow">WORKSPACE / LEARNING</p>
            <h1 className="mt-3 text-[clamp(34px,4.1vw,58px)] font-medium leading-[1.02] tracking-[-0.065em] text-[#22211d]">
              Learn from what shipped.
            </h1>
            <p className="ideas-page-intro">
              Evidence-based observations from measured published posts, with model recommendations
              kept separate from what the numbers show.
            </p>
          </div>
          <Button
            type="button"
            variant="primary"
            busy={generate.isPending}
            disabled={generate.isPending}
            onClick={runLearning}
          >
            Run learning analysis
          </Button>
        </div>
      </header>

      {generate.isError ? (
        <p className="ideas-page-error" role="alert">{errorMessage(generate.error)}</p>
      ) : null}

      {lastRun ? (
        <section className="mt-7 border-b border-[#d7d2c8] pb-6" aria-labelledby="learning-run-title">
          <div className="flex flex-wrap items-center gap-2">
            <Badge tone={lastRun.hasSufficientData ? "positive" : "warning"}>
              {lastRun.hasSufficientData ? "Analysis complete" : "More measured history needed"}
            </Badge>
            <span className="text-xs text-[#77736a]">{dateRange(lastRun)}</span>
          </div>
          <h2 id="learning-run-title" className="mt-3 text-lg font-medium tracking-[-0.03em] text-[#302e29]">
            {lastRun.measuredPosts} measured of {lastRun.postsInWindow} published posts
          </h2>
          <p className="mt-2 max-w-2xl text-sm leading-6 text-[#706d66]">
            {lastRun.hasSufficientData
              ? `${lastRun.insights.length} evidence-backed ${lastRun.insights.length === 1 ? "signal was" : "signals were"} produced.`
              : `At least ${lastRun.minimumMeasuredPosts} measured posts are needed for useful comparisons. This run did not create a finding.`}
          </p>
        </section>
      ) : null}

      <section className="pt-7" aria-labelledby="stored-insights-title">
        <div className="flex flex-wrap items-end justify-between gap-3 border-b border-[#d7d2c8] pb-4">
          <div>
            <p className="eyebrow">SAVED SIGNALS</p>
            <h2 id="stored-insights-title" className="mt-2 text-xl font-medium tracking-[-0.04em] text-[#2d2b26]">
              Workspace learning
            </h2>
          </div>
          {insights.isSuccess ? (
            <span className="text-xs text-[#77736a]">{insights.data.length} stored</span>
          ) : null}
        </div>

        {insights.isPending ? (
          <p className="py-7 text-sm text-[#77736a]" role="status">Loading stored learning signals…</p>
        ) : insights.isError ? (
          <div className="py-6">
            <ErrorState message={errorMessage(insights.error)} onRetry={() => void insights.refetch()} />
          </div>
        ) : insights.data.length === 0 ? (
          <EmptyState
            eyebrow="NO STORED SIGNALS"
            title="There is not enough evidence yet"
            description="Learning runs compare real performance snapshots. Publish content and wait for metrics to be recorded, then run the analysis again."
          />
        ) : (
          <ul className="m-0 list-none p-0">
            {insights.data.map((insight) => (
              <li key={insight.id} className="grid min-w-0 gap-4 border-b border-[#e3ded4] py-6 md:grid-cols-[minmax(150px,0.62fr)_minmax(0,1.38fr)]">
                <div>
                  <Badge tone="accent">{insight.dimension}</Badge>
                  <p className="mt-3 break-words font-mono text-[10px] tracking-[0.04em] text-[#77736a]">{insight.type}</p>
                  <p className="mt-2 text-xs text-[#77736a]">
                    {insight.evidenceCount} measured {insight.evidenceCount === 1 ? "post" : "posts"}
                    {insight.confidence === null
                      ? ""
                      : ` · evidence strength ${new Intl.NumberFormat(undefined, { maximumFractionDigits: 1 }).format(insight.confidence)}/100`}
                  </p>
                  <p className="mt-1 text-xs text-[#89847a]">
                    {new Intl.DateTimeFormat(undefined, { dateStyle: "medium" }).format(new Date(insight.createdAt))}
                  </p>
                </div>
                <div className="min-w-0">
                  <p className="eyebrow">MEASURED OBSERVATION</p>
                  <p className="mt-2 break-words text-sm leading-6 text-[#302e29]">{insight.observation}</p>
                  {insight.recommendation ? (
                    <div className="mt-4 border-l-2 border-[#c5a36d] pl-4">
                      <p className="eyebrow">MODEL RECOMMENDATION</p>
                      <p className="mt-2 break-words text-sm leading-6 text-[#625e56]">{insight.recommendation}</p>
                    </div>
                  ) : null}
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>
    </main>
  );
}

export default function AiInsightsPage() {
  const { session } = useSession();
  const workspaceId = session?.workspaceId ?? "";
  return <AiInsightsWorkspace key={workspaceId} workspaceId={workspaceId} />;
}

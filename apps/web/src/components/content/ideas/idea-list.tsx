"use client";

import { useId } from "react";
import { Badge, Button, Skeleton } from "@/components/ui/primitives";
import type {
  ContentIdea,
  ContentIdeaStatus,
  ContentPillar,
  ContentPlatform,
} from "@/lib/api/models";

const statuses: ContentIdeaStatus[] = [
  "Suggested",
  "Approved",
  "Rejected",
  "Used",
  "Archived",
];

interface IdeaListProps {
  ideas: ContentIdea[];
  selectedId: string | null;
  status: ContentIdeaStatus | "";
  platform: ContentPlatform | "";
  pillarId: string;
  pillars: ContentPillar[];
  pillarsError: string | null;
  isLoading: boolean;
  onStatusChange: (value: ContentIdeaStatus | "") => void;
  onPlatformChange: (value: ContentPlatform | "") => void;
  onPillarChange: (value: string) => void;
  onSelect: (idea: ContentIdea) => void;
  setIdeaButtonRef: (id: string, element: HTMLButtonElement | null) => void;
  onGenerate: () => void;
  onRetryPillars: () => void;
}

function statusTone(status: ContentIdeaStatus): "neutral" | "accent" | "positive" | "warning" | "negative" {
  switch (status) {
    case "Suggested":
      return "accent";
    case "Approved":
      return "positive";
    case "Rejected":
      return "negative";
    case "Used":
      return "neutral";
    case "Archived":
      return "warning";
  }
}

function shortDate(value: string): string {
  return new Intl.DateTimeFormat(undefined, {
    month: "short",
    day: "numeric",
  }).format(new Date(value));
}

export function IdeaList({
  ideas,
  selectedId,
  status,
  platform,
  pillarId,
  pillars,
  pillarsError,
  isLoading,
  onStatusChange,
  onPlatformChange,
  onPillarChange,
  onSelect,
  setIdeaButtonRef,
  onGenerate,
  onRetryPillars,
}: IdeaListProps) {
  const statusId = useId();
  const platformId = useId();
  const pillarIdControl = useId();

  return (
    <section className="ideas-list-pane" aria-label="Content idea stream">
      <div className="ideas-list-heading">
        <div>
          <p className="eyebrow">IDEA STREAM</p>
          <h2>{isLoading ? "Loading ideas" : `${ideas.length} ${ideas.length === 1 ? "idea" : "ideas"}`}</h2>
        </div>
        <Button type="button" variant="quiet" onClick={onGenerate}>
          Generate
        </Button>
      </div>
      <div className="ideas-filters" aria-label="Filter ideas">
        <div className="ideas-filter">
          <label htmlFor={statusId}>Status</label>
          <select
            id={statusId}
            className="ideas-filter-select"
            value={status}
            onChange={(event) => onStatusChange(event.target.value as ContentIdeaStatus | "")}
          >
            <option value="">All statuses</option>
            {statuses.map((item) => (
              <option key={item} value={item}>{item}</option>
            ))}
          </select>
        </div>
        <div className="ideas-filter">
          <label htmlFor={platformId}>Platform</label>
          <select
            id={platformId}
            className="ideas-filter-select"
            value={platform}
            onChange={(event) => onPlatformChange(event.target.value as ContentPlatform | "")}
          >
            <option value="">All platforms</option>
            <option value="LinkedIn">LinkedIn</option>
            <option value="X">X</option>
          </select>
        </div>
        <div className="ideas-filter">
          <label htmlFor={pillarIdControl}>Pillar</label>
          <select
            id={pillarIdControl}
            className="ideas-filter-select"
            value={pillarId}
            onChange={(event) => onPillarChange(event.target.value)}
            disabled={pillarsError !== null}
          >
            <option value="">All pillars</option>
            {pillars.map((pillar) => (
              <option key={pillar.id} value={pillar.id}>{pillar.name}</option>
            ))}
          </select>
        </div>
      </div>
      {pillarsError ? (
        <div className="ideas-inline-error" role="alert">
          <span>Pillar filters could not be loaded.</span>
          <Button type="button" variant="quiet" onClick={onRetryPillars}>Retry</Button>
        </div>
      ) : null}
      <div className="ideas-list-content" aria-live="polite">
        {isLoading ? (
          <div className="idea-list-skeletons" role="status" aria-label="Loading ideas">
            {Array.from({ length: 4 }, (_, index) => (
              <div className="idea-list-skeleton" key={index}>
                <Skeleton className="idea-skeleton-index" />
                <div>
                  <Skeleton className="idea-skeleton-title" />
                  <Skeleton className="idea-skeleton-meta" />
                </div>
              </div>
            ))}
          </div>
        ) : ideas.length > 0 ? (
          <ol className="idea-list">
            {ideas.map((idea, index) => (
              <li key={idea.id}>
                <button
                  type="button"
                  id={`idea-${idea.id}`}
                  ref={(element) => setIdeaButtonRef(idea.id, element)}
                  className="idea-list-item"
                  aria-pressed={selectedId === idea.id}
                  onClick={() => onSelect(idea)}
                >
                  <span className="idea-list-index" aria-hidden="true">
                    {String(index + 1).padStart(2, "0")}
                  </span>
                  <span className="idea-list-copy">
                    <span className="idea-list-title">{idea.title}</span>
                    <span className="idea-list-hook">{idea.hook}</span>
                    <span className="idea-list-meta">
                      <Badge tone={statusTone(idea.status)}>{idea.status}</Badge>
                      <span>{idea.platform}</span>
                      <span>{shortDate(idea.createdAt)}</span>
                    </span>
                  </span>
                  <span className="idea-list-score" aria-label={`Overall score ${idea.overallScore} out of 100`}>
                    {idea.overallScore.toLocaleString(undefined, { maximumFractionDigits: 1 })}
                  </span>
                </button>
              </li>
            ))}
          </ol>
        ) : (
          <div className="ideas-list-empty">
            <p className="eyebrow">NO MATCHES</p>
            <h3>{status || platform || pillarId ? "No ideas match these filters." : "Nothing has been proposed yet."}</h3>
            <p>
              {status || platform || pillarId
                ? "Adjust the filters to widen the current view."
                : "Ideas turn your workspace strategy into platform-specific directions you can review and develop."}
            </p>
            {!status && !platform && !pillarId ? (
              <Button type="button" variant="secondary" onClick={onGenerate}>
                Generate the first ideas
              </Button>
            ) : null}
          </div>
        )}
      </div>
      <p className="ideas-sort-note">Ordered by overall score, then most recent.</p>
    </section>
  );
}

export { statusTone };

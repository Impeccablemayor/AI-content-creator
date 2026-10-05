"use client";

import { useId, useState, type FormEvent, type RefObject } from "react";
import { Badge, Button, Dialog } from "@/components/ui/primitives";
import type { ContentIdea, ContentPlatform } from "@/lib/api/models";
import { statusTone } from "./idea-list";

interface IdeaDetailProps {
  idea: ContentIdea | null;
  pillarName: string | null;
  busyAction: "approve" | "reject" | "draft" | null;
  actionError: string | null;
  announcement: string | null;
  titleRef: RefObject<HTMLHeadingElement | null>;
  onApprove: () => void;
  onReject: () => void;
  onCreateDraft: (platform: ContentPlatform) => Promise<boolean>;
  onBackToList: () => void;
}

function score(value: number): string {
  return value.toLocaleString(undefined, { maximumFractionDigits: 1 });
}

export function IdeaDetail({
  idea,
  pillarName,
  busyAction,
  actionError,
  announcement,
  titleRef,
  onApprove,
  onReject,
  onCreateDraft,
  onBackToList,
}: IdeaDetailProps) {
  const [rejectOpen, setRejectOpen] = useState(false);
  const [draftOpen, setDraftOpen] = useState(false);
  const [draftPlatform, setDraftPlatform] = useState<ContentPlatform>("LinkedIn");
  const platformId = useId();

  async function submitDraft(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (await onCreateDraft(draftPlatform)) setDraftOpen(false);
  }

  if (!idea) {
    return (
      <section className="idea-detail-pane idea-detail-unselected" aria-label="Selected idea">
        <p className="eyebrow">EDITORIAL REVIEW</p>
        <h2>Select an idea to inspect its direction.</h2>
        <p>Review the hook, context and actual scoring signals before deciding what happens next.</p>
      </section>
    );
  }

  const hashtags = idea.suggestedHashtags
    .split(/\s+/)
    .map((tag) => tag.trim())
    .filter(Boolean);

  return (
    <article className="idea-detail-pane" aria-labelledby="idea-detail-title">
      <div className="idea-detail-topline">
        <Button type="button" variant="quiet" className="idea-back-button" onClick={onBackToList}>
          Back to ideas
        </Button>
        <p className="eyebrow">EDITORIAL REVIEW / {idea.platform.toUpperCase()}</p>
        <Badge tone={statusTone(idea.status)}>
          {idea.status}
        </Badge>
      </div>
      <header className="idea-detail-header">
        <h2 id="idea-detail-title" ref={titleRef} tabIndex={-1}>
          {idea.title}
        </h2>
        <p className="idea-detail-hook">{idea.hook}</p>
      </header>
      <div className="idea-detail-body">
        <section className="idea-detail-section">
          <p className="eyebrow">THE ANGLE</p>
          <p className="idea-angle">{idea.angle}</p>
        </section>
        <section className="idea-detail-section">
          <p className="eyebrow">WHY IT FITS</p>
          <p className="idea-reason">{idea.reason}</p>
        </section>
        <section className="idea-quality" aria-labelledby="idea-quality-title">
          <div className="idea-quality-heading">
            <div>
              <p className="eyebrow">EDITORIAL SIGNALS</p>
              <h3 id="idea-quality-title">Quality profile</h3>
            </div>
            <p className="idea-score-scale">Scores out of 100</p>
          </div>
          <dl className="idea-score-list">
            <div><dt>Relevance</dt><dd>{score(idea.relevanceScore)}</dd></div>
            <div><dt>Engagement potential</dt><dd>{score(idea.engagementPotentialScore)}</dd></div>
            <div><dt>Brand fit</dt><dd>{score(idea.brandFitScore)}</dd></div>
            <div className="idea-score-overall"><dt>Overall</dt><dd>{score(idea.overallScore)}</dd></div>
          </dl>
        </section>
        <dl className="idea-metadata">
          <div><dt>Topic</dt><dd>{idea.topic}</dd></div>
          <div><dt>Content pillar</dt><dd>{pillarName ?? "Unassigned"}</dd></div>
          <div>
            <dt>Created</dt>
            <dd>{new Intl.DateTimeFormat(undefined, { dateStyle: "medium" }).format(new Date(idea.createdAt))}</dd>
          </div>
          {idea.suggestedPublishAt ? (
            <div>
              <dt>Suggested date</dt>
              <dd>{new Intl.DateTimeFormat(undefined, { dateStyle: "medium" }).format(new Date(idea.suggestedPublishAt))}</dd>
            </div>
          ) : null}
        </dl>
        {hashtags.length > 0 ? (
          <section className="idea-detail-section idea-hashtags">
            <p className="eyebrow">SUGGESTED HASHTAGS</p>
            <p>{hashtags.join(" ")}</p>
          </section>
        ) : null}
      </div>
      {actionError ? <p className="idea-action-error" role="alert">{actionError}</p> : null}
      {announcement ? <p className="idea-action-success" role="status">{announcement}</p> : null}
      <footer className="idea-actions">
        {idea.status === "Suggested" ? (
          <>
            <Button
              type="button"
              variant="primary"
              busy={busyAction === "approve"}
              disabled={busyAction !== null}
              onClick={onApprove}
            >
              Approve idea
            </Button>
            <Button
              type="button"
              variant="danger"
              busy={busyAction === "reject"}
              disabled={busyAction !== null}
              onClick={() => setRejectOpen(true)}
            >
              Reject
            </Button>
          </>
        ) : null}
        {idea.status === "Approved" ? (
          <Button
            type="button"
            variant="primary"
            busy={busyAction === "draft"}
            disabled={busyAction !== null}
            onClick={() => setDraftOpen(true)}
          >
            Develop into a draft
          </Button>
        ) : null}
      </footer>
      <Dialog
        open={rejectOpen}
        title="Reject this idea?"
        onClose={() => setRejectOpen(false)}
      >
        <div className="idea-confirmation">
          <p>This moves the idea to Rejected. The backend does not provide an undo action.</p>
          <div className="dialog-actions">
            <Button type="button" variant="quiet" onClick={() => setRejectOpen(false)}>
              Keep idea
            </Button>
            <Button
              type="button"
              variant="danger"
              busy={busyAction === "reject"}
              disabled={busyAction !== null}
              onClick={() => {
                setRejectOpen(false);
                onReject();
              }}
            >
              Reject idea
            </Button>
          </div>
        </div>
      </Dialog>
      <Dialog
        open={draftOpen}
        title="Develop this idea"
        onClose={() => setDraftOpen(false)}
      >
        <form className="idea-draft-form" onSubmit={(event) => void submitDraft(event)}>
          <p>The approved idea will be used to generate a platform-specific draft.</p>
          {actionError ? <p className="idea-action-error" role="alert">{actionError}</p> : null}
          <div className="field">
            <label className="field-label" htmlFor={platformId}>Draft platform</label>
            <select
              id={platformId}
              className="field-input"
              value={draftPlatform}
              onChange={(event) => setDraftPlatform(event.target.value as ContentPlatform)}
            >
              <option value="LinkedIn">LinkedIn</option>
              <option value="X">X</option>
            </select>
          </div>
          <div className="dialog-actions">
            <Button type="button" variant="quiet" onClick={() => setDraftOpen(false)} disabled={busyAction === "draft"}>
              Cancel
            </Button>
            <Button type="submit" variant="primary" busy={busyAction === "draft"}>
              Generate draft
            </Button>
          </div>
        </form>
      </Dialog>
    </article>
  );
}

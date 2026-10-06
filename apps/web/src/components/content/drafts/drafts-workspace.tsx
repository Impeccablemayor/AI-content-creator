"use client";

import Link from "next/link";
import {
  useCallback,
  useEffect,
  useId,
  useMemo,
  useRef,
  useState,
  type FormEvent,
  type RefObject,
} from "react";
import { useSession } from "@/components/session/session-provider";
import { Badge, Button, EmptyState, ErrorState, Field, Input, Textarea } from "@/components/ui/primitives";
import { ApiClientError } from "@/lib/api/client";
import type {
  ContentEvaluation,
  ContentPost,
  ContentPostStatus,
  ContentType,
  UpdateContentPostRequest,
} from "@/lib/api/models";
import {
  useApproveContentPost,
  useContentPost,
  useContentPosts,
  useEvaluateContentPost,
  useRegenerateContentPost,
  useRejectContentPost,
  useScheduleContentPost,
  useUpdateContentPost,
} from "@/lib/queries/hooks";

type PostEditor = UpdateContentPostRequest;

const contentTypes: ContentType[] = [
  "Educational",
  "Story",
  "Opinion",
  "HowTo",
  "CaseStudy",
  "Insight",
  "Question",
];
const EMPTY_POSTS: ContentPost[] = [];

const evaluationDimensions: Array<[keyof ContentEvaluation["dimensions"], string]> = [
  ["strategicFit", "Strategic fit"],
  ["audienceFit", "Audience fit"],
  ["hookStrength", "Hook strength"],
  ["value", "Value"],
  ["clarity", "Clarity"],
  ["originality", "Originality"],
  ["storytelling", "Storytelling"],
  ["platformFit", "Platform fit"],
  ["voiceMatch", "Voice match"],
  ["factualReliability", "Factual reliability"],
];

function errorMessage(error: unknown): string {
  if (error instanceof ApiClientError) {
    return error.traceId ? `${error.message} Reference: ${error.traceId}` : error.message;
  }
  if (error instanceof Error) return error.message;
  return "The request could not be completed. Try again.";
}

function statusTone(status: ContentPostStatus): "neutral" | "accent" | "positive" | "warning" | "negative" {
  if (status === "Draft") return "accent";
  if (status === "Approved" || status === "Published") return "positive";
  if (status === "Scheduled") return "warning";
  return "negative";
}

function formatDate(value: string | null): string {
  if (!value) return "—";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "—";
  return new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" }).format(date);
}

function toLocalDateTimeInput(date: Date): string {
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 16);
}

function editorFromPost(post: ContentPost): PostEditor {
  return {
    content: post.content,
    hook: post.hook,
    callToAction: post.callToAction,
    hashtags: [...post.hashtags],
    contentType: post.contentType,
    storytellingStructure: post.storytellingStructure,
  };
}

function DraftLoadingState() {
  return (
    <div className="drafts-page">
      <header className="drafts-heading">
        <p className="eyebrow">CONTENT / DRAFTS</p>
        <h1>Writing, ready for review.</h1>
        <p className="ideas-page-intro">Your workspace drafts, from first pass through approval.</p>
      </header>
      <div className="drafts-loading" role="status">
        <span className="ideas-loading-title">Loading editorial work</span>
        <span className="ideas-loading-line" />
        <span className="ideas-loading-line short" />
      </div>
    </div>
  );
}

function PostRow({
  post,
  selected,
  setButtonRef,
  onSelect,
}: {
  post: ContentPost;
  selected: boolean;
  setButtonRef: (id: string, element: HTMLButtonElement | null) => void;
  onSelect: (id: string) => void;
}) {
  return (
    <li>
      <button
        ref={(element) => setButtonRef(post.id, element)}
        type="button"
        className="draft-list-item"
        aria-pressed={selected}
        onClick={() => onSelect(post.id)}
      >
        <span className="draft-list-mark" aria-hidden="true">—</span>
        <span className="draft-list-copy">
          <span className="draft-list-hook">{post.hook || "Untitled draft"}</span>
          <span className="draft-list-meta">
            <Badge tone={statusTone(post.status)}>{post.status}</Badge>
            <span>{post.platform}</span>
            <span>Updated {formatDate(post.updatedAt)}</span>
          </span>
        </span>
      </button>
    </li>
  );
}

function EvaluationPanel({ evaluation }: { evaluation: ContentEvaluation }) {
  return (
    <section className="draft-evaluation" aria-labelledby="draft-evaluation-heading">
      <div className="draft-section-heading">
        <div>
          <p className="eyebrow">EDITORIAL REVIEW</p>
          <h3 id="draft-evaluation-heading">Quality evaluation</h3>
        </div>
        <Badge tone={evaluation.passed ? "positive" : "warning"}>
          {evaluation.passed ? "Passed" : "Review advised"}
        </Badge>
      </div>
      <p className="draft-evaluation-recommendation">{evaluation.recommendation}</p>
      <dl className="draft-score-grid">
        <div>
          <dt>Overall</dt>
          <dd>{evaluation.overallScore.toLocaleString()}</dd>
        </div>
        <div>
          <dt>Pass threshold</dt>
          <dd>{evaluation.passThreshold.toLocaleString()}</dd>
        </div>
        {evaluationDimensions.map(([key, label]) => (
          <div key={key}>
            <dt>{label}</dt>
            <dd>{evaluation.dimensions[key].toLocaleString()}</dd>
          </div>
        ))}
      </dl>
      {evaluation.strengths.length > 0 ? (
        <div className="draft-evaluation-notes">
          <h4>Strengths</h4>
          <ul>{evaluation.strengths.map((item, index) => <li key={`${index}-${item}`}>{item}</li>)}</ul>
        </div>
      ) : null}
      {evaluation.issues.length > 0 ? (
        <div className="draft-evaluation-notes">
          <h4>Areas to revisit</h4>
          <ul>{evaluation.issues.map((item, index) => <li key={`${index}-${item}`}>{item}</li>)}</ul>
        </div>
      ) : null}
      <div className="draft-repetition-note">
        <span>Repetition check</span>
        <span>
          {evaluation.repetition.isRepetitive
            ? `Similar content detected (${evaluation.repetition.similarity.toLocaleString()})`
            : `No repetition flagged (${evaluation.repetition.similarity.toLocaleString()})`}
        </span>
      </div>
    </section>
  );
}

function DraftDetail({
  post,
  titleRef,
  editor,
  dirty,
  saving,
  busyAction,
  error,
  announcement,
  evaluation,
  scheduleValue,
  scheduleMin,
  onEditorChange,
  onSave,
  onEvaluate,
  onApprove,
  onReject,
  onRegenerate,
  onScheduleValueChange,
  onSchedule,
  onBackToList,
}: {
  post: ContentPost;
  titleRef: RefObject<HTMLHeadingElement | null>;
  editor: PostEditor;
  dirty: boolean;
  saving: boolean;
  busyAction: "evaluate" | "approve" | "reject" | "regenerate" | "schedule" | null;
  error: string | null;
  announcement: string | null;
  evaluation: ContentEvaluation | null;
  scheduleValue: string;
  scheduleMin: string;
  onEditorChange: <K extends keyof PostEditor>(key: K, value: PostEditor[K]) => void;
  onSave: (event: FormEvent<HTMLFormElement>) => void;
  onEvaluate: () => void;
  onApprove: () => void;
  onReject: () => void;
  onRegenerate: () => void;
  onScheduleValueChange: (value: string) => void;
  onSchedule: (event: FormEvent<HTMLFormElement>) => void;
  onBackToList: () => void;
}) {
  const id = useId();
  const isDraft = post.status === "Draft";
  const canRegenerate = isDraft || post.status === "Rejected";
  const canSchedule = post.status === "Approved" || post.status === "Scheduled";
  const contentId = `${id}-content`;
  const hookId = `${id}-hook`;
  const ctaId = `${id}-cta`;
  const hashtagsId = `${id}-hashtags`;
  const contentTypeId = `${id}-type`;
  const structureId = `${id}-structure`;
  const scheduleId = `${id}-schedule`;

  return (
    <article className="draft-detail" aria-labelledby="draft-detail-title">
      <div className="draft-detail-topline">
        <Button type="button" variant="quiet" className="draft-back-button" onClick={onBackToList}>
          Back to drafts
        </Button>
        <p className="eyebrow">EDITORIAL DESK / {post.platform.toUpperCase()}</p>
        <Badge tone={statusTone(post.status)}>{post.status}</Badge>
      </div>
      <header className="draft-detail-heading">
        <h2 id="draft-detail-title" ref={titleRef} tabIndex={-1}>
          {post.hook || "Untitled draft"}
        </h2>
        <p className="draft-detail-lede">
          {post.contentType} · Source idea {post.contentIdeaId}
        </p>
      </header>

      <dl className="draft-metadata">
        <div><dt>Created</dt><dd>{formatDate(post.createdAt)}</dd></div>
        <div><dt>Last edited</dt><dd>{formatDate(post.updatedAt)}</dd></div>
        {post.scheduledAt ? <div><dt>Scheduled</dt><dd>{formatDate(post.scheduledAt)}</dd></div> : null}
        {post.publishedAt ? <div><dt>Published</dt><dd>{formatDate(post.publishedAt)}</dd></div> : null}
      </dl>

      {post.publishFailureReason ? (
        <p className="draft-inline-error" role="alert">
          The last publish attempt failed: {post.publishFailureReason}
        </p>
      ) : null}

      <form className="draft-editor" onSubmit={onSave}>
        <div className="draft-editor-heading">
          <div>
            <p className="eyebrow">THE COPY</p>
            <h3>{isDraft ? "Edit draft" : "Published copy"}</h3>
          </div>
          {isDraft ? (
            <Button
              type="submit"
              variant="secondary"
              busy={saving}
              disabled={!dirty || busyAction !== null}
            >
              Save changes
            </Button>
          ) : null}
        </div>
        <div className="draft-editor-fields">
          <Field label="Opening hook" htmlFor={hookId}>
            <Input
              id={hookId}
              maxLength={300}
              value={editor.hook}
              disabled={!isDraft}
              onChange={(event) => onEditorChange("hook", event.target.value)}
            />
          </Field>
          <Field label="Post copy" htmlFor={contentId} hint={`${editor.content.length} / 3000 characters`}>
            <Textarea
              id={contentId}
              maxLength={3000}
              rows={12}
              value={editor.content}
              disabled={!isDraft}
              onChange={(event) => onEditorChange("content", event.target.value)}
            />
          </Field>
          <div className="draft-editor-secondary">
            <Field label="Call to action" htmlFor={ctaId}>
              <Input
                id={ctaId}
                maxLength={300}
                value={editor.callToAction}
                disabled={!isDraft}
                onChange={(event) => onEditorChange("callToAction", event.target.value)}
              />
            </Field>
            <Field label="Content type" htmlFor={contentTypeId}>
              <select
                id={contentTypeId}
                className="field-input"
                value={editor.contentType}
                disabled={!isDraft}
                onChange={(event) => onEditorChange("contentType", event.target.value as ContentType)}
              >
                {contentTypes.map((type) => <option key={type} value={type}>{type}</option>)}
              </select>
            </Field>
          </div>
          <Field label="Hashtags" htmlFor={hashtagsId} hint="Use one hashtag per line. Values are saved as provided.">
            <Textarea
              id={hashtagsId}
              rows={3}
              value={editor.hashtags.join("\n")}
              disabled={!isDraft}
              onChange={(event) => onEditorChange(
                "hashtags",
                event.target.value.split(/\r?\n/).map((tag) => tag.trim()).filter(Boolean),
              )}
            />
          </Field>
          <Field label="Storytelling structure" htmlFor={structureId}>
            <Input
              id={structureId}
              maxLength={300}
              value={editor.storytellingStructure}
              disabled={!isDraft}
              onChange={(event) => onEditorChange("storytellingStructure", event.target.value)}
            />
          </Field>
        </div>
        {!isDraft ? (
          <p className="draft-readonly-note">
            Only Draft posts can be edited. This post is {post.status}.
          </p>
        ) : null}
      </form>

      <section className="draft-actions" aria-labelledby="draft-actions-heading">
        <div className="draft-section-heading">
          <div>
            <p className="eyebrow">NEXT STEP</p>
            <h3 id="draft-actions-heading">Review and move forward</h3>
          </div>
        </div>
        <div className="draft-action-row">
          <Button
            type="button"
            variant="secondary"
            busy={busyAction === "evaluate"}
            disabled={dirty || saving || busyAction !== null}
            onClick={onEvaluate}
          >
            Evaluate copy
          </Button>
          {isDraft ? (
            <>
              <Button
                type="button"
                variant="primary"
                busy={busyAction === "approve"}
                disabled={dirty || saving || busyAction !== null}
                onClick={onApprove}
              >
                Approve
              </Button>
              <Button
                type="button"
                variant="danger"
                busy={busyAction === "reject"}
                disabled={dirty || saving || busyAction !== null}
                onClick={onReject}
              >
                Reject
              </Button>
            </>
          ) : null}
          {canRegenerate ? (
            <Button
              type="button"
              variant="quiet"
              busy={busyAction === "regenerate"}
              disabled={dirty || saving || busyAction !== null}
              onClick={onRegenerate}
            >
              Regenerate copy
            </Button>
          ) : null}
        </div>
        {dirty ? (
          <p className="draft-readonly-note">
            Save your edits before evaluating, changing status or scheduling this post.
          </p>
        ) : null}
        {canSchedule ? (
          <form className="draft-schedule-form" onSubmit={onSchedule}>
            <Field
              label={post.status === "Scheduled" ? "Reschedule for" : "Schedule for"}
              htmlFor={scheduleId}
              hint="Scheduling uses your local time zone."
            >
              <Input
                id={scheduleId}
                type="datetime-local"
                min={scheduleMin}
                required
                value={scheduleValue}
                onChange={(event) => onScheduleValueChange(event.target.value)}
              />
            </Field>
            <Button
              type="submit"
              variant="primary"
              busy={busyAction === "schedule"}
              disabled={dirty || saving || busyAction !== null}
            >
              {post.status === "Scheduled" ? "Update schedule" : "Schedule post"}
            </Button>
          </form>
        ) : null}
      </section>

      {error ? <p className="draft-operation-error" role="alert">{error}</p> : null}
      {announcement ? <p className="draft-operation-success" role="status">{announcement}</p> : null}
      {evaluation ? <EvaluationPanel evaluation={evaluation} /> : null}
    </article>
  );
}

export function DraftsWorkspace() {
  const { session } = useSession();
  const workspaceId = session?.workspaceId ?? "";
  return <DraftsWorkspaceForWorkspace key={workspaceId || "loading"} workspaceId={workspaceId} />;
}

function DraftsWorkspaceForWorkspace({ workspaceId }: { workspaceId: string }) {
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [editorState, setEditorState] = useState<{
    postId: string;
    updatedAt: string;
    value: PostEditor;
  } | null>(null);
  const [scheduleState, setScheduleState] = useState<{
    postId: string;
    updatedAt: string;
    value: string;
  } | null>(null);
  const [busyAction, setBusyAction] = useState<
    "evaluate" | "approve" | "reject" | "regenerate" | "schedule" | null
  >(null);
  const [operationError, setOperationError] = useState<string | null>(null);
  const [announcement, setAnnouncement] = useState<string | null>(null);
  const [evaluationResult, setEvaluationResult] = useState<{
    postId: string;
    evaluation: ContentEvaluation;
  } | null>(null);
  const titleRef = useRef<HTMLHeadingElement>(null);
  const postButtonRefs = useRef(new Map<string, HTMLButtonElement>());
  const pendingMobileFocus = useRef<
    { target: "detail"; postId: string } | { target: "list"; postId: string } | null
  >(null);

  const setPostButtonRef = useCallback((id: string, element: HTMLButtonElement | null) => {
    if (element) postButtonRefs.current.set(id, element);
    else postButtonRefs.current.delete(id);
  }, []);

  const postsQuery = useContentPosts(workspaceId);
  const postQuery = useContentPost(workspaceId, selectedId ?? "");
  const updateMutation = useUpdateContentPost(workspaceId, selectedId ?? "");
  const evaluateMutation = useEvaluateContentPost();
  const approveMutation = useApproveContentPost(workspaceId);
  const rejectMutation = useRejectContentPost(workspaceId);
  const regenerateMutation = useRegenerateContentPost(workspaceId);
  const scheduleMutation = useScheduleContentPost(workspaceId, selectedId ?? "");

  const posts = postsQuery.data ?? EMPTY_POSTS;
  const selectedPost = postQuery.data ?? null;
  const currentEditor = selectedPost
    ? editorState?.postId === selectedPost.id && editorState.updatedAt === selectedPost.updatedAt
      ? editorState.value
      : editorFromPost(selectedPost)
    : null;
  const dirty = Boolean(selectedPost && currentEditor &&
    JSON.stringify(currentEditor) !== JSON.stringify(editorFromPost(selectedPost)));
  const evaluation = selectedPost && evaluationResult?.postId === selectedPost.id
    ? evaluationResult.evaluation
    : null;
  const scheduleValue = selectedPost
    ? scheduleState?.postId === selectedPost.id && scheduleState.updatedAt === selectedPost.updatedAt
      ? scheduleState.value
      : selectedPost.scheduledAt
        ? toLocalDateTimeInput(new Date(selectedPost.scheduledAt))
        : ""
    : "";
  const scheduleMin = toLocalDateTimeInput(new Date());

  const visiblePosts = useMemo(
    () => [...posts].sort((a, b) => {
      const statusOrder: Record<ContentPostStatus, number> = {
        Draft: 0,
        Approved: 1,
        Scheduled: 2,
        Rejected: 3,
        Published: 4,
      };
      return statusOrder[a.status] - statusOrder[b.status] ||
        new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime();
    }),
    [posts],
  );

  useEffect(() => {
    const pendingFocus = pendingMobileFocus.current;
    if (!pendingFocus) return;
    pendingMobileFocus.current = null;
    if (!window.matchMedia("(max-width: 700px)").matches) return;

    if (pendingFocus.target === "detail" && selectedId === pendingFocus.postId) {
      titleRef.current?.focus();
    } else if (pendingFocus.target === "list" && selectedId === null) {
      const target =
        postButtonRefs.current.get(pendingFocus.postId) ??
        postButtonRefs.current.values().next().value;
      target?.focus();
    }
  }, [selectedId]);

  if (!workspaceId || postsQuery.isPending) return <DraftLoadingState />;

  if (postsQuery.isError) {
    return (
      <div className="drafts-page">
        <header className="drafts-heading">
          <p className="eyebrow">CONTENT / DRAFTS</p>
          <h1>Writing, ready for review.</h1>
          <p className="ideas-page-intro">Your workspace drafts, from first pass through approval.</p>
        </header>
        <ErrorState
          title="The content library could not be loaded"
          message={errorMessage(postsQuery.error)}
          onRetry={() => void postsQuery.refetch()}
        />
      </div>
    );
  }

  function selectPost(id: string) {
    pendingMobileFocus.current = { target: "detail", postId: id };
    setSelectedId(id);
    setEditorState(null);
    setScheduleState(null);
    setOperationError(null);
    setAnnouncement(null);
    setEvaluationResult(null);
  }

  function backToList() {
    if (selectedId) pendingMobileFocus.current = { target: "list", postId: selectedId };
    setSelectedId(null);
    setEditorState(null);
    setScheduleState(null);
    setOperationError(null);
    setAnnouncement(null);
  }

  function changeEditor<K extends keyof PostEditor>(key: K, value: PostEditor[K]) {
    if (!selectedPost || !currentEditor) return;
    setEditorState({
      postId: selectedPost.id,
      updatedAt: selectedPost.updatedAt,
      value: { ...currentEditor, [key]: value },
    });
    setOperationError(null);
    setAnnouncement(null);
    setEvaluationResult(null);
  }

  async function runAction(
    action: "approve" | "reject" | "regenerate",
    operation: () => Promise<unknown>,
    successMessage: string,
  ) {
    if (!selectedPost) return;
    setBusyAction(action);
    setOperationError(null);
    setAnnouncement(null);
    try {
      await operation();
      setAnnouncement(successMessage);
    } catch (error) {
      setOperationError(errorMessage(error));
    } finally {
      setBusyAction(null);
    }
  }

  async function evaluatePost() {
    if (!selectedPost) return;
    setBusyAction("evaluate");
    setOperationError(null);
    setAnnouncement(null);
    try {
      const result = await evaluateMutation.mutateAsync(selectedPost.id);
      setEvaluationResult({ postId: selectedPost.id, evaluation: result });
    } catch (error) {
      setOperationError(errorMessage(error));
    } finally {
      setBusyAction(null);
    }
  }

  async function saveDraft(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selectedPost || !currentEditor || selectedPost.status !== "Draft") return;
    setOperationError(null);
    setAnnouncement(null);
    try {
      await updateMutation.mutateAsync(currentEditor);
      setAnnouncement("Draft changes saved.");
    } catch (error) {
      setOperationError(errorMessage(error));
    }
  }

  async function schedulePost(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selectedPost || !scheduleValue) return;
    const scheduledAt = new Date(scheduleValue);
    if (Number.isNaN(scheduledAt.getTime()) || scheduledAt.getTime() <= Date.now()) {
      setOperationError("Choose a schedule time in the future.");
      return;
    }
    setBusyAction("schedule");
    setOperationError(null);
    setAnnouncement(null);
    try {
      await scheduleMutation.mutateAsync({ scheduledAt: scheduledAt.toISOString() });
      setAnnouncement("Schedule saved.");
    } catch (error) {
      setOperationError(errorMessage(error));
    } finally {
      setBusyAction(null);
    }
  }

  function changeScheduleValue(value: string) {
    if (!selectedPost) return;
    setScheduleState({ postId: selectedPost.id, updatedAt: selectedPost.updatedAt, value });
  }

  return (
    <div className="drafts-page">
      <header className="drafts-heading">
        <div className="drafts-title-row">
          <div>
            <p className="eyebrow">CONTENT / DRAFTS</p>
            <h1>Writing, ready for review.</h1>
            <p className="ideas-page-intro">
              Shape each draft, evaluate the copy and move approved work toward publication.
            </p>
          </div>
          <Link className="button button-secondary drafts-ideas-link" href="/content/ideas">
            Open ideas
          </Link>
        </div>
      </header>

      {postQuery.isError && selectedId ? (
        <div className="draft-detail-error">
          <ErrorState
            title="This draft could not be opened"
            message={errorMessage(postQuery.error)}
            onRetry={() => void postQuery.refetch()}
          />
        </div>
      ) : null}
      {postQuery.isPending && selectedId ? (
        <div className="draft-detail-loading" role="status">Opening draft…</div>
      ) : null}

      {posts.length === 0 ? (
        <EmptyState
          eyebrow="EDITORIAL DESK"
          title="No content drafts yet."
          description="Generate a draft from an approved idea to begin working here. Drafts and their status will appear in this workspace."
          action={<Link className="button button-primary" href="/content/ideas">Review approved ideas</Link>}
        />
      ) : (
        <div className={`drafts-workspace${selectedId ? " has-selection" : ""}`}>
          <section className="draft-list-pane" aria-labelledby="draft-list-heading">
            <div className="draft-list-heading">
              <div>
                <p className="eyebrow">WORKSPACE CONTENT</p>
                <h2 id="draft-list-heading">Drafts &amp; review</h2>
              </div>
              <span className="draft-count">{posts.length}</span>
            </div>
            <ul className="draft-list">
              {visiblePosts.map((post) => (
                <PostRow
                  key={post.id}
                  post={post}
                  selected={post.id === selectedId}
                  setButtonRef={setPostButtonRef}
                  onSelect={selectPost}
                />
              ))}
            </ul>
          </section>
          <section className="draft-detail-pane">
            {selectedPost && currentEditor ? (
              <DraftDetail
                key={selectedPost.id}
                post={selectedPost}
                titleRef={titleRef}
                editor={currentEditor}
                dirty={dirty}
                saving={updateMutation.isPending}
                busyAction={busyAction}
                error={operationError}
                announcement={announcement}
                evaluation={evaluation}
                scheduleValue={scheduleValue}
                scheduleMin={scheduleMin}
                onEditorChange={changeEditor}
                onSave={saveDraft}
                onEvaluate={() => void evaluatePost()}
                onApprove={() => void runAction(
                  "approve",
                  () => approveMutation.mutateAsync(selectedPost.id),
                  "Draft approved.",
                )}
                onReject={() => void runAction(
                  "reject",
                  () => rejectMutation.mutateAsync(selectedPost.id),
                  "Draft rejected.",
                )}
                onRegenerate={() => void runAction(
                  "regenerate",
                  () => regenerateMutation.mutateAsync(selectedPost.id),
                  "Draft copy regenerated.",
                )}
                onScheduleValueChange={changeScheduleValue}
                onSchedule={schedulePost}
                onBackToList={backToList}
              />
            ) : (
              <div className="draft-unselected">
                <p className="eyebrow">EDITORIAL DESK</p>
                <h2>Select a draft to begin review.</h2>
                <p>Open a piece of workspace content to edit, evaluate or move it to its next supported state.</p>
              </div>
            )}
          </section>
        </div>
      )}
    </div>
  );
}

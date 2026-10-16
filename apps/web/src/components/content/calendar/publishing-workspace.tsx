"use client";

import {
  useEffect,
  useMemo,
  useRef,
  useState,
  type FormEvent,
  type RefObject,
} from "react";
import { useSession } from "@/components/session/session-provider";
import { Badge, Button, Dialog, EmptyState, ErrorState, Field, Input } from "@/components/ui/primitives";
import { ApiClientError } from "@/lib/api/client";
import type { ContentPlatform, ContentPost, ContentPostStatus, SocialAccount } from "@/lib/api/models";
import {
  useCancelContentPostSchedule,
  useContentPosts,
  usePublishContentPostNow,
  useScheduleContentPost,
  useSocialAccounts,
} from "@/lib/queries/hooks";

type PublishingView = "Scheduled" | "Approved" | "Published";

const EMPTY_POSTS: ContentPost[] = [];
const EMPTY_ACCOUNTS: SocialAccount[] = [];

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

function formatDate(value: string | null, includeTime = true): string {
  if (!value) return "Not scheduled";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "Date unavailable";
  return new Intl.DateTimeFormat(undefined, includeTime
    ? { dateStyle: "medium", timeStyle: "short" }
    : { dateStyle: "full" }).format(date);
}

function localDay(value: string): string {
  const date = new Date(value);
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
}

function localDateTimeValue(value: Date): string {
  const adjusted = new Date(value.getTime() - value.getTimezoneOffset() * 60_000);
  return adjusted.toISOString().slice(0, 16);
}

function dateTimeInputValue(post: ContentPost): string {
  return post.scheduledAt
    ? localDateTimeValue(new Date(post.scheduledAt))
    : localDateTimeValue(new Date(Date.now() + 60 * 60_000));
}

function humanizeFailure(reason: string): string {
  return reason.replace(/([a-z])([A-Z])/g, "$1 $2").toLowerCase();
}

function PublishingLoading() {
  return (
    <div className="publishing-page">
      <header className="publishing-heading">
        <p className="eyebrow">CONTENT / CALENDAR</p>
        <h1>Publishing, on your terms.</h1>
        <p className="ideas-page-intro">A clear view of what is queued, ready to schedule and already published.</p>
      </header>
      <div className="publishing-loading" role="status">
        <span className="ideas-loading-title">Loading the publishing queue</span>
        <span className="ideas-loading-line" />
        <span className="ideas-loading-line short" />
      </div>
    </div>
  );
}

function QueueItem({
  post,
  selected,
  buttonRef,
  currentTime,
  onSelect,
}: {
  post: ContentPost;
  selected: boolean;
  buttonRef: (id: string, element: HTMLButtonElement | null) => void;
  currentTime: number;
  onSelect: (id: string) => void;
}) {
  const preview = post.content.trim();
  const isOverdue = post.status === "Scheduled" && post.scheduledAt !== null &&
    currentTime > 0 && new Date(post.scheduledAt).getTime() <= currentTime;

  return (
    <li>
      <button
        ref={(element) => buttonRef(post.id, element)}
        type="button"
        className="publishing-queue-item"
        aria-pressed={selected}
        onClick={() => onSelect(post.id)}
      >
        <span className="publishing-queue-date">
          {post.status === "Scheduled" ? formatDate(post.scheduledAt) : post.status}
        </span>
        <span className="publishing-queue-copy">
          <span className="publishing-queue-hook">{post.hook || "Untitled post"}</span>
          <span className="publishing-queue-preview">{preview || "No post copy."}</span>
          <span className="publishing-queue-meta">
            <Badge tone={statusTone(post.status)}>{post.status}</Badge>
            <span>{post.platform}</span>
            {isOverdue ? <span className="publishing-overdue">Awaiting publishing worker</span> : null}
            {post.publishFailureReason ? <span className="publishing-failed">Last attempt: {humanizeFailure(post.publishFailureReason)}</span> : null}
          </span>
        </span>
      </button>
    </li>
  );
}

function AccountReadiness({
  post,
  accounts,
  isPending,
  accountsError,
  onRetry,
}: {
  post: ContentPost;
  accounts: SocialAccount[];
  isPending: boolean;
  accountsError: unknown;
  onRetry: () => void;
}) {
  if (accountsError) {
    return (
      <div className="publishing-account-error" role="alert">
        <span>Connected account status could not be loaded: {errorMessage(accountsError)}</span>
        <Button type="button" variant="quiet" onClick={onRetry}>Retry</Button>
      </div>
    );
  }
  if (isPending) {
    return <p className="publishing-account-note" role="status">Checking this workspace’s connected accounts…</p>;
  }
  const connectedAccounts = accounts.filter(
    (item) => item.platform === post.platform && item.status === "Connected",
  );
  if (connectedAccounts.length > 1) {
    return (
      <p className="publishing-account-note" role="status">
        More than one {post.platform} account is connected in this workspace. Publishing requires exactly one; disconnect any accounts you do not intend to use.
      </p>
    );
  }
  const account = connectedAccounts[0];
  if (account) {
    const name = account.displayName || account.username;
    return (
      <p className="publishing-account-note">
        Connected {post.platform} account{name ? `: ${name}` : ""}. Immediate publishing and scheduled delivery use this workspace’s connected account.
      </p>
    );
  }
  return (
    <p className="publishing-account-note">
      No connected {post.platform} account is reported for this workspace. The worker publishes due posts only through a connected account; check Connections before the scheduled time.
    </p>
  );
}

function PostDetail({
  post,
  titleRef,
  accountList,
  accountsPending,
  accountsError,
  onRetryAccounts,
  isScheduling,
  isPublishingNow,
  isCancelingSchedule,
  isBusy,
  error,
  announcement,
  scheduleValue,
  minDateTime,
  onScheduleValueChange,
  onSchedule,
  onPublishNow,
  onCancelSchedule,
  onBack,
}: {
  post: ContentPost;
  titleRef: RefObject<HTMLHeadingElement | null>;
  accountList: SocialAccount[];
  accountsPending: boolean;
  accountsError: unknown;
  onRetryAccounts: () => void;
  isScheduling: boolean;
  isPublishingNow: boolean;
  isCancelingSchedule: boolean;
  isBusy: boolean;
  error: string | null;
  announcement: string | null;
  scheduleValue: string;
  minDateTime: string;
  onScheduleValueChange: (value: string) => void;
  onSchedule: (event: FormEvent<HTMLFormElement>) => void;
  onPublishNow: () => void;
  onCancelSchedule: () => void;
  onBack: () => void;
}) {
  const [confirmPublishOpen, setConfirmPublishOpen] = useState(false);
  const scheduleId = useMemo(() => `publishing-schedule-${post.id}`, [post.id]);
  const isScheduled = post.status === "Scheduled";
  const canSchedule = post.status === "Approved" || isScheduled;
  const canPublishImmediately = post.status === "Approved" || isScheduled;
  const isPublished = post.status === "Published";

  return (
    <article className="publishing-detail" aria-labelledby="publishing-detail-title">
      <div className="publishing-detail-topline">
        <Button type="button" variant="quiet" className="publishing-back-button" onClick={onBack}>
          Back to queue
        </Button>
        <p className="eyebrow">PUBLISHING DESK / {post.platform.toUpperCase()}</p>
        <Badge tone={statusTone(post.status)}>{post.status}</Badge>
      </div>
      <header className="publishing-detail-heading">
        <h2 id="publishing-detail-title" ref={titleRef} tabIndex={-1}>
          {post.hook || "Untitled post"}
        </h2>
        <p>{post.platform} · {post.contentType} · Source idea {post.contentIdeaId}</p>
      </header>

      <div className="publishing-copy-preview">
        {post.content ? <p>{post.content}</p> : <p className="publishing-no-copy">No post copy was returned by the API.</p>}
        {post.callToAction ? <p className="publishing-cta">{post.callToAction}</p> : null}
        {post.hashtags.length > 0 ? <p className="publishing-hashtags">{post.hashtags.join(" ")}</p> : null}
      </div>

      <dl className="publishing-metadata">
        <div><dt>Status</dt><dd><Badge tone={statusTone(post.status)}>{post.status}</Badge></dd></div>
        <div><dt>Platform</dt><dd>{post.platform}</dd></div>
        {post.scheduledAt ? <div><dt>Scheduled for</dt><dd>{formatDate(post.scheduledAt)}</dd></div> : null}
        {post.publishedAt ? <div><dt>Published at</dt><dd>{formatDate(post.publishedAt)}</dd></div> : null}
      </dl>

      {post.publishFailureReason ? (
        <div className="publishing-failure" role="alert">
          <strong>Publishing has not completed.</strong>
          <span>The latest attempt reported: {humanizeFailure(post.publishFailureReason)}. The post remains scheduled and the worker retries due posts.</span>
        </div>
      ) : null}

      {canSchedule ? (
        <section className="publishing-actions" aria-labelledby="publishing-actions-heading">
          <div>
            <p className="eyebrow">SCHEDULING</p>
            <h3 id="publishing-actions-heading">{isScheduled ? "Adjust the scheduled time" : "Choose a publishing time"}</h3>
            <p className="publishing-action-copy">
              {isScheduled
                ? "Changing the time updates the existing scheduled post."
                : "The publishing worker handles delivery after an approved post's scheduled time."}
            </p>
          </div>
          <form className="publishing-schedule-form" onSubmit={onSchedule}>
            <Field
              label={isScheduled ? "Reschedule for" : "Schedule for"}
              htmlFor={scheduleId}
              hint="Time is entered in your local time zone."
            >
              <Input
                id={scheduleId}
                type="datetime-local"
                min={minDateTime}
                required
                value={scheduleValue}
                disabled={isBusy}
                onChange={(event) => onScheduleValueChange(event.target.value)}
              />
            </Field>
            <Button type="submit" variant="primary" busy={isScheduling} disabled={isBusy}>
              {isScheduled ? "Update schedule" : "Schedule post"}
            </Button>
          </form>
          {canPublishImmediately ? (
            <AccountReadiness
              post={post}
              accounts={accountList}
              isPending={accountsPending}
              accountsError={accountsError}
              onRetry={onRetryAccounts}
            />
          ) : null}
          <div className="publishing-direct-actions">
            <Button
              type="button"
              variant="primary"
              disabled={isBusy}
              onClick={() => setConfirmPublishOpen(true)}
            >
              Publish now
            </Button>
            {isScheduled ? (
              <Button
                type="button"
                variant="danger"
                busy={isCancelingSchedule}
                disabled={isBusy}
                onClick={onCancelSchedule}
              >
                Cancel schedule
              </Button>
            ) : null}
          </div>
        </section>
      ) : null}

      {isPublished ? (
        <p className="publishing-complete-note">
          This post is marked Published by the backend.
        </p>
      ) : null}
      {post.status === "Draft" || post.status === "Rejected" ? (
        <p className="publishing-transition-note">
          This post is {post.status.toLowerCase()}. Only Approved posts can be scheduled; review it in <a href="/content/drafts">Drafts</a>.
        </p>
      ) : null}
      {error ? <p className="publishing-error" role="alert">{error}</p> : null}
      {announcement ? <p className="publishing-success" role="status">{announcement}</p> : null}
      <Dialog
        open={confirmPublishOpen}
        title="Publish this post now?"
        onClose={() => setConfirmPublishOpen(false)}
      >
        <div className="publishing-confirmation">
          <p>
            This will send the {post.platform} post to its connected account immediately.
            Publishing cannot be undone.
          </p>
          <div className="dialog-actions">
            <Button type="button" variant="quiet" onClick={() => setConfirmPublishOpen(false)}>
              Keep reviewing
            </Button>
            <Button
              type="button"
              variant="primary"
              busy={isPublishingNow}
              disabled={isBusy}
              onClick={() => {
                setConfirmPublishOpen(false);
                onPublishNow();
              }}
            >
              Publish now
            </Button>
          </div>
        </div>
      </Dialog>
    </article>
  );
}

export function PublishingWorkspace() {
  const { session } = useSession();
  const workspaceId = session?.workspaceId ?? "";
  return (
    <PublishingWorkspaceForWorkspace
      key={workspaceId || "loading"}
      workspaceId={workspaceId}
    />
  );
}

function PublishingWorkspaceForWorkspace({ workspaceId }: { workspaceId: string }) {
  const [view, setView] = useState<PublishingView>("Scheduled");
  const [platform, setPlatform] = useState<ContentPlatform | "">("");
  const [scheduledDay, setScheduledDay] = useState("");
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [scheduleValue, setScheduleValue] = useState("");
  const [currentTime, setCurrentTime] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [announcement, setAnnouncement] = useState<string | null>(null);
  const pendingFocus = useRef<
    { target: "detail"; postId: string } | { target: "list"; postId: string } | null
  >(null);
  const titleRef = useRef<HTMLHeadingElement>(null);
  const rowRefs = useRef(new Map<string, HTMLButtonElement>());

  const postsQuery = useContentPosts(workspaceId, {}, 30_000);
  const accountsQuery = useSocialAccounts(workspaceId);
  const scheduleMutation = useScheduleContentPost(workspaceId, selectedId ?? "");
  const publishNowMutation = usePublishContentPostNow(workspaceId, selectedId ?? "");
  const cancelScheduleMutation = useCancelContentPostSchedule(workspaceId, selectedId ?? "");
  const posts = postsQuery.data ?? EMPTY_POSTS;
  const selectedPost = posts.find((post) => post.id === selectedId) ?? null;
  const accounts = accountsQuery.data ?? EMPTY_ACCOUNTS;
  const minDateTime = currentTime ? localDateTimeValue(new Date(currentTime)) : "";

  useEffect(() => {
    const timer = window.setTimeout(() => setCurrentTime(Date.now()), 0);
    const interval = window.setInterval(() => setCurrentTime(Date.now()), 30_000);
    return () => {
      window.clearTimeout(timer);
      window.clearInterval(interval);
    };
  }, []);

  const counts = useMemo(() => ({
    Scheduled: posts.filter((post) => post.status === "Scheduled").length,
    Approved: posts.filter((post) => post.status === "Approved").length,
    Published: posts.filter((post) => post.status === "Published").length,
  }), [posts]);

  const visiblePosts = useMemo(() => {
    const filtered = posts.filter((post) =>
      post.status === view && (!platform || post.platform === platform) &&
      (view !== "Scheduled" || !scheduledDay || (post.scheduledAt && localDay(post.scheduledAt) === scheduledDay)),
    );
    return [...filtered].sort((a, b) => {
      if (view === "Scheduled") {
        const firstTime = a.scheduledAt ? new Date(a.scheduledAt).getTime() : Number.POSITIVE_INFINITY;
        const secondTime = b.scheduledAt ? new Date(b.scheduledAt).getTime() : Number.POSITIVE_INFINITY;
        const firstOverdue = currentTime > 0 && firstTime <= currentTime;
        const secondOverdue = currentTime > 0 && secondTime <= currentTime;
        if (firstOverdue !== secondOverdue) return firstOverdue ? -1 : 1;
        return firstOverdue ? secondTime - firstTime : firstTime - secondTime;
      }
      return new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime();
    });
  }, [currentTime, platform, posts, scheduledDay, view]);

  useEffect(() => {
    const target = pendingFocus.current;
    if (!target) return;
    pendingFocus.current = null;
    if (!window.matchMedia("(max-width: 700px)").matches) return;
    if (target.target === "detail" && selectedId === target.postId) {
      titleRef.current?.focus();
    } else if (target.target === "list" && selectedId === null) {
      (rowRefs.current.get(target.postId) ?? rowRefs.current.values().next().value)?.focus();
    }
  }, [selectedId]);

  if (!workspaceId || postsQuery.isPending) {
    return (
      <PublishingLoading />
    );
  }

  if (postsQuery.isError) {
    return (
      <div className="publishing-page">
        <header className="publishing-heading">
          <p className="eyebrow">CONTENT / CALENDAR</p>
          <h1>Publishing, on your terms.</h1>
          <p className="ideas-page-intro">A clear view of what is queued, ready to schedule and already published.</p>
        </header>
        <ErrorState
          title="The publishing queue could not be loaded"
          message={errorMessage(postsQuery.error)}
          onRetry={() => void postsQuery.refetch()}
        />
      </div>
    );
  }

  function choosePost(id: string) {
    pendingFocus.current = { target: "detail", postId: id };
    setSelectedId(id);
    const post = posts.find((item) => item.id === id);
    setScheduleValue(post ? dateTimeInputValue(post) : "");
    setError(null);
    setAnnouncement(null);
  }

  function returnToQueue() {
    if (selectedId) pendingFocus.current = { target: "list", postId: selectedId };
    setSelectedId(null);
    setError(null);
    setAnnouncement(null);
  }

  function changeView(nextView: PublishingView) {
    setView(nextView);
    setScheduledDay("");
    setSelectedId(null);
    setError(null);
    setAnnouncement(null);
  }

  async function submitSchedule(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selectedPost || !scheduleValue) return;
    const scheduledAt = new Date(scheduleValue);
    if (Number.isNaN(scheduledAt.getTime()) || scheduledAt.getTime() <= Date.now()) {
      setError("Choose a schedule time in the future.");
      return;
    }
    setError(null);
    setAnnouncement(null);
    try {
      await scheduleMutation.mutateAsync({ scheduledAt: scheduledAt.toISOString() });
      setView("Scheduled");
      setScheduledDay("");
      setAnnouncement(selectedPost.status === "Scheduled" ? "Scheduled time updated." : "Post scheduled.");
    } catch (scheduleError) {
      setError(errorMessage(scheduleError));
    }
  }

  async function publishNow() {
    if (!selectedPost) return;
    setError(null);
    setAnnouncement(null);
    try {
      const result = await publishNowMutation.mutateAsync();
      if (result.outcome === "Published") {
        setView("Published");
        setAnnouncement("Post published.");
      } else {
        setView("Scheduled");
        setAnnouncement(null);
        setError(
          result.failureReason
            ? `The publish attempt did not complete: ${humanizeFailure(result.failureReason)}. The post remains scheduled and the worker will retry due posts.`
            : "The publish attempt did not complete. The post remains scheduled and the worker will retry due posts.",
        );
      }
    } catch (publishError) {
      setError(errorMessage(publishError));
    }
  }

  async function cancelSchedule() {
    if (!selectedPost) return;
    setError(null);
    setAnnouncement(null);
    try {
      await cancelScheduleMutation.mutateAsync();
      setView("Approved");
      setScheduledDay("");
      setAnnouncement("Schedule cancelled. The post is approved and ready to schedule again.");
    } catch (cancelError) {
      setError(errorMessage(cancelError));
    }
  }

  const viewLabel: Record<PublishingView, string> = {
    Scheduled: "Scheduled",
    Approved: "Ready to schedule",
    Published: "Published",
  };

  return (
    <div className="publishing-page">
      <header className="publishing-heading">
        <div className="publishing-title-row">
          <div>
            <p className="eyebrow">CONTENT / CALENDAR</p>
            <h1>Publishing, on your terms.</h1>
            <p className="ideas-page-intro">
              An editorial view of what is queued, approved and already published.
            </p>
          </div>
        </div>
        <div className="publishing-controls">
          <div className="publishing-view-switch" role="group" aria-label="Content status filter">
            {(["Scheduled", "Approved", "Published"] as const).map((status) => (
              <Button
                key={status}
                type="button"
                variant={view === status ? "primary" : "quiet"}
                aria-pressed={view === status}
                onClick={() => changeView(status)}
              >
                {viewLabel[status]} <span className="publishing-view-count">{counts[status]}</span>
              </Button>
            ))}
          </div>
          <label className="publishing-platform-filter">
            <span>Platform</span>
            <select value={platform} onChange={(event) => setPlatform(event.target.value as ContentPlatform | "")}>
              <option value="">All platforms</option>
              <option value="LinkedIn">LinkedIn</option>
              <option value="X">X</option>
            </select>
          </label>
          {view === "Scheduled" ? (
            <label className="publishing-date-filter">
              <span>Scheduled on</span>
              <Input type="date" value={scheduledDay} onChange={(event) => setScheduledDay(event.target.value)} />
            </label>
          ) : null}
        </div>
      </header>

      <div className={`publishing-workspace${selectedId ? " has-selection" : ""}`}>
        <section className="publishing-queue-pane" aria-labelledby="publishing-queue-heading">
          <div className="publishing-queue-heading">
            <div>
              <p className="eyebrow">WORKSPACE / {view.toUpperCase()}</p>
              <h2 id="publishing-queue-heading">{viewLabel[view]}</h2>
            </div>
            <span className="publishing-queue-count">{visiblePosts.length}</span>
          </div>
          {view === "Scheduled" ? (
            <p className="publishing-queue-note">
              Due posts are published by the background worker. Schedule dates are filtered in your browser from this workspace’s real post list.
            </p>
          ) : null}
          {visiblePosts.length === 0 ? (
            <EmptyState
              eyebrow={view === "Scheduled" ? "PUBLISHING QUEUE" : "CONTENT STATUS"}
              title={view === "Scheduled"
                ? scheduledDay ? "Nothing is scheduled for this date." : "Nothing is scheduled yet."
                : view === "Approved" ? "No approved posts are waiting." : "No published posts returned."}
              description={view === "Scheduled"
                ? "Posts appear here after they are scheduled through the supported content workflow."
                : view === "Approved"
                  ? "Approve a draft in the Drafts workspace, then return here to schedule it."
                  : "Posts appear here after the publishing worker reports a successful publication."}
            />
          ) : (
            <ul className="publishing-queue">
              {visiblePosts.map((post) => (
                <QueueItem
                  key={post.id}
                  post={post}
                  selected={post.id === selectedId}
                  currentTime={currentTime}
                  buttonRef={(id, element) => {
                    if (element) rowRefs.current.set(id, element);
                    else rowRefs.current.delete(id);
                  }}
                  onSelect={choosePost}
                />
              ))}
            </ul>
          )}
        </section>

        <section className="publishing-detail-pane">
          {selectedPost ? (
            <PostDetail
              key={selectedPost.id}
              post={selectedPost}
              titleRef={titleRef}
              accountList={accounts}
              accountsPending={accountsQuery.isPending}
              accountsError={accountsQuery.error}
              onRetryAccounts={() => void accountsQuery.refetch()}
              isScheduling={scheduleMutation.isPending}
              isPublishingNow={publishNowMutation.isPending}
              isCancelingSchedule={cancelScheduleMutation.isPending}
              isBusy={scheduleMutation.isPending || publishNowMutation.isPending || cancelScheduleMutation.isPending}
              error={error}
              announcement={announcement}
              scheduleValue={scheduleValue}
              minDateTime={minDateTime}
              onScheduleValueChange={(value) => {
                setScheduleValue(value);
                setError(null);
              }}
              onSchedule={submitSchedule}
              onPublishNow={() => void publishNow()}
              onCancelSchedule={() => void cancelSchedule()}
              onBack={returnToQueue}
            />
          ) : (
            <div className="publishing-unselected">
              <p className="eyebrow">PUBLISHING DESK</p>
              <h2>Select content to inspect its preview and available next step.</h2>
              <p>The queue, schedule and published state shown here come from this workspace’s API response.</p>
            </div>
          )}
        </section>
      </div>
    </div>
  );
}

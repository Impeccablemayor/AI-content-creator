"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { ApiClientError } from "@/lib/api/client";
import { useSession } from "@/components/session/session-provider";
import {
  useApproveContentIdea,
  useAudienceProfile,
  useBrandProfile,
  useContentIdeas,
  useContentPillars,
  useContentStrategy,
  useGenerateContentIdeas,
  useGeneratePostFromIdea,
  useRejectContentIdea,
} from "@/lib/queries/hooks";
import { Button, ErrorState } from "@/components/ui/primitives";
import type {
  ContentIdea,
  ContentIdeaStatus,
  ContentPlatform,
  GenerateContentIdeasRequest,
} from "@/lib/api/models";
import { IdeaDetail } from "./idea-detail";
import { IdeaGenerationDialog } from "./idea-generation-dialog";
import { IdeaList } from "./idea-list";

function errorMessage(error: unknown): string {
  if (error instanceof ApiClientError) {
    return error.traceId ? `${error.message} Reference: ${error.traceId}` : error.message;
  }
  if (error instanceof Error) return error.message;
  return "The request could not be completed. Try again.";
}

function isMissingProfile(error: unknown, code: string): boolean {
  return error instanceof ApiClientError && error.status === 404 && error.code === code;
}

function ContextLine({
  label,
  value,
  status,
}: {
  label: string;
  value: string;
  status: "ready" | "missing" | "error" | "loading";
}) {
  const statusText = {
    ready: "Saved",
    missing: "Not configured",
    error: "Unavailable",
    loading: "Checking",
  }[status];

  return (
    <div className={`idea-context-line idea-context-${status}`}>
      <span className="idea-context-name">{label}</span>
      <span className="idea-context-value">{value}</span>
      <span className="idea-context-status">{statusText}</span>
    </div>
  );
}

function getContextStatus(
  isPending: boolean,
  isError: boolean,
  error: unknown,
  missingCode: string,
): "ready" | "missing" | "error" | "loading" {
  if (isPending) return "loading";
  if (!isError) return "ready";
  return isMissingProfile(error, missingCode) ? "missing" : "error";
}

function LoadingWorkspace() {
  return (
    <div className="ideas-page">
      <div className="ideas-page-heading">
        <p className="eyebrow">CONTENT / IDEAS</p>
        <h1>Ideas with a point of view.</h1>
        <p className="ideas-page-intro">Your workspace strategy, translated into directions worth developing.</p>
      </div>
      <div className="ideas-loading-state" role="status">
        <span className="ideas-loading-title">Restoring your editorial workspace</span>
        <span className="ideas-loading-line" />
        <span className="ideas-loading-line short" />
      </div>
    </div>
  );
}

export function IdeasWorkspace() {
  const { session } = useSession();
  const workspaceId = session?.workspaceId ?? "";
  const [status, setStatus] = useState<ContentIdeaStatus | "">("");
  const [platform, setPlatform] = useState<ContentPlatform | "">("");
  const [pillarId, setPillarId] = useState("");
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [generationOpen, setGenerationOpen] = useState(false);
  const [busyAction, setBusyAction] = useState<"approve" | "reject" | "draft" | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [announcement, setAnnouncement] = useState<string | null>(null);
  const ideaDetailTitleRef = useRef<HTMLHeadingElement>(null);
  const ideaButtonRefs = useRef(new Map<string, HTMLButtonElement>());
  const pendingMobileFocus = useRef<
    { target: "detail"; ideaId: string } | { target: "list"; ideaId: string } | null
  >(null);

  const setIdeaButtonRef = useCallback(
    (id: string, element: HTMLButtonElement | null) => {
      if (element) {
        ideaButtonRefs.current.set(id, element);
      } else {
        ideaButtonRefs.current.delete(id);
      }
    },
    [],
  );

  const filters = useMemo(
    () => ({
      ...(status ? { status } : {}),
      ...(platform ? { platform } : {}),
      ...(pillarId ? { pillarId } : {}),
    }),
    [pillarId, platform, status],
  );
  const ideasQuery = useContentIdeas(workspaceId, filters);
  const pillarsQuery = useContentPillars(workspaceId);
  const brandQuery = useBrandProfile(workspaceId);
  const audienceQuery = useAudienceProfile(workspaceId);
  const strategyQuery = useContentStrategy(workspaceId);
  const generateMutation = useGenerateContentIdeas(workspaceId);
  const approveMutation = useApproveContentIdea(workspaceId);
  const rejectMutation = useRejectContentIdea(workspaceId);

  const ideas = ideasQuery.data ?? [];
  const selectedIdea = selectedId
    ? ideas.find((idea) => idea.id === selectedId) ?? null
    : null;
  const draftMutation = useGeneratePostFromIdea(workspaceId, selectedIdea?.id ?? "");

  const brandStatus = getContextStatus(
    brandQuery.isPending,
    brandQuery.isError,
    brandQuery.error,
    "BRAND_PROFILE_NOT_FOUND",
  );
  const audienceStatus = getContextStatus(
    audienceQuery.isPending,
    audienceQuery.isError,
    audienceQuery.error,
    "AUDIENCE_PROFILE_NOT_FOUND",
  );
  const strategyStatus = getContextStatus(
    strategyQuery.isPending,
    strategyQuery.isError,
    strategyQuery.error,
    "CONTENT_STRATEGY_NOT_FOUND",
  );
  const contextStatuses = [brandStatus, audienceStatus, strategyStatus];
  const contextReady = contextStatuses.every((item) => item === "ready");
  const contextHasError = contextStatuses.some((item) => item === "error");

  useEffect(() => {
    const pendingFocus = pendingMobileFocus.current;
    if (!pendingFocus) return;
    pendingMobileFocus.current = null;
    if (!window.matchMedia("(max-width: 700px)").matches) return;

    if (pendingFocus.target === "detail" && selectedId === pendingFocus.ideaId) {
      ideaDetailTitleRef.current?.focus();
    } else if (pendingFocus.target === "list" && selectedId === null) {
      const target =
        ideaButtonRefs.current.get(pendingFocus.ideaId) ??
        ideaButtonRefs.current.values().next().value;
      target?.focus();
    }
  }, [selectedId]);

  if (!workspaceId) return <LoadingWorkspace />;

  if (ideasQuery.isError) {
    return (
      <div className="ideas-page">
        <header className="ideas-page-heading">
          <p className="eyebrow">CONTENT / IDEAS</p>
          <h1>Ideas with a point of view.</h1>
          <p className="ideas-page-intro">
            Your workspace strategy, translated into directions worth developing.
          </p>
        </header>
        <ErrorState
          title="The idea stream could not be loaded"
          message={errorMessage(ideasQuery.error)}
          onRetry={() => void ideasQuery.refetch()}
        />
      </div>
    );
  }

  async function generateIdeas(request: GenerateContentIdeasRequest) {
    const generated = await generateMutation.mutateAsync(request);
    setStatus("");
    setPillarId("");
    setPlatform(request.platform);
    setSelectedId(generated[0]?.id ?? null);
    setGenerationOpen(false);
  }

  async function runIdeaAction(
    action: "approve" | "reject",
    idea: ContentIdea,
  ) {
    setActionError(null);
    setAnnouncement(null);
    setBusyAction(action);
    const mutation = action === "approve" ? approveMutation : rejectMutation;
    mutation.reset();
    try {
      await mutation.mutateAsync(idea.id);
      setAnnouncement(action === "approve" ? "Idea approved." : "Idea rejected.");
    } catch (error) {
      setActionError(errorMessage(error));
    } finally {
      setBusyAction(null);
    }
  }

  async function createDraft(draftPlatform: ContentPlatform): Promise<boolean> {
    if (!selectedIdea) return false;
    setActionError(null);
    setAnnouncement(null);
    setBusyAction("draft");
    draftMutation.reset();
    try {
      await draftMutation.mutateAsync({ platform: draftPlatform });
      setAnnouncement(`A ${draftPlatform} draft was created from this approved idea.`);
      return true;
    } catch (error) {
      setActionError(errorMessage(error));
      return false;
    } finally {
      setBusyAction(null);
    }
  }

  const anyIdeaLoading = ideasQuery.isPending;
  const contextErrorMessage = contextHasError
    ? "Saved strategy context could not be verified. Retry the relevant profile request before generating ideas."
    : null;
  const selectedPillar = selectedIdea?.contentPillarId
    ? pillarsQuery.data?.find((pillar) => pillar.id === selectedIdea.contentPillarId)?.name ?? null
    : null;

  return (
    <div className="ideas-page">
      <header className="ideas-page-heading">
        <div className="ideas-title-row">
          <div>
            <p className="eyebrow">CONTENT / IDEAS</p>
            <h1>Ideas with a point of view.</h1>
            <p className="ideas-page-intro">
              Review platform-specific directions shaped by your workspace strategy.
            </p>
          </div>
          <Button
            type="button"
            variant="primary"
            onClick={() => {
              generateMutation.reset();
              setGenerationOpen(true);
            }}
          >
            Generate ideas
          </Button>
        </div>
        <section className="idea-strategy-context" aria-labelledby="idea-context-heading">
          <div className="idea-strategy-context-heading">
            <p className="eyebrow">GENERATION CONTEXT</p>
            <h2 id="idea-context-heading">Grounded in this workspace</h2>
          </div>
          <div className="idea-context-lines">
            <ContextLine
              label="Brand"
              value={brandQuery.data?.name ?? "Brand profile"}
              status={brandStatus}
            />
            <ContextLine
              label="Audience"
              value={audienceQuery.data?.description || "Audience profile"}
              status={audienceStatus}
            />
            <ContextLine
              label="Strategy"
              value={strategyQuery.data ? `${strategyQuery.data.postsPerWeek} posts / week` : "Content strategy"}
              status={strategyStatus}
            />
          </div>
          {contextHasError ? (
            <div className="ideas-inline-error" role="alert">
              <span>{contextErrorMessage}</span>
              <Button
                type="button"
                variant="quiet"
                onClick={() => {
                  void brandQuery.refetch();
                  void audienceQuery.refetch();
                  void strategyQuery.refetch();
                }}
              >
                Retry
              </Button>
            </div>
          ) : null}
          {!contextReady && !contextHasError && !contextStatuses.includes("loading") ? (
            <p className="idea-context-blocked" role="status">
              Generation needs all three saved foundations: brand, audience and content
              strategy.
            </p>
          ) : null}
        </section>
      </header>
      {generateMutation.isError && !generationOpen ? (
        <p className="ideas-page-error" role="alert">{errorMessage(generateMutation.error)}</p>
      ) : null}
      {announcement ? <p className="ideas-page-announcement" role="status">{announcement}</p> : null}
      <main className={`ideas-workspace${selectedIdea ? " has-selection" : ""}`}>
        <IdeaList
          ideas={ideas}
          selectedId={selectedIdea?.id ?? null}
          status={status}
          platform={platform}
          pillarId={pillarId}
          pillars={pillarsQuery.data ?? []}
          pillarsError={pillarsQuery.isError ? errorMessage(pillarsQuery.error) : null}
          isLoading={anyIdeaLoading}
          onStatusChange={setStatus}
          onPlatformChange={setPlatform}
          onPillarChange={setPillarId}
          onSelect={(idea) => {
            if (window.matchMedia("(max-width: 700px)").matches) {
              pendingMobileFocus.current = { target: "detail", ideaId: idea.id };
            }
            setSelectedId(idea.id);
            setActionError(null);
            setAnnouncement(null);
          }}
          setIdeaButtonRef={setIdeaButtonRef}
          onGenerate={() => {
            generateMutation.reset();
            setGenerationOpen(true);
          }}
          onRetryPillars={() => void pillarsQuery.refetch()}
        />
        <IdeaDetail
          idea={selectedIdea}
          pillarName={selectedPillar}
          busyAction={busyAction}
          actionError={actionError}
          announcement={announcement}
          titleRef={ideaDetailTitleRef}
          onApprove={() => {
            if (selectedIdea) void runIdeaAction("approve", selectedIdea);
          }}
          onReject={() => {
            if (selectedIdea) void runIdeaAction("reject", selectedIdea);
          }}
          onCreateDraft={createDraft}
          onBackToList={() => {
            if (window.matchMedia("(max-width: 700px)").matches && selectedIdea) {
              pendingMobileFocus.current = { target: "list", ideaId: selectedIdea.id };
            }
            setSelectedId(null);
          }}
        />
      </main>
      <IdeaGenerationDialog
        open={generationOpen}
        onClose={() => setGenerationOpen(false)}
        busy={generateMutation.isPending}
        contextReady={contextReady && !contextHasError}
        error={generateMutation.isError ? errorMessage(generateMutation.error) : null}
        onGenerate={generateIdeas}
      />
    </div>
  );
}

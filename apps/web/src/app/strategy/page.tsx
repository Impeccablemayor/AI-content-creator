"use client";

import { useId, type FormEvent } from "react";
import { ApiClientError } from "@/lib/api/client";
import type { ContentPillar } from "@/lib/api/models";
import { useSession } from "@/components/session/session-provider";
import {
  useAudienceProfile,
  useBrandProfile,
  useContentPillars,
  useContentStrategy,
  useCreateContentPillar,
  useDeleteContentPillar,
  useUpdateAudienceProfile,
  useUpdateBrandProfile,
  useUpdateContentPillar,
  useUpdateContentStrategy,
} from "@/lib/queries/hooks";
import {
  Badge,
  Button,
  EmptyState,
  ErrorState,
  Field,
  Input,
  Textarea,
} from "@/components/ui/primitives";

function errorMessage(error: unknown): string {
  if (error instanceof ApiClientError) {
    return error.traceId ? `${error.message} Reference: ${error.traceId}` : error.message;
  }
  return error instanceof Error
    ? error.message
    : "The request could not be completed. Try again.";
}

function profileNotConfigured(error: unknown, code: string): boolean {
  return error instanceof ApiClientError && error.status === 404 && error.code === code;
}

type FoundationStatus = {
  label: string;
  tone: "positive" | "warning" | "negative";
};

function profileStatus(
  query: { isPending: boolean; isSuccess: boolean; isError: boolean; error: unknown },
  missingCode: string,
): FoundationStatus {
  if (query.isPending) return { label: "Checking", tone: "warning" };
  if (query.isSuccess) return { label: "Configured", tone: "positive" };
  if (query.isError && profileNotConfigured(query.error, missingCode)) {
    return { label: "Not configured", tone: "warning" };
  }
  return { label: "Unavailable", tone: "negative" };
}

function value(form: HTMLFormElement, key: string): string {
  return String(new FormData(form).get(key) ?? "").trim();
}

function submitBrand(event: FormEvent<HTMLFormElement>, save: (request: {
  name: string;
  description: string;
  industry: string;
  toneOfVoice: string;
  contentGoals: string;
}) => void) {
  event.preventDefault();
  const form = event.currentTarget;
  save({
    name: value(form, "name"),
    description: value(form, "description"),
    industry: value(form, "industry"),
    toneOfVoice: value(form, "toneOfVoice"),
    contentGoals: value(form, "contentGoals"),
  });
}

function submitAudience(event: FormEvent<HTMLFormElement>, save: (request: {
  description: string;
  demographics: string;
  interests: string;
  painPoints: string;
  goals: string;
}) => void) {
  event.preventDefault();
  const form = event.currentTarget;
  save({
    description: value(form, "description"),
    demographics: value(form, "demographics"),
    interests: value(form, "interests"),
    painPoints: value(form, "painPoints"),
    goals: value(form, "goals"),
  });
}

function submitStrategy(event: FormEvent<HTMLFormElement>, save: (request: {
  postsPerWeek: number;
  contentPillars: string;
  contentObjectives: string;
  preferredFormats: string;
  storytellingStyle: string;
  hashtagStrategy: string;
  topicsToPrioritize: string;
  topicsToAvoid: string;
  preferredPostingTimes: string;
}) => void) {
  event.preventDefault();
  const form = event.currentTarget;
  save({
    postsPerWeek: Number(value(form, "postsPerWeek")),
    contentPillars: value(form, "contentPillars"),
    contentObjectives: value(form, "contentObjectives"),
    preferredFormats: value(form, "preferredFormats"),
    storytellingStyle: value(form, "storytellingStyle"),
    hashtagStrategy: value(form, "hashtagStrategy"),
    topicsToPrioritize: value(form, "topicsToPrioritize"),
    topicsToAvoid: value(form, "topicsToAvoid"),
    preferredPostingTimes: value(form, "preferredPostingTimes"),
  });
}

function SectionHeader({
  index,
  title,
  description,
  status,
  headingId,
}: {
  index: string;
  title: string;
  description: string;
  status?: FoundationStatus;
  headingId?: string;
}) {
  return (
    <div className="mb-6 flex flex-wrap items-start justify-between gap-4 border-b border-[#d7d2c8] pb-4">
      <div>
        <p className="eyebrow">FOUNDATION / {index}</p>
        <h2 id={headingId} className="mt-2 text-xl font-medium tracking-[-0.04em] text-[#2d2b26]">
          {title}
        </h2>
        <p className="mt-2 max-w-2xl text-sm leading-6 text-[#706d66]">{description}</p>
      </div>
      {status ? <Badge tone={status.tone}>{status.label}</Badge> : null}
    </div>
  );
}

function MutationFeedback({
  error,
  saved,
}: {
  error: unknown;
  saved: boolean;
}) {
  if (error) {
    return (
      <p className="ideas-page-error" role="alert">
        {errorMessage(error)}
      </p>
    );
  }
  return saved ? (
    <p className="idea-action-success" role="status">
      Saved to this workspace.
    </p>
  ) : null;
}

function PillarEditor({
  workspaceId,
  pillar,
}: {
  workspaceId: string;
  pillar: ContentPillar;
}) {
  const id = useId();
  const update = useUpdateContentPillar(workspaceId, pillar.id);
  const remove = useDeleteContentPillar(workspaceId);

  function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    update.mutate({
      name: value(form, "name"),
      description: value(form, "description"),
      objective: value(form, "objective") || null,
      weight: Number(value(form, "weight")),
    });
  }

  return (
    <article className="border-b border-[#e3ded4] py-5 last:border-b-0">
      <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="text-base font-medium tracking-[-0.025em] text-[#302e29]">
              {pillar.name}
            </h3>
            <Badge tone={pillar.isActive ? "positive" : "neutral"}>
              {pillar.isActive ? "Active" : "Inactive"}
            </Badge>
          </div>
          <p className="mt-1 text-xs text-[#77736a]">
            {pillar.ideaCount} {pillar.ideaCount === 1 ? "idea" : "ideas"} · weight{" "}
            {pillar.weight}
          </p>
        </div>
        <Button
          type="button"
          variant="quiet"
          disabled={update.isPending}
          onClick={() => update.mutate({ isActive: !pillar.isActive })}
        >
          {pillar.isActive ? "Deactivate" : "Activate"}
        </Button>
      </div>
      <form className="grid gap-4 md:grid-cols-2" onSubmit={save}>
        <Field label="Pillar name" htmlFor={`${id}-name`}>
          <Input
            id={`${id}-name`}
            name="name"
            required
            maxLength={150}
            defaultValue={pillar.name}
            onChange={() => update.reset()}
          />
        </Field>
        <Field
          label="Relative weight"
          hint="A percentage from greater than 0 up to 100. Pillars are listed by weight."
          htmlFor={`${id}-weight`}
        >
          <Input
            id={`${id}-weight`}
            name="weight"
            type="number"
            min="0.01"
            max="100"
            step="0.01"
            required
            defaultValue={pillar.weight}
            onChange={() => update.reset()}
          />
        </Field>
        <Field label="Description" htmlFor={`${id}-description`}>
          <Textarea
            id={`${id}-description`}
            name="description"
            maxLength={2000}
            defaultValue={pillar.description}
            onChange={() => update.reset()}
          />
        </Field>
        <Field label="Objective" htmlFor={`${id}-objective`}>
          <Textarea
            id={`${id}-objective`}
            name="objective"
            maxLength={1000}
            defaultValue={pillar.objective ?? ""}
            onChange={() => update.reset()}
          />
        </Field>
        <div className="flex flex-wrap items-center gap-2 md:col-span-2">
          <Button type="submit" variant="secondary" busy={update.isPending}>
            Save pillar
          </Button>
          <Button
            type="button"
            variant="quiet"
            disabled={remove.isPending}
            onClick={() => {
              if (window.confirm(`Delete the “${pillar.name}” pillar? Existing ideas will remain.`)) {
                remove.mutate(pillar.id);
              }
            }}
          >
            Delete
          </Button>
          {update.isSuccess ? (
            <span className="text-xs text-[#557153]" role="status">Pillar saved.</span>
          ) : null}
          {update.isError || remove.isError ? (
            <span className="text-xs text-[#914a42]" role="alert">
              {errorMessage(update.error ?? remove.error)}
            </span>
          ) : null}
        </div>
      </form>
    </article>
  );
}

function StrategyWorkspace({ workspaceId }: { workspaceId: string }) {
  const brandId = useId();
  const audienceId = useId();
  const strategyId = useId();
  const pillarId = useId();
  const brand = useBrandProfile(workspaceId);
  const audience = useAudienceProfile(workspaceId);
  const strategy = useContentStrategy(workspaceId);
  const pillars = useContentPillars(workspaceId);
  const saveBrand = useUpdateBrandProfile(workspaceId);
  const saveAudience = useUpdateAudienceProfile(workspaceId);
  const saveStrategy = useUpdateContentStrategy(workspaceId);
  const createPillar = useCreateContentPillar(workspaceId);
  const brandMissing = profileNotConfigured(brand.error, "BRAND_PROFILE_NOT_FOUND");
  const audienceMissing = profileNotConfigured(audience.error, "AUDIENCE_PROFILE_NOT_FOUND");
  const strategyMissing = profileNotConfigured(strategy.error, "CONTENT_STRATEGY_NOT_FOUND");
  const brandStatus = profileStatus(brand, "BRAND_PROFILE_NOT_FOUND");
  const audienceStatus = profileStatus(audience, "AUDIENCE_PROFILE_NOT_FOUND");
  const strategyStatus = profileStatus(strategy, "CONTENT_STRATEGY_NOT_FOUND");

  function saveNewPillar(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    createPillar.mutate(
      {
        name: value(form, "name"),
        description: value(form, "description"),
        objective: value(form, "objective") || null,
        weight: Number(value(form, "weight")),
        audienceProfileId: audience.data?.id ?? null,
        isActive: true,
      },
      { onSuccess: () => form.reset() },
    );
  }

  return (
    <main className="ideas-page">
      <header className="ideas-page-heading">
        <div>
          <p className="eyebrow">WORKSPACE / STRATEGY</p>
          <h1 className="mt-3 text-[clamp(34px,4.1vw,58px)] font-medium leading-[1.02] tracking-[-0.065em] text-[#22211d]">
            Give every idea a reason.
          </h1>
          <p className="ideas-page-intro">
            Configure the brand, audience, and publishing direction the content engine uses.
            Changes are saved to the active workspace.
          </p>
        </div>
        <div className="grid gap-0 border-t border-[#e1ddd4] sm:grid-cols-3">
          {[
            { index: "01", label: "Brand", status: brandStatus },
            { index: "02", label: "Audience", status: audienceStatus },
            { index: "03", label: "Strategy", status: strategyStatus },
          ].map(({ index, label, status }) => (
            <div
              className="flex items-center justify-between border-b border-[#e1ddd4] py-3 sm:mr-5 sm:border-b-0 sm:border-r sm:pr-5"
              key={label}
            >
              <span className="text-xs text-[#625e56]">
                <span className="mr-2 font-mono text-[10px] text-[#969186]">{index}</span>
                {label}
              </span>
              <Badge tone={status.tone}>{status.label}</Badge>
            </div>
          ))}
        </div>
      </header>

      <div className="space-y-9 pt-8">
        <section aria-labelledby={`${brandId}-heading`}>
          <SectionHeader
            index="01"
            title="Brand profile"
            description="The identity, voice, and objectives that should remain consistent across content."
            status={brandStatus}
            headingId={`${brandId}-heading`}
          />
          {brand.isPending ? (
            <p className="text-sm text-[#77736a]" role="status">Loading brand profile…</p>
          ) : brand.isError && !brandMissing ? (
            <ErrorState message={errorMessage(brand.error)} onRetry={() => void brand.refetch()} />
          ) : (
            <form
              className="grid gap-x-6 gap-y-5 md:grid-cols-2"
              onSubmit={(event) => submitBrand(event, (request) => saveBrand.mutate(request))}
              aria-labelledby={`${brandId}-heading`}
            >
              <Field label="Brand name" htmlFor={`${brandId}-name`}>
                <Input id={`${brandId}-name`} name="name" required maxLength={200} defaultValue={brand.data?.name ?? ""} onChange={() => saveBrand.reset()} />
              </Field>
              <Field label="Industry" htmlFor={`${brandId}-industry`}>
                <Input id={`${brandId}-industry`} name="industry" required maxLength={150} defaultValue={brand.data?.industry ?? ""} onChange={() => saveBrand.reset()} />
              </Field>
              <Field label="Brand description" hint="What the brand does and why it matters." htmlFor={`${brandId}-description`}>
                <Textarea id={`${brandId}-description`} name="description" required maxLength={4000} defaultValue={brand.data?.description ?? ""} onChange={() => saveBrand.reset()} />
              </Field>
              <Field label="Tone of voice" hint="A few concrete qualities are more useful than broad labels." htmlFor={`${brandId}-tone`}>
                <Textarea id={`${brandId}-tone`} name="toneOfVoice" required maxLength={2000} defaultValue={brand.data?.toneOfVoice ?? ""} onChange={() => saveBrand.reset()} />
              </Field>
              <Field label="Content goals" hint="What should your content help the brand achieve?" htmlFor={`${brandId}-goals`}>
                <Textarea id={`${brandId}-goals`} name="contentGoals" required maxLength={2000} defaultValue={brand.data?.contentGoals ?? ""} onChange={() => saveBrand.reset()} />
              </Field>
              <div className="flex items-end gap-3">
                <Button type="submit" variant="primary" busy={saveBrand.isPending}>
                  Save brand profile
                </Button>
                <MutationFeedback error={saveBrand.error} saved={saveBrand.isSuccess} />
              </div>
            </form>
          )}
        </section>

        <section aria-labelledby={`${audienceId}-heading`} className="border-t border-[#d7d2c8] pt-8">
          <SectionHeader
            index="02"
            title="Audience profile"
            description="Describe the people the content is for: their context, interests, friction, and desired outcomes."
            status={audienceStatus}
            headingId={`${audienceId}-heading`}
          />
          {audience.isPending ? (
            <p className="text-sm text-[#77736a]" role="status">Loading audience profile…</p>
          ) : audience.isError && !audienceMissing ? (
            <ErrorState message={errorMessage(audience.error)} onRetry={() => void audience.refetch()} />
          ) : (
            <form
              className="grid gap-x-6 gap-y-5 md:grid-cols-2"
              onSubmit={(event) => submitAudience(event, (request) => saveAudience.mutate(request))}
              aria-labelledby={`${audienceId}-heading`}
            >
              <Field label="Audience description" hint="A concise description of who this audience is." htmlFor={`${audienceId}-description`}>
                <Textarea id={`${audienceId}-description`} name="description" required maxLength={4000} defaultValue={audience.data?.description ?? ""} onChange={() => saveAudience.reset()} />
              </Field>
              <Field label="Demographics and context" htmlFor={`${audienceId}-demographics`}>
                <Textarea id={`${audienceId}-demographics`} name="demographics" maxLength={3000} defaultValue={audience.data?.demographics ?? ""} onChange={() => saveAudience.reset()} />
              </Field>
              <Field label="Interests" htmlFor={`${audienceId}-interests`}>
                <Textarea id={`${audienceId}-interests`} name="interests" maxLength={3000} defaultValue={audience.data?.interests ?? ""} onChange={() => saveAudience.reset()} />
              </Field>
              <Field label="Pain points" htmlFor={`${audienceId}-pain-points`}>
                <Textarea id={`${audienceId}-pain-points`} name="painPoints" maxLength={3000} defaultValue={audience.data?.painPoints ?? ""} onChange={() => saveAudience.reset()} />
              </Field>
              <Field label="Goals and desired outcomes" htmlFor={`${audienceId}-goals`}>
                <Textarea id={`${audienceId}-goals`} name="goals" maxLength={3000} defaultValue={audience.data?.goals ?? ""} onChange={() => saveAudience.reset()} />
              </Field>
              <div className="flex items-end gap-3">
                <Button type="submit" variant="primary" busy={saveAudience.isPending}>
                  Save audience profile
                </Button>
                <MutationFeedback error={saveAudience.error} saved={saveAudience.isSuccess} />
              </div>
            </form>
          )}
        </section>

        <section aria-labelledby={`${strategyId}-heading`} className="border-t border-[#d7d2c8] pt-8">
          <SectionHeader
            index="03"
            title="Content strategy"
            description="Set the working cadence, priorities, formats, and posting guidance used by idea and draft generation."
            status={strategyStatus}
            headingId={`${strategyId}-heading`}
          />
          {strategy.isPending ? (
            <p className="text-sm text-[#77736a]" role="status">Loading content strategy…</p>
          ) : strategy.isError && !strategyMissing ? (
            <ErrorState message={errorMessage(strategy.error)} onRetry={() => void strategy.refetch()} />
          ) : (
            <form
              className="grid gap-x-6 gap-y-5 md:grid-cols-2"
              onSubmit={(event) => submitStrategy(event, (request) => saveStrategy.mutate(request))}
              aria-labelledby={`${strategyId}-heading`}
            >
              <Field label="Posts per week" hint="The API accepts a value from 1 to 50." htmlFor={`${strategyId}-frequency`}>
                <Input id={`${strategyId}-frequency`} name="postsPerWeek" type="number" min="1" max="50" required defaultValue={strategy.data?.postsPerWeek ?? ""} onChange={() => saveStrategy.reset()} />
              </Field>
              <Field label="Content pillars" hint="Use the structured pillar section below to manage the actual pillars; this field remains the strategy's free-text guidance." htmlFor={`${strategyId}-pillars`}>
                <Textarea id={`${strategyId}-pillars`} name="contentPillars" defaultValue={strategy.data?.contentPillars ?? ""} onChange={() => saveStrategy.reset()} />
              </Field>
              <Field label="Content objectives" htmlFor={`${strategyId}-objectives`}>
                <Textarea id={`${strategyId}-objectives`} name="contentObjectives" defaultValue={strategy.data?.contentObjectives ?? ""} onChange={() => saveStrategy.reset()} />
              </Field>
              <Field label="Preferred formats" htmlFor={`${strategyId}-formats`}>
                <Textarea id={`${strategyId}-formats`} name="preferredFormats" defaultValue={strategy.data?.preferredFormats ?? ""} onChange={() => saveStrategy.reset()} />
              </Field>
              <Field label="Storytelling style" htmlFor={`${strategyId}-storytelling`}>
                <Textarea id={`${strategyId}-storytelling`} name="storytellingStyle" defaultValue={strategy.data?.storytellingStyle ?? ""} onChange={() => saveStrategy.reset()} />
              </Field>
              <Field label="Hashtag strategy" htmlFor={`${strategyId}-hashtags`}>
                <Textarea id={`${strategyId}-hashtags`} name="hashtagStrategy" defaultValue={strategy.data?.hashtagStrategy ?? ""} onChange={() => saveStrategy.reset()} />
              </Field>
              <Field label="Topics to prioritize" htmlFor={`${strategyId}-prioritize`}>
                <Textarea id={`${strategyId}-prioritize`} name="topicsToPrioritize" defaultValue={strategy.data?.topicsToPrioritize ?? ""} onChange={() => saveStrategy.reset()} />
              </Field>
              <Field label="Topics to avoid" htmlFor={`${strategyId}-avoid`}>
                <Textarea id={`${strategyId}-avoid`} name="topicsToAvoid" defaultValue={strategy.data?.topicsToAvoid ?? ""} onChange={() => saveStrategy.reset()} />
              </Field>
              <Field label="Preferred posting times" htmlFor={`${strategyId}-times`}>
                <Textarea id={`${strategyId}-times`} name="preferredPostingTimes" defaultValue={strategy.data?.preferredPostingTimes ?? ""} onChange={() => saveStrategy.reset()} />
              </Field>
              <div className="flex items-end gap-3">
                <Button type="submit" variant="primary" busy={saveStrategy.isPending}>
                  Save strategy
                </Button>
                <MutationFeedback error={saveStrategy.error} saved={saveStrategy.isSuccess} />
              </div>
            </form>
          )}
        </section>

        <section aria-labelledby={`${pillarId}-heading`} className="border-t border-[#d7d2c8] pt-8">
          <SectionHeader
            index="04"
            title="Content pillars"
            description="Manage recurring themes and their relative weight. The backend returns pillars in descending weight order."
            headingId={`${pillarId}-heading`}
          />
          {pillars.isError ? (
            <ErrorState message={errorMessage(pillars.error)} onRetry={() => void pillars.refetch()} />
          ) : null}
          {pillars.isPending ? (
            <p className="text-sm text-[#77736a]" role="status">Loading content pillars…</p>
          ) : null}
          {pillars.isSuccess && pillars.data.length === 0 ? (
            <EmptyState
              title="No content pillars yet"
              description="Add a recurring theme below. Ideas can then be attributed to a real pillar and filtered by it."
            />
          ) : null}
          {pillars.data?.map((pillar) => (
            <PillarEditor key={pillar.id} workspaceId={workspaceId} pillar={pillar} />
          ))}
          <form
            className="mt-7 grid gap-x-6 gap-y-5 border-t border-[#e3ded4] pt-6 md:grid-cols-2"
            onSubmit={saveNewPillar}
            aria-labelledby={`${pillarId}-heading`}
          >
            <h3 className="md:col-span-2 text-base font-medium text-[#302e29]">
              Add a pillar
            </h3>
            <Field label="Pillar name" htmlFor={`${pillarId}-name`}>
              <Input id={`${pillarId}-name`} name="name" required maxLength={150} onChange={() => createPillar.reset()} />
            </Field>
            <Field label="Relative weight" hint="Greater than 0 and no more than 100." htmlFor={`${pillarId}-weight`}>
              <Input id={`${pillarId}-weight`} name="weight" type="number" min="0.01" max="100" step="0.01" placeholder="Choose a value" required onChange={() => createPillar.reset()} />
            </Field>
            <Field label="Description" htmlFor={`${pillarId}-description`}>
              <Textarea id={`${pillarId}-description`} name="description" maxLength={2000} onChange={() => createPillar.reset()} />
            </Field>
            <Field label="Objective" htmlFor={`${pillarId}-objective`}>
              <Textarea id={`${pillarId}-objective`} name="objective" maxLength={1000} onChange={() => createPillar.reset()} />
            </Field>
            <div className="flex flex-wrap items-center gap-3 md:col-span-2">
              <Button type="submit" variant="secondary" busy={createPillar.isPending}>
                Add content pillar
              </Button>
              {audience.data ? (
                <span className="text-xs text-[#77736a]">
                  New pillar will target the configured audience profile.
                </span>
              ) : (
                <span className="text-xs text-[#77736a]">
                  No audience is configured; the pillar will be created without an audience link.
                </span>
              )}
              {createPillar.isSuccess ? (
                <span className="text-xs text-[#557153]" role="status">Pillar added.</span>
              ) : null}
              {createPillar.isError ? (
                <span className="text-xs text-[#914a42]" role="alert">{errorMessage(createPillar.error)}</span>
              ) : null}
            </div>
          </form>
        </section>
      </div>
    </main>
  );
}

export default function StrategyPage() {
  const { session } = useSession();
  const workspaceId = session?.workspaceId ?? "";
  return <StrategyWorkspace key={workspaceId} workspaceId={workspaceId} />;
}

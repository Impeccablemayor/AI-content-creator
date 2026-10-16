"use client";

import { useId, type FormEvent } from "react";
import { ApiClientError } from "@/lib/api/client";
import { useSession } from "@/components/session/session-provider";
import { Badge, Button, ErrorState, Field, Input } from "@/components/ui/primitives";
import {
  useCurrentWorkspace,
  useUpdateCurrentWorkspace,
} from "@/lib/queries/hooks";

function errorMessage(error: unknown): string {
  if (error instanceof ApiClientError) {
    return error.traceId ? `${error.message} Reference: ${error.traceId}` : error.message;
  }
  return error instanceof Error
    ? error.message
    : "The request could not be completed. Try again.";
}

function SettingsWorkspace({
  workspaceId,
  role,
}: {
  workspaceId: string;
  role: string;
}) {
  const nameId = useId();
  const workspace = useCurrentWorkspace(workspaceId);
  const update = useUpdateCurrentWorkspace(workspaceId);
  const canRename = ["owner", "admin"].includes(role.toLowerCase());

  function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const name = String(new FormData(event.currentTarget).get("workspaceName") ?? "").trim();
    update.mutate(name);
  }

  return (
    <main className="ideas-page">
      <header className="ideas-page-heading">
        <div>
          <p className="eyebrow">WORKSPACE / SETTINGS</p>
          <h1 className="mt-3 text-[clamp(34px,4.1vw,58px)] font-medium leading-[1.02] tracking-[-0.065em] text-[#22211d]">
            Keep the workspace clear.
          </h1>
          <p className="ideas-page-intro">
            Manage the workspace setting the backend currently exposes. Brand, audience, and
            content strategy live in Strategy.
          </p>
        </div>
      </header>

      <section className="max-w-3xl pt-8" aria-labelledby="workspace-settings-title">
        <div className="flex flex-wrap items-start justify-between gap-3 border-b border-[#d7d2c8] pb-4">
          <div>
            <p className="eyebrow">IDENTITY</p>
            <h2 id="workspace-settings-title" className="mt-2 text-xl font-medium tracking-[-0.04em] text-[#2d2b26]">
              Workspace name
            </h2>
          </div>
          <Badge>{role}</Badge>
        </div>
        {workspace.isPending ? (
          <p className="py-6 text-sm text-[#77736a]" role="status">Loading workspace settings…</p>
        ) : workspace.isError ? (
          <div className="py-6">
            <ErrorState
              message={errorMessage(workspace.error)}
              onRetry={() => void workspace.refetch()}
            />
          </div>
        ) : (
          <form className="grid gap-5 py-6" onSubmit={save}>
            <Field
              label="Name"
              hint={canRename ? "This name appears in the workspace switcher." : "Only workspace owners and admins can change this name."}
              htmlFor={nameId}
            >
              <Input
                id={nameId}
                name="workspaceName"
                required
                maxLength={120}
                readOnly={!canRename}
                key={workspaceId}
                defaultValue={workspace.data.name}
                onChange={() => update.reset()}
              />
            </Field>
            <div className="flex flex-wrap items-center gap-3">
              {canRename ? (
                <Button type="submit" variant="primary" busy={update.isPending}>
                  Save workspace name
                </Button>
              ) : null}
              {update.isSuccess ? (
                <span className="text-xs text-[#557153]" role="status">Workspace name updated.</span>
              ) : null}
              {update.isError ? (
                <span className="text-xs text-[#914a42]" role="alert">{errorMessage(update.error)}</span>
              ) : null}
            </div>
          </form>
        )}
      </section>

      <section className="mt-6 max-w-3xl border-t border-[#d7d2c8] pt-6" aria-labelledby="settings-boundary">
        <p className="eyebrow">AVAILABLE CONTROLS</p>
        <h2 id="settings-boundary" className="mt-2 text-lg font-medium tracking-[-0.03em] text-[#302e29]">
          Settings stay tied to real capabilities.
        </h2>
        <p className="mt-2 text-sm leading-6 text-[#706d66]">
          The current API does not expose automation preferences, notification controls, billing,
          password changes, or member management. Those controls are intentionally not presented.
        </p>
      </section>
    </main>
  );
}

export default function SettingsPage() {
  const { session } = useSession();
  const workspaceId = session?.workspaceId ?? "";
  return (
    <SettingsWorkspace
      key={workspaceId}
      workspaceId={workspaceId}
      role={session?.role ?? ""}
    />
  );
}

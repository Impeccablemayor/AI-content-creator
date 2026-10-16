"use client";

import { useState } from "react";
import { ApiClientError } from "@/lib/api/client";
import type { ContentPlatform, SocialAccount } from "@/lib/api/models";
import { useSession } from "@/components/session/session-provider";
import {
  Badge,
  Button,
  Dialog,
  EmptyState,
  ErrorState,
} from "@/components/ui/primitives";
import {
  useConnectSocialAccount,
  useDisconnectSocialAccount,
  useSocialAccounts,
} from "@/lib/queries/hooks";

const platforms: Array<{ id: ContentPlatform; name: string; description: string }> = [
  {
    id: "LinkedIn",
    name: "LinkedIn",
    description: "Authorize the workspace to connect a LinkedIn account.",
  },
  {
    id: "X",
    name: "X",
    description: "Authorize the workspace to connect an X account.",
  },
];

function errorMessage(error: unknown): string {
  if (error instanceof ApiClientError) {
    return error.traceId ? `${error.message} Reference: ${error.traceId}` : error.message;
  }
  return error instanceof Error
    ? error.message
    : "The request could not be completed. Try again.";
}

function statusTone(status: string): "neutral" | "positive" | "warning" | "negative" {
  switch (status.toLowerCase()) {
    case "connected":
      return "positive";
    case "expired":
      return "warning";
    case "revoked":
    case "error":
      return "negative";
    default:
      return "neutral";
  }
}

function formatDate(value: string | null): string {
  if (!value) return "Not provided";
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? "Not provided"
    : new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" }).format(date);
}

function ConnectionsWorkspace({ workspaceId }: { workspaceId: string }) {
  const accounts = useSocialAccounts(workspaceId);
  const connect = useConnectSocialAccount(workspaceId);
  const disconnect = useDisconnectSocialAccount(workspaceId);
  const [selectedAccount, setSelectedAccount] = useState<SocialAccount | null>(null);

  return (
    <main className="ideas-page">
      <header className="ideas-page-heading">
        <div>
          <p className="eyebrow">WORKSPACE / DISTRIBUTION</p>
          <h1 className="mt-3 text-[clamp(34px,4.1vw,58px)] font-medium leading-[1.02] tracking-[-0.065em] text-[#22211d]">
            Connected channels.
          </h1>
          <p className="ideas-page-intro">
            Review authorization state for the platforms supported by this workspace.
            Credentials stay on the server and are never shown here.
          </p>
        </div>
      </header>

      {connect.isError ? (
        <p className="ideas-page-error" role="alert">{errorMessage(connect.error)}</p>
      ) : null}
      {disconnect.isError ? (
        <p className="ideas-page-error" role="alert">{errorMessage(disconnect.error)}</p>
      ) : null}

      <section className="grid gap-5 border-b border-[#d7d2c8] py-7 md:grid-cols-2" aria-labelledby="supported-platforms">
        <h2 id="supported-platforms" className="sr-only">Supported social platforms</h2>
        {platforms.map((platform) => (
          <article key={platform.id} className="min-w-0 border-t border-[#d7d2c8] pt-4">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <p className="eyebrow">SUPPORTED PLATFORM</p>
                <h3 className="mt-2 text-xl font-medium tracking-[-0.04em] text-[#2d2b26]">{platform.name}</h3>
                <p className="mt-2 text-sm leading-6 text-[#706d66]">{platform.description}</p>
              </div>
              <Button
                type="button"
                variant="secondary"
                busy={connect.isPending}
                disabled={connect.isPending}
                onClick={() => connect.mutate(platform.id, {
                  onSuccess: (result) => window.location.assign(result.authorizationUrl),
                })}
              >
                Connect {platform.name}
              </Button>
            </div>
          </article>
        ))}
      </section>

      <section className="pt-7" aria-labelledby="account-list-title">
        <div className="flex flex-wrap items-end justify-between gap-3 border-b border-[#d7d2c8] pb-4">
          <div>
            <p className="eyebrow">AUTHORIZATION</p>
            <h2 id="account-list-title" className="mt-2 text-xl font-medium tracking-[-0.04em] text-[#2d2b26]">
              Workspace accounts
            </h2>
          </div>
          {accounts.isSuccess ? (
            <span className="text-xs text-[#77736a]">{accounts.data.length} returned by the API</span>
          ) : null}
        </div>
        {accounts.isPending ? (
          <p className="py-7 text-sm text-[#77736a]" role="status">Loading connected accounts…</p>
        ) : accounts.isError ? (
          <div className="py-6">
            <ErrorState
              title="Account status is unavailable"
              message={errorMessage(accounts.error)}
              onRetry={() => void accounts.refetch()}
            />
          </div>
        ) : accounts.data.length === 0 ? (
          <EmptyState
            title="No connected accounts"
            description="Connect LinkedIn or X to create a workspace authorization. Actual publishing availability depends on the backend worker and platform permissions."
          />
        ) : (
          <ul className="m-0 list-none p-0">
            {accounts.data.map((account) => (
              <li key={account.id} className="grid min-w-0 gap-4 border-b border-[#e3ded4] py-5 md:grid-cols-[minmax(0,1fr)_minmax(160px,0.55fr)_auto] md:items-center">
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <h3 className="text-base font-medium text-[#302e29]">
                      {account.displayName || account.username || account.platform}
                    </h3>
                    <Badge tone={statusTone(account.status)}>{account.status}</Badge>
                  </div>
                  <p className="mt-1 break-all text-xs text-[#77736a]">
                    {account.platform}{account.username ? ` · @${account.username.replace(/^@/, "")}` : ""}
                  </p>
                  {account.profileUrl ? (
                    <a
                      className="mt-2 inline-block text-xs text-[#685437] underline underline-offset-4"
                      href={account.profileUrl}
                      target="_blank"
                      rel="noreferrer"
                    >
                      Open platform profile
                    </a>
                  ) : null}
                </div>
                <div className="text-xs leading-5 text-[#77736a]">
                  <span className="block">Connected {formatDate(account.connectedAt)}</span>
                  <span className="block">Authorization expires {formatDate(account.tokenExpiresAt)}</span>
                </div>
                <Button type="button" variant="quiet" onClick={() => setSelectedAccount(account)}>
                  Disconnect
                </Button>
              </li>
            ))}
          </ul>
        )}
      </section>

      <p className="mt-8 border-t border-[#d7d2c8] pt-4 text-xs leading-5 text-[#77736a]">
        Authorization and account metadata come from the workspace-scoped social API. This page does not expose tokens or claim that a platform is ready to publish unless the backend reports its account state.
      </p>

      <Dialog
        open={selectedAccount !== null}
        title="Disconnect social account?"
        onClose={() => setSelectedAccount(null)}
      >
        <p className="text-sm leading-6 text-[#625e56]">
          {selectedAccount
            ? `This removes the stored authorization for ${selectedAccount.displayName || selectedAccount.username || selectedAccount.platform}.`
            : "This removes the stored workspace authorization."}
        </p>
        <div className="dialog-actions mt-5">
          <Button type="button" variant="quiet" onClick={() => setSelectedAccount(null)}>
            Keep connected
          </Button>
          <Button
            type="button"
            variant="danger"
            busy={disconnect.isPending}
            disabled={!selectedAccount || disconnect.isPending}
            onClick={() => {
              if (selectedAccount) {
                disconnect.mutate(selectedAccount.id, {
                  onSuccess: () => setSelectedAccount(null),
                });
              }
            }}
          >
            Disconnect
          </Button>
        </div>
      </Dialog>
    </main>
  );
}

export default function ConnectionsPage() {
  const { session } = useSession();
  const workspaceId = session?.workspaceId ?? "";
  return <ConnectionsWorkspace key={workspaceId} workspaceId={workspaceId} />;
}

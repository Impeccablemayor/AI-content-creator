"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useEffect, useId, useState, type FormEvent, type MouseEvent, type ReactNode } from "react";
import { ApiClientError } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/query-keys";
import { workspaceApi } from "@/lib/api/workspaces";
import type { IconName } from "@/components/ui/icon";
import { Icon } from "@/components/ui/icon";
import { Button, Dialog, ErrorState, Field, IconButton, Input } from "@/components/ui/primitives";
import { useSession } from "@/components/session/session-provider";

interface NavigationItem {
  label: string;
  href: string;
  icon: IconName;
}

interface NavigationGroup {
  label: string;
  items: NavigationItem[];
}

const navigation: NavigationGroup[] = [
  {
    label: "Workspace",
    items: [{ label: "Dashboard", href: "/", icon: "dashboard" }],
  },
  {
    label: "Content",
    items: [
      { label: "Content", href: "/content", icon: "content" },
      { label: "Ideas", href: "/content/ideas", icon: "idea" },
      { label: "Calendar", href: "/calendar", icon: "calendar" },
    ],
  },
  {
    label: "Strategy",
    items: [{ label: "Strategy", href: "/strategy", icon: "strategy" }],
  },
  {
    label: "Insights",
    items: [
      { label: "Analytics", href: "/analytics", icon: "analytics" },
      { label: "AI Insights", href: "/ai-insights", icon: "insight" },
    ],
  },
  {
    label: "Distribution",
    items: [{ label: "Connections", href: "/connections", icon: "connections" }],
  },
  {
    label: "System",
    items: [{ label: "Settings", href: "/settings", icon: "settings" }],
  },
];

function errorMessage(error: unknown): string {
  if (error instanceof Error) return error.message;
  return "The request could not be completed. Try again.";
}

function fullName(firstName: string, lastName: string): string {
  return `${firstName} ${lastName}`.trim();
}

function initials(firstName: string, lastName: string): string {
  return `${firstName.charAt(0)}${lastName.charAt(0)}`.toUpperCase();
}

function NavigationLinks({
  pathname,
  onNavigate,
}: {
  pathname: string;
  onNavigate?: () => void;
}) {
  return (
    <nav className="nav-groups" aria-label="Primary navigation">
      {navigation.map((group) => (
        <div className="nav-group" key={group.label}>
          <h2 className="nav-group-label">{group.label}</h2>
          {group.items.map((item) => {
            const active =
              pathname === item.href ||
              (item.href !== "/" && pathname.startsWith(`${item.href}/`));

            return (
              <Link
                key={item.href}
                className="nav-link"
                href={item.href}
                aria-current={active ? "page" : undefined}
                onClick={onNavigate}
              >
                <Icon name={item.icon} className="nav-icon" />
                <span>{item.label}</span>
              </Link>
            );
          })}
        </div>
      ))}
    </nav>
  );
}

function WorkspaceSwitcher({ workspaceId }: { workspaceId: string }) {
  const [createOpen, setCreateOpen] = useState(false);
  const [workspaceName, setWorkspaceName] = useState("");
  const workspaceNameId = useId();
  const { refreshWorkspaceSession } = useSession();
  const workspacesQuery = useQuery({
    queryKey: queryKeys.workspaces(workspaceId),
    queryFn: workspaceApi.list,
  });
  const switchMutation = useMutation({
    mutationFn: workspaceApi.switch,
    onSuccess: refreshWorkspaceSession,
  });
  const createMutation = useMutation({
    mutationFn: workspaceApi.create,
    onSuccess: async () => {
      await refreshWorkspaceSession();
      setCreateOpen(false);
      setWorkspaceName("");
    },
  });
  const activeWorkspace = workspacesQuery.data?.find((workspace) => workspace.isCurrent);

  function submitWorkspace(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const name = workspaceName.trim();
    if (name) createMutation.mutate(name);
  }

  function closeWorkspaceMenu(event: MouseEvent<HTMLButtonElement>) {
    event.currentTarget.closest("details")?.removeAttribute("open");
  }

  return (
    <>
      <details className="workspace-picker">
        <summary className="workspace-trigger" aria-label="Choose active workspace">
          <span className="workspace-trigger-copy">
            <span className="workspace-trigger-label">Current workspace</span>
            <span className="workspace-trigger-name">
              {workspacesQuery.isPending
                ? "Loading workspaces"
                : workspacesQuery.isError
                  ? "Workspace unavailable"
                  : activeWorkspace?.name ?? "Workspace"}
            </span>
          </span>
          <Icon name="chevron" className="nav-icon" />
        </summary>
        <div className="workspace-menu">
          <p className="workspace-menu-label">Your workspaces</p>
          {workspacesQuery.isError ? (
            <div className="workspace-menu-status">
              <span>{errorMessage(workspacesQuery.error)}</span>
              <Button
                type="button"
                variant="quiet"
                onClick={() => void workspacesQuery.refetch()}
              >
                Retry
              </Button>
            </div>
          ) : workspacesQuery.isPending ? (
            <p className="workspace-menu-status" role="status">
              Loading your workspaces…
            </p>
          ) : (
            workspacesQuery.data.map((workspace) => (
              <button
                key={workspace.id}
                className="workspace-option"
                type="button"
                aria-current={workspace.isCurrent}
                disabled={workspace.isCurrent || switchMutation.isPending}
                onClick={(event) => {
                  closeWorkspaceMenu(event);
                  switchMutation.mutate(workspace.id);
                }}
              >
                <span>{workspace.name}</span>
                {workspace.isCurrent ? <span className="workspace-current-mark" /> : null}
              </button>
            ))
          )}
          {switchMutation.isError ? (
            <p className="workspace-menu-status" role="alert">
              {errorMessage(switchMutation.error)}
            </p>
          ) : null}
          <button
            className="workspace-create"
            type="button"
            onClick={(event) => {
              closeWorkspaceMenu(event);
              setCreateOpen(true);
            }}
          >
            <Icon name="plus" className="size-4" />
            <span>Create workspace</span>
          </button>
        </div>
      </details>
      <Dialog
        open={createOpen}
        title="Create workspace"
        onClose={() => setCreateOpen(false)}
      >
        <form className="workspace-form" onSubmit={submitWorkspace}>
          <p className="workspace-form-copy">
            A workspace is a separate content environment. Creating one makes it active in
            this browser session.
          </p>
          <Field label="Workspace name" htmlFor={workspaceNameId}>
            <Input
              id={workspaceNameId}
              autoFocus
              required
              maxLength={120}
              value={workspaceName}
              onChange={(event) => setWorkspaceName(event.target.value)}
              placeholder="e.g. Studio"
            />
          </Field>
          {createMutation.isError ? (
            <p className="auth-error" role="alert">
              {errorMessage(createMutation.error)}
            </p>
          ) : null}
          <div className="dialog-actions">
            <Button type="button" variant="quiet" onClick={() => setCreateOpen(false)}>
              Cancel
            </Button>
            <Button
              type="submit"
              variant="primary"
              busy={createMutation.isPending}
              disabled={!workspaceName.trim()}
            >
              Create workspace
            </Button>
          </div>
        </form>
      </Dialog>
    </>
  );
}

function AccountControl({ mobile = false }: { mobile?: boolean }) {
  const { session, signOut } = useSession();
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function handleSignOut() {
    setError(null);
    setBusy(true);
    try {
      await signOut();
      router.replace("/login");
    } catch (signOutError) {
      setError(errorMessage(signOutError));
    } finally {
      setBusy(false);
    }
  }

  if (!session) return null;

  if (mobile) {
    return (
      <div className="mobile-account">
        <span className="account-monogram" aria-hidden="true">
          {initials(session.user.firstName, session.user.lastName)}
        </span>
        <span className="account-details">
          <span className="account-name">{fullName(session.user.firstName, session.user.lastName)}</span>
          <span className="account-email">{session.user.email}</span>
        </span>
        <IconButton
          type="button"
          className="account-signout"
          label={busy ? "Signing out" : "Sign out"}
          icon="logout"
          disabled={busy}
          onClick={() => void handleSignOut()}
        />
        {error ? <span className="mobile-signout-error" role="alert">{error}</span> : null}
      </div>
    );
  }

  return (
    <>
      <div className="account-row">
        <span className="account-monogram" aria-hidden="true">
          {initials(session.user.firstName, session.user.lastName)}
        </span>
        <span className="account-details">
          <span className="account-name">
            {fullName(session.user.firstName, session.user.lastName)}
          </span>
          <span className="account-email">{session.user.email}</span>
        </span>
        <IconButton
          type="button"
          className="account-signout"
          label={busy ? "Signing out" : "Sign out"}
          icon="logout"
          disabled={busy}
          onClick={() => void handleSignOut()}
        />
      </div>
      {error ? <p className="signout-error" role="alert">{error}</p> : null}
    </>
  );
}

function AppLoading() {
  return (
    <div className="session-loading" role="status" aria-label="Restoring your session">
      <div className="session-loading-rail">
        <span className="skeleton session-loading-mark" />
        <span className="skeleton session-loading-line" />
        <span className="skeleton session-loading-line short" />
      </div>
      <div className="session-loading-content">
        <span className="eyebrow">MAYORX / SESSION</span>
        <span className="skeleton session-loading-title" />
        <span className="skeleton session-loading-line" />
      </div>
    </div>
  );
}

function MobileNavigation({
  pathname,
  workspaceId,
}: {
  pathname: string;
  workspaceId: string;
}) {
  const [open, setOpen] = useState(false);

  return (
    <>
      <div className="mobile-topbar">
        <IconButton
          type="button"
          label="Open navigation"
          icon="menu"
          onClick={() => setOpen(true)}
        />
        <span className="mobile-brand">MAYORX</span>
        <WorkspaceSwitcher workspaceId={workspaceId} />
      </div>
      <div className="mobile-navigation">
        <Dialog
          open={open}
          title="Navigation"
          onClose={() => setOpen(false)}
          placement="left"
        >
          <NavigationLinks pathname={pathname} onNavigate={() => setOpen(false)} />
          <div className="mobile-drawer-account">
            <AccountControl mobile />
          </div>
        </Dialog>
      </div>
    </>
  );
}

export function ApplicationShell({ children }: { children: ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const { status, session, error, retry } = useSession();
  const isAuthRoute = pathname === "/login" || pathname === "/register";
  const pageTitle =
    navigation.flatMap((group) => group.items).find((item) => item.href === pathname)?.label ??
    "Workspace";

  useEffect(() => {
    if (isAuthRoute && status === "authenticated") {
      router.replace("/");
    } else if (!isAuthRoute && status === "unauthenticated") {
      router.replace("/login");
    }
  }, [isAuthRoute, router, status]);

  if (isAuthRoute) {
    return status === "authenticated" ? <AppLoading /> : children;
  }

  if (status === "loading" || status === "unauthenticated") {
    return <AppLoading />;
  }

  if (status === "error") {
    return (
      <div className="session-error">
        <ErrorState
          title="Your session could not be restored"
          message={
            error instanceof ApiClientError && error.traceId
              ? `${errorMessage(error)} Reference: ${error.traceId}`
              : errorMessage(error)
          }
          onRetry={() => void retry()}
        />
      </div>
    );
  }

  if (!session) return <AppLoading />;

  return (
    <div className="app-frame">
      <aside className="app-rail" aria-label="Application sidebar">
        <div className="brand-lockup">
          <span className="brand-mark" aria-hidden="true">MX</span>
          <span>
            <span className="brand-name">Mayorx</span>
            <span className="brand-caption">Content Engine</span>
          </span>
        </div>
        <div className="rail-rule" />
        <WorkspaceSwitcher workspaceId={session.workspaceId} />
        <div className="rail-rule rail-workspace-rule" />
        <NavigationLinks pathname={pathname} />
        <div className="rail-footer">
          <div className="rail-footer-rule" />
          <AccountControl />
        </div>
      </aside>
      <MobileNavigation pathname={pathname} workspaceId={session.workspaceId} />
      <div className="app-main">
        <header className="app-topbar">
          <div className="topbar-context">
            <span>MAYORX</span>
            <span aria-hidden="true">/</span>
            <strong>{pageTitle}</strong>
          </div>
          <div className="topbar-account">
            <span className="topbar-account-label">{session.user.email}</span>
          </div>
        </header>
        <div className="app-content">{children}</div>
      </div>
    </div>
  );
}

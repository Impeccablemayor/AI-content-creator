"use client";

import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  type ReactNode,
} from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiClientError } from "@/lib/api/client";
import { authApi, type LoginRequest, type RegisterRequest } from "@/lib/api/auth";
import { queryKeys } from "@/lib/api/query-keys";
import type { CurrentSession } from "@/lib/api/models";

type SessionStatus = "loading" | "authenticated" | "unauthenticated" | "error";

interface SessionContextValue {
  status: SessionStatus;
  session: CurrentSession | null;
  error: unknown;
  signIn: (request: LoginRequest) => Promise<void>;
  register: (request: RegisterRequest) => Promise<void>;
  signOut: () => Promise<void>;
  refreshWorkspaceSession: () => Promise<void>;
  retry: () => Promise<void>;
}

const SessionContext = createContext<SessionContextValue | null>(null);

async function loadSession(): Promise<CurrentSession | null> {
  try {
    return await authApi.current();
  } catch (error) {
    if (error instanceof ApiClientError && error.status === 401) {
      return null;
    }

    throw error;
  }
}

export function SessionProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const sessionQuery = useQuery({
    queryKey: queryKeys.session,
    queryFn: loadSession,
    retry: false,
    refetchOnWindowFocus: false,
  });
  const refetchSession = sessionQuery.refetch;

  const clearWorkspaceData = useCallback(async () => {
    await queryClient.cancelQueries();
    queryClient.removeQueries({
      predicate: (query) => query.queryKey[0] !== queryKeys.session[0],
    });
  }, [queryClient]);

  const establishSession = useCallback(async () => {
    await clearWorkspaceData();
    await queryClient.fetchQuery({
      queryKey: queryKeys.session,
      queryFn: loadSession,
      staleTime: 0,
    });
  }, [clearWorkspaceData, queryClient]);

  const signIn = useCallback(
    async (request: LoginRequest) => {
      await authApi.login(request);
      await establishSession();
    },
    [establishSession],
  );

  const register = useCallback(
    async (request: RegisterRequest) => {
      await authApi.register(request);
      await establishSession();
    },
    [establishSession],
  );

  const signOut = useCallback(async () => {
    await authApi.logout();
    await queryClient.cancelQueries();
    queryClient.removeQueries({
      predicate: (query) => query.queryKey[0] !== queryKeys.session[0],
    });
    queryClient.setQueryData(queryKeys.session, null);
  }, [queryClient]);

  const refreshWorkspaceSession = useCallback(async () => {
    await establishSession();
  }, [establishSession]);

  const retry = useCallback(async () => {
    await refetchSession();
  }, [refetchSession]);

  const status: SessionStatus = sessionQuery.isPending
    ? "loading"
    : sessionQuery.isError
      ? "error"
      : sessionQuery.data
        ? "authenticated"
        : "unauthenticated";

  const value = useMemo<SessionContextValue>(
    () => ({
      status,
      session: sessionQuery.data ?? null,
      error: sessionQuery.error,
      signIn,
      register,
      signOut,
      refreshWorkspaceSession,
      retry,
    }),
    [
      sessionQuery.error,
      register,
      refreshWorkspaceSession,
      retry,
      sessionQuery.data,
      signIn,
      signOut,
      status,
    ],
  );

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

export function useSession(): SessionContextValue {
  const value = useContext(SessionContext);
  if (!value) {
    throw new Error("useSession must be used within SessionProvider.");
  }

  return value;
}

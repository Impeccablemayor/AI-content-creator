"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { analyticsApi, type AnalyticsFilters } from "@/lib/api/analytics";
import { audienceApi, brandApi, pillarsApi, strategyApi } from "@/lib/api/profiles";
import { ideasApi, postsApi } from "@/lib/api/content";
import { learningApi } from "@/lib/api/learning";
import { queryKeys } from "@/lib/api/query-keys";
import { socialApi } from "@/lib/api/social";
import { workspaceApi } from "@/lib/api/workspaces";
import type {
  ContentIdeaStatus,
  ContentPlatform,
  ContentPostStatus,
  CreateContentPillarRequest,
  GenerateContentIdeasRequest,
  GenerateContentPostRequest,
  PublishNowResponse,
  ScheduleContentPostRequest,
  UpdateAudienceProfileRequest,
  UpdateBrandProfileRequest,
  UpdateContentPillarRequest,
  UpdateContentPostRequest,
  UpdateContentStrategyRequest,
  Workspace,
} from "@/lib/api/models";

function invalidatePrefix(
  queryClient: ReturnType<typeof useQueryClient>,
  prefix: readonly unknown[],
) {
  return queryClient.invalidateQueries({ queryKey: prefix });
}

export function useCurrentWorkspace(workspaceId: string) {
  return useQuery({
    queryKey: queryKeys.currentWorkspace(workspaceId),
    queryFn: workspaceApi.current,
    enabled: Boolean(workspaceId),
  });
}

export function useUpdateCurrentWorkspace(workspaceId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: workspaceApi.updateCurrent,
    onSuccess: async (workspace: Workspace) => {
      queryClient.setQueryData(queryKeys.currentWorkspace(workspaceId), workspace);
      await invalidatePrefix(queryClient, queryKeys.workspaces(workspaceId));
    },
  });
}

export function useBrandProfile(workspaceId: string) {
  return useQuery({
    queryKey: queryKeys.brand(workspaceId),
    queryFn: brandApi.get,
    enabled: Boolean(workspaceId),
  });
}

export function useUpdateBrandProfile(workspaceId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateBrandProfileRequest) => brandApi.update(request),
    onSuccess: (profile) => queryClient.setQueryData(queryKeys.brand(workspaceId), profile),
  });
}

export function useAudienceProfile(workspaceId: string) {
  return useQuery({
    queryKey: queryKeys.audience(workspaceId),
    queryFn: audienceApi.get,
    enabled: Boolean(workspaceId),
  });
}

export function useUpdateAudienceProfile(workspaceId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateAudienceProfileRequest) => audienceApi.update(request),
    onSuccess: (profile) =>
      queryClient.setQueryData(queryKeys.audience(workspaceId), profile),
  });
}

export function useContentStrategy(workspaceId: string) {
  return useQuery({
    queryKey: queryKeys.strategy(workspaceId),
    queryFn: strategyApi.get,
    enabled: Boolean(workspaceId),
  });
}

export function useUpdateContentStrategy(workspaceId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateContentStrategyRequest) => strategyApi.update(request),
    onSuccess: (strategy) =>
      queryClient.setQueryData(queryKeys.strategy(workspaceId), strategy),
  });
}

export function useContentPillars(workspaceId: string, isActive?: boolean) {
  return useQuery({
    queryKey: [...queryKeys.pillars(workspaceId), { isActive }],
    queryFn: () => pillarsApi.list(isActive),
    enabled: Boolean(workspaceId),
  });
}

export function useCreateContentPillar(workspaceId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateContentPillarRequest) => pillarsApi.create(request),
    onSuccess: () => invalidatePrefix(queryClient, queryKeys.pillars(workspaceId)),
  });
}

export function useUpdateContentPillar(workspaceId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateContentPillarRequest) => pillarsApi.update(id, request),
    onSuccess: () => invalidatePrefix(queryClient, queryKeys.pillars(workspaceId)),
  });
}

export function useDeleteContentPillar(workspaceId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => pillarsApi.remove(id),
    onSuccess: () => invalidatePrefix(queryClient, queryKeys.pillars(workspaceId)),
  });
}

export function useContentIdeas(
  workspaceId: string,
  filters: {
    status?: ContentIdeaStatus;
    platform?: ContentPlatform;
    pillarId?: string;
  } = {},
) {
  return useQuery({
    queryKey: [...queryKeys.ideas(workspaceId), filters],
    queryFn: () => ideasApi.list(filters),
    enabled: Boolean(workspaceId),
  });
}

export function useGenerateContentIdeas(workspaceId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: GenerateContentIdeasRequest) => ideasApi.generate(request),
    onSuccess: () => invalidatePrefix(queryClient, queryKeys.ideas(workspaceId)),
  });
}

export function useApproveContentIdea(workspaceId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => ideasApi.approve(id),
    onSuccess: () => invalidatePrefix(queryClient, queryKeys.ideas(workspaceId)),
  });
}

export function useRejectContentIdea(workspaceId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => ideasApi.reject(id),
    onSuccess: () => invalidatePrefix(queryClient, queryKeys.ideas(workspaceId)),
  });
}

export function useGeneratePostFromIdea(workspaceId: string, ideaId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: GenerateContentPostRequest) =>
      ideasApi.generatePost(ideaId, request),
    onSuccess: async () => {
      await Promise.all([
        invalidatePrefix(queryClient, queryKeys.ideas(workspaceId)),
        invalidatePrefix(queryClient, queryKeys.posts(workspaceId)),
      ]);
    },
  });
}

export function useContentPosts(
  workspaceId: string,
  filters: { status?: ContentPostStatus; platform?: ContentPlatform } = {},
  refetchInterval?: number | false,
) {
  return useQuery({
    queryKey: [...queryKeys.posts(workspaceId), filters],
    queryFn: () => postsApi.list(filters),
    enabled: Boolean(workspaceId),
    refetchInterval,
  });
}

export function useContentPost(workspaceId: string, id: string) {
  return useQuery({
    queryKey: [...queryKeys.posts(workspaceId), id],
    queryFn: () => postsApi.get(id),
    enabled: Boolean(workspaceId) && Boolean(id),
  });
}

export function useUpdateContentPost(workspaceId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateContentPostRequest) => postsApi.update(id, request),
    onSuccess: async (post) => {
      queryClient.setQueryData([...queryKeys.posts(workspaceId), id], post);
      await invalidatePrefix(queryClient, queryKeys.posts(workspaceId));
    },
  });
}

function usePostTransition(
  workspaceId: string,
  action: (id: string) => Promise<unknown>,
) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: action,
    onSuccess: async () => {
      await invalidatePrefix(queryClient, queryKeys.posts(workspaceId));
    },
  });
}

export function useRegenerateContentPost(workspaceId: string) {
  return usePostTransition(workspaceId, postsApi.regenerate);
}

export function useApproveContentPost(workspaceId: string) {
  return usePostTransition(workspaceId, postsApi.approve);
}

export function useRejectContentPost(workspaceId: string) {
  return usePostTransition(workspaceId, postsApi.reject);
}

export function useScheduleContentPost(workspaceId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ScheduleContentPostRequest) => postsApi.schedule(id, request),
    onSuccess: async (post) => {
      queryClient.setQueryData([...queryKeys.posts(workspaceId), id], post);
      await invalidatePrefix(queryClient, queryKeys.posts(workspaceId));
    },
  });
}

export function usePublishContentPostNow(workspaceId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => postsApi.publishNow(id),
    onSuccess: (result: PublishNowResponse) => {
      queryClient.setQueryData([...queryKeys.posts(workspaceId), id], result.post);
    },
    onSettled: () => invalidatePrefix(queryClient, queryKeys.posts(workspaceId)),
  });
}

export function useCancelContentPostSchedule(workspaceId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => postsApi.cancelSchedule(id),
    onSuccess: (post) => {
      queryClient.setQueryData([...queryKeys.posts(workspaceId), id], post);
    },
    onSettled: () => invalidatePrefix(queryClient, queryKeys.posts(workspaceId)),
  });
}

export function useEvaluateContentPost() {
  return useMutation({ mutationFn: postsApi.evaluate });
}

export function useAnalyticsOverview(
  workspaceId: string,
  filters: AnalyticsFilters = {},
) {
  return useQuery({
    queryKey: [...queryKeys.analytics(workspaceId), "overview", filters],
    queryFn: () => analyticsApi.overview(filters),
    enabled: Boolean(workspaceId),
  });
}

export function usePostAnalyticsList(
  workspaceId: string,
  filters: AnalyticsFilters = {},
) {
  return useQuery({
    queryKey: [...queryKeys.analytics(workspaceId), "posts", filters],
    queryFn: () => analyticsApi.posts(filters),
    enabled: Boolean(workspaceId),
  });
}

export function usePostAnalytics(workspaceId: string, id: string) {
  return useQuery({
    queryKey: [...queryKeys.analytics(workspaceId), id],
    queryFn: () => analyticsApi.post(id),
    enabled: Boolean(workspaceId) && Boolean(id),
  });
}

export function useLearningInsights(workspaceId: string, limit?: number) {
  return useQuery({
    queryKey: [...queryKeys.learning(workspaceId), { limit }],
    queryFn: () => learningApi.insights(limit),
    enabled: Boolean(workspaceId),
  });
}

export function useGenerateLearningInsights(workspaceId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: learningApi.generate,
    onSuccess: () => invalidatePrefix(queryClient, queryKeys.learning(workspaceId)),
  });
}

export function useSocialAccounts(workspaceId: string) {
  return useQuery({
    queryKey: queryKeys.socialAccounts(workspaceId),
    queryFn: socialApi.accounts,
    enabled: Boolean(workspaceId),
  });
}

export function useConnectSocialAccount(workspaceId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: socialApi.connect,
    onSuccess: () => invalidatePrefix(queryClient, queryKeys.socialAccounts(workspaceId)),
  });
}

export function useDisconnectSocialAccount(workspaceId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => socialApi.disconnect(id),
    onSuccess: () => invalidatePrefix(queryClient, queryKeys.socialAccounts(workspaceId)),
  });
}

import { apiRequest, jsonRequest, withQuery } from "./client";
import type {
  ContentEvaluation,
  ContentIdea,
  ContentIdeaStatus,
  ContentPlatform,
  ContentPost,
  ContentPostStatus,
  GenerateContentIdeasRequest,
  GenerateContentPostRequest,
  ScheduleContentPostRequest,
  UpdateContentPostRequest,
} from "./models";

export const ideasApi = {
  list: (filters: {
    status?: ContentIdeaStatus;
    platform?: ContentPlatform;
    pillarId?: string;
  } = {}) =>
    apiRequest<ContentIdea[]>(withQuery("/content-ideas", filters)),
  generate: (request: GenerateContentIdeasRequest) =>
    apiRequest<ContentIdea[]>("/content-ideas/generate", jsonRequest("POST", request)),
  approve: (id: string) =>
    apiRequest<ContentIdea>(`/content-ideas/${id}/approve`, jsonRequest("POST")),
  reject: (id: string) =>
    apiRequest<ContentIdea>(`/content-ideas/${id}/reject`, jsonRequest("POST")),
  generatePost: (id: string, request: GenerateContentPostRequest) =>
    apiRequest<ContentPost>(
      `/content-ideas/${id}/generate-post`,
      jsonRequest("POST", request),
    ),
};

export const postsApi = {
  list: (filters: { status?: ContentPostStatus; platform?: ContentPlatform } = {}) =>
    apiRequest<ContentPost[]>(withQuery("/content-posts", filters)),
  get: (id: string) => apiRequest<ContentPost>(`/content-posts/${id}`),
  update: (id: string, request: UpdateContentPostRequest) =>
    apiRequest<ContentPost>(`/content-posts/${id}`, jsonRequest("PATCH", request)),
  regenerate: (id: string) =>
    apiRequest<ContentPost>(`/content-posts/${id}/regenerate`, jsonRequest("POST")),
  approve: (id: string) =>
    apiRequest<ContentPost>(`/content-posts/${id}/approve`, jsonRequest("POST")),
  reject: (id: string) =>
    apiRequest<ContentPost>(`/content-posts/${id}/reject`, jsonRequest("POST")),
  schedule: (id: string, request: ScheduleContentPostRequest) =>
    apiRequest<ContentPost>(`/content-posts/${id}/schedule`, jsonRequest("POST", request)),
  evaluate: (id: string) =>
    apiRequest<ContentEvaluation>(`/content-posts/${id}/evaluate`, jsonRequest("POST")),
};

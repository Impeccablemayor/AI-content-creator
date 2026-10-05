import { apiRequest, jsonRequest, withQuery } from "./client";
import type {
  AudienceProfile,
  BrandProfile,
  ContentPillar,
  ContentStrategy,
  CreateContentPillarRequest,
  UpdateAudienceProfileRequest,
  UpdateBrandProfileRequest,
  UpdateContentPillarRequest,
  UpdateContentStrategyRequest,
} from "./models";

export const brandApi = {
  get: () => apiRequest<BrandProfile>("/brand-profile"),
  update: (request: UpdateBrandProfileRequest) =>
    apiRequest<BrandProfile>("/brand-profile", jsonRequest("PUT", request)),
};

export const audienceApi = {
  get: () => apiRequest<AudienceProfile>("/audience-profile"),
  update: (request: UpdateAudienceProfileRequest) =>
    apiRequest<AudienceProfile>("/audience-profile", jsonRequest("PUT", request)),
};

export const strategyApi = {
  get: () => apiRequest<ContentStrategy>("/content-strategy"),
  update: (request: UpdateContentStrategyRequest) =>
    apiRequest<ContentStrategy>("/content-strategy", jsonRequest("PUT", request)),
};

export const pillarsApi = {
  list: (isActive?: boolean) =>
    apiRequest<ContentPillar[]>(withQuery("/strategy/pillars", { isActive })),
  get: (id: string) => apiRequest<ContentPillar>(`/strategy/pillars/${id}`),
  create: (request: CreateContentPillarRequest) =>
    apiRequest<ContentPillar>("/strategy/pillars", jsonRequest("POST", request)),
  update: (id: string, request: UpdateContentPillarRequest) =>
    apiRequest<ContentPillar>(`/strategy/pillars/${id}`, jsonRequest("PATCH", request)),
  remove: (id: string) =>
    apiRequest<void>(`/strategy/pillars/${id}`, { method: "DELETE" }),
};

import { apiRequest, withQuery } from "./client";
import type { AnalyticsOverview, ContentPlatform, PostAnalytics } from "./models";

export interface AnalyticsFilters {
  platform?: ContentPlatform;
  from?: string;
  to?: string;
  pillarId?: string;
}

export const analyticsApi = {
  overview: (filters: AnalyticsFilters = {}) =>
    apiRequest<AnalyticsOverview>(withQuery("/analytics/overview", { ...filters })),
  posts: (filters: AnalyticsFilters = {}) =>
    apiRequest<PostAnalytics[]>(withQuery("/analytics/posts", { ...filters })),
  post: (id: string) => apiRequest<PostAnalytics>(`/analytics/posts/${id}`),
};

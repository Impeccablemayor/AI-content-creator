import { apiRequest, jsonRequest } from "./client";
import type { LearningInsight, LearningInsightsRun } from "./models";

export const learningApi = {
  insights: (limit?: number) =>
    apiRequest<LearningInsight[]>(
      limit === undefined ? "/learning/insights" : `/learning/insights?limit=${limit}`,
    ),
  generate: () =>
    apiRequest<LearningInsightsRun>("/learning/insights", jsonRequest("POST")),
};

import { apiRequest, jsonRequest } from "./client";
import type { AuthResponse, Workspace, WorkspaceMembership } from "./models";

export const workspaceApi = {
  list: () => apiRequest<WorkspaceMembership[]>("/workspaces"),
  current: () => apiRequest<Workspace>("/workspaces/current"),
  create: (name: string) =>
    apiRequest<AuthResponse>("/workspaces", jsonRequest("POST", { name })),
  switch: (workspaceId: string) =>
    apiRequest<AuthResponse>(`/workspaces/${workspaceId}/switch`, jsonRequest("POST")),
  updateCurrent: (name: string) =>
    apiRequest<Workspace>("/workspaces/current", jsonRequest("PATCH", { name })),
};

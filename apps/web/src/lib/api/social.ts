import { apiRequest } from "./client";
import type { ContentPlatform, SocialAccount, SocialConnectResponse } from "./models";

export const socialApi = {
  accounts: () => apiRequest<SocialAccount[]>("/social/accounts"),
  connect: (platform: ContentPlatform) =>
    apiRequest<SocialConnectResponse>(`/social/${platform}/connect`),
  disconnect: (id: string) =>
    apiRequest<SocialAccount>(`/social/accounts/${id}`, { method: "DELETE" }),
};

export const queryKeys = {
  session: ["session"] as const,
  workspaces: (workspaceId: string) => ["workspaces", workspaceId] as const,
  currentWorkspace: (workspaceId: string) => ["workspace-current", workspaceId] as const,
  brand: (workspaceId: string) => ["brand", workspaceId] as const,
  audience: (workspaceId: string) => ["audience", workspaceId] as const,
  strategy: (workspaceId: string) => ["strategy", workspaceId] as const,
  pillars: (workspaceId: string) => ["pillars", workspaceId] as const,
  ideas: (workspaceId: string) => ["ideas", workspaceId] as const,
  posts: (workspaceId: string) => ["posts", workspaceId] as const,
  analytics: (workspaceId: string) => ["analytics", workspaceId] as const,
  learning: (workspaceId: string) => ["learning", workspaceId] as const,
  socialAccounts: (workspaceId: string) => ["social-accounts", workspaceId] as const,
};

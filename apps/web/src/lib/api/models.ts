export interface ApiEnvelope<T> {
  data: T;
  message: string;
}

export interface ApiErrorBody {
  error: {
    code: string;
    message: string;
    details?: unknown;
  };
  traceId: string;
}

export interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
}

export interface AuthResponse {
  expiresAt: string;
  user: User;
  workspaceId: string;
}

export interface CurrentSession {
  user: User;
  workspaceId: string;
  role: string;
}

export interface WorkspaceMembership {
  id: string;
  name: string;
  status: string;
  role: string;
  isCurrent: boolean;
}

export interface Workspace {
  id: string;
  name: string;
  status: string;
  role: string;
}

export type ContentPlatform = "LinkedIn" | "X";
export type ContentIdeaStatus = "Suggested" | "Approved" | "Rejected" | "Used" | "Archived";
export type ContentPostStatus = "Draft" | "Approved" | "Scheduled" | "Published" | "Rejected";
export type ContentType =
  | "Educational"
  | "Story"
  | "Opinion"
  | "HowTo"
  | "CaseStudy"
  | "Insight"
  | "Question";

export interface BrandProfile {
  id: string;
  workspaceId: string;
  name: string;
  description: string;
  industry: string;
  toneOfVoice: string;
  contentGoals: string;
}

export interface UpdateBrandProfileRequest {
  name: string;
  description: string;
  industry: string;
  toneOfVoice: string;
  contentGoals: string;
}

export interface AudienceProfile {
  id: string;
  workspaceId: string;
  description: string;
  demographics: string;
  interests: string;
  painPoints: string;
  goals: string;
}

export interface UpdateAudienceProfileRequest {
  description: string;
  demographics: string;
  interests: string;
  painPoints: string;
  goals: string;
}

export interface ContentStrategy {
  id: string;
  workspaceId: string;
  postsPerWeek: number;
  contentPillars: string;
  contentObjectives: string;
  preferredFormats: string;
  storytellingStyle: string;
  hashtagStrategy: string;
  topicsToPrioritize: string;
  topicsToAvoid: string;
  preferredPostingTimes: string;
}

export type UpdateContentStrategyRequest = Omit<ContentStrategy, "id" | "workspaceId">;

export interface ContentPillar {
  id: string;
  name: string;
  description: string;
  objective: string | null;
  audienceProfileId: string | null;
  weight: number;
  isActive: boolean;
  ideaCount: number;
  createdAt: string;
  updatedAt: string;
}

export type CreateContentPillarRequest = Partial<
  Pick<
    ContentPillar,
    "name" | "description" | "objective" | "audienceProfileId" | "weight" | "isActive"
  >
>;

export type UpdateContentPillarRequest = CreateContentPillarRequest;

export interface ContentIdea {
  id: string;
  status: ContentIdeaStatus;
  platform: ContentPlatform;
  contentPillarId: string | null;
  title: string;
  topic: string;
  angle: string;
  hook: string;
  reason: string;
  suggestedHashtags: string;
  relevanceScore: number;
  engagementPotentialScore: number;
  brandFitScore: number;
  overallScore: number;
  suggestedPublishAt: string | null;
  createdAt: string;
}

export interface GenerateContentIdeasRequest {
  platform: ContentPlatform;
  count: number;
  topicHint?: string;
}

export interface ContentPost {
  id: string;
  contentIdeaId: string;
  platform: ContentPlatform;
  status: ContentPostStatus;
  content: string;
  hook: string;
  callToAction: string;
  hashtags: string[];
  contentType: ContentType;
  storytellingStructure: string;
  scheduledAt: string | null;
  publishedAt: string | null;
  publishFailureReason: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface PublishNowResponse {
  post: ContentPost;
  outcome: "Published" | "Failed" | "Skipped";
  failureReason: string | null;
  externalPostId: string | null;
}

export interface GenerateContentPostRequest {
  platform: ContentPlatform;
}

export interface UpdateContentPostRequest {
  content: string;
  hook: string;
  callToAction: string;
  hashtags: string[];
  contentType: ContentType;
  storytellingStructure: string;
}

export interface ScheduleContentPostRequest {
  scheduledAt: string;
}

export interface ContentEvaluation {
  contentPostId: string;
  overallScore: number;
  recommendation: string;
  passed: boolean;
  passThreshold: number;
  dimensions: {
    strategicFit: number;
    audienceFit: number;
    hookStrength: number;
    value: number;
    clarity: number;
    originality: number;
    storytelling: number;
    platformFit: number;
    voiceMatch: number;
    factualReliability: number;
  };
  strengths: string[];
  issues: string[];
  repetition: {
    isRepetitive: boolean;
    similarity: number;
    similarToContentPostId: string | null;
    threshold: number;
  };
}

export interface AnalyticsOverview {
  period: {
    from: string | null;
    to: string | null;
  };
  postsPublished: number;
  impressions: number | null;
  likes: number | null;
  comments: number | null;
  shares: number | null;
  clicks: number | null;
  profileVisits: number | null;
  followersGained: number | null;
  engagementRate: number | null;
}

export interface PostAnalytics {
  contentPostId: string;
  platform: ContentPlatform;
  contentPillarId: string | null;
  contentPillarName: string | null;
  publishedAt: string | null;
  recordedAt: string | null;
  impressions: number | null;
  likes: number | null;
  comments: number | null;
  shares: number | null;
  clicks: number | null;
  profileVisits: number | null;
  followersGained: number | null;
  engagementRate: number | null;
}

export interface LearningInsight {
  id: string;
  type: string;
  dimension: string;
  observation: string;
  recommendation: string | null;
  evidenceCount: number;
  confidence: number | null;
  createdAt: string;
}

export interface LearningInsightsRun {
  from: string;
  to: string;
  postsInWindow: number;
  measuredPosts: number;
  minimumMeasuredPosts: number;
  hasSufficientData: boolean;
  insights: LearningInsight[];
}

export interface SocialAccount {
  id: string;
  platform: string;
  username: string | null;
  displayName: string | null;
  profileUrl: string | null;
  status: string;
  tokenExpiresAt: string | null;
  connectedAt: string;
  updatedAt: string;
}

export interface SocialConnectResponse {
  platform: string;
  authorizationUrl: string;
  expiresAt: string;
}

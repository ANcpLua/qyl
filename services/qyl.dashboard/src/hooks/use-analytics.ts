import {useQuery} from '@tanstack/react-query';
import {fetchJson} from '@/lib/api';

// =============================================================================
// Domain Interfaces (match AnalyticsQueryService C# DTOs, camelCase from .NET)
// =============================================================================

export interface ConversationSummary {
    conversationId: string;
    startTime: string;
    durationMs: number;
    turnCount: number;
    errorCount: number;
    hasErrors: boolean;
    totalInputTokens: number;
    totalOutputTokens: number;
    userId: string | null;
    firstQuestion: string | null;
}

export interface ConversationListResult {
    conversations: ConversationSummary[];
    total: number;
    page: number;
    pageSize: number;
}

export interface ConversationTurn {
    spanId: string;
    name: string;
    timestamp: string;
    durationMs: number;
    statusCode: number;
    statusMessage: string | null;
    provider: string | null;
    model: string | null;
    inputTokens: number;
    outputTokens: number;
    toolName: string | null;
    stopReason: string | null;
    operationName: string | null;
    userId: string | null;
    dataSourceId: string | null;
}

export interface ConversationDetail {
    conversationId: string;
    turns: ConversationTurn[];
}

export interface CoverageGap {
    topic: string;
    conversationCount: number;
    finding: string | null;
    recommendation: string | null;
    sampleConversationIds: string[];
}

export interface CoverageGapsResult {
    conversationsProcessed: number;
    gapsIdentified: number;
    gaps: CoverageGap[];
}

export interface TopQuestionCluster {
    topic: string;
    conversationCount: number;
    sampleConversationIds: string[];
}

export interface TopQuestionsResult {
    conversationsProcessed: number;
    clustersIdentified: number;
    clusters: TopQuestionCluster[];
}

export interface SourceUsage {
    sourceId: string;
    citationCount: number;
    topQuestions: string[];
}

export interface SourceAnalyticsResult {
    sources: SourceUsage[];
}

export interface SatisfactionByModel {
    model: string;
    rate: number;
    downvotes: number;
}

export interface SatisfactionByTopic {
    topic: string;
    rate: number;
    downvotes: number;
}

export interface SatisfactionResult {
    totalFeedback: number;
    upvotes: number;
    downvotes: number;
    satisfactionRate: number;
    byModel: SatisfactionByModel[];
    byTopic: SatisfactionByTopic[];
}

export interface UserSummary {
    userId: string;
    conversationCount: number;
    firstSeen: string;
    lastSeen: string;
    topTopics: string[];
}

export interface UserListResult {
    users: UserSummary[];
    total: number;
}

export interface UserConversation {
    conversationId: string;
    date: string;
    topic: string | null;
    turnCount: number;
    satisfied: boolean;
}

export interface UserJourneyResult {
    userId: string;
    conversations: UserConversation[];
    totalTokens: number;
    frequentTopics: string[];
    retentionDays: number;
}

// =============================================================================
// Fetch helper
// =============================================================================


// =============================================================================
// Query key factory
// =============================================================================

export const analyticsKeys = {
    all: ['analytics'] as const,
    conversations: (p: string, o: number, page: number, hasErrors?: boolean, userId?: string, model?: string) =>
        [...analyticsKeys.all, 'conversations', p, o, page, hasErrors, userId, model] as const,
    conversation: (id: string) =>
        [...analyticsKeys.all, 'conversation', id] as const,
    coverageGaps: (p: string, o: number) =>
        [...analyticsKeys.all, 'coverage-gaps', p, o] as const,
    topQuestions: (p: string, o: number, min: number) =>
        [...analyticsKeys.all, 'top-questions', p, o, min] as const,
    sourceAnalytics: (p: string, o: number) =>
        [...analyticsKeys.all, 'source-analytics', p, o] as const,
    satisfaction: (p: string, o: number) =>
        [...analyticsKeys.all, 'satisfaction', p, o] as const,
    users: (p: string, o: number, page: number) =>
        [...analyticsKeys.all, 'users', p, o, page] as const,
    userJourney: (userId: string) =>
        [...analyticsKeys.all, 'user-journey', userId] as const,
};

// =============================================================================
// Hooks
// =============================================================================

export function useConversations(
    period: string,
    offset: number,
    page: number,
    hasErrors?: boolean,
    userId?: string,
    model?: string,
) {
    return useQuery({
        queryKey: analyticsKeys.conversations(period, offset, page, hasErrors, userId, model),
        queryFn: () => {
            const p = new URLSearchParams({period, offset: String(offset), page: String(page), pageSize: '20'});
            if (hasErrors != null) p.set('hasErrors', String(hasErrors));
            if (userId) p.set('userId', userId);
            if (model) p.set('model', model);
            return fetchJson<ConversationListResult>(`/api/v1/analytics/conversations?${p}`);
        },
        staleTime: 60_000,
    });
}

export function useConversationDetail(conversationId: string) {
    return useQuery({
        queryKey: analyticsKeys.conversation(conversationId),
        queryFn: () =>
            fetchJson<ConversationDetail>(`/api/v1/analytics/conversations/${encodeURIComponent(conversationId)}`),
        enabled: !!conversationId,
        staleTime: 60_000,
    });
}

export function useCoverageGaps(period: string, offset: number) {
    return useQuery({
        queryKey: analyticsKeys.coverageGaps(period, offset),
        queryFn: () =>
            fetchJson<CoverageGapsResult>(`/api/v1/analytics/coverage-gaps?period=${encodeURIComponent(period)}&offset=${offset}`),
        staleTime: 60_000,
    });
}

export function useTopQuestions(period: string, offset: number, minConversations = 3) {
    return useQuery({
        queryKey: analyticsKeys.topQuestions(period, offset, minConversations),
        queryFn: () =>
            fetchJson<TopQuestionsResult>(`/api/v1/analytics/top-questions?period=${encodeURIComponent(period)}&offset=${offset}&minConversations=${minConversations}`),
        staleTime: 60_000,
    });
}

export function useSourceAnalytics(period: string, offset: number) {
    return useQuery({
        queryKey: analyticsKeys.sourceAnalytics(period, offset),
        queryFn: () =>
            fetchJson<SourceAnalyticsResult>(`/api/v1/analytics/source-analytics?period=${encodeURIComponent(period)}&offset=${offset}`),
        staleTime: 60_000,
    });
}

export function useSatisfaction(period: string, offset: number) {
    return useQuery({
        queryKey: analyticsKeys.satisfaction(period, offset),
        queryFn: () =>
            fetchJson<SatisfactionResult>(`/api/v1/analytics/satisfaction?period=${encodeURIComponent(period)}&offset=${offset}`),
        staleTime: 60_000,
    });
}

export function useUsers(period: string, offset: number, page: number) {
    return useQuery({
        queryKey: analyticsKeys.users(period, offset, page),
        queryFn: () => {
            const p = new URLSearchParams({period, offset: String(offset), page: String(page), pageSize: '20'});
            return fetchJson<UserListResult>(`/api/v1/analytics/users?${p}`);
        },
        staleTime: 60_000,
    });
}

export function useUserJourney(userId: string) {
    return useQuery({
        queryKey: analyticsKeys.userJourney(userId),
        queryFn: () =>
            fetchJson<UserJourneyResult>(`/api/v1/analytics/users/${encodeURIComponent(userId)}/journey`),
        enabled: !!userId,
        staleTime: 60_000,
    });
}

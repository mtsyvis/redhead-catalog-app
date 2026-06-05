import type { CurrentUserProfileLimits } from '../types/auth.types';

export const EXPORT_USAGE_LIMIT_REASONS = {
  dailyUniqueDomainLimitReached: 'DailyUniqueDomainLimitReached',
  weeklyUniqueDomainLimitReached: 'WeeklyUniqueDomainLimitReached',
  dailyExportOperationLimitReached: 'DailyExportOperationLimitReached',
  weeklyExportOperationLimitReached: 'WeeklyExportOperationLimitReached',
} as const;

const USAGE_LIMIT_REASON_MESSAGES: Record<string, string> = {
  [EXPORT_USAGE_LIMIT_REASONS.dailyUniqueDomainLimitReached]:
    'Daily export limit reached. You can export more domains later or contact Redhead to increase your limit.',
  [EXPORT_USAGE_LIMIT_REASONS.weeklyUniqueDomainLimitReached]:
    'Weekly export limit reached. You can export more domains later or contact Redhead to increase your limit.',
  [EXPORT_USAGE_LIMIT_REASONS.dailyExportOperationLimitReached]:
    'Export limit reached. You can export more later or contact Redhead to increase your limit.',
  [EXPORT_USAGE_LIMIT_REASONS.weeklyExportOperationLimitReached]:
    'Export limit reached. You can export more later or contact Redhead to increase your limit.',
};

export function getExportUsageLimitMessage(reason: string | null | undefined): string | null {
  if (!reason) return null;
  return USAGE_LIMIT_REASON_MESSAGES[reason] ?? null;
}

export function isExportUsageLimitReason(reason: string | null | undefined): boolean {
  return Boolean(getExportUsageLimitMessage(reason));
}

export function formatUsageLimitPair(
  used: number | null | undefined,
  limit: number | null | undefined
): string | null {
  if (used == null || limit == null) return null;
  return `${used.toLocaleString()} / ${limit.toLocaleString()}`;
}

export function hasClientExportUsage(limits: CurrentUserProfileLimits | null | undefined): boolean {
  if (!limits) return false;

  return [
    limits.dailyUniqueExportedDomainsLimit,
    limits.weeklyUniqueExportedDomainsLimit,
    limits.dailyExportOperationsLimit,
    limits.weeklyExportOperationsLimit,
  ].some((value) => value != null);
}

export function getExportUsageLimitUsageLine(
  reason: string | null | undefined,
  limits: CurrentUserProfileLimits | null | undefined
): string | null {
  if (!limits) return null;

  if (reason === EXPORT_USAGE_LIMIT_REASONS.dailyUniqueDomainLimitReached) {
    const usage = formatUsageLimitPair(
      limits.dailyUniqueExportedDomainsUsed,
      limits.dailyUniqueExportedDomainsLimit
    );
    return usage ? `Daily usage: ${usage}.` : null;
  }

  if (reason === EXPORT_USAGE_LIMIT_REASONS.weeklyUniqueDomainLimitReached) {
    const usage = formatUsageLimitPair(
      limits.weeklyUniqueExportedDomainsUsed,
      limits.weeklyUniqueExportedDomainsLimit
    );
    return usage ? `Weekly usage: ${usage}.` : null;
  }

  if (reason === EXPORT_USAGE_LIMIT_REASONS.dailyExportOperationLimitReached) {
    const usage = formatUsageLimitPair(
      limits.dailyExportOperationsUsed,
      limits.dailyExportOperationsLimit
    );
    return usage ? `Daily exports: ${usage}.` : null;
  }

  if (reason === EXPORT_USAGE_LIMIT_REASONS.weeklyExportOperationLimitReached) {
    const usage = formatUsageLimitPair(
      limits.weeklyExportOperationsUsed,
      limits.weeklyExportOperationsLimit
    );
    return usage ? `Weekly exports: ${usage}.` : null;
  }

  return null;
}

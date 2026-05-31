export const STOP_LIST_MAX_DOMAINS = 50000;
export const STOP_LIST_STORAGE_KEY = 'redhead.sitesCatalog.stopListDomains';

const TOKEN_SEPARATOR = /[\s,;]+/;

export interface StopListParseResult {
  domains: string[];
  invalidValues: string[];
  totalEntries: number;
  duplicateCount: number;
}

export function normalizeDomainInput(input: string | null | undefined): string {
  if (input == null || input.trim() === '') {
    return '';
  }

  let result = input.trim();

  if (result.toLowerCase().startsWith('http://')) {
    result = result.substring(7);
  } else if (result.toLowerCase().startsWith('https://')) {
    result = result.substring(8);
  }

  const slashIndex = result.indexOf('/');
  if (slashIndex >= 0) {
    result = result.substring(0, slashIndex);
  }

  const questionIndex = result.indexOf('?');
  if (questionIndex >= 0) {
    result = result.substring(0, questionIndex);
  }

  const hashIndex = result.indexOf('#');
  if (hashIndex >= 0) {
    result = result.substring(0, hashIndex);
  }

  if (result.toLowerCase().startsWith('www.')) {
    result = result.substring(4);
  }

  return result.replace(/\/+$/, '').toLowerCase();
}

export function parseStopListInput(input: string): StopListParseResult {
  const tokens = input
    .split(TOKEN_SEPARATOR)
    .map((token) => token.trim())
    .filter((token) => token !== '');

  const domains = new Set<string>();
  const invalidValues: string[] = [];
  let validEntryCount = 0;

  for (const token of tokens) {
    const normalized = normalizeDomainInput(token);
    if (!isValidNormalizedDomain(normalized)) {
      invalidValues.push(token);
      continue;
    }

    validEntryCount += 1;
    domains.add(normalized);
  }

  return {
    domains: Array.from(domains).sort((a, b) => (a < b ? -1 : a > b ? 1 : 0)),
    invalidValues,
    totalEntries: tokens.length,
    duplicateCount: validEntryCount - domains.size,
  };
}

export function formatStopListInput(domains: string[]): string {
  return domains.join('\n');
}

export function loadStoredStopListDomains(): string[] {
  try {
    const raw = globalThis.localStorage?.getItem(STOP_LIST_STORAGE_KEY);
    if (!raw) {
      return [];
    }

    const parsed: unknown = JSON.parse(raw);
    if (!Array.isArray(parsed) || !parsed.every((value) => typeof value === 'string')) {
      globalThis.localStorage?.removeItem(STOP_LIST_STORAGE_KEY);
      return [];
    }

    const result = parseStopListInput(parsed.join('\n'));
    if (result.invalidValues.length > 0 || result.domains.length > STOP_LIST_MAX_DOMAINS) {
      globalThis.localStorage?.removeItem(STOP_LIST_STORAGE_KEY);
      return [];
    }

    return result.domains;
  } catch {
    globalThis.localStorage?.removeItem(STOP_LIST_STORAGE_KEY);
    return [];
  }
}

export function persistStopListDomains(domains: string[]): void {
  if (domains.length === 0) {
    globalThis.localStorage?.removeItem(STOP_LIST_STORAGE_KEY);
    return;
  }

  globalThis.localStorage?.setItem(STOP_LIST_STORAGE_KEY, JSON.stringify(domains));
}

function isValidNormalizedDomain(normalizedDomain: string): boolean {
  if (
    normalizedDomain.trim() === '' ||
    normalizedDomain.length > 253 ||
    !normalizedDomain.includes('.') ||
    /\s/.test(normalizedDomain) ||
    /[/?#:@]/.test(normalizedDomain)
  ) {
    return false;
  }

  return normalizedDomain.split('.').every(isValidDomainLabel);
}

function isValidDomainLabel(label: string): boolean {
  if (label.length === 0 || label.length > 63 || label.startsWith('-') || label.endsWith('-')) {
    return false;
  }

  return /^[\p{L}\p{N}\p{M}-]+$/u.test(label);
}

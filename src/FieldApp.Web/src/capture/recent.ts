import { readJson, readStorage, writeStorage } from '../lib/storage';

// Device-local "recently used" lists per user and project. The server has no capture history until items exist
// (later slices), so recency comes from this device's selections. Preference data only: losing it is harmless.

const maxRecent = 5;

export type RecentKind = 'trades' | 'locations';

function key(userId: string, projectId: string, kind: RecentKind): string {
  return `fieldapp.recent.v1.${userId}.${projectId}.${kind}`;
}

export function readRecent(userId: string, projectId: string, kind: RecentKind): string[] {
  const value = readJson<unknown>(key(userId, projectId, kind), []);
  return Array.isArray(value) ? value.filter((id): id is string => typeof id === 'string') : [];
}

/** Moves (or adds) an ID to the front of the list, keeping at most five. */
export function recordRecent(userId: string, projectId: string, kind: RecentKind, id: string): string[] {
  const next = [id, ...readRecent(userId, projectId, kind).filter((existing) => existing !== id)].slice(
    0,
    maxRecent,
  );
  writeStorage(key(userId, projectId, kind), JSON.stringify(next));
  return next;
}

/** Keeps recent IDs that still exist in the current selectable list, in recency order. */
export function pickRecent<T>(
  recentIds: string[],
  items: T[],
  idOf: (item: T) => string,
  limit: number,
): T[] {
  const byId = new Map(items.map((item) => [idOf(item), item]));
  return recentIds.flatMap((id) => byId.get(id) ?? []).slice(0, limit);
}

/** A recently used location: a structured Area, a Location Detail, or both. */
export interface RecentLocation {
  areaId: string | null;
  locationDetail: string;
}

export function encodeLocation(location: RecentLocation): string {
  return JSON.stringify([location.areaId, location.locationDetail.trim()]);
}

export function decodeLocation(value: string): RecentLocation | null {
  try {
    const parsed: unknown = JSON.parse(value);
    if (
      Array.isArray(parsed) &&
      parsed.length === 2 &&
      (parsed[0] === null || typeof parsed[0] === 'string') &&
      typeof parsed[1] === 'string'
    ) {
      return { areaId: parsed[0] as string | null, locationDetail: parsed[1] };
    }
  } catch {
    // Ignore malformed entries.
  }

  return null;
}

const lastProjectKey = (userId: string) => `fieldapp.lastProject.v1.${userId}`;

export function readLastProjectId(userId: string): string | null {
  return readStorage(lastProjectKey(userId));
}

export function writeLastProjectId(userId: string, projectId: string): void {
  writeStorage(lastProjectKey(userId), projectId);
}

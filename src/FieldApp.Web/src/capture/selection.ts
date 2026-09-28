import type { Area, CaptureTrade } from '../api/types';

export type ItemType = 'Observation' | 'PunchList';

export const itemTypeLabels: Record<ItemType, string> = {
  Observation: 'Observation',
  PunchList: 'Punch List',
};

/** Every whitespace-separated term must appear in the full path (same rule as the API's search). */
export function matchesSearch(area: Area, query: string): boolean {
  const path = area.path.toLocaleLowerCase();
  return query
    .toLocaleLowerCase()
    .split(/\s+/)
    .filter(Boolean)
    .every((term) => path.includes(term));
}

/** Five trade chips plus More fill a 3 x 2 grid at phone width. */
export const quickTradeCount = 5;

/**
 * Quick trade chips: recently used first, topped up with the remaining trades in list order, always including
 * the selected trade. Stable order keeps muscle memory during repeat capture.
 */
export function quickTrades(
  trades: CaptureTrade[],
  recent: CaptureTrade[],
  selected: CaptureTrade | null,
): CaptureTrade[] {
  const ordered = [...recent, ...trades];
  const unique = ordered.filter(
    (trade, index) => ordered.findIndex((other) => other.tradeId === trade.tradeId) === index,
  );
  const chips = unique.slice(0, quickTradeCount);

  if (selected && !chips.some((trade) => trade.tradeId === selected.tradeId)) {
    chips[chips.length - 1] = selected;
  }

  return chips;
}

/** A location choice: a structured Area, a free-text Location Detail, or both. */
export interface LocationChoice {
  area: Area | null;
  locationDetail: string;
}

/** Short chip text, e.g. "Office 201 · North wall" or "Unit 214". */
export function locationLabel({ area, locationDetail }: LocationChoice): string {
  return [area?.name, locationDetail.trim()].filter(Boolean).join(' · ');
}

/** Full accessible name, e.g. "Building A / Level 2 / Office 201, North wall". */
export function locationAccessibleName({ area, locationDetail }: LocationChoice): string {
  return [area?.path, locationDetail.trim()].filter(Boolean).join(', ');
}

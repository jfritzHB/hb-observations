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

const quickTradeLimit = 6;
const recentTradeLimit = 4;

/** Large chips: recent trades (or, before any history, the first few), always including the selected one. */
export function quickTrades(
  trades: CaptureTrade[],
  recent: CaptureTrade[],
  selected: CaptureTrade | null,
): CaptureTrade[] {
  const base = recent.length > 0 ? recent.slice(0, recentTradeLimit) : trades.slice(0, quickTradeLimit);
  return selected && !base.some((trade) => trade.tradeId === selected.tradeId) ? [selected, ...base] : base;
}

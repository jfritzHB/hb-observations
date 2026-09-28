import { describe, expect, it } from 'vitest';
import { areas, trades } from '../test/fakeApi';
import { pickRecent, readRecent, recordRecent } from './recent';
import { matchesSearch, quickTrades } from './selection';

describe('matchesSearch', () => {
  it('requires every term, case-insensitively, anywhere in the full path', () => {
    const office = areas.find((area) => area.name === 'Office 201');
    expect(office).toBeDefined();
    if (!office) return;

    expect(matchesSearch(office, 'OFFICE 201')).toBe(true);
    expect(matchesSearch(office, '  level 2   201 ')).toBe(true);
    expect(matchesSearch(office, 'mechanical 201')).toBe(false);
  });
});

describe('quickTrades', () => {
  it('shows the first trades when there is no history', () => {
    expect(quickTrades(trades, [], null)).toHaveLength(6);
  });

  it('prefers recent trades and always includes the selected trade', () => {
    const plumbing = trades.find((trade) => trade.name === 'Plumbing');
    const drywall = trades.find((trade) => trade.name === 'Drywall');
    if (!plumbing || !drywall) throw new Error('fixture');

    expect(quickTrades(trades, [drywall], plumbing).map((trade) => trade.name)).toEqual([
      'Plumbing',
      'Drywall',
    ]);
  });
});

describe('recent selections', () => {
  it('records most recent first, de-duplicated and capped at five, per user and project', () => {
    for (const id of ['a', 'b', 'c', 'd', 'e', 'f', 'b']) {
      recordRecent('user-1', 'project-1', 'areas', id);
    }

    expect(readRecent('user-1', 'project-1', 'areas')).toEqual(['b', 'f', 'e', 'd', 'c']);
    expect(readRecent('user-2', 'project-1', 'areas')).toEqual([]);
    expect(readRecent('user-1', 'project-2', 'areas')).toEqual([]);
  });

  it('ignores recent IDs that are no longer selectable', () => {
    expect(pickRecent(['gone', 'a-A2-201'], areas, (area) => area.id, 3).map((area) => area.id)).toEqual([
      'a-A2-201',
    ]);
  });

  it('survives corrupt stored data', () => {
    window.localStorage.setItem('fieldapp.recent.v1.user-1.project-1.trades', '{not json');
    expect(readRecent('user-1', 'project-1', 'trades')).toEqual([]);
  });
});

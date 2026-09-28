import { describe, expect, it } from 'vitest';
import { areas, trades } from '../test/fakeApi';
import { hasLocation, initialCaptureContext, isReadyForPhoto } from './captureContext';
import { decodeLocation, encodeLocation, pickRecent, readRecent, recordRecent } from './recent';
import { locationAccessibleName, locationLabel, matchesSearch, quickTrades } from './selection';

const office201 = areas.find((area) => area.name === 'Office 201');
if (!office201) throw new Error('fixture');

describe('matchesSearch', () => {
  it('requires every term, case-insensitively, anywhere in the full path', () => {
    expect(matchesSearch(office201, 'OFFICE 201')).toBe(true);
    expect(matchesSearch(office201, '  level 2   201 ')).toBe(true);
    expect(matchesSearch(office201, 'mechanical 201')).toBe(false);
  });
});

describe('quickTrades', () => {
  it('fills five chips in list order when there is no history', () => {
    expect(quickTrades(trades, [], null).map((trade) => trade.name)).toEqual([
      'Doors & Hardware',
      'Drywall',
      'Electrical',
      'Flooring',
      'HVAC',
    ]);
  });

  it('puts recent trades first, tops up with the rest, and always includes the selection', () => {
    const plumbing = trades.find((trade) => trade.name === 'Plumbing');
    const painting = trades.find((trade) => trade.name === 'Painting');
    if (!plumbing || !painting) throw new Error('fixture');

    expect(quickTrades(trades, [plumbing], null).map((trade) => trade.name)).toEqual([
      'Plumbing',
      'Doors & Hardware',
      'Drywall',
      'Electrical',
      'Flooring',
    ]);
    expect(quickTrades(trades, [], painting).map((trade) => trade.name)).toContain('Painting');
  });
});

describe('location labels', () => {
  it('uses the area name and detail on chips and the full path for assistive technology', () => {
    const choice = { area: office201, locationDetail: ' North wall ' };
    expect(locationLabel(choice)).toBe('Office 201 · North wall');
    expect(locationAccessibleName(choice)).toBe('Building A / Level 2 / Office 201, North wall');
    expect(locationLabel({ area: null, locationDetail: 'Unit 214' })).toBe('Unit 214');
  });
});

describe('capture context', () => {
  it('has a location with a structured area, a detail, or both', () => {
    const empty = initialCaptureContext(null);
    expect(hasLocation(empty)).toBe(false);
    expect(hasLocation({ ...empty, locationDetail: '   ' })).toBe(false);
    expect(hasLocation({ ...empty, locationDetail: 'Unit 214' })).toBe(true);
    expect(hasLocation({ ...empty, areaId: 'a-1' })).toBe(true);
  });

  it('is ready for the photo only with location, trade and type', () => {
    const base = { areaId: null, locationDetail: 'Unit 214', tradeId: 'dry', itemType: null };
    expect(isReadyForPhoto(base)).toBe(false);
    expect(isReadyForPhoto({ ...base, itemType: 'PunchList' })).toBe(true);
    expect(isReadyForPhoto({ ...base, locationDetail: '', itemType: 'PunchList' })).toBe(false);
  });

  it('starts from a retained context (future Capture Another) and caps detail length', () => {
    const context = initialCaptureContext({
      captureContext: {
        areaId: 'a-1',
        locationDetail: 'x'.repeat(200),
        tradeId: 'dry',
        itemType: 'Observation',
      },
    });
    expect(context.areaId).toBe('a-1');
    expect(context.locationDetail).toHaveLength(120);
    expect(context.itemType).toBe('Observation');
  });
});

describe('recent selections', () => {
  it('records most recent first, de-duplicated and capped at five, per user and project', () => {
    for (const id of ['a', 'b', 'c', 'd', 'e', 'f', 'b']) {
      recordRecent('user-1', 'project-1', 'trades', id);
    }

    expect(readRecent('user-1', 'project-1', 'trades')).toEqual(['b', 'f', 'e', 'd', 'c']);
    expect(readRecent('user-2', 'project-1', 'trades')).toEqual([]);
    expect(readRecent('user-1', 'project-2', 'trades')).toEqual([]);
  });

  it('round-trips recent locations and rejects malformed entries', () => {
    const encoded = encodeLocation({ areaId: 'a-A2-201', locationDetail: ' North wall ' });
    expect(decodeLocation(encoded)).toEqual({ areaId: 'a-A2-201', locationDetail: 'North wall' });
    expect(decodeLocation(encodeLocation({ areaId: null, locationDetail: 'Unit 214' }))).toEqual({
      areaId: null,
      locationDetail: 'Unit 214',
    });
    expect(decodeLocation('{not json')).toBeNull();
    expect(decodeLocation('[1, 2]')).toBeNull();
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

import { dayLabel, niceMax } from './stats-chart';
import { vocabularySegments } from './stats';

describe('stats helpers', () => {
  it('rounds the chart scale up to 1/2/5 steps', () => {
    expect(niceMax(0)).toBe(10);
    expect(niceMax(7)).toBe(10);
    expect(niceMax(13)).toBe(20);
    expect(niceMax(43)).toBe(50);
    expect(niceMax(100)).toBe(100);
    expect(niceMax(101)).toBe(200);
  });

  it('labels days with weekday', () => {
    expect(dayLabel('2026-11-26')).toBe('26.11, чт');
  });

  it('splits vocabulary into stages by share', () => {
    const segs = vocabularySegments({ notStarted: 5, learning: 3, passive: 2, active: '0', activeThresholdDays: 21 });
    expect(segs.map((s) => s.key)).toEqual(['active', 'passive', 'learning', 'notStarted']);
    expect(segs.find((s) => s.key === 'notStarted')!.share).toBe(0.5);
    expect(segs.find((s) => s.key === 'active')!.share).toBe(0);
  });
});

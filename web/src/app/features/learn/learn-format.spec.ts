import { formatDuration, formatNextReview, pluralRu } from './learn-format';

describe('learn format', () => {
  const now = new Date('2026-11-26T09:00:00Z');
  const at = (minutes: number) => new Date(now.getTime() + minutes * 60_000).toISOString();

  it.each([
    [1, 'через 1 минуту'],
    [10, 'через 10 минут'],
    [180, 'через 3 часа'],
    [1440, 'завтра'],
    [4 * 1440, 'через 4 дня'],
    [21 * 1440, 'через 21 день'],
    [90 * 1440, 'через 3 месяца'],
    [800 * 1440, 'через 2 года'],
  ])('%i min → %s', (minutes, text) => expect(formatNextReview(at(minutes), now)).toBe(text));

  it('handles missing date and durations', () => {
    expect(formatNextReview(null, now)).toBe('');
    expect(formatDuration(45)).toBe('45 с');
    expect(formatDuration(80)).toBe('1 мин 20 с');
    expect(pluralRu(22, 'слово', 'слова', 'слов')).toBe('слова');
  });
});

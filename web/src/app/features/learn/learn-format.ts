/** «через 10 минут», «завтра», «через 4 дня», «через 2 месяца». */
export function formatNextReview(iso: string | null | undefined, now = new Date()): string {
  if (!iso) {
    return '';
  }
  const minutes = Math.max(1, Math.round((new Date(iso).getTime() - now.getTime()) / 60_000));
  if (minutes < 60) {
    return `через ${minutes} ${pluralRu(minutes, 'минуту', 'минуты', 'минут')}`;
  }
  const hours = Math.round(minutes / 60);
  if (hours < 20) {
    return `через ${hours} ${pluralRu(hours, 'час', 'часа', 'часов')}`;
  }
  const days = Math.round(minutes / 1440);
  if (days <= 1) {
    return 'завтра';
  }
  if (days < 45) {
    return `через ${days} ${pluralRu(days, 'день', 'дня', 'дней')}`;
  }
  const months = Math.round(days / 30);
  if (months < 12) {
    return `через ${months} ${pluralRu(months, 'месяц', 'месяца', 'месяцев')}`;
  }
  const years = Math.round(days / 365);
  return `через ${years} ${pluralRu(years, 'год', 'года', 'лет')}`;
}

export function pluralRu(n: number, one: string, few: string, many: string): string {
  const mod10 = n % 10;
  const mod100 = n % 100;
  if (mod10 === 1 && mod100 !== 11) {
    return one;
  }
  if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) {
    return few;
  }
  return many;
}

/** «1 мин 20 с», «45 с». */
export function formatDuration(seconds: number): string {
  const s = Math.max(0, Math.round(seconds));
  const m = Math.floor(s / 60);
  return m > 0 ? `${m} мин ${s % 60} с` : `${s} с`;
}

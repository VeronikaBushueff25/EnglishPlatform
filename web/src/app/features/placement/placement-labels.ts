import { Cefr } from '../../core/api/models/cefr';
import { Skill } from '../../core/api/models/skill';

export const SKILL_LABELS: Record<Skill, string> = {
  Vocabulary: 'Лексика',
  Grammar: 'Грамматика',
  Reading: 'Чтение',
};

/** Описание уровня CEFR простыми словами. */
export const CEFR_LABELS: Record<NonNullable<Cefr>, string> = {
  A1: 'Начальный',
  A2: 'Элементарный',
  B1: 'Средний',
  B2: 'Выше среднего',
  C1: 'Продвинутый',
  C2: 'Свободное владение',
};

export const GOAL_TITLES: Record<string, string> = {
  Conversation: 'Разговорная речь',
  Travel: 'Путешествия',
  Work: 'Работа',
  Exams: 'Экзамены',
  Reading: 'Чтение',
  MoviesAndSeries: 'Фильмы и сериалы',
  Writing: 'Письмо',
};

/** Ширина полоски уровня: A1 — 1/6, C2 — вся. */
export function levelPercent(level: Cefr | null | undefined): number {
  const order: Cefr[] = ['A1', 'A2', 'B1', 'B2', 'C1', 'C2'];
  return level ? ((order.indexOf(level) + 1) / order.length) * 100 : 0;
}

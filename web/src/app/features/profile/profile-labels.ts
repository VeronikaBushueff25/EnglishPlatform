import { InterfaceLanguage } from '../../core/api/models/interface-language';
import { LearningGoal } from '../../core/api/models/learning-goal';
import { SubscriptionPlan } from '../../core/api/models/subscription-plan';

export const GOAL_LABELS: Record<LearningGoal, string> = {
  Conversation: 'Разговорная речь',
  Travel: 'Путешествия',
  Work: 'Работа',
  Exams: 'Экзамены',
  Reading: 'Чтение',
  MoviesAndSeries: 'Фильмы и сериалы',
  Writing: 'Письмо',
};

export const LANGUAGE_LABELS: Record<InterfaceLanguage, string> = {
  Ru: 'Русский',
  En: 'English',
};

export const PLAN_LABELS: Record<SubscriptionPlan, string> = {
  Free: 'Бесплатный',
  Premium: 'Премиум',
};

/** Все часовые пояса IANA, известные браузеру; сервер принимает те же идентификаторы. */
export function allTimeZones(): string[] {
  try {
    return Intl.supportedValuesOf('timeZone');
  } catch {
    return [];
  }
}

export function browserTimeZone(): string | null {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone ?? null;
  } catch {
    return null;
  }
}

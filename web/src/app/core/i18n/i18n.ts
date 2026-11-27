import { Injectable, signal } from '@angular/core';
import { InterfaceLanguage } from '../api/models/interface-language';
import { setTranslate } from './tr';

const KEY = 'owlish-lang';

function stored(): InterfaceLanguage {
  try {
    return localStorage.getItem(KEY) === 'En' ? 'En' : 'Ru';
  } catch {
    return 'Ru';
  }
}

/**
 * Язык интерфейса. Выбор хранится в профиле (на всех устройствах) и в localStorage —
 * чтобы при следующем открытии страница сразу была на нужном языке, ещё до входа.
 */
@Injectable({ providedIn: 'root' })
export class I18n {
  private readonly current = signal<InterfaceLanguage>('Ru');
  readonly language = this.current.asReadonly();
  private applied: Promise<void> = Promise.resolve();

  /** При старте приложения: язык из прошлого визита, чтобы не мигал русский. */
  init(): Promise<void> {
    return this.use(stored());
  }

  use(language: InterfaceLanguage): Promise<void> {
    try {
      localStorage.setItem(KEY, language);
    } catch {
      // приватный режим: язык просто не запомнится до следующего входа
    }
    if (language === this.current()) return this.applied;
    this.current.set(language);
    document.documentElement.lang = language === 'En' ? 'en' : 'ru';
    // Переводчик и словарь — отдельный чанк: русскому интерфейсу они не нужны.
    this.applied = Promise.all([import('./translator'), import('./en')]).then(([translator, { EN }]) => {
      if (this.current() === 'En') {
        translator.startTranslating(EN);
        setTranslate(translator.tr);
      } else {
        setTranslate(null);
        translator.stopTranslating();
      }
    });
    return this.applied;
  }
}

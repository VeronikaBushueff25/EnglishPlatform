import { Injectable, Signal, effect, inject, signal, untracked } from '@angular/core';
import { Api } from '../../core/api/api';
import { adoptPet } from '../../core/api/fn/pet/adopt-pet';
import { feedPet } from '../../core/api/fn/pet/feed-pet';
import { getMyPet } from '../../core/api/fn/pet/get-my-pet';
import { patPet } from '../../core/api/fn/pet/pat-pet';
import { answerPetQuiz } from '../../core/api/fn/pet/answer-pet-quiz';
import { recordPetActivity } from '../../core/api/fn/pet/record-pet-activity';
import { updatePet } from '../../core/api/fn/pet/update-pet';
import { AdoptPetRequest } from '../../core/api/models/adopt-pet-request';
import { PetActivity } from '../../core/api/models/pet-activity';
import { PetDto } from '../../core/api/models/pet-dto';
import { PetProgressDto } from '../../core/api/models/pet-progress-dto';
import { PetQuestDto } from '../../core/api/models/pet-quest-dto';
import { UpdatePetRequest } from '../../core/api/models/update-pet-request';
import { PetMood, PetStage } from './pet-art';

const ROAM_KEY = 'owlish-pet-roam';

function readRoam(): boolean {
  try {
    return localStorage.getItem(ROAM_KEY) !== 'off';
  } catch {
    return true;
  }
}

/** Питомец пользователя: один экземпляр на приложение — его видят и вкладка «Питомец», и питомец на страницах. */
@Injectable({ providedIn: 'root' })
export class PetStore {
  private readonly api = inject(Api);
  private loading: Promise<void> | null = null;

  /** undefined — ещё не загружен, null — питомца нет. */
  readonly pet = signal<PetDto | null | undefined>(undefined);
  /** Только что выполненные просьбы — питомец на страницах поздравит и покажет награду. */
  readonly completed = signal<PetQuestDto[]>([]);
  /** Гуляет ли питомец по остальным страницам. */
  readonly roaming = signal(readRoam());

  load(force = false): Promise<void> {
    if (!force && this.pet() !== undefined) return Promise.resolve();
    this.loading ??= this.api
      .invoke(getMyPet)
      .then((r) => this.pet.set(r.pet ?? null))
      .catch(() => {
        if (this.pet() === undefined) this.pet.set(null);
      })
      .finally(() => (this.loading = null));
    return this.loading;
  }

  async adopt(body: AdoptPetRequest): Promise<PetDto> {
    return this.keep(await this.api.invoke(adoptPet, { body }));
  }

  async update(body: UpdatePetRequest): Promise<PetDto> {
    return this.keep(await this.api.invoke(updatePet, { body }));
  }

  async feed(): Promise<PetDto> {
    return this.keep(await this.api.invoke(feedPet));
  }

  async pat(): Promise<PetDto> {
    return this.keep(await this.api.invoke(patPet));
  }

  /** Ответ на вопрос питомца о слове. */
  async quiz(correct: boolean): Promise<PetProgressDto> {
    const result = await this.api.invoke(answerPetQuiz, { body: { correct } });
    this.keep(result.pet);
    if (result.completed.length > 0) this.completed.set(result.completed);
    return result;
  }

  /**
   * Сыграл в игру или дочитал текст. Без питомца — тихо ничего не делаем; ошибки сети не мешают игре.
   * Возвращает выполненные просьбы — о них питомец на страницах скажет «спасибо».
   */
  async record(activity: PetActivity): Promise<PetQuestDto[]> {
    await this.load();
    if (!this.pet()) return [];
    try {
      const result = await this.api.invoke(recordPetActivity, { body: { activity } });
      this.keep(result.pet);
      if (result.completed.length > 0) this.completed.set(result.completed);
      return result.completed;
    } catch {
      return [];
    }
  }

  setRoaming(on: boolean): void {
    this.roaming.set(on);
    try {
      localStorage.setItem(ROAM_KEY, on ? 'on' : 'off');
    } catch {
      // приватный режим — настройка живёт до перезагрузки
    }
  }

  private keep(pet: PetDto): PetDto {
    this.pet.set(pet);
    return pet;
  }
}

/**
 * Засчитать питомцу дело, когда игра закончилась (state стал 'done'). Вызывать в конструкторе компонента.
 * Если питомца нет, ничего не происходит.
 */
export function reportWhenDone(state: Signal<string>, activity: PetActivity): void {
  const store = inject(PetStore);
  effect(() => {
    if (state() === 'done') untracked(() => void store.record(activity));
  });
}

export const n = (v: number | string | null | undefined): number => Number(v ?? 0);

export const isNight = (now = new Date()): boolean => now.getHours() >= 23 || now.getHours() < 7;

/** Обычное состояние питомца по настроению с сервера (ночью спит, если ему не грустно). */
export function baseMood(pet: PetDto, now = new Date()): PetMood {
  switch (pet.mood) {
    case 'Away':
      return 'away';
    case 'Sad':
    case 'Lonely':
      return 'sad';
    case 'Hungry':
      return 'hungry';
    default:
      return isNight(now) ? 'sleeping' : 'idle';
  }
}

export const stageOf = (pet: PetDto): PetStage => (pet.stage.toLowerCase() as PetStage) ?? 'baby';

export const MOOD_LABELS: Record<string, string> = {
  Happy: 'Счастлив',
  Content: 'Доволен',
  Hungry: 'Проголодался',
  Lonely: 'Скучает по ласке',
  Sad: 'Грустит без занятий',
  Away: 'Ушёл гулять',
};

export const STAGE_LABELS: Record<string, string> = { Baby: 'Малыш', Teen: 'Подросток', Adult: 'Взрослый' };

/** Просьбы питомца: как он просит, куда вести и как назвать кнопку. */
export const QUESTS: Record<string, { request: string; action: string; url: string | null }> = {
  Lesson: { request: 'Пройди сегодняшнее занятие — я буду болеть за тебя!', action: 'Начать занятие', url: '/learn' },
  MistakesLesson: { request: 'Давай повторим слова, в которых ты ошибаешься.', action: 'Работа над ошибками', url: '/learn' },
  Match: { request: 'Сыграй со мной в «Пары» — соедини слова с переводами.', action: 'Играть в «Пары»', url: '/practice/match' },
  Blitz: { request: 'Давай наперегонки: 60 секунд «Блица»!', action: 'Играть в «Блиц»', url: '/practice/blitz' },
  Scramble: { request: 'Собери для меня слово из букв.', action: 'Играть в «Собери слово»', url: '/practice/scramble' },
  Reading: { request: 'Почитай мне текст на английском.', action: 'Выбрать текст', url: '/reading' },
  AddWords: { request: 'Научи меня новым словам — добавь их в словарь.', action: 'Найти слова', url: '/dictionary' },
  Quiz: { request: 'Ответь на мои вопросы о словах.', action: 'Ответить', url: null },
};

/** Вещи комнаты и за что они открываются. */
export const DECOR: Record<string, string> = {
  rug: 'Коврик',
  plant: 'Цветок',
  lamp: 'Торшер',
  books: 'Книжная полка',
  globe: 'Глобус',
  armchair: 'Кресло для чтения',
  poster: 'Постер «Hello!»',
  fishbowl: 'Аквариум',
  bed: 'Лежанка',
  lights: 'Гирлянда',
  trophy: 'Кубок',
};

export const DECOR_COUNTERS: Record<string, string> = {
  Lessons: 'Занятия',
  WordsAdded: 'Добавленные слова',
  BestStreak: 'Дней подряд',
  QuestsCompleted: 'Просьбы питомца',
  TextsRead: 'Прочитанные тексты',
  QuizCorrect: 'Верные ответы питомцу',
  GamesPlayed: 'Сыгранные игры',
};

export interface PetWord {
  text: string;
  translation: string;
}

export interface PetLine {
  text: string;
  /** Английское слово из словаря — нажать, чтобы послушать (и увидеть перевод). */
  word?: PetWord;
  /** Куда позвать: занятие, игра, текст… */
  link?: { label: string; url: string };
  /** Спросить слово: показать перевод, английское — по нажатию. */
  ask?: boolean;
  /** Предложить покормить (угощения есть). */
  feed?: boolean;
  /** Предложить ответить на вопросы. */
  quiz?: boolean;
}

/** Первая невыполненная просьба дня. */
export const nextQuest = (pet: PetDto): PetQuestDto | undefined => pet.quests.find((q) => !q.done);

function questLine(quest: PetQuestDto): PetLine {
  const info = QUESTS[quest.kind];
  return info.url ? { text: info.request, link: { label: info.action, url: info.url } } : { text: info.request, quiz: true };
}

/**
 * Что питомец «говорит» сейчас. Сначала — о деле: голоден — где взять угощения, грустит — позовёт заниматься;
 * когда всё хорошо — по очереди просьбы дня, слова из вашего словаря, похвала за серию.
 */
export function petLine(pet: PetDto, words: PetWord[], tick: number, now = new Date()): PetLine {
  const treats = n(pet.treats);
  const quest = nextQuest(pet);
  switch (pet.mood) {
    case 'Hungry':
      return treats > 0
        ? { text: 'Я проголодался… Угостишь?', feed: true }
        : { text: 'Я проголодался. Угощения дают за занятия — пройди одно, и покормишь меня.', link: { label: 'Начать занятие', url: '/learn' } };
    case 'Sad':
      return { text: 'Я скучаю. Давай позанимаемся сегодня?', link: { label: 'Начать занятие', url: '/learn' } };
    case 'Lonely':
      return { text: 'Мне одиноко. Ответь на мои вопросы о словах — мне будет приятно.', quiz: true };
  }
  if (isNight(now)) return { text: 'Zzz… Спокойной ночи.' };

  const lines: PetLine[] = [];
  if (!pet.studiedToday) lines.push({ text: 'Сегодня мы ещё не занимались. Начнём?', link: { label: 'Начать занятие', url: '/learn' } });
  if (quest) lines.push(questLine(quest));
  if (words.length > 0) {
    const word = words[tick % words.length];
    lines.push({ text: 'Помнишь это слово? Нажми — скажу вслух.', word }, { text: 'Как будет по-английски:', word, ask: true });
  }
  const streak = n(pet.streak);
  if (streak >= 2) lines.push({ text: `Мы занимаемся ${streak} дн. подряд — не прерывай серию!` });
  if (pet.studiedToday) lines.push({ text: 'Сегодня ты молодец! Я тобой горжусь.' });
  if (treats > 0 && n(pet.satiety) < 80) lines.push({ text: 'У тебя есть угощения… Я просто напоминаю.', feed: true });
  const xpLeft = n(pet.nextLevelXp) - n(pet.xp);
  if (xpLeft > 0 && xpLeft <= 30) lines.push({ text: 'Ещё чуть-чуть — и я подрасту!' });
  return lines[tick % lines.length] ?? { text: 'Привет!' };
}

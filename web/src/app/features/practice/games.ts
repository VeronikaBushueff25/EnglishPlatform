import { ChangeDetectionStrategy, Component, DestroyRef, HostListener, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideArrowLeft, lucideCheck, lucideDelete, lucideLightbulb, lucideRotateCcw, lucideSkipForward, lucideTrophy, lucideX } from '@ng-icons/lucide';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmSpinnerImports } from '@spartan-ng/helm/spinner';
import { Api } from '../../core/api/api';
import { getPracticeWords } from '../../core/api/fn/practice/get-practice-words';
import { PracticeWordDto } from '../../core/api/models/practice-word-dto';
import { Pronunciation } from '../../shared/pronunciation';
import { BlitzRound, bestScore, blitzRounds, mainTranslation, scrambleCandidates, scrambled, shuffle } from './game-logic';

const ICONS = { lucideArrowLeft, lucideCheck, lucideDelete, lucideLightbulb, lucideRotateCcw, lucideSkipForward, lucideTrophy, lucideX };

/** Шапка игры: назад к практике, название, правило. */
@Component({
  selector: 'app-game-header',
  imports: [RouterLink, NgIcon, HlmButtonImports],
  providers: [provideIcons(ICONS)],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a hlmBtn variant="ghost" size="sm" routerLink="/practice" class="self-start">
      <ng-icon name="lucideArrowLeft" />
      Практика
    </a>
    <ng-content />
  `,
  host: { class: 'flex flex-col gap-2' },
})
export class GameHeader {}

interface Card {
  id: string;
  pair: string;
  text: string;
  lang: 'en' | 'ru';
  done: boolean;
}

/** «Пары»: 6 слов и 6 переводов вперемешку — соединить на время. */
@Component({
  selector: 'app-match-game',
  imports: [NgIcon, GameHeader, HlmButtonImports, HlmCardImports, HlmSpinnerImports],
  providers: [provideIcons(ICONS)],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    @keyframes shake {
      25% { transform: translateX(-4px); }
      75% { transform: translateX(4px); }
    }
    .shake { animation: shake 0.25s 2; }
  `,
  template: `
    <div class="flex flex-col gap-4">
      <app-game-header>
        <h1 class="text-2xl font-semibold tracking-tight">Пары</h1>
        <p class="text-muted-foreground text-sm">Соедините слово с переводом. Быстрее и без ошибок — лучше.</p>
      </app-game-header>

      @if (state() === 'loading') {
        <div class="text-muted-foreground flex items-center gap-2"><hlm-spinner aria-label="Загрузка" /> Готовим слова…</div>
      } @else if (state() === 'few') {
        <p class="text-muted-foreground">Нужно хотя бы 4 слова с переводом в словаре.</p>
      } @else if (state() === 'done') {
        <section hlmCard class="items-center text-center">
          <div class="flex flex-col items-center gap-2 px-6 text-center">
            <ng-icon name="lucideTrophy" class="text-primary text-4xl" />
            <h2 class="text-2xl font-semibold">{{ seconds() }} с · ошибок {{ mistakes() }}</h2>
            <p class="text-muted-foreground text-sm">{{ record() ? 'Новый рекорд!' : 'Рекорд: ' + best() + ' с' }}</p>
          </div>
          <div class="flex justify-center px-6">
            <button hlmBtn type="button" class="h-12" (click)="start()"><ng-icon name="lucideRotateCcw" /> Ещё раз</button>
          </div>
        </section>
      } @else {
        <div class="text-muted-foreground flex justify-between text-sm tabular-nums">
          <span>{{ seconds() }} с</span>
          <span>ошибок: {{ mistakes() }}</span>
        </div>
        <div class="grid grid-cols-2 gap-2">
          @for (column of [left(), right()]; track $index) {
            <div class="flex flex-col gap-2">
              @for (card of column; track card.id) {
                <button
                  type="button"
                  class="bg-card min-h-14 rounded-2xl border px-3 py-2 text-start text-sm font-medium transition-all sm:text-base"
                  [class.opacity-0]="card.done"
                  [class.pointer-events-none]="card.done"
                  [class.border-primary]="picked()?.id === card.id"
                  [class.ring-2]="picked()?.id === card.id"
                  [class.ring-primary]="picked()?.id === card.id"
                  [class.shake]="wrong() === card.id"
                  [class.border-destructive]="wrong() === card.id"
                  [attr.lang]="card.lang"
                  [disabled]="card.done"
                  (click)="pick(card)"
                >
                  {{ card.text }}
                </button>
              }
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class MatchGame {
  private readonly api = inject(Api);
  private readonly pronunciation = inject(Pronunciation);

  protected readonly state = signal<'loading' | 'few' | 'play' | 'done'>('loading');
  protected readonly left = signal<Card[]>([]);
  protected readonly right = signal<Card[]>([]);
  protected readonly picked = signal<Card | null>(null);
  protected readonly wrong = signal<string | null>(null);
  protected readonly mistakes = signal(0);
  protected readonly seconds = signal(0);
  protected readonly best = signal(0);
  protected readonly record = signal(false);
  private timer: ReturnType<typeof setInterval> | undefined;

  constructor() {
    inject(DestroyRef).onDestroy(() => clearInterval(this.timer));
    void this.start();
  }

  protected async start(): Promise<void> {
    this.state.set('loading');
    clearInterval(this.timer);
    const words = (await this.api.invoke(getPracticeWords, { count: 6 })).filter((w) => mainTranslation(w));
    if (words.length < 4) {
      this.state.set('few');
      return;
    }
    this.left.set(shuffle(words.map((w) => ({ id: 'en' + w.id, pair: w.id, text: w.text, lang: 'en' as const, done: false }))));
    this.right.set(shuffle(words.map((w) => ({ id: 'ru' + w.id, pair: w.id, text: mainTranslation(w), lang: 'ru' as const, done: false }))));
    this.mistakes.set(0);
    this.seconds.set(0);
    this.picked.set(null);
    this.state.set('play');
    this.timer = setInterval(() => this.seconds.update((s) => s + 1), 1000);
  }

  protected pick(card: Card): void {
    const first = this.picked();
    if (card.lang === 'en') {
      this.pronunciation.speak(card.text);
    }
    if (!first || first.lang === card.lang) {
      this.picked.set(card);
      return;
    }
    if (first.pair === card.pair) {
      const done = (list: Card[]) => list.map((c) => (c.pair === card.pair ? { ...c, done: true } : c));
      this.left.update(done);
      this.right.update(done);
      this.picked.set(null);
      if (this.left().every((c) => c.done)) {
        clearInterval(this.timer);
        const { best, record } = bestScore('ep-best-match', this.seconds() + this.mistakes() * 3, true);
        this.best.set(best);
        this.record.set(record);
        this.state.set('done');
      }
    } else {
      this.mistakes.update((m) => m + 1);
      this.wrong.set(card.id);
      this.picked.set(null);
      setTimeout(() => this.wrong.set(null), 500);
    }
  }
}

/** «Блиц»: 60 секунд, слово и перевод — верно или нет. Клавиши ← и →. */
@Component({
  selector: 'app-blitz-game',
  imports: [NgIcon, GameHeader, HlmButtonImports, HlmCardImports, HlmSpinnerImports],
  providers: [provideIcons(ICONS)],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4">
      <app-game-header>
        <h1 class="text-2xl font-semibold tracking-tight">Блиц</h1>
        <p class="text-muted-foreground text-sm">60 секунд: перевод верный? Отвечайте быстро — клавиши ← и → тоже работают.</p>
      </app-game-header>

      @if (state() === 'loading') {
        <div class="text-muted-foreground flex items-center gap-2"><hlm-spinner aria-label="Загрузка" /> Готовим слова…</div>
      } @else if (state() === 'few') {
        <p class="text-muted-foreground">Нужно хотя бы 4 слова с переводом в словаре.</p>
      } @else if (state() === 'done') {
        <section hlmCard class="items-center text-center">
          <div class="flex flex-col items-center gap-2 px-6 text-center">
            <ng-icon name="lucideTrophy" class="text-primary text-4xl" />
            <h2 class="text-2xl font-semibold">{{ score() }} верных</h2>
            <p class="text-muted-foreground text-sm">Ошибок: {{ errors() }} · {{ record() ? 'новый рекорд!' : 'рекорд: ' + best() }}</p>
          </div>
          <div class="flex justify-center px-6">
            <button hlmBtn type="button" class="h-12" (click)="start()"><ng-icon name="lucideRotateCcw" /> Ещё раз</button>
          </div>
        </section>
      } @else if (round(); as r) {
        <div class="flex items-center gap-3">
          <div class="bg-muted h-2 flex-1 overflow-hidden rounded-full">
            <div class="bg-primary h-full rounded-full transition-all duration-1000 ease-linear" [style.width.%]="(left() / 60) * 100"></div>
          </div>
          <span class="text-muted-foreground w-16 text-end text-sm tabular-nums">{{ left() }} с · {{ score() }}</span>
        </div>
        <section hlmCard class="items-center gap-2 py-10 text-center" [class.border-success]="flash() === 'ok'" [class.border-destructive]="flash() === 'bad'">
          <p class="text-3xl font-semibold" lang="en">{{ r.word.text }}</p>
          <p class="text-muted-foreground text-xl">{{ r.shown }}</p>
        </section>
        <div class="grid grid-cols-2 gap-3">
          <button hlmBtn variant="outline" type="button" class="h-14 text-base" (click)="answer(false)"><ng-icon name="lucideX" /> Неверно</button>
          <button hlmBtn type="button" class="h-14 text-base" (click)="answer(true)"><ng-icon name="lucideCheck" /> Верно</button>
        </div>
      }
    </div>
  `,
})
export class BlitzGame {
  private readonly api = inject(Api);

  protected readonly state = signal<'loading' | 'few' | 'play' | 'done'>('loading');
  protected readonly rounds = signal<BlitzRound[]>([]);
  protected readonly index = signal(0);
  protected readonly round = computed(() => this.rounds()[this.index() % Math.max(1, this.rounds().length)]);
  protected readonly score = signal(0);
  protected readonly errors = signal(0);
  protected readonly left = signal(60);
  protected readonly flash = signal<'ok' | 'bad' | null>(null);
  protected readonly best = signal(0);
  protected readonly record = signal(false);
  private words: PracticeWordDto[] = [];
  private timer: ReturnType<typeof setInterval> | undefined;

  constructor() {
    inject(DestroyRef).onDestroy(() => clearInterval(this.timer));
    void this.start();
  }

  @HostListener('document:keydown', ['$event'])
  protected onKey(event: KeyboardEvent): void {
    if (this.state() !== 'play') return;
    if (event.key === 'ArrowLeft') this.answer(false);
    if (event.key === 'ArrowRight') this.answer(true);
  }

  protected async start(): Promise<void> {
    this.state.set('loading');
    clearInterval(this.timer);
    this.words = (await this.api.invoke(getPracticeWords, { count: 40 })).filter((w) => mainTranslation(w));
    if (this.words.length < 4) {
      this.state.set('few');
      return;
    }
    this.rounds.set(blitzRounds(shuffle(this.words)));
    this.index.set(0);
    this.score.set(0);
    this.errors.set(0);
    this.left.set(60);
    this.state.set('play');
    this.timer = setInterval(() => {
      this.left.update((s) => s - 1);
      if (this.left() <= 0) {
        clearInterval(this.timer);
        const { best, record } = bestScore('ep-best-blitz', this.score());
        this.best.set(best);
        this.record.set(record);
        this.state.set('done');
      }
    }, 1000);
  }

  protected answer(saysCorrect: boolean): void {
    const r = this.round();
    if (!r || this.state() !== 'play') return;
    const ok = r.correct === saysCorrect;
    if (ok) {
      this.score.update((s) => s + 1);
    } else {
      this.errors.update((e) => e + 1);
    }
    this.flash.set(ok ? 'ok' : 'bad');
    setTimeout(() => this.flash.set(null), 250);
    // Слова закончились раньше минуты — новый круг в другом порядке.
    if (this.index() + 1 >= this.rounds().length) {
      this.rounds.set(blitzRounds(shuffle(this.words)));
      this.index.set(0);
    } else {
      this.index.update((i) => i + 1);
    }
  }
}

/** «Собери слово»: перевод и буквы вперемешку — собрать английское слово. */
@Component({
  selector: 'app-scramble-game',
  imports: [NgIcon, GameHeader, HlmButtonImports, HlmCardImports, HlmSpinnerImports],
  providers: [provideIcons(ICONS)],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4">
      <app-game-header>
        <h1 class="text-2xl font-semibold tracking-tight">Собери слово</h1>
        <p class="text-muted-foreground text-sm">Соберите английское слово из букв. Тренирует написание.</p>
      </app-game-header>

      @if (state() === 'loading') {
        <div class="text-muted-foreground flex items-center gap-2"><hlm-spinner aria-label="Загрузка" /> Готовим слова…</div>
      } @else if (state() === 'few') {
        <p class="text-muted-foreground">Нужно хотя бы 3 одиночных слова (без пробелов) с переводом в словаре.</p>
      } @else if (state() === 'done') {
        <section hlmCard class="items-center text-center">
          <div class="flex flex-col items-center gap-2 px-6 text-center">
            <ng-icon name="lucideTrophy" class="text-primary text-4xl" />
            <h2 class="text-2xl font-semibold">Собрано {{ solved() }} из {{ words().length }}</h2>
            <p class="text-muted-foreground text-sm">Подсказок: {{ hints() }}</p>
          </div>
          <div class="flex justify-center px-6">
            <button hlmBtn type="button" class="h-12" (click)="start()"><ng-icon name="lucideRotateCcw" /> Ещё раз</button>
          </div>
        </section>
      } @else if (word(); as w) {
        <p class="text-muted-foreground text-sm tabular-nums">{{ index() + 1 }} / {{ words().length }}</p>
        <section hlmCard class="items-center gap-4 py-8 text-center" [class.border-success]="status() === 'ok'" [class.border-destructive]="status() === 'bad'">
          <p class="text-2xl font-semibold">{{ translation() }}</p>
          <div class="flex min-h-12 flex-wrap justify-center gap-1.5" lang="en" aria-live="polite">
            @for (slot of slots(); track $index) {
              <span class="bg-muted flex size-10 items-center justify-center rounded-xl text-xl font-semibold uppercase" [class.bg-primary/15]="slot">{{ slot }}</span>
            }
          </div>
          @if (status() === 'bad') {
            <p class="text-destructive text-sm">Правильно: <b lang="en">{{ w.text }}</b></p>
          }
        </section>
        <div class="flex flex-wrap justify-center gap-2" lang="en">
          @for (letter of letters(); track $index; let i = $index) {
            <button
              hlmBtn
              variant="outline"
              type="button"
              class="size-12 text-xl font-semibold uppercase"
              [disabled]="used().has(i) || status() !== null"
              (click)="tap(i)"
            >
              {{ letter }}
            </button>
          }
        </div>
        <div class="flex flex-wrap justify-center gap-2">
          <button hlmBtn variant="ghost" size="sm" type="button" [disabled]="typed().length === 0 || status() !== null" (click)="erase()"><ng-icon name="lucideDelete" /> Стереть</button>
          <button hlmBtn variant="ghost" size="sm" type="button" [disabled]="status() !== null" (click)="hint()"><ng-icon name="lucideLightbulb" /> Подсказка</button>
          <button hlmBtn variant="ghost" size="sm" type="button" (click)="next()"><ng-icon name="lucideSkipForward" /> {{ status() ? 'Дальше' : 'Пропустить' }}</button>
        </div>
      }
    </div>
  `,
})
export class ScrambleGame {
  private readonly api = inject(Api);
  private readonly pronunciation = inject(Pronunciation);

  protected readonly state = signal<'loading' | 'few' | 'play' | 'done'>('loading');
  protected readonly words = signal<PracticeWordDto[]>([]);
  protected readonly index = signal(0);
  protected readonly word = computed(() => this.words()[this.index()]);
  protected readonly translation = computed(() => (this.word() ? mainTranslation(this.word()!) : ''));
  protected readonly letters = signal<string[]>([]);
  protected readonly used = signal<ReadonlySet<number>>(new Set());
  protected readonly typed = signal<number[]>([]);
  protected readonly status = signal<'ok' | 'bad' | null>(null);
  protected readonly solved = signal(0);
  protected readonly hints = signal(0);
  protected readonly slots = computed(() => {
    const letters = this.letters();
    const typed = this.typed().map((i) => letters[i]);
    return Array.from({ length: letters.length }, (_, i) => typed[i] ?? '');
  });

  constructor() {
    void this.start();
  }

  @HostListener('document:keydown', ['$event'])
  protected onKey(event: KeyboardEvent): void {
    if (this.state() !== 'play' || this.status() !== null) return;
    if (event.key === 'Backspace') {
      this.erase();
      return;
    }
    const key = event.key.toLowerCase();
    const i = this.letters().findIndex((l, idx) => l === key && !this.used().has(idx));
    if (i >= 0) this.tap(i);
  }

  protected async start(): Promise<void> {
    this.state.set('loading');
    const words = scrambleCandidates((await this.api.invoke(getPracticeWords, { count: 30 })).filter((w) => mainTranslation(w))).slice(0, 10);
    if (words.length < 3) {
      this.state.set('few');
      return;
    }
    this.words.set(words);
    this.index.set(0);
    this.solved.set(0);
    this.hints.set(0);
    this.setUp();
    this.state.set('play');
  }

  protected tap(i: number): void {
    this.used.update((s) => new Set(s).add(i));
    this.typed.update((t) => [...t, i]);
    if (this.typed().length === this.letters().length) {
      this.check();
    }
  }

  protected erase(): void {
    const last = this.typed().at(-1);
    if (last === undefined) return;
    this.typed.update((t) => t.slice(0, -1));
    this.used.update((s) => {
      const next = new Set(s);
      next.delete(last);
      return next;
    });
  }

  /** Подсказка: стираем неверное и ставим следующую правильную букву. */
  protected hint(): void {
    const target = this.word()!.text.toLowerCase();
    const letters = this.letters();
    let correct = 0;
    while (correct < this.typed().length && letters[this.typed()[correct]] === target[correct]) correct++;
    while (this.typed().length > correct) this.erase();
    const need = target[correct];
    const i = letters.findIndex((l, idx) => l === need && !this.used().has(idx));
    if (i >= 0) {
      this.hints.update((h) => h + 1);
      this.tap(i);
    }
  }

  protected next(): void {
    if (this.index() + 1 >= this.words().length) {
      this.state.set('done');
      return;
    }
    this.index.update((i) => i + 1);
    this.setUp();
  }

  private check(): void {
    const word = this.word()!;
    const ok = this.typed().map((i) => this.letters()[i]).join('') === word.text.toLowerCase();
    this.status.set(ok ? 'ok' : 'bad');
    this.pronunciation.speak(word.text);
    if (ok) {
      this.solved.update((s) => s + 1);
      setTimeout(() => this.next(), 900);
    }
  }

  private setUp(): void {
    this.letters.set(scrambled(this.word()!.text));
    this.used.set(new Set());
    this.typed.set([]);
    this.status.set(null);
  }
}

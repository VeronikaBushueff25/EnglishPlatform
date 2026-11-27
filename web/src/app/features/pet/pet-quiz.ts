import { ChangeDetectionStrategy, Component, computed, inject, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCheck, lucideVolume2, lucideX } from '@ng-icons/lucide';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmSpinnerImports } from '@spartan-ng/helm/spinner';
import { Api } from '../../core/api/api';
import { getPracticeWords } from '../../core/api/fn/practice/get-practice-words';
import { PracticeWordDto } from '../../core/api/models/practice-word-dto';
import { Pronunciation } from '../../shared/pronunciation';
import { PetStore } from './pet-store';

const ROUND = 5;

interface Question {
  /** Спрашиваем по-английски (выбрать перевод) или по-русски (выбрать слово). */
  english: boolean;
  prompt: string;
  options: string[];
  answer: string;
  word: PracticeWordDto;
}

const shuffle = <T,>(items: T[]): T[] => items.map((x) => [Math.random(), x] as const).sort((a, b) => a[0] - b[0]).map(([, x]) => x);
const translationOf = (w: PracticeWordDto) => w.translations[0] ?? '';

/**
 * «Поиграть» с питомцем — это его вопросы о ваших словах: 5 вопросов то с английского, то на английский.
 * Верный ответ радует питомца и идёт в просьбу «ответь на мои вопросы» и в достижение «аквариум».
 */
@Component({
  selector: 'app-pet-quiz',
  imports: [RouterLink, NgIcon, HlmButtonImports, HlmSpinnerImports],
  providers: [provideIcons({ lucideCheck, lucideVolume2, lucideX })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="bg-card rounded-3xl border p-4" aria-live="polite">
      @if (loading()) {
        <div class="text-muted-foreground flex items-center gap-2 text-sm"><hlm-spinner aria-label="Загрузка" /> Питомец придумывает вопросы…</div>
      } @else if (questions().length === 0) {
        <div class="flex flex-wrap items-center justify-between gap-2">
          <p class="text-muted-foreground text-sm">Чтобы питомец задавал вопросы, нужно хотя бы 4 слова с переводом в вашем словаре.</p>
          <a hlmBtn size="sm" routerLink="/dictionary">Добавить слова</a>
        </div>
      } @else if (index() < questions().length) {
        @if (current(); as q) {
          <div class="flex items-center justify-between gap-2">
            <p class="text-muted-foreground text-sm">Вопрос {{ index() + 1 }} из {{ questions().length }}</p>
            <button hlmBtn variant="ghost" size="icon" type="button" aria-label="Закрыть" (click)="closed.emit()"><ng-icon name="lucideX" /></button>
          </div>
          <p class="mt-1 text-lg">
            {{ q.english ? 'Что значит' : 'Как по-английски' }}
            <span class="font-semibold" [attr.lang]="q.english ? 'en' : null" translate="no">«{{ q.prompt }}»</span>?
            @if (q.english) {
              <button hlmBtn variant="ghost" size="icon" type="button" class="size-8" aria-label="Послушать" (click)="say(q.prompt)"><ng-icon name="lucideVolume2" /></button>
            }
          </p>
          <div class="mt-3 grid gap-2 sm:grid-cols-2">
            @for (o of q.options; track o) {
              <button type="button" translate="no" [attr.lang]="q.english ? null : 'en'" [disabled]="chosen() !== null" (click)="choose(o)"
                      class="rounded-2xl border px-4 py-3 text-start transition-colors"
                      [class.border-success]="chosen() !== null && o === q.answer" [class.bg-success/10]="chosen() !== null && o === q.answer"
                      [class.border-destructive]="chosen() === o && o !== q.answer" [class.bg-destructive/10]="chosen() === o && o !== q.answer">
                {{ o }}
              </button>
            }
          </div>
          @if (chosen() !== null) {
            <div class="mt-3 flex items-center justify-between gap-2">
              <p class="text-sm font-medium" [class.text-success]="chosen() === q.answer" [class.text-destructive]="chosen() !== q.answer">
                @if (chosen() === q.answer) { Верно! Питомец ловит мячик. } @else { Не то — правильно «<span translate="no">{{ q.answer }}</span>». }
              </p>
              <button hlmBtn size="sm" type="button" (click)="next()">Дальше</button>
            </div>
          }
        }
      } @else {
        <p class="text-lg font-medium">Верно {{ correct() }} из {{ questions().length }}</p>
        <p class="text-muted-foreground text-sm">{{ correct() >= 4 ? 'Питомец в восторге!' : 'Питомец рад, что вы поиграли. Слова с ошибками попадутся снова.' }}</p>
        <div class="mt-3 flex gap-2">
          <button hlmBtn size="sm" type="button" (click)="start()">Ещё вопросы</button>
          <button hlmBtn variant="outline" size="sm" type="button" (click)="closed.emit()">Закрыть</button>
        </div>
      }
    </section>
  `,
})
export class PetQuiz {
  private readonly api = inject(Api);
  private readonly store = inject(PetStore);
  private readonly pronunciation = inject(Pronunciation);

  /** Ответ дан: true — верно (питомец радуется). */
  readonly answered = output<boolean>();
  readonly closed = output<void>();

  protected readonly loading = signal(true);
  protected readonly questions = signal<Question[]>([]);
  protected readonly index = signal(0);
  protected readonly chosen = signal<string | null>(null);
  protected readonly correct = signal(0);
  protected readonly current = computed(() => this.questions()[this.index()]);

  constructor() {
    void this.start();
  }

  protected async start(): Promise<void> {
    this.loading.set(true);
    this.index.set(0);
    this.correct.set(0);
    this.chosen.set(null);
    try {
      const words = (await this.api.invoke(getPracticeWords, { count: 12 })).filter((w) => translationOf(w));
      this.questions.set(words.length < 4 ? [] : this.build(words));
    } catch {
      this.questions.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  private build(words: PracticeWordDto[]): Question[] {
    return words.slice(0, ROUND).map((word, i) => {
      const english = i % 2 === 0;
      const others = shuffle(words.filter((w) => w.id !== word.id)).slice(0, 3);
      const pick = (w: PracticeWordDto) => (english ? translationOf(w) : w.text);
      const answer = pick(word);
      return {
        english,
        prompt: english ? word.text : translationOf(word),
        answer,
        options: shuffle([answer, ...others.map(pick).filter((o) => o !== answer)]),
        word,
      };
    });
  }

  protected choose(option: string): void {
    const q = this.current();
    if (!q || this.chosen() !== null) return;
    this.chosen.set(option);
    const ok = option === q.answer;
    if (ok) this.correct.update((c) => c + 1);
    this.say(q.word.text);
    this.answered.emit(ok);
    if (ok) void this.store.quiz(true).catch(() => undefined);
  }

  protected next(): void {
    this.chosen.set(null);
    this.index.update((i) => i + 1);
  }

  protected say(text: string): void {
    this.pronunciation.speak(text);
  }
}

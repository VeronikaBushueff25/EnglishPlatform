import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCheck, lucideChevronsRight, lucidePause, lucideVolume2, lucideX } from '@ng-icons/lucide';
import { toast } from '@spartan-ng/brain/sonner';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { Api } from '../../core/api/api';
import { listMyWordTexts } from '../../core/api/fn/my-words/list-my-word-texts';
import { Pronunciation } from '../../shared/pronunciation';
import { WordLookup } from '../../shared/word-lookup';
import { PetStore } from '../pet/pet-store';
import { Token, baseForms, normalizeWord, paragraphsOf, phrase, tokenize } from './tokens';

interface Selection {
  paragraph: number;
  from: number;
  to: number;
}

/**
 * Читалка: каждое слово кликабельно. Нажали — снизу карточка: перевод, транскрипция, озвучка, «в мои слова».
 * «+ слово» расширяет выделение на следующее (put → put off, jump → jump to conclusions).
 * Слова из личного словаря подчёркнуты точками.
 */
@Component({
  selector: 'app-reader',
  imports: [NgIcon, WordLookup, HlmButtonImports],
  providers: [provideIcons({ lucideCheck, lucideChevronsRight, lucidePause, lucideVolume2, lucideX })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .w {
      border-radius: 0.375rem;
      padding: 0 1px;
      cursor: pointer;
      transition: background-color 0.15s;
    }
    .w:hover {
      background: color-mix(in oklch, var(--primary) 12%, transparent);
    }
    .w.known {
      text-decoration: underline dotted color-mix(in oklch, var(--primary) 70%, transparent);
      text-underline-offset: 4px;
      text-decoration-thickness: 2px;
    }
    .w.on {
      background: color-mix(in oklch, var(--primary) 25%, transparent);
    }
  `,
  template: `
    <div class="flex flex-col gap-4 pb-[45vh]">
      <div class="flex flex-wrap items-center gap-2">
        @if (pronunciation.supported) {
          <button hlmBtn variant="outline" size="sm" type="button" (click)="toggleListen()">
            <ng-icon [name]="listening() ? 'lucidePause' : 'lucideVolume2'" />
            {{ listening() ? 'Остановить' : 'Слушать текст' }}
          </button>
        }
        <p class="text-muted-foreground text-sm">Нажмите на слово — перевод и «в мои слова». Подчёркнуты слова из вашего словаря.</p>
      </div>

      <article class="flex flex-col gap-4 text-lg leading-relaxed" lang="en">
        @for (p of paragraphs(); track $index; let pi = $index) {
          <p>
            @for (t of p; track $index) {
              @if (t.word) {
                <span
                  class="w"
                  role="button"
                  tabindex="0"
                  [class.known]="isKnown(t.text)"
                  [class.on]="isSelected(pi, t.index)"
                  (click)="select(pi, t.index)"
                  (keydown.enter)="select(pi, t.index)"
                  >{{ t.text }}</span
                >
              } @else {<ng-container>{{ t.text }}</ng-container>}
            }
          </p>
        }
      </article>

      <!-- Дочитал — питомец благодарит: просьба «почитай мне», кресло в комнате. -->
      <div class="flex flex-wrap items-center gap-3 border-t pt-4">
        @if (finished()) {
          <p class="text-success flex items-center gap-1.5 text-sm font-medium"><ng-icon name="lucideCheck" /> Текст прочитан</p>
        } @else {
          <button hlmBtn variant="outline" type="button" (click)="markRead()"><ng-icon name="lucideCheck" /> Я дочитал(а)</button>
          <p class="text-muted-foreground text-sm">Отметьте — питомец тоже любит, когда ему читают.</p>
        }
      </div>
    </div>

    @if (query(); as q) {
      <aside
        class="bg-popover text-popover-foreground fixed inset-x-0 bottom-0 z-20 mx-auto max-h-[60vh] w-full max-w-3xl overflow-y-auto rounded-t-3xl border-t p-4 shadow-2xl sm:bottom-4 sm:rounded-3xl sm:border"
        aria-label="Слово из текста"
      >
        <div class="mb-2 flex items-center gap-2">
          <span class="text-muted-foreground text-sm">Выделено: <b class="text-foreground" lang="en">{{ q }}</b></span>
          @if (canExtend()) {
            <button hlmBtn variant="ghost" size="sm" type="button" (click)="extend()" title="Добавить следующее слово — для фраз и фразовых глаголов">
              <ng-icon name="lucideChevronsRight" /> + слово
            </button>
          }
          <button hlmBtn variant="ghost" size="icon" type="button" class="ms-auto" aria-label="Закрыть" (click)="selection.set(null)">
            <ng-icon name="lucideX" />
          </button>
        </div>
        <app-word-lookup [text]="q" [showOwnWordLink]="false" (wordAdded)="remember($event.text)" />
      </aside>
    }
  `,
})
export class Reader {
  private readonly api = inject(Api);
  private readonly petStore = inject(PetStore);
  protected readonly finished = signal(false);
  protected readonly pronunciation = inject(Pronunciation);

  readonly body = input.required<string>();

  protected readonly paragraphs = computed<Token[][]>(() => paragraphsOf(this.body()).map(tokenize));
  protected readonly selection = signal<Selection | null>(null);
  protected readonly listening = signal(false);
  private readonly known = signal<ReadonlySet<string>>(new Set());

  protected readonly query = computed(() => {
    const s = this.selection();
    return s ? phrase(this.paragraphs()[s.paragraph], s.from, s.to) : null;
  });
  protected readonly canExtend = computed(() => {
    const s = this.selection();
    if (!s || s.to - s.from >= 4) return false;
    return this.paragraphs()[s.paragraph].some((t) => t.word && t.index === s.to + 1);
  });

  protected async markRead(): Promise<void> {
    this.finished.set(true);
    const done = await this.petStore.record('Reading');
    if (done.length > 0) toast.success('Питомец благодарит за текст!', { description: 'Просьба выполнена — угощения уже у него.' });
  }

  constructor() {
    void this.api
      .invoke(listMyWordTexts)
      .then((texts) => this.known.set(new Set(texts)))
      .catch(() => undefined);
  }

  protected isKnown(word: string): boolean {
    const known = this.known();
    return known.size > 0 && baseForms(word).some((f) => known.has(f));
  }

  protected isSelected(paragraph: number, index: number): boolean {
    const s = this.selection();
    return !!s && s.paragraph === paragraph && index >= s.from && index <= s.to;
  }

  protected select(paragraph: number, index: number): void {
    const s = this.selection();
    this.selection.set(s && s.paragraph === paragraph && s.from === index && s.to === index ? null : { paragraph, from: index, to: index });
  }

  protected extend(): void {
    this.selection.update((s) => (s ? { ...s, to: s.to + 1 } : s));
  }

  protected remember(text: string): void {
    this.known.update((set) => new Set(set).add(normalizeWord(text)));
  }

  protected toggleListen(): void {
    if (this.listening()) {
      speechSynthesis.cancel();
      this.listening.set(false);
      return;
    }
    const text = paragraphsOf(this.body()).join('\n\n');
    const utterance = new SpeechSynthesisUtterance(text);
    utterance.lang = 'en-GB';
    utterance.rate = 0.95;
    utterance.onend = () => this.listening.set(false);
    utterance.onerror = () => this.listening.set(false);
    speechSynthesis.cancel();
    speechSynthesis.speak(utterance);
    this.listening.set(true);
  }
}

import { ChangeDetectionStrategy, Component, computed, inject, input, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideArrowLeft, lucideBookOpenText, lucideClipboardPaste, lucideClock } from '@ng-icons/lucide';
import { HlmBadgeImports } from '@spartan-ng/helm/badge';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmSpinnerImports } from '@spartan-ng/helm/spinner';
import { HlmTextareaImports } from '@spartan-ng/helm/textarea';
import { Api } from '../../core/api/api';
import { getReadingText } from '../../core/api/fn/reading/get-reading-text';
import { listReadingTexts } from '../../core/api/fn/reading/list-reading-texts';
import { CefrLevel } from '../../core/api/models/cefr-level';
import { ErrorAlert } from '../../shared/error-alert';
import { CEFR_LEVELS } from '../dictionary/vocabulary-labels';
import { Reader } from './reader';

const OWN_TEXT_KEY = 'ep-own-text';

/** Минут чтения при ~150 словах в минуту для изучающего язык. */
export function readingMinutes(words: number): number {
  return Math.max(1, Math.round(words / 150));
}

/** Библиотека текстов (/reading): по уровням, плюс «Свой текст». */
@Component({
  selector: 'app-reading-list',
  imports: [RouterLink, NgIcon, ErrorAlert, HlmBadgeImports, HlmButtonImports, HlmCardImports, HlmSpinnerImports],
  providers: [provideIcons({ lucideBookOpenText, lucideClipboardPaste, lucideClock })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <h1 class="text-2xl font-semibold tracking-tight">Чтение</h1>
        <a hlmBtn variant="outline" size="sm" routerLink="/reading/own">
          <ng-icon name="lucideClipboardPaste" />
          Свой текст
        </a>
      </div>
      <p class="text-muted-foreground -mt-2 text-sm">Читайте и нажимайте на незнакомые слова: перевод, произношение и «в мои слова» — не отрываясь от текста.</p>

      <div class="flex flex-wrap gap-1.5" role="group" aria-label="Уровень">
        <button hlmBtn size="sm" type="button" [variant]="level() === null ? 'default' : 'outline'" (click)="level.set(null)">Все</button>
        @for (l of levels; track l) {
          <button hlmBtn size="sm" type="button" [variant]="level() === l ? 'default' : 'outline'" (click)="level.set(l)">{{ l }}</button>
        }
      </div>

      @if (texts.error()) {
        <app-error-alert message="Не удалось загрузить тексты." />
      }
      <ul class="flex flex-col gap-2 transition-opacity" [class.opacity-60]="texts.isLoading()">
        @for (t of texts.value()?.items ?? []; track t.id) {
          <li>
            <a hlmCard [routerLink]="['/reading', t.id]" class="hover:border-primary/40 gap-2 py-4 transition-colors">
              <div class="flex items-center gap-2 px-5">
                <span hlmBadge variant="secondary">{{ t.level }}</span>
                @if (t.topic) {
                  <span class="text-muted-foreground text-xs">{{ t.topic }}</span>
                }
                <span class="text-muted-foreground ms-auto flex items-center gap-1 text-xs"><ng-icon name="lucideClock" /> {{ minutes(+t.wordCount) }} мин</span>
              </div>
              <p class="px-5 text-lg font-semibold" lang="en">{{ t.title }}</p>
              <p class="text-muted-foreground line-clamp-2 px-5 text-sm" lang="en">{{ t.preview }}</p>
            </a>
          </li>
        } @empty {
          @if (!texts.isLoading()) {
            <li class="text-muted-foreground py-6 text-center">Текстов этого уровня пока нет.</li>
          }
        }
      </ul>
    </div>
  `,
})
export class ReadingList {
  private readonly api = inject(Api);

  protected readonly levels = CEFR_LEVELS;
  protected readonly level = signal<CefrLevel | null>(null);
  protected readonly minutes = readingMinutes;
  protected readonly texts = resource({
    params: () => ({ level: this.level() ?? undefined, pageSize: 50 }),
    loader: ({ params }) => this.api.invoke(listReadingTexts, params),
  });
}

/** Текст из библиотеки (/reading/:id). */
@Component({
  selector: 'app-reading-text',
  imports: [RouterLink, NgIcon, Reader, ErrorAlert, HlmBadgeImports, HlmButtonImports, HlmSpinnerImports],
  providers: [provideIcons({ lucideArrowLeft })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4">
      <a hlmBtn variant="ghost" size="sm" routerLink="/reading" class="self-start">
        <ng-icon name="lucideArrowLeft" />
        Все тексты
      </a>
      @if (text.error()) {
        <app-error-alert message="Текст не найден." />
      } @else if (text.value(); as t) {
        <header class="flex flex-col gap-1">
          <div class="flex items-center gap-2">
            <span hlmBadge variant="secondary">{{ t.level }}</span>
            @if (t.topic) {
              <span class="text-muted-foreground text-sm">{{ t.topic }}</span>
            }
          </div>
          <h1 class="text-2xl font-semibold tracking-tight" lang="en">{{ t.title }}</h1>
        </header>
        <app-reader [body]="t.body" />
      } @else {
        <div class="text-muted-foreground flex items-center gap-2"><hlm-spinner aria-label="Загрузка" /> Загружаем…</div>
      }
    </div>
  `,
})
export class ReadingTextPage {
  private readonly api = inject(Api);

  readonly id = input.required<string>();
  protected readonly text = resource({
    params: () => ({ id: this.id() }),
    loader: ({ params }) => this.api.invoke(getReadingText, params),
  });
}

/** Свой текст (/reading/own): вставить статью, письмо, субтитры — и читать с переводом по нажатию. */
@Component({
  selector: 'app-reading-own',
  imports: [FormsModule, RouterLink, NgIcon, Reader, HlmButtonImports, HlmTextareaImports],
  providers: [provideIcons({ lucideArrowLeft })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4">
      <a hlmBtn variant="ghost" size="sm" routerLink="/reading" class="self-start">
        <ng-icon name="lucideArrowLeft" />
        Чтение
      </a>
      <h1 class="text-2xl font-semibold tracking-tight">Свой текст</h1>
      @if (reading()) {
        <button hlmBtn variant="outline" size="sm" type="button" class="self-start" (click)="reading.set(false)">Изменить текст</button>
        <app-reader [body]="draft()" />
      } @else {
        <p class="text-muted-foreground -mt-2 text-sm">Вставьте статью, письмо, текст песни или субтитры на английском. Текст остаётся только в этом браузере.</p>
        <textarea
          hlmTextarea
          rows="12"
          lang="en"
          class="text-base"
          aria-label="Текст на английском"
          placeholder="Paste any English text here…"
          [ngModel]="draft()"
          (ngModelChange)="draft.set($event)"
        ></textarea>
        <button hlmBtn type="button" class="h-12 w-full sm:w-auto sm:self-start" [disabled]="!canRead()" (click)="read()">Читать</button>
      }
    </div>
  `,
})
export class ReadingOwn {
  protected readonly draft = signal(load());
  protected readonly reading = signal(false);
  protected readonly canRead = computed(() => /[A-Za-z]{2,}/.test(this.draft()));

  protected read(): void {
    try {
      localStorage.setItem(OWN_TEXT_KEY, this.draft().slice(0, 20000));
    } catch {
      // приватный режим — просто не запомним
    }
    this.reading.set(true);
  }
}

function load(): string {
  try {
    return localStorage.getItem(OWN_TEXT_KEY) ?? '';
  } catch {
    return '';
  }
}

import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideArrowLeft, lucideArrowRight, lucideCheck, lucideWandSparkles } from '@ng-icons/lucide';
import { toast } from '@spartan-ng/brain/sonner';
import { HlmBadgeImports } from '@spartan-ng/helm/badge';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmCheckboxImports } from '@spartan-ng/helm/checkbox';
import { HlmSpinnerImports } from '@spartan-ng/helm/spinner';
import { Api } from '../../core/api/api';
import { applyMyWordSuggestions } from '../../core/api/fn/my-words/apply-my-word-suggestions';
import { suggestMyWords } from '../../core/api/fn/my-words/suggest-my-words';
import { ApplySuggestion } from '../../core/api/models/apply-suggestion';
import { WordSuggestionDto } from '../../core/api/models/word-suggestion-dto';
import { problemMessage } from '../../core/http/problem';
import { ErrorAlert } from '../../shared/error-alert';
import { LEXEME_TYPE_LABELS, PART_OF_SPEECH_LABELS, formatTranscription } from './vocabulary-labels';

const PAGE = 15;

export type Field = 'text' | 'type' | 'level' | 'partOfSpeech' | 'transcription' | 'example';

export interface Change {
  field: Field;
  label: string;
  from: string;
  to: string;
}

/** Предложения по слову в виде строк «было → стало» — по ним ставят галочки. */
export function changesOf(s: WordSuggestionDto): Change[] {
  const changes: Change[] = [];
  if (s.suggestedText) {
    const tr = s.suggestedTranslations?.length ? ` (${s.suggestedTranslations.join(', ')})` : '';
    changes.push({ field: 'text', label: 'Начальная форма', from: s.text, to: s.suggestedText + tr });
  }
  if (s.suggestedType) {
    changes.push({ field: 'type', label: 'Тип', from: LEXEME_TYPE_LABELS[s.type], to: LEXEME_TYPE_LABELS[s.suggestedType] });
  }
  if (s.suggestedLevel) {
    changes.push({ field: 'level', label: 'Уровень', from: s.level ?? '—', to: s.suggestedLevel });
  }
  if (s.suggestedPartOfSpeech) {
    changes.push({ field: 'partOfSpeech', label: 'Часть речи', from: '—', to: PART_OF_SPEECH_LABELS[s.suggestedPartOfSpeech] });
  }
  if (s.suggestedTranscription) {
    changes.push({ field: 'transcription', label: 'Транскрипция', from: '—', to: formatTranscription(s.suggestedTranscription) });
  }
  if (s.suggestedExample) {
    changes.push({ field: 'example', label: 'Пример', from: '—', to: s.suggestedExample });
  }
  return changes;
}

/** Тело запроса: только отмеченные правки. Переводы начальной формы идут вместе с ней. */
export function toApply(s: WordSuggestionDto, picked: ReadonlySet<Field>): ApplySuggestion {
  return {
    userWordId: s.userWordId,
    text: picked.has('text') ? s.suggestedText : null,
    translations: picked.has('text') ? (s.suggestedTranslations ?? null) : s.translations.length === 0 ? (s.suggestedTranslations ?? null) : null,
    type: picked.has('type') ? s.suggestedType : null,
    level: picked.has('level') ? s.suggestedLevel : null,
    partOfSpeech: picked.has('partOfSpeech') ? s.suggestedPartOfSpeech : null,
    transcription: picked.has('transcription') ? s.suggestedTranscription : null,
    example: picked.has('example') ? s.suggestedExample : null,
  };
}

/** «Улучшить словарь» (/dictionary/improve): проверка личного словаря по общей базе, Викисловарю и спискам CEFR. */
@Component({
  selector: 'app-improve-words',
  imports: [RouterLink, NgIcon, ErrorAlert, HlmBadgeImports, HlmButtonImports, HlmCardImports, HlmCheckboxImports, HlmSpinnerImports],
  providers: [provideIcons({ lucideArrowLeft, lucideArrowRight, lucideCheck, lucideWandSparkles })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4">
      <a hlmBtn variant="ghost" size="sm" routerLink="/dictionary" class="self-start">
        <ng-icon name="lucideArrowLeft" />
        Словарь
      </a>

      <section hlmCard>
        <div hlmCardHeader>
          <span class="bg-primary/10 text-primary mb-2 flex size-12 items-center justify-center rounded-2xl">
            <ng-icon name="lucideWandSparkles" class="text-2xl" />
          </span>
          <h1 hlmCardTitle class="text-2xl">Улучшить словарь</h1>
          <p hlmCardDescription>
            Сверим ваши слова с общей базой, Викисловарём и списками уровней CEFR: поправим уровень и тип
            (фразовый глагол, идиома), добавим транскрипцию и часть речи, предложим начальную форму (buried → bury).
            Ничего не меняется без вашей галочки.
          </p>
        </div>
        <div hlmCardContent class="flex flex-col gap-3">
          @if (phase() === 'idle') {
            <button hlmBtn type="button" class="h-12 w-full sm:w-auto sm:self-start" (click)="check()">
              Проверить словарь
              <ng-icon name="lucideArrowRight" />
            </button>
          } @else {
            <div class="flex items-center gap-3">
              <div class="bg-muted h-2 flex-1 overflow-hidden rounded-full" role="progressbar" [attr.aria-valuenow]="checked()" [attr.aria-valuemax]="total()">
                <div class="bg-primary h-full rounded-full transition-all" [style.width.%]="total() ? (checked() / total()) * 100 : 0"></div>
              </div>
              <span class="text-muted-foreground text-sm tabular-nums">{{ checked() }} / {{ total() }}</span>
            </div>
            @if (phase() === 'checking') {
              <p class="text-muted-foreground flex items-center gap-2 text-sm"><hlm-spinner aria-label="Проверяем" /> Проверяем — новые слова ищем в словаре, это может занять минуту.</p>
            } @else {
              <p class="text-sm">
                @if (suggestions().length === 0) {
                  Всё в порядке — исправлять нечего.
                } @else {
                  Есть что улучшить в {{ suggestions().length }} словах.
                  @for (s of summary(); track s.label) {
                    <span hlmBadge variant="secondary" class="ms-1">{{ s.label }}: {{ s.count }}</span>
                  }
                }
              </p>
            }
          }
          @if (error(); as message) {
            <app-error-alert [message]="message" />
          }
        </div>
      </section>

      @if (suggestions().length > 0) {
        <div class="bg-background/90 sticky top-14 z-[5] -mx-4 flex flex-wrap items-center gap-2 px-4 py-2 backdrop-blur">
          <button hlmBtn variant="ghost" size="sm" type="button" (click)="pickAll(true)">Отметить всё</button>
          <button hlmBtn variant="ghost" size="sm" type="button" (click)="pickAll(false)">Снять всё</button>
          <button hlmBtn size="sm" type="button" class="ms-auto" [disabled]="applying() || pickedCount() === 0 || phase() === 'checking'" (click)="apply()">
            @if (applying()) {
              <hlm-spinner aria-label="Применяем" />
            } @else {
              <ng-icon name="lucideCheck" />
            }
            Применить ({{ pickedCount() }})
          </button>
        </div>

        <ul class="flex flex-col gap-2">
          @for (s of suggestions(); track s.userWordId) {
            <li hlmCard class="gap-2 py-4">
              <div class="flex flex-wrap items-baseline gap-2 px-5">
                <span class="text-lg font-semibold" lang="en">{{ s.text }}</span>
                <span class="text-muted-foreground text-sm">{{ s.translations.join(', ') }}</span>
              </div>
              <ul class="flex flex-col gap-1 px-5">
                @for (c of changes(s); track c.field) {
                  <li>
                    <label class="hover:bg-muted/60 -mx-2 flex cursor-pointer items-start gap-3 rounded-xl px-2 py-1.5 text-sm">
                      <hlm-checkbox class="mt-0.5" [checked]="isPicked(s.userWordId, c.field)" (checkedChange)="setPicked(s.userWordId, c.field, $event)" />
                      <span class="text-muted-foreground w-28 shrink-0">{{ c.label }}</span>
                      <span class="min-w-0 flex-1">
                        @if (c.from !== '—') {
                          <span class="text-muted-foreground line-through">{{ c.from }}</span> →
                        }
                        <span class="font-medium" [attr.lang]="c.field === 'example' || c.field === 'text' ? 'en' : null">{{ c.to }}</span>
                      </span>
                    </label>
                  </li>
                }
              </ul>
            </li>
          }
        </ul>
      }
    </div>
  `,
})
export class ImproveWords {
  private readonly api = inject(Api);

  protected readonly phase = signal<'idle' | 'checking' | 'done'>('idle');
  protected readonly checked = signal(0);
  protected readonly total = signal(0);
  protected readonly suggestions = signal<WordSuggestionDto[]>([]);
  protected readonly picked = signal<ReadonlyMap<string, ReadonlySet<Field>>>(new Map());
  protected readonly applying = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly changes = changesOf;
  protected readonly pickedCount = computed(() => [...this.picked().values()].filter((f) => f.size > 0).length);
  protected readonly summary = computed(() => {
    const counts = new Map<string, number>();
    for (const s of this.suggestions()) {
      for (const c of changesOf(s)) {
        counts.set(c.label, (counts.get(c.label) ?? 0) + 1);
      }
    }
    return [...counts].map(([label, count]) => ({ label, count }));
  });

  protected async check(): Promise<void> {
    this.phase.set('checking');
    this.error.set(null);
    this.suggestions.set([]);
    this.picked.set(new Map());
    this.checked.set(0);
    try {
      let offset = 0;
      for (;;) {
        const page = await this.api.invoke(suggestMyWords, { offset, limit: PAGE });
        this.total.set(Number(page.total));
        this.suggestions.update((list) => [...list, ...page.items]);
        // Всё предложенное отмечено сразу — снять галочку проще, чем ставить сотню.
        this.picked.update((map) => {
          const next = new Map(map);
          for (const s of page.items) {
            next.set(s.userWordId, new Set(changesOf(s).map((c) => c.field)));
          }
          return next;
        });
        offset += Number(page.checked);
        this.checked.set(offset);
        if (Number(page.checked) === 0 || offset >= Number(page.total)) {
          break;
        }
      }
      this.phase.set('done');
    } catch (e) {
      this.error.set(problemMessage(e, 'Проверка прервалась. Можно запустить ещё раз — проверенное запомнилось в базе.'));
      this.phase.set('done');
    }
  }

  protected isPicked(id: string, field: Field): boolean {
    return this.picked().get(id)?.has(field) ?? false;
  }

  protected setPicked(id: string, field: Field, on: boolean): void {
    this.picked.update((map) => {
      const next = new Map(map);
      const fields = new Set(next.get(id) ?? []);
      if (on) {
        fields.add(field);
      } else {
        fields.delete(field);
      }
      next.set(id, fields);
      return next;
    });
  }

  protected pickAll(on: boolean): void {
    this.picked.set(new Map(this.suggestions().map((s) => [s.userWordId, on ? new Set(changesOf(s).map((c) => c.field)) : new Set<Field>()])));
  }

  protected async apply(): Promise<void> {
    const items = this.suggestions()
      .filter((s) => (this.picked().get(s.userWordId)?.size ?? 0) > 0)
      .map((s) => toApply(s, this.picked().get(s.userWordId)!));
    this.applying.set(true);
    try {
      const result = await this.api.invoke(applyMyWordSuggestions, { body: { items } });
      const skipped = Number(result.skipped);
      toast.success(`Обновлено слов: ${result.updated}`, {
        description: skipped > 0 ? `Начальная форма уже есть в словаре у ${skipped} — их не переименовывали.` : undefined,
      });
      const done = new Set(items.map((i) => i.userWordId));
      this.suggestions.update((list) => list.filter((s) => !done.has(s.userWordId)));
    } catch (e) {
      toast.error(problemMessage(e, 'Не удалось применить правки.'));
    } finally {
      this.applying.set(false);
    }
  }
}

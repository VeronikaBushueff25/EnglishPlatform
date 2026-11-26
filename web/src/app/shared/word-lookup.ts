import { ChangeDetectionStrategy, Component, computed, inject, input, output, resource, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCheck, lucidePlus, lucideSparkles } from '@ng-icons/lucide';
import { toast } from '@spartan-ng/brain/sonner';
import { HlmBadgeImports } from '@spartan-ng/helm/badge';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmSpinnerImports } from '@spartan-ng/helm/spinner';
import { Api } from '../core/api/api';
import { lookupWord } from '../core/api/fn/dictionary/lookup-word';
import { addMyWord } from '../core/api/fn/my-words/add-my-word';
import { LexemeDto } from '../core/api/models/lexeme-dto';
import { LookupResultDto } from '../core/api/models/lookup-result-dto';
import { problemMessage } from '../core/http/problem';
import { LEXEME_TYPE_LABELS, PART_OF_SPEECH_LABELS, formatTranscription, fromBase } from '../features/dictionary/vocabulary-labels';
import { SpeakButton } from './pronunciation';

/** Похоже на английское слово или выражение — его есть смысл искать во внешнем словаре. */
export function looksEnglish(text: string): boolean {
  return /^[a-z][a-z' -]{0,60}$/i.test(text.trim());
}

/**
 * Слово из словаря: ищет в общей базе, а если нет — во внешнем словаре (и сохраняет в базу).
 * Транскрипция, озвучка, переводы, пример и «В мои слова». Используется в поиске словаря и при чтении текстов.
 */
@Component({
  selector: 'app-word-lookup',
  imports: [RouterLink, NgIcon, SpeakButton, HlmBadgeImports, HlmButtonImports, HlmSpinnerImports],
  providers: [provideIcons({ lucideCheck, lucidePlus, lucideSparkles })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (result.isLoading()) {
      <div class="text-muted-foreground flex items-center gap-2 py-3 text-sm">
        <hlm-spinner aria-label="Ищем" /> Ищем «{{ text() }}» в словаре…
      </div>
    } @else if (result.error()) {
      <p class="text-muted-foreground py-3 text-sm">Словарь сейчас недоступен. Попробуйте позже.</p>
    } @else if (view(); as r) {
      @if (r.items.length === 0) {
        <div class="text-muted-foreground flex flex-col gap-1 py-3 text-sm">
          <p>Не нашли «{{ text() }}» ни в базе, ни в словаре.</p>
          @if (showOwnWordLink()) {
            <a routerLink="/dictionary/words/new" class="text-primary font-medium underline-offset-4 hover:underline">Добавить своё слово</a>
          }
        </div>
      } @else {
        <div class="flex flex-col gap-3">
          @if (r.formOf) {
            <p class="text-muted-foreground text-sm">«{{ r.formOf }}» — форма слова <span class="text-foreground font-medium" lang="en">{{ r.items[0].text }}</span></p>
          }
          @for (item of r.items; track item.id) {
            <article class="flex flex-col gap-2" [class.border-t]="!$first" [class.pt-3]="!$first">
              <div class="flex items-start gap-2">
                <div class="min-w-0 flex-1">
                  <p class="flex flex-wrap items-baseline gap-x-2">
                    <span class="text-lg font-semibold" lang="en">{{ item.text }}</span>
                    @if (item.transcription) {
                      <span class="text-muted-foreground text-sm">{{ transcription(item.transcription) }}</span>
                    }
                  </p>
                  <div class="mt-1 flex flex-wrap gap-1.5">
                    @if (item.level) {
                      <span hlmBadge variant="secondary">{{ item.level }}</span>
                    }
                    @if (item.partOfSpeech) {
                      <span hlmBadge variant="outline">{{ posLabels[item.partOfSpeech] }}</span>
                    } @else if (item.type !== 'Word') {
                      <span hlmBadge variant="outline">{{ typeLabels[item.type] }}</span>
                    }
                  </div>
                </div>
                <app-speak-button [text]="item.text" [audioUrl]="item.audioUrl" />
                @if (item.myWordId || added().has(item.id)) {
                  <span class="text-success flex h-9 items-center gap-1 px-2 text-sm font-medium">
                    <ng-icon name="lucideCheck" /> В словаре
                  </span>
                } @else {
                  <button hlmBtn size="sm" type="button" class="h-9" [disabled]="adding().has(item.id)" (click)="add(item)">
                    @if (adding().has(item.id)) {
                      <hlm-spinner aria-label="Добавляем" />
                    } @else {
                      <ng-icon name="lucidePlus" />
                    }
                    В мои слова
                  </button>
                }
              </div>
              <p class="text-base">{{ translations(item) }}</p>
              @if (item.examples[0]; as example) {
                <p class="text-muted-foreground text-sm italic" lang="en">{{ example.text }}</p>
              }
            </article>
          }
          @if (r.fromDictionary) {
            <p class="text-muted-foreground flex items-center gap-1.5 text-xs">
              <ng-icon name="lucideSparkles" /> Из Викисловаря (CC BY-SA) — слово добавлено в общую базу.
            </p>
          }
        </div>
      }
    }
  `,
})
export class WordLookup {
  private readonly api = inject(Api);

  readonly text = input.required<string>();
  readonly showOwnWordLink = input(true);
  readonly wordAdded = output<LexemeDto>();

  protected readonly typeLabels = LEXEME_TYPE_LABELS;
  protected readonly posLabels = PART_OF_SPEECH_LABELS;
  protected readonly transcription = formatTranscription;
  protected readonly adding = signal<ReadonlySet<string>>(new Set());
  protected readonly added = signal<ReadonlySet<string>>(new Set());

  protected readonly result = resource({
    params: () => ({ text: this.text().trim() }),
    loader: ({ params }) => this.api.invoke(lookupWord, params),
  });
  protected readonly view = computed<LookupResultDto | undefined>(() => (this.result.hasValue() ? this.result.value() : undefined));

  protected translations(item: LexemeDto): string {
    return item.translations.map((t) => t.text).join(', ');
  }

  protected async add(item: LexemeDto): Promise<void> {
    this.adding.update((s) => new Set(s).add(item.id));
    try {
      await this.api.invoke(addMyWord, { body: fromBase(item.id) });
      this.added.update((s) => new Set(s).add(item.id));
      this.wordAdded.emit(item);
      toast.success(`«${item.text}» в вашем словаре`);
    } catch (e) {
      toast.error(problemMessage(e, 'Не удалось добавить слово.'));
    } finally {
      this.adding.update((s) => {
        const next = new Set(s);
        next.delete(item.id);
        return next;
      });
    }
  }
}

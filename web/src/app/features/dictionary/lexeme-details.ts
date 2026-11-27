import { ChangeDetectionStrategy, Component, computed, inject, input, resource, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideArrowLeft, lucideCheck, lucidePlus } from '@ng-icons/lucide';
import { toast } from '@spartan-ng/brain/sonner';
import { HlmBadgeImports } from '@spartan-ng/helm/badge';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmSpinnerImports } from '@spartan-ng/helm/spinner';
import { Api } from '../../core/api/api';
import { addMyWord, getLexeme } from '../../core/api/functions';
import { AuthService } from '../../core/auth/auth.service';
import { problemMessage } from '../../core/http/problem';
import { ErrorAlert } from '../../shared/error-alert';
import { SpeakButton } from '../../shared/pronunciation';
import { LEXEME_TYPE_LABELS, PART_OF_SPEECH_LABELS, formatTranscription, fromBase } from './vocabulary-labels';

/** Карточка слова общей базы: переводы, примеры, категории, добавление в свой словарь. */
@Component({
  selector: 'app-lexeme-details',
  imports: [RouterLink, NgIcon, ErrorAlert, SpeakButton, HlmBadgeImports, HlmButtonImports, HlmCardImports, HlmSpinnerImports],
  providers: [provideIcons({ lucideArrowLeft, lucideCheck, lucidePlus })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4">
      <a hlmBtn variant="ghost" size="sm" routerLink="/dictionary" [queryParams]="{ tab: 'base' }" class="self-start">
        <ng-icon name="lucideArrowLeft" />
        Общая база
      </a>

      @if (loadError(); as message) {
        <app-error-alert [message]="message" />
      } @else if (lexeme.hasValue()) {
        @let l = lexeme.value();
        <section hlmCard>
          <div hlmCardHeader>
            <div class="flex items-start gap-2">
              <div class="flex min-w-0 flex-1 flex-col gap-1">
                <h1 class="text-3xl font-semibold tracking-tight break-words">{{ l.text }}</h1>
                <span class="text-muted-foreground font-mono">{{ transcription(l.transcription) }}</span>
              </div>
              <app-speak-button [text]="l.text" [audioUrl]="l.audioUrl" />
            </div>
            <div class="flex flex-wrap gap-1.5 pt-2">
              @if (l.level) {
                <span hlmBadge variant="secondary">{{ l.level }}</span>
              }
              <span hlmBadge variant="outline">{{ typeLabels[l.type] }}</span>
              @if (l.partOfSpeech) {
                <span hlmBadge variant="outline">{{ posLabels[l.partOfSpeech] }}</span>
              }
            </div>
          </div>

          <div hlmCardContent class="flex flex-col gap-5">
            <div>
              <h2 class="text-muted-foreground mb-1 text-sm font-medium">Перевод</h2>
              <p translate="no" class="text-lg">{{ translations() }}</p>
            </div>

            @if (l.examples.length > 0) {
              <div>
                <h2 class="text-muted-foreground mb-2 text-sm font-medium">Примеры</h2>
                <ul class="flex flex-col gap-3">
                  @for (example of l.examples; track $index) {
                    <li class="border-primary/30 border-s-2 ps-3">
                      <div class="flex items-start gap-1">
                        <p class="flex-1 italic">{{ example.text }}</p>
                        <app-speak-button [text]="example.text" />
                      </div>
                      @if (example.translation) {
                        <p translate="no" class="text-muted-foreground text-sm">{{ example.translation }}</p>
                      }
                    </li>
                  }
                </ul>
              </div>
            }

            @if (l.categories.length > 0) {
              <div class="flex flex-wrap gap-1.5">
                @for (category of l.categories; track category.id) {
                  <span hlmBadge variant="outline">{{ category.name }}</span>
                }
              </div>
            }
          </div>

          <div hlmCardFooter class="flex flex-col gap-2 sm:flex-row">
            @if (myWordId(); as wordId) {
              <a hlmBtn variant="outline" class="h-11 w-full sm:w-auto" [routerLink]="['/dictionary/words', wordId]">
                <ng-icon name="lucideCheck" class="text-success!" />
                В вашем словаре
              </a>
            } @else {
              <button hlmBtn type="button" class="h-11 w-full sm:w-auto" [disabled]="adding()" (click)="add()">
                @if (adding()) {
                  <hlm-spinner aria-label="Добавляем" />
                } @else {
                  <ng-icon name="lucidePlus" />
                }
                Добавить в мой словарь
              </button>
            }
            @if (auth.isAdmin()) {
              <a hlmBtn variant="ghost" class="h-11 w-full sm:w-auto" [routerLink]="['/admin/lexemes', l.id]">Редактировать в админке</a>
            }
          </div>
        </section>
      } @else {
        <div class="text-muted-foreground flex items-center gap-2"><hlm-spinner aria-label="Загрузка" /> Загружаем…</div>
      }
    </div>
  `,
})
export class LexemeDetails {
  private readonly api = inject(Api);
  protected readonly auth = inject(AuthService);

  readonly id = input.required<string>();

  protected readonly typeLabels = LEXEME_TYPE_LABELS;
  protected readonly posLabels = PART_OF_SPEECH_LABELS;
  protected readonly transcription = formatTranscription;

  protected readonly lexeme = resource({
    params: () => ({ id: this.id() }),
    loader: ({ params }) => this.api.invoke(getLexeme, params),
  });

  protected readonly loadError = computed(() =>
    this.lexeme.error() ? problemMessage(this.lexeme.error(), 'Не удалось загрузить слово.') : null,
  );
  protected readonly translations = computed(() =>
    this.lexeme.hasValue() ? this.lexeme.value().translations.map((t) => t.text).join(', ') : '',
  );
  protected readonly myWordId = computed(() => (this.lexeme.hasValue() ? this.lexeme.value().myWordId : null));
  protected readonly adding = signal(false);

  protected async add(): Promise<void> {
    this.adding.set(true);
    try {
      const word = await this.api.invoke(addMyWord, { body: fromBase(this.id()) });
      this.lexeme.update((l) => (l ? { ...l, myWordId: word.id } : l));
      toast.success(`«${word.text}» в вашем словаре`);
    } catch (e) {
      toast.error(problemMessage(e, 'Не удалось добавить слово.'));
    } finally {
      this.adding.set(false);
    }
  }
}

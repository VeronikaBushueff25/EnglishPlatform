import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, linkedSignal, resource, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCheck, lucideFileUp, lucideListChecks, lucidePlus } from '@ng-icons/lucide';
import { toast } from '@spartan-ng/brain/sonner';
import { HlmBadgeImports } from '@spartan-ng/helm/badge';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmSpinnerImports } from '@spartan-ng/helm/spinner';
import { Api } from '../../../core/api/api';
import { adminBulkUpdateLexemes, adminPublishMyWords, listCategories, listLexemes } from '../../../core/api/functions';
import { problemMessage } from '../../../core/http/problem';
import { ConfirmDelete } from '../../../shared/confirm-delete';
import { ErrorAlert } from '../../../shared/error-alert';
import { Pager } from '../../../shared/pager';
import { BulkBar, BulkChange } from '../../dictionary/bulk-bar';
import { EMPTY_FILTER, LexemeFilterValue, LexemeFilters, filterQuery } from '../../dictionary/lexeme-filters';
import { LEXEME_TYPE_LABELS, PART_OF_SPEECH_LABELS, formatTranscription, plural } from '../../dictionary/vocabulary-labels';
import { AdminNav } from '../admin-nav';
import { CoreImport } from './core-import';

const PAGE_SIZE = 30;

/**
 * Общая база лексики в админке: поиск, правка, создание, импорт, уровень и категория сразу для многих слов,
 * перенос своих слов администратора в базу.
 */
@Component({
  selector: 'app-admin-lexemes',
  imports: [
    CoreImport,
    NgTemplateOutlet,
    RouterLink,
    NgIcon,
    AdminNav,
    BulkBar,
    ConfirmDelete,
    ErrorAlert,
    LexemeFilters,
    Pager,
    HlmBadgeImports,
    HlmButtonImports,
    HlmCardImports,
    HlmSpinnerImports,
  ],
  providers: [provideIcons({ lucideCheck, lucideFileUp, lucideListChecks, lucidePlus })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4">
      <app-admin-nav />
      <app-core-import />

      <div class="flex flex-wrap items-center justify-between gap-2">
        <h1 class="text-2xl font-semibold tracking-tight">Слова общей базы</h1>
        <div class="flex gap-2">
          <a hlmBtn variant="outline" size="sm" routerLink="/admin/lexemes/import" aria-label="Импорт из CSV">
            <ng-icon name="lucideFileUp" />
            <span class="hidden sm:inline">Импорт</span>
          </a>
          <a hlmBtn size="sm" routerLink="/admin/lexemes/new">
            <ng-icon name="lucidePlus" />
            Новое слово
          </a>
        </div>
      </div>

      <app-confirm-delete
        [destructive]="false"
        label="Перенести мои слова в базу"
        confirmLabel="Перенести"
        triggerClass="h-10 w-full sm:w-auto sm:self-start"
        title="Перенести ваши слова в общую базу?"
        description="Свои слова из «Моих слов» станут словами общей базы, их смогут добавлять все. В вашем словаре они останутся. Тип и часть речи определятся по заметке («Глагол; …», «Фразовый глагол»). Слова, которые в базе уже есть, не задвоятся."
        [disabled]="publishing()"
        (confirmed)="publishMyWords()"
      />

      <app-lexeme-filters [(value)]="filter" [categories]="categoryList()" />

      @if (loadError(); as message) {
        <app-error-alert [message]="message" />
      }

      <div class="-mb-2 flex min-h-8 items-center justify-between gap-2">
        <p class="text-muted-foreground text-sm">Всего: {{ totalCount() }}</p>
        @if (totalCount() > 0 && !selecting()) {
          <button hlmBtn variant="ghost" size="sm" type="button" (click)="toggleSelecting()">
            <ng-icon name="lucideListChecks" />
            Выбрать
          </button>
        }
      </div>

      <ul class="flex flex-col gap-2 transition-opacity" [class.opacity-60]="lexemes.isLoading()">
        @if (lexemes.hasValue()) {
          @for (lexeme of lexemes.value().items; track lexeme.id) {
            <li>
              <ng-template #body>
                <div class="flex items-start gap-3 p-4">
                  @if (selecting()) {
                    <span
                      class="mt-1 flex size-5 shrink-0 items-center justify-center rounded-md border transition-colors"
                      [class]="selected().has(lexeme.id) ? 'bg-primary border-primary text-primary-foreground' : 'border-input'"
                      aria-hidden="true"
                    >
                      @if (selected().has(lexeme.id)) {
                        <ng-icon name="lucideCheck" class="text-sm" />
                      }
                    </span>
                  }
                  <div class="flex min-w-0 flex-col gap-1">
                    <div class="flex flex-wrap items-baseline gap-x-2">
                      <span class="font-semibold break-words">{{ lexeme.text }}</span>
                      <span class="text-muted-foreground font-mono text-sm">{{ transcription(lexeme.transcription) }}</span>
                    </div>
                    <span class="text-sm break-words">{{ lexeme.translations.join(', ') }}</span>
                    <div class="flex flex-wrap gap-1.5 pt-1">
                      @if (lexeme.level) {
                        <span hlmBadge variant="secondary">{{ lexeme.level }}</span>
                      } @else {
                        <span hlmBadge variant="outline" class="text-muted-foreground border-dashed">без уровня</span>
                      }
                      <span hlmBadge variant="outline">{{ typeLabels[lexeme.type] }}</span>
                      @if (lexeme.partOfSpeech) {
                        <span hlmBadge variant="outline">{{ posLabels[lexeme.partOfSpeech] }}</span>
                      }
                    </div>
                  </div>
                </div>
              </ng-template>
              @if (selecting()) {
                <button
                  hlmCard
                  type="button"
                  class="w-full gap-0 py-0 text-start transition-colors"
                  [class.border-primary]="selected().has(lexeme.id)"
                  [attr.aria-pressed]="selected().has(lexeme.id)"
                  (click)="toggle(lexeme.id)"
                >
                  <ng-container [ngTemplateOutlet]="body" />
                </button>
              } @else {
                <a hlmCard [routerLink]="['/admin/lexemes', lexeme.id]" class="hover:border-primary/40 gap-0 py-0 transition-colors">
                  <ng-container [ngTemplateOutlet]="body" />
                </a>
              }
            </li>
          } @empty {
            <li class="text-muted-foreground py-6 text-center">Ничего не нашли.</li>
          }
        } @else if (lexemes.isLoading()) {
          <li class="text-muted-foreground flex items-center gap-2"><hlm-spinner aria-label="Загрузка" /> Загружаем…</li>
        }
      </ul>

      <app-pager [(page)]="page" [totalPages]="totalPages()" [disabled]="lexemes.isLoading()" />

      @if (selecting()) {
        <app-bulk-bar
          [count]="selected().size"
          [categories]="categoryList()"
          [busy]="applying()"
          (apply)="applyBulk($event)"
          (selectPage)="selectPage()"
          (cancel)="toggleSelecting()"
        />
      }
    </div>
  `,
})
export class AdminLexemes {
  private readonly api = inject(Api);

  protected readonly typeLabels = LEXEME_TYPE_LABELS;
  protected readonly posLabels = PART_OF_SPEECH_LABELS;
  protected readonly transcription = formatTranscription;

  protected readonly filter = signal<LexemeFilterValue>(EMPTY_FILTER);
  protected readonly page = linkedSignal({ source: this.filter, computation: () => 1 });
  protected readonly categories = resource({ loader: () => this.api.invoke(listCategories) });
  protected readonly categoryList = computed(() => (this.categories.hasValue() ? this.categories.value() : []));

  protected readonly lexemes = resource({
    params: () => ({ ...filterQuery(this.filter()), page: this.page(), pageSize: PAGE_SIZE }),
    loader: ({ params }) => this.api.invoke(listLexemes, params),
  });

  protected readonly totalCount = computed(() => (this.lexemes.hasValue() ? Number(this.lexemes.value().totalCount) : 0));
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / PAGE_SIZE)));
  protected readonly loadError = computed(() =>
    this.lexemes.error() ? problemMessage(this.lexemes.error(), 'Не удалось загрузить слова.') : null,
  );

  protected readonly selecting = signal(false);
  protected readonly selected = signal<ReadonlySet<string>>(new Set());
  protected readonly applying = signal(false);
  protected readonly publishing = signal(false);

  protected toggleSelecting(): void {
    this.selecting.update((v) => !v);
    this.selected.set(new Set());
  }

  protected toggle(id: string): void {
    this.selected.update((set) => {
      const next = new Set(set);
      if (!next.delete(id)) {
        next.add(id);
      }
      return next;
    });
  }

  protected selectPage(): void {
    const items = this.lexemes.value()?.items ?? [];
    this.selected.update((set) => new Set([...set, ...items.map((l) => l.id)]));
  }

  protected async applyBulk(change: BulkChange): Promise<void> {
    this.applying.set(true);
    try {
      const result = await this.api.invoke(adminBulkUpdateLexemes, {
        body: { ids: [...this.selected()], level: change.level, addCategoryId: change.categoryId },
      });
      toast.success(`Обновлено: ${result.updated} ${plural(Number(result.updated))}`);
      this.selecting.set(false);
      this.selected.set(new Set());
      this.lexemes.reload();
      this.categories.reload();
    } catch (e) {
      toast.error(problemMessage(e, 'Не удалось обновить слова.'));
    } finally {
      this.applying.set(false);
    }
  }

  protected async publishMyWords(): Promise<void> {
    this.publishing.set(true);
    try {
      const result = await this.api.invoke(adminPublishMyWords);
      const added = Number(result.added);
      const linked = Number(result.updated);
      if (added + linked === 0) {
        toast.info('Своих слов, которых нет в базе, у вас нет');
      } else {
        toast.success(
          `В базу добавлено: ${added} ${plural(added)}` + (linked > 0 ? `; уже были в базе и привязаны: ${linked}` : ''),
        );
      }
      this.lexemes.reload();
    } catch (e) {
      toast.error(problemMessage(e, 'Не удалось перенести слова.'));
    } finally {
      this.publishing.set(false);
    }
  }
}

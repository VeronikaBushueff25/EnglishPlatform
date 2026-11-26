import { ChangeDetectionStrategy, Component, computed, inject, input, linkedSignal, resource, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCheck, lucideFileUp, lucideListChecks, lucidePlus } from '@ng-icons/lucide';
import { toast } from '@spartan-ng/brain/sonner';
import { HlmBadgeImports } from '@spartan-ng/helm/badge';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmSpinnerImports } from '@spartan-ng/helm/spinner';
import { Api } from '../../core/api/api';
import { addMyWord, bulkUpdateMyWords, listCategories, listLexemes, listMyWords } from '../../core/api/functions';
import { LexemeSummaryDto } from '../../core/api/models/lexeme-summary-dto';
import { problemMessage } from '../../core/http/problem';
import { ErrorAlert } from '../../shared/error-alert';
import { Pager } from '../../shared/pager';
import { SpeakButton } from '../../shared/pronunciation';
import { BulkBar, BulkChange } from './bulk-bar';
import { EMPTY_FILTER, LexemeFilterValue, LexemeFilters, filterQuery } from './lexeme-filters';
import { LEXEME_TYPE_LABELS, formatTranscription, fromBase, plural } from './vocabulary-labels';

const PAGE_SIZE = 20;

type Tab = 'mine' | 'base';

/** Словарь: свои слова и общая база с поиском и фильтрами. Вкладка хранится в адресе (?tab=base). */
@Component({
  selector: 'app-dictionary',
  imports: [
    NgTemplateOutlet,
    RouterLink,
    NgIcon,
    ErrorAlert,
    LexemeFilters,
    Pager,
    SpeakButton,
    HlmBadgeImports,
    HlmButtonImports,
    HlmCardImports,
    HlmSpinnerImports,
    BulkBar,
  ],
  providers: [provideIcons({ lucideCheck, lucideFileUp, lucideListChecks, lucidePlus })],
  templateUrl: './dictionary.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Dictionary {
  private readonly api = inject(Api);
  private readonly router = inject(Router);

  /** Параметр адреса ?tab=… */
  readonly tabParam = input<string | undefined>(undefined, { alias: 'tab' });
  protected readonly tab = computed<Tab>(() => (this.tabParam() === 'base' ? 'base' : 'mine'));

  protected readonly typeLabels = LEXEME_TYPE_LABELS;
  protected readonly transcription = formatTranscription;

  protected readonly filter = signal<LexemeFilterValue>(EMPTY_FILTER);
  /** При смене вкладки или фильтра возвращаемся на первую страницу. */
  protected readonly page = linkedSignal({ source: () => [this.tab(), this.filter()], computation: () => 1 });

  protected readonly categories = resource({ loader: () => this.api.invoke(listCategories) });

  private readonly query = computed(() => ({ ...filterQuery(this.filter()), page: this.page(), pageSize: PAGE_SIZE }));

  protected readonly myWords = resource({
    params: () => (this.tab() === 'mine' ? this.query() : undefined),
    loader: ({ params }) => this.api.invoke(listMyWords, params),
  });

  protected readonly lexemes = resource({
    params: () => (this.tab() === 'base' ? this.query() : undefined),
    loader: ({ params }) => this.api.invoke(listLexemes, params),
  });

  /** Состояние списка активной вкладки. */
  private readonly currentPage = computed(() => (this.tab() === 'mine' ? this.myWords.value() : this.lexemes.value()));
  protected readonly loaded = computed(() => this.currentPage() !== undefined);
  protected readonly loading = computed(() => (this.tab() === 'mine' ? this.myWords.isLoading() : this.lexemes.isLoading()));
  protected readonly totalCount = computed(() => Number(this.currentPage()?.totalCount ?? 0));
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / PAGE_SIZE)));
  protected readonly loadError = computed(() => {
    const error = this.tab() === 'mine' ? this.myWords.error() : this.lexemes.error();
    return error ? problemMessage(error, 'Не удалось загрузить слова.') : null;
  });
  protected readonly filtered = computed(() => {
    const f = this.filter();
    return !!(f.search || f.type || f.level || f.categoryId);
  });

  /** id слов общей базы, которые сейчас добавляются. */
  protected readonly adding = signal<ReadonlySet<string>>(new Set());

  /** Режим выбора слов для массового действия (только «Мои слова»). */
  protected readonly selecting = signal(false);
  protected readonly selected = signal<ReadonlySet<string>>(new Set());
  protected readonly applying = signal(false);

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
    const items = this.myWords.value()?.items ?? [];
    this.selected.update((set) => new Set([...set, ...items.map((w) => w.id)]));
  }

  protected async applyBulk(change: BulkChange): Promise<void> {
    if (!change.level) {
      return;
    }
    this.applying.set(true);
    try {
      const result = await this.api.invoke(bulkUpdateMyWords, { body: { ids: [...this.selected()], level: change.level } });
      toast.success(`Уровень ${change.level}: ${result.updated} ${plural(Number(result.updated))}`);
      this.selecting.set(false);
      this.selected.set(new Set());
      this.myWords.reload();
    } catch (e) {
      toast.error(problemMessage(e, 'Не удалось проставить уровень.'));
    } finally {
      this.applying.set(false);
    }
  }

  protected selectTab(tab: Tab): void {
    this.selecting.set(false);
    this.selected.set(new Set());
    void this.router.navigate([], { queryParams: { tab: tab === 'base' ? 'base' : null }, replaceUrl: true });
  }

  protected async add(lexeme: LexemeSummaryDto): Promise<void> {
    this.adding.update((s) => new Set(s).add(lexeme.id));
    try {
      const word = await this.api.invoke(addMyWord, { body: fromBase(lexeme.id) });
      this.lexemes.update((page) =>
        page ? { ...page, items: page.items.map((l) => (l.id === lexeme.id ? { ...l, myWordId: word.id } : l)) } : page,
      );
      toast.success(`«${lexeme.text}» в вашем словаре`);
    } catch (e) {
      toast.error(problemMessage(e, 'Не удалось добавить слово.'));
    } finally {
      this.adding.update((s) => {
        const next = new Set(s);
        next.delete(lexeme.id);
        return next;
      });
    }
  }
}

import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, input, model, signal } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideSearch, lucideX } from '@ng-icons/lucide';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { HlmNativeSelectImports } from '@spartan-ng/helm/native-select';
import { CategoryDto } from '../../core/api/models/category-dto';
import { LexemeType } from '../../core/api/models/lexeme-type';
import { CEFR_LEVELS, CefrLevelValue, LEXEME_TYPES, LEXEME_TYPE_LABELS } from './vocabulary-labels';

const SEARCH_DEBOUNCE_MS = 300;

/** Значения фильтров списка слов. Пустая строка — «все». */
export interface LexemeFilterValue {
  search: string;
  type: LexemeType | '';
  /** 'none' — только слова без уровня. */
  level: CefrLevelValue | 'none' | '';
  categoryId: string;
}

export const EMPTY_FILTER: LexemeFilterValue = { search: '', type: '', level: '', categoryId: '' };

/** Параметры запроса списка из значений фильтров. */
export function filterQuery(f: LexemeFilterValue) {
  return {
    search: f.search || undefined,
    type: f.type || undefined,
    level: f.level && f.level !== 'none' ? f.level : undefined,
    noLevel: f.level === 'none' ? true : undefined,
    categoryId: f.categoryId || undefined,
  };
}

/** Поиск и фильтры по типу, уровню и категории. Поиск уходит наружу с задержкой, чтобы не дёргать API на каждую букву. */
@Component({
  selector: 'app-lexeme-filters',
  imports: [NgIcon, HlmButtonImports, HlmInputImports, HlmNativeSelectImports],
  providers: [provideIcons({ lucideSearch, lucideX })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-2">
      <div class="relative">
        <ng-icon name="lucideSearch" class="text-muted-foreground pointer-events-none absolute start-3.5 top-1/2 -translate-y-1/2" />
        <input
          hlmInput
          type="search"
          class="h-11 ps-10"
          [placeholder]="placeholder()"
          [attr.aria-label]="placeholder()"
          [value]="text()"
          (input)="onSearch($any($event.target).value)"
        />
      </div>

      <div class="grid grid-cols-2 gap-2 sm:grid-cols-3">
        <hlm-native-select class="w-full" selectClass="h-10" aria-label="Тип" [value]="value().type" (valueChange)="patch({ type: $any($event ?? '') })">
          <option value="">Все типы</option>
          @for (type of types; track type) {
            <option [value]="type">{{ typeLabels[type] }}</option>
          }
        </hlm-native-select>

        <hlm-native-select class="w-full" selectClass="h-10" aria-label="Уровень" [value]="value().level" (valueChange)="patch({ level: $any($event ?? '') })">
          <option value="">Все уровни</option>
          <option value="none">Без уровня</option>
          @for (level of levels; track level) {
            <option [value]="level">{{ level }}</option>
          }
        </hlm-native-select>

        @if (categories().length > 0) {
          <hlm-native-select
            class="col-span-2 w-full sm:col-span-1"
            selectClass="h-10"
            aria-label="Категория"
            [value]="value().categoryId"
            (valueChange)="patch({ categoryId: $event ?? '' })"
          >
            <option value="">Все категории</option>
            @for (category of categories(); track category.id) {
              <option [value]="category.id">{{ category.name }}</option>
            }
          </hlm-native-select>
        }
      </div>

      @if (active()) {
        <button hlmBtn variant="ghost" size="sm" type="button" class="self-start" (click)="reset()">
          <ng-icon name="lucideX" />
          Сбросить фильтры
        </button>
      }
    </div>
  `,
})
export class LexemeFilters {
  readonly value = model<LexemeFilterValue>(EMPTY_FILTER);
  readonly categories = input<CategoryDto[]>([]);
  readonly placeholder = input('Слово или перевод');

  protected readonly types = LEXEME_TYPES;
  protected readonly typeLabels = LEXEME_TYPE_LABELS;
  protected readonly levels = CEFR_LEVELS;

  /** То, что набрано в поле поиска; в value попадает после паузы. */
  protected readonly text = signal('');
  protected readonly active = computed(() => {
    const v = this.value();
    return !!(v.search || v.type || v.level || v.categoryId || this.text());
  });

  private timer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.timer));
  }

  protected onSearch(text: string): void {
    this.text.set(text);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => this.patch({ search: text.trim() }), SEARCH_DEBOUNCE_MS);
  }

  protected patch(change: Partial<LexemeFilterValue>): void {
    this.value.update((v) => ({ ...v, ...change }));
  }

  protected reset(): void {
    clearTimeout(this.timer);
    this.text.set('');
    this.value.set(EMPTY_FILTER);
  }
}

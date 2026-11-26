import { ChangeDetectionStrategy, Component, booleanAttribute, computed, input, output, signal } from '@angular/core';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmNativeSelectImports } from '@spartan-ng/helm/native-select';
import { HlmSpinnerImports } from '@spartan-ng/helm/spinner';
import { CategoryDto } from '../../core/api/models/category-dto';
import { CEFR_LEVELS, CefrLevelValue } from './vocabulary-labels';

export interface BulkChange {
  level: CefrLevelValue | null;
  categoryId: string | null;
}

/** Панель внизу списка в режиме выбора: сколько выбрано, уровень (и категория в админке), «Применить». */
@Component({
  selector: 'app-bulk-bar',
  imports: [HlmButtonImports, HlmNativeSelectImports, HlmSpinnerImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'sticky bottom-0 z-10 block' },
  template: `
    <div class="bg-background/95 -mx-4 border-t px-4 py-3 shadow-lg backdrop-blur sm:mx-0 sm:rounded-3xl sm:border">
      <div class="flex flex-wrap items-center gap-2">
        <span class="text-sm font-medium tabular-nums" aria-live="polite">Выбрано: {{ count() }}</span>
        <button hlmBtn variant="ghost" size="sm" type="button" (click)="selectPage.emit()">Все на странице</button>
        <button hlmBtn variant="ghost" size="sm" type="button" class="ms-auto" (click)="cancel.emit()">Отмена</button>
      </div>
      <div class="mt-2 flex flex-wrap gap-2">
        <hlm-native-select class="min-w-28 flex-1 sm:flex-none" selectClass="h-11" aria-label="Уровень" [value]="level()" (valueChange)="level.set($any($event ?? ''))">
          <option value="">Уровень…</option>
          @for (l of levels; track l) {
            <option [value]="l">{{ l }}</option>
          }
        </hlm-native-select>
        @if (categories().length > 0) {
          <hlm-native-select class="min-w-40 flex-1 sm:flex-none" selectClass="h-11" aria-label="Добавить категорию" [value]="categoryId()" (valueChange)="categoryId.set($event ?? '')">
            <option value="">Категория…</option>
            @for (category of categories(); track category.id) {
              <option [value]="category.id">{{ category.name }}</option>
            }
          </hlm-native-select>
        }
        <button hlmBtn type="button" class="h-11 flex-1 sm:flex-none" [disabled]="!canApply()" (click)="submit()">
          @if (busy()) {
            <hlm-spinner aria-label="Сохраняем" />
          }
          Применить
        </button>
      </div>
    </div>
  `,
})
export class BulkBar {
  readonly count = input.required<number>();
  readonly categories = input<CategoryDto[]>([]);
  readonly busy = input(false, { transform: booleanAttribute });

  readonly apply = output<BulkChange>();
  readonly selectPage = output<void>();
  readonly cancel = output<void>();

  protected readonly levels = CEFR_LEVELS;
  protected readonly level = signal<CefrLevelValue | ''>('');
  protected readonly categoryId = signal('');
  protected readonly canApply = computed(() => this.count() > 0 && !this.busy() && !!(this.level() || this.categoryId()));

  protected submit(): void {
    this.apply.emit({ level: this.level() || null, categoryId: this.categoryId() || null });
  }
}

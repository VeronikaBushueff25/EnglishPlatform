import { ChangeDetectionStrategy, Component, booleanAttribute, input, model } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideChevronLeft, lucideChevronRight } from '@ng-icons/lucide';
import { HlmButtonImports } from '@spartan-ng/helm/button';

/** Переключатель страниц списка; скрыт, если страница одна. */
@Component({
  selector: 'app-pager',
  imports: [NgIcon, HlmButtonImports],
  providers: [provideIcons({ lucideChevronLeft, lucideChevronRight })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (totalPages() > 1) {
      <nav class="flex items-center justify-center gap-3" aria-label="Страницы">
        <button
          hlmBtn
          variant="outline"
          size="icon-lg"
          type="button"
          aria-label="Назад"
          [disabled]="page() <= 1 || disabled()"
          (click)="page.set(page() - 1)"
        >
          <ng-icon name="lucideChevronLeft" />
        </button>
        <span class="text-sm tabular-nums">{{ page() }} из {{ totalPages() }}</span>
        <button
          hlmBtn
          variant="outline"
          size="icon-lg"
          type="button"
          aria-label="Дальше"
          [disabled]="page() >= totalPages() || disabled()"
          (click)="page.set(page() + 1)"
        >
          <ng-icon name="lucideChevronRight" />
        </button>
      </nav>
    }
  `,
})
export class Pager {
  readonly page = model.required<number>();
  readonly totalPages = input.required<number>();
  readonly disabled = input(false, { transform: booleanAttribute });
}

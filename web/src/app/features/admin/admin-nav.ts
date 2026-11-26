import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

/** Разделы админки. */
@Component({
  selector: 'app-admin-nav',
  imports: [RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="bg-muted grid grid-cols-3 gap-1 rounded-3xl p-1 text-xs font-medium sm:grid-cols-6 sm:rounded-full sm:text-sm" aria-label="Разделы админки">
      @for (item of items; track item.link) {
        <a
          [routerLink]="item.link"
          routerLinkActive="bg-background text-foreground! shadow-sm"
          class="text-muted-foreground hover:text-foreground flex h-10 items-center justify-center rounded-full transition-colors"
        >
          {{ item.label }}
        </a>
      }
    </nav>
  `,
})
export class AdminNav {
  protected readonly items = [
    { link: '/admin/users', label: 'Пользователи' },
    { link: '/admin/lexemes', label: 'Слова' },
    { link: '/admin/categories', label: 'Категории' },
    { link: '/admin/grammar', label: 'Грамматика' },
    { link: '/admin/placement', label: 'Тест уровня' },
    { link: '/admin/reading', label: 'Тексты' },
  ];
}

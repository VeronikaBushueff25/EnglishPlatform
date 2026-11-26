import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCheck, lucideMonitor, lucideMoon, lucideSun } from '@ng-icons/lucide';
import { HlmCardImports } from '@spartan-ng/helm/card';

export type ThemeMode = 'system' | 'light' | 'dark';

export interface Palette {
  id: string;
  name: string;
  hint: string;
  /** Цвета превью: фон, карточка, основной — светлый и тёмный режим. */
  light: [string, string, string];
  dark: [string, string, string];
}

/** Палитры: CSS каждой лежит в public/themes/<id>.css и грузится только при выборе («Классика» встроена). */
export const PALETTES: Palette[] = [
  { id: 'classic', name: 'Классика', hint: 'синий', light: ['#ffffff', '#f5f5f5', '#2563eb'], dark: ['#0a0a0a', '#171717', '#3b82f6'] },
  { id: 'ocean', name: 'Океан', hint: 'бирюза', light: ['#f8fdfe', '#eef6f8', '#0e7490'], dark: ['#0b1a1f', '#11252b', '#5ccbe0'] },
  { id: 'forest', name: 'Лес', hint: 'зелёный', light: ['#f9fdf9', '#eff6ef', '#15803d'], dark: ['#0c1a10', '#122618', '#6fdc8c'] },
  { id: 'sunset', name: 'Закат', hint: 'тёплый оранжевый', light: ['#fffcf7', '#faf2e7', '#c2410c'], dark: ['#1d140e', '#291d15', '#f6a15a'] },
  { id: 'lavender', name: 'Лаванда', hint: 'фиолетовый', light: ['#fcfaff', '#f4effb', '#7c3aed'], dark: ['#17121f', '#211a2c', '#c4a6f7'] },
  { id: 'rose', name: 'Роза', hint: 'розовый', light: ['#fffafa', '#fbf0f1', '#be185d'], dark: ['#1d1113', '#29191c', '#f48fb1'] },
  { id: 'graphite', name: 'Графит', hint: 'строгий монохром', light: ['#ffffff', '#f5f5f5', '#262626'], dark: ['#0a0a0a', '#171717', '#e5e5e5'] },
  { id: 'paper', name: 'Бумага', hint: 'для долгого чтения', light: ['#f6efe1', '#fbf6ec', '#8a5a2b'], dark: ['#241d15', '#2c241b', '#e2bc84'] },
];

interface EpTheme {
  get(): { mode: ThemeMode; palette: string };
  set(mode: ThemeMode, palette: string): void;
}

/** Оформление живёт в скрипте index.html (применяется до запуска Angular, без вспышки); здесь — только обёртка. */
function epTheme(): EpTheme | undefined {
  return (window as unknown as { epTheme?: EpTheme }).epTheme;
}

/** Карточка «Оформление» в профиле: режим (как в системе / светлый / тёмный) и палитра. Хранится в этом браузере. */
@Component({
  selector: 'app-appearance',
  imports: [NgIcon, HlmCardImports],
  providers: [provideIcons({ lucideCheck, lucideMonitor, lucideMoon, lucideSun })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section hlmCard>
      <div hlmCardHeader>
        <h2 hlmCardTitle>Оформление</h2>
        <p hlmCardDescription>Запоминается на этом устройстве.</p>
      </div>
      <div hlmCardContent class="flex flex-col gap-5">
        <div class="bg-muted grid grid-cols-3 gap-1 rounded-full p-1 text-sm font-medium" role="radiogroup" aria-label="Режим">
          @for (m of modes; track m.id) {
            <button
              type="button"
              role="radio"
              class="flex h-10 items-center justify-center gap-2 rounded-full transition-colors"
              [class]="mode() === m.id ? 'bg-background shadow-sm' : 'text-muted-foreground hover:text-foreground'"
              [attr.aria-checked]="mode() === m.id"
              (click)="setMode(m.id)"
            >
              <ng-icon [name]="m.icon" />
              {{ m.name }}
            </button>
          }
        </div>

        <div class="grid grid-cols-2 gap-3 sm:grid-cols-4" role="radiogroup" aria-label="Палитра">
          @for (p of palettes; track p.id) {
            @let c = isDark() ? p.dark : p.light;
            <button
              type="button"
              role="radio"
              class="group flex flex-col gap-2 rounded-2xl text-start outline-none"
              [attr.aria-checked]="palette() === p.id"
              (click)="setPalette(p.id)"
            >
              <span
                class="relative flex h-20 flex-col justify-end gap-1.5 overflow-hidden rounded-2xl border p-2.5 transition-shadow group-focus-visible:ring-2"
                [class.ring-2]="palette() === p.id"
                [style.background]="c[0]"
                [style.--tw-ring-color]="c[2]"
                aria-hidden="true"
              >
                <span class="h-2 w-3/4 rounded-full" [style.background]="c[1]"></span>
                <span class="flex items-center gap-1.5">
                  <span class="h-5 w-12 rounded-full" [style.background]="c[2]"></span>
                  <span class="h-5 flex-1 rounded-full" [style.background]="c[1]"></span>
                </span>
                @if (palette() === p.id) {
                  <span class="absolute top-2 right-2 flex size-6 items-center justify-center rounded-full text-white" [style.background]="c[2]">
                    <ng-icon name="lucideCheck" class="text-sm" [style.color]="c[0]" />
                  </span>
                }
              </span>
              <span class="px-1">
                <span class="block text-sm font-medium">{{ p.name }}</span>
                <span class="text-muted-foreground block text-xs">{{ p.hint }}</span>
              </span>
            </button>
          }
        </div>
      </div>
    </section>
  `,
})
export class Appearance {
  protected readonly modes: { id: ThemeMode; name: string; icon: string }[] = [
    { id: 'system', name: 'Авто', icon: 'lucideMonitor' },
    { id: 'light', name: 'Светлая', icon: 'lucideSun' },
    { id: 'dark', name: 'Тёмная', icon: 'lucideMoon' },
  ];
  protected readonly palettes = PALETTES;
  protected readonly mode = signal<ThemeMode>(epTheme()?.get().mode ?? 'system');
  protected readonly palette = signal<string>(epTheme()?.get().palette ?? 'classic');
  protected readonly isDark = signal(document.documentElement.classList.contains('dark'));

  protected setMode(mode: ThemeMode): void {
    this.mode.set(mode);
    this.apply();
  }

  protected setPalette(palette: string): void {
    this.palette.set(palette);
    this.apply();
  }

  private apply(): void {
    epTheme()?.set(this.mode(), this.palette());
    this.isDark.set(document.documentElement.classList.contains('dark'));
  }
}

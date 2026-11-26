import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { DayStatDto } from '../../core/api/models/day-stat-dto';

const WEEKDAYS = ['вс', 'пн', 'вт', 'ср', 'чт', 'пт', 'сб'];

/** «Красивый» верх шкалы: 1, 2, 5 × 10ⁿ, не меньше максимума. */
export function niceMax(max: number): number {
  if (max <= 0) return 10;
  const power = 10 ** Math.floor(Math.log10(max));
  for (const step of [1, 2, 5, 10]) {
    if (step * power >= max) return step * power;
  }
  return 10 * power;
}

export function dayLabel(iso: string): string {
  const [y, m, d] = iso.split('-').map(Number);
  const date = new Date(y, m - 1, d);
  return `${d}.${String(m).padStart(2, '0')}, ${WEEKDAYS[date.getDay()]}`;
}

/**
 * Ответы по дням за месяц: столбики одного цвета от нуля, шкала с «красивым» максимумом, подсказка при наведении
 * и фокусе, рядом — таблица с теми же числами.
 */
@Component({
  selector: 'app-stats-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      --bar: #2a78d6;
      --bar-today: #1c5cab;
      display: block;
    }
    :host-context(.dark) {
      --bar: #3987e5;
      --bar-today: #86b6ef;
    }
  `,
  template: `
    <figure class="flex flex-col gap-2">
      <div class="relative">
        <div class="grid grid-cols-[auto_1fr] gap-x-2">
          <div class="text-muted-foreground flex h-40 flex-col justify-between text-end text-xs tabular-nums" aria-hidden="true">
            <span class="-mt-1.5">{{ top() }}</span>
            <span class="-mb-1.5">0</span>
          </div>
          <div class="relative h-40 border-b" (mouseleave)="hover.set(null)">
            <div class="border-border/60 absolute inset-x-0 top-0 border-t border-dashed" aria-hidden="true"></div>
            <div class="border-border/60 absolute inset-x-0 top-1/2 border-t border-dashed" aria-hidden="true"></div>
            <ol class="absolute inset-0 flex items-end gap-[2px]" aria-label="Ответы по дням">
              @for (d of days(); track d.day; let i = $index; let last = $last) {
                <li class="group relative flex h-full flex-1 items-end justify-center outline-none"
                    tabindex="0"
                    [attr.aria-label]="dayLabel(d.day) + ': ответов ' + d.reviews + ', верно ' + d.successful + ', новых слов ' + d.newWords"
                    (mouseenter)="hover.set(i)" (focus)="hover.set(i)" (blur)="hover.set(null)">
                  @if (+d.reviews > 0) {
                    <span class="block w-full max-w-4 rounded-t-[4px] transition-opacity"
                          [style.height.%]="(+d.reviews / top()) * 100"
                          [style.background]="last ? 'var(--bar-today)' : 'var(--bar)'"
                          [class.opacity-60]="hover() !== null && hover() !== i"></span>
                  }
                </li>
              }
            </ol>
            @if (hovered(); as h) {
              <div class="bg-popover text-popover-foreground pointer-events-none absolute top-0 z-10 w-max -translate-x-1/2 rounded-xl border px-3 py-2 text-xs shadow-md"
                   [style.left.%]="h.left" role="status">
                <p class="font-medium">{{ dayLabel(h.day.day) }}</p>
                @if (+h.day.reviews > 0) {
                  <p class="tabular-nums">Ответов: {{ h.day.reviews }} · верно {{ pct(h.day) }}%</p>
                  <p class="text-muted-foreground tabular-nums">Новых слов: {{ h.day.newWords }}</p>
                } @else {
                  <p class="text-muted-foreground">Без занятий</p>
                }
              </div>
            }
          </div>
          <span></span>
          <div class="text-muted-foreground mt-1 flex justify-between text-xs" aria-hidden="true">
            <span>{{ short(days()[0]?.day) }}</span>
            <span>{{ short(days()[mid()]?.day) }}</span>
            <span>сегодня</span>
          </div>
        </div>
      </div>
      <figcaption class="sr-only">Ответов в день за последние {{ days().length }} дней</figcaption>
      <details class="text-sm">
        <summary class="text-muted-foreground hover:text-foreground cursor-pointer select-none">Показать таблицей</summary>
        <div class="mt-2 max-h-64 overflow-y-auto">
          <table class="w-full text-sm tabular-nums">
            <thead class="text-muted-foreground text-xs">
              <tr><th class="py-1 text-start font-normal">День</th><th class="text-end font-normal">Ответов</th><th class="text-end font-normal">Верно</th><th class="text-end font-normal">Новых</th></tr>
            </thead>
            <tbody>
              @for (d of activeDays(); track d.day) {
                <tr class="border-t">
                  <td class="py-1">{{ dayLabel(d.day) }}</td>
                  <td class="text-end">{{ d.reviews }}</td>
                  <td class="text-end">{{ pct(d) }}%</td>
                  <td class="text-end">{{ d.newWords }}</td>
                </tr>
              } @empty {
                <tr><td colspan="4" class="text-muted-foreground py-2">За этот период занятий не было.</td></tr>
              }
            </tbody>
          </table>
        </div>
      </details>
    </figure>
  `,
})
export class StatsChart {
  readonly days = input.required<DayStatDto[]>();

  protected readonly dayLabel = dayLabel;
  protected readonly hover = signal<number | null>(null);
  protected readonly top = computed(() => niceMax(Math.max(0, ...this.days().map((d) => Number(d.reviews)))));
  protected readonly mid = computed(() => Math.floor(this.days().length / 2));
  protected readonly activeDays = computed(() => this.days().filter((d) => Number(d.reviews) > 0).reverse());
  protected readonly hovered = computed(() => {
    const i = this.hover();
    const days = this.days();
    if (i === null || !days[i]) return null;
    // Подсказку держим внутри графика: у краёв не центрируем по столбику.
    const left = Math.min(80, Math.max(20, ((i + 0.5) / days.length) * 100));
    return { day: days[i], left };
  });

  protected pct(d: DayStatDto): number {
    return Number(d.reviews) > 0 ? Math.round((Number(d.successful) / Number(d.reviews)) * 100) : 0;
  }

  protected short(iso: string | undefined): string {
    if (!iso) return '';
    const [, m, d] = iso.split('-').map(Number);
    return `${d}.${String(m).padStart(2, '0')}`;
  }
}

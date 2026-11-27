import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HlmButtonImports } from '@spartan-ng/helm/button';

const LESSONS_KEY = 'owlish-support-lessons';
const HIDDEN_KEY = 'owlish-support-hidden-until';
export const SUPPORTER_KEY = 'owlish-supporter';

function read(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function write(key: string, value: string): void {
  try {
    localStorage.setItem(key, value);
  } catch {
    // без localStorage приглашение просто покажется по общим правилам
  }
}

/**
 * Ненавязчивое приглашение поддержать проект. После занятия — только в моменты удачи (3-е занятие, потом каждое
 * 10-е), его можно скрыть на месяц; тем, кто уже поддержал, не показывается. Без всплывающих окон.
 */
@Component({
  selector: 'app-support-invite',
  imports: [RouterLink, HlmButtonImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[style.display]': "visible() ? 'block' : 'none'" },
  template: `
    <div class="rounded-2xl border border-rose-200 bg-rose-50/70 p-4 text-sm dark:border-rose-400/25 dark:bg-rose-400/10">
      <p class="font-medium">{{ occasion() === 'lesson' ? 'Нравится заниматься с Owlish?' : 'Owlish делается одним человеком' }}</p>
      <p class="text-muted-foreground mt-1">
        {{ occasion() === 'lesson'
          ? 'Приложение бесплатное и без рекламы. Если оно помогает, поддержите его — так быстрее появятся новые функции и другие языки.'
          : 'Без рекламы и платных серий. Поддержка оплачивает сервер и новые функции, а питомец получит сердечко.' }}
      </p>
      <div class="mt-3 flex flex-wrap gap-2">
        <a hlmBtn size="sm" routerLink="/support">Поддержать проект ♥</a>
        @if (occasion() === 'lesson') {
          <button hlmBtn variant="ghost" size="sm" type="button" (click)="hide()">Не сейчас</button>
        }
      </div>
    </div>
  `,
})
export class SupportInvite {
  /** lesson — после занятия (редко и с «Не сейчас»), profile — всегда, пока не поддержал. */
  readonly occasion = input<'lesson' | 'profile'>('profile');

  private readonly hidden = signal(false);
  private readonly lessonCount: number;

  constructor() {
    // Считаем завершённые занятия, чтобы позвать в удачный момент, а не каждый раз.
    this.lessonCount = Number(read(LESSONS_KEY) ?? 0);
  }

  protected readonly visible = computed(() => {
    if (this.hidden() || read(SUPPORTER_KEY) === '1') return false;
    if (this.occasion() === 'profile') return true;
    if (Date.now() < Number(read(HIDDEN_KEY) ?? 0)) return false;
    const n = this.lessonCount + 1;
    return n === 3 || (n > 3 && n % 10 === 0);
  });

  ngOnInit(): void {
    if (this.occasion() === 'lesson') write(LESSONS_KEY, String(this.lessonCount + 1));
  }

  protected hide(): void {
    this.hidden.set(true);
    write(HIDDEN_KEY, String(Date.now() + 30 * 86_400_000));
  }
}

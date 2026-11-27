import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCheck, lucideHeart, lucideShare2 } from '@ng-icons/lucide';
import { toast } from '@spartan-ng/brain/sonner';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { Api } from '../../core/api/api';
import { getMe } from '../../core/api/fn/profile/get-me';
import { markSupporter } from '../../core/api/fn/profile/mark-supporter';
import { getSupportInfo } from '../../core/api/fn/support/get-support-info';
import { SupportInfoDto } from '../../core/api/models/support-info-dto';
import { AuthService } from '../../core/auth/auth.service';
import { problemMessage } from '../../core/http/problem';
import { SUPPORTER_KEY } from '../../shared/support-invite';
import { PetMood, toSpecies } from '../pet/pet-art';
import { PetStore, n, stageOf } from '../pet/pet-store';
import { PetView } from '../pet/pet-view';

/**
 * «Поддержать Owlish». Деньги принимают внешние сервисы (CloudTips, Boosty) — ссылки и цель сбора задаются
 * в настройках сервера. Здесь — зачем поддерживать, что уже сделано вместе, на что пойдут деньги и спасибо:
 * значок в профиле и сердечко для питомца (отмечает сам пользователь, на доверии).
 */
@Component({
  selector: 'app-support-page',
  imports: [RouterLink, NgIcon, PetView, HlmButtonImports, HlmCardImports],
  providers: [provideIcons({ lucideCheck, lucideHeart, lucideShare2 })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mx-auto flex max-w-2xl flex-col gap-5">
      <section class="flex flex-col items-center gap-2 text-center sm:flex-row sm:text-start">
        <app-pet-view class="w-36 shrink-0" [species]="species()" [palette]="palette()" [mood]="petMood()" [stage]="stage()"
                      [accessory]="thanked() ? 'heart' : pet()?.accessory ?? null" [label]="'Питомец'" />
        <div>
          <h1 class="text-2xl font-semibold tracking-tight">Поддержать Owlish</h1>
          <p class="text-muted-foreground mt-2">
            Привет! Owlish я делаю одна. Хотелось приложение, в котором слова не забываются через неделю,
            нет рекламы и никто не продаёт серию дней за деньги. Основное обучение и персонализация для каждого здесь бесплатно.
          </p>
        </div>
      </section>

      @if (pet(); as p) {
        <section class="rounded-3xl border bg-violet-50 p-4 dark:bg-violet-400/10">
          <p class="font-medium">Вы с Owlish уже {{ daysTogether() }} дн.</p>
          <p class="text-muted-foreground mt-1 text-sm">
            Занятий: {{ p.lessonsCompleted }} · своих слов: {{ p.wordsAdded }} · выполнено просьб питомца: {{ p.questsCompleted }} · лучшая серия: {{ p.bestStreak }} дн.
          </p>
        </section>
      }

      <section hlmCard class="gap-3 py-5">
        <div class="px-5">
          <h2 class="font-semibold">На что пойдут деньги</h2>
        </div>
        <ul class="text-muted-foreground flex list-disc flex-col gap-1.5 px-5 ps-10 text-sm">
          <li><span class="text-foreground">Сервер и база.</span> Это ежемесячный расход: без него Owlish просто не откроется.</li>
          <li><span class="text-foreground">Разговорный партнёр на ИИ.</span> Каждая беседа с моделью стоит денег, поэтому без поддержки эту функцию не запустить.</li>
          <li><span class="text-foreground">Развитие Owlish.</span> Новые функции, каких нет в других приложениях.</li>
          <li><span class="text-foreground">Другие языки.</span> Изучение других языков и поддержка многоязычного интерфейса.</li>
        </ul>
      </section>

      @if (info(); as i) {
        @if (i.goalTitle && i.goalAmount) {
          <section class="rounded-3xl border p-4">
            <div class="flex items-baseline justify-between gap-2 text-sm">
              <p><span class="text-muted-foreground">Сейчас собираем на:</span> <span class="font-medium">{{ i.goalTitle }}</span></p>
              <p class="tabular-nums">{{ money(i.goalRaised ?? 0) }} из {{ money(i.goalAmount) }} ₽</p>
            </div>
            <div class="bg-muted mt-2 h-3 overflow-hidden rounded-full" role="meter" aria-label="Собрано" aria-valuemin="0" [attr.aria-valuemax]="i.goalAmount" [attr.aria-valuenow]="i.goalRaised ?? 0">
              <div class="h-full rounded-full bg-rose-400" [style.width.%]="goalPercent()"></div>
            </div>
          </section>
        }

        @if (i.cloudTipsUrl || i.boostyUrl) {
          <div class="grid gap-3 sm:grid-cols-2">
            @if (i.cloudTipsUrl) {
              <a [href]="i.cloudTipsUrl" target="_blank" rel="noopener" (click)="opened.set(true)"
                 class="hover:border-primary flex flex-col gap-1 rounded-3xl border p-4 transition-colors">
                <span class="flex items-center gap-2 font-semibold"><ng-icon name="lucideHeart" class="text-rose-500" /> Разово</span>
                <span class="text-muted-foreground text-sm">Любая сумма картой или через СБП, через CloudTips от Т-Банка. Даже 100 ₽ — это день работы сервера.</span>
              </a>
            }
            @if (i.boostyUrl) {
              <a [href]="i.boostyUrl" target="_blank" rel="noopener" (click)="opened.set(true)"
                 class="hover:border-primary flex flex-col gap-1 rounded-3xl border p-4 transition-colors">
                <span class="flex items-center gap-2 font-semibold"><ng-icon name="lucideHeart" class="text-rose-500" /> Каждый месяц</span>
                <span class="text-muted-foreground text-sm">Подписка на Boosty: рассказываю там, что делаю, и первыми показываю новые функции.</span>
              </a>
            }
          </div>
        } @else {
          <p class="text-muted-foreground rounded-3xl border border-dashed p-4 text-sm">Ссылки для поддержки скоро появятся.</p>
        }
      }

      <!-- Спасибо -->
      <section hlmCard class="gap-3 py-5">
        <div class="px-5">
          <h2 class="font-semibold">Что вы получите</h2>
          <p class="text-muted-foreground mt-1 text-sm">
            Сердечко для питомца и значок «Поддерживает Owlish» в профиле. И новые функции раньше, потому что на них будет время.
            В учёбе поддержка ничего не меняет: занятия, слова и просьбы питомца одинаковые для всех.
          </p>
        </div>
        <div class="flex flex-wrap items-center gap-2 px-5">
          @if (thanked()) {
            <p class="text-success flex items-center gap-1.5 text-sm font-medium"><ng-icon name="lucideCheck" /> Спасибо! Сердечко уже у питомца.</p>
          } @else if (auth.isAuthenticated()) {
            <button hlmBtn type="button" [variant]="opened() ? 'default' : 'outline'" [disabled]="busy()" (click)="thank()">
              <ng-icon name="lucideHeart" /> Я поддержал(а)
            </button>
            <p class="text-muted-foreground text-xs">Нажмите после оплаты — проверить её я не могу, верю на слово.</p>
          } @else {
            <p class="text-muted-foreground text-sm"><a routerLink="/login" class="text-primary hover:underline">Войдите</a>, чтобы питомец получил сердечко.</p>
          }
        </div>
      </section>

      <section class="rounded-3xl border p-4 text-sm">
        <p class="font-medium">Поддержать можно и бесплатно</p>
        <p class="text-muted-foreground mt-1">Расскажите об Owlish тому, кто давно собирается учить английский. Для одного разработчика это лучшая реклама.</p>
        <button hlmBtn variant="outline" size="sm" type="button" class="mt-3" (click)="share()"><ng-icon name="lucideShare2" /> Поделиться</button>
      </section>
    </div>
  `,
})
export class SupportPage {
  private readonly api = inject(Api);
  protected readonly auth = inject(AuthService);
  private readonly petStore = inject(PetStore);

  protected readonly info = signal<SupportInfoDto | null>(null);
  protected readonly thanked = signal(false);
  protected readonly busy = signal(false);
  /** Открыли ссылку на оплату — кнопку «Я поддержал(а)» делаем заметнее. */
  protected readonly opened = signal(false);

  protected readonly pet = computed(() => this.petStore.pet() ?? null);
  protected readonly species = computed(() => toSpecies(this.pet()?.species ?? 'cat'));
  protected readonly palette = computed(() => this.pet()?.palette ?? 'ginger');
  protected readonly stage = computed(() => (this.pet() ? stageOf(this.pet()!) : 'teen'));
  protected readonly petMood = computed<PetMood>(() => (this.thanked() ? 'happy' : 'idle'));
  protected readonly daysTogether = computed(() => {
    const p = this.pet();
    return p ? Math.max(1, Math.ceil((Date.now() - Date.parse(p.createdAt)) / 86_400_000)) : 0;
  });
  protected readonly goalPercent = computed(() => {
    const i = this.info();
    return i?.goalAmount ? Math.min(100, Math.round((n(i.goalRaised) / n(i.goalAmount)) * 100)) : 0;
  });

  constructor() {
    this.api.invoke(getSupportInfo).then(
      (info) => this.info.set(info),
      () => this.info.set({ cloudTipsUrl: null, boostyUrl: null, goalTitle: null, goalAmount: null, goalRaised: null }),
    );
    if (this.auth.isAuthenticated()) {
      void this.petStore.load();
      this.api.invoke(getMe).then(
        (me) => {
          if (me.supporter) this.remember();
        },
        () => undefined,
      );
    }
  }

  protected money(value: number | string | null | undefined): string {
    return n(value).toLocaleString('ru-RU');
  }

  protected async thank(): Promise<void> {
    this.busy.set(true);
    try {
      await this.api.invoke(markSupporter);
      this.remember();
      await this.petStore.load(true);
      toast.success('Спасибо огромное!', { description: 'Питомцу открыто сердечко — наденьте его в гардеробе.' });
    } catch (e) {
      toast.error(problemMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async share(): Promise<void> {
    const url = location.origin;
    const text = 'Owlish — английский каждый день: интервальные повторения, тексты, игры и питомец, который ждёт вас на занятиях.';
    try {
      if (navigator.share) {
        await navigator.share({ title: 'Owlish', text, url });
      } else {
        await navigator.clipboard.writeText(`${text} ${url}`);
        toast.success('Ссылка скопирована');
      }
    } catch {
      // пользователь закрыл окно «Поделиться»
    }
  }

  private remember(): void {
    this.thanked.set(true);
    try {
      localStorage.setItem(SUPPORTER_KEY, '1');
    } catch {
      // приглашения просто продолжат показываться
    }
  }
}

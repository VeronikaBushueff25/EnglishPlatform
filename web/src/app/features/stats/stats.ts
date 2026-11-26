import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, resource, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideBookMarked, lucideFlame, lucideGauge, lucidePlay } from '@ng-icons/lucide';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmSpinnerImports } from '@spartan-ng/helm/spinner';
import { Api } from '../../core/api/api';
import { listSessions } from '../../core/api/fn/learning/list-sessions';
import { getStatsSummary } from '../../core/api/fn/statistics/get-stats-summary';
import { getWeakSpots } from '../../core/api/fn/statistics/get-weak-spots';
import { StatsSummaryDto } from '../../core/api/models/stats-summary-dto';
import { ErrorAlert } from '../../shared/error-alert';
import { formatDuration, pluralRu } from '../learn/learn-format';
import { SKILL_LABELS, levelPercent } from '../placement/placement-labels';
import { StatsChart } from './stats-chart';

export interface VocabularySegment {
  key: 'learning' | 'passive' | 'active' | 'notStarted';
  label: string;
  hint: string;
  count: number;
  share: number;
}

/** Путь слов по этапам: доли считаем от всех слов словаря, пустые этапы не рисуем, но в легенде оставляем. */
export function vocabularySegments(s: Pick<StatsSummaryDto, 'notStarted' | 'learning' | 'passive' | 'active' | 'activeThresholdDays'>): VocabularySegment[] {
  const items: Omit<VocabularySegment, 'share'>[] = [
    { key: 'active', label: 'Использую', hint: `вспоминаете сами и через ${s.activeThresholdDays} дн.`, count: Number(s.active) },
    { key: 'passive', label: 'Узнаю', hint: 'понимаете, увидев или услышав', count: Number(s.passive) },
    { key: 'learning', label: 'Учу', hint: 'только начали запоминать', count: Number(s.learning) },
    { key: 'notStarted', label: 'Ещё не начаты', hint: 'ждут первого занятия', count: Number(s.notStarted) },
  ];
  const total = items.reduce((sum, i) => sum + i.count, 0);
  return items.map((i) => ({ ...i, share: total > 0 ? i.count / total : 0 }));
}

/** Статистика (/stats): серия дней, неделя в цифрах, путь слов «учу → узнаю → использую», ответы за месяц, уровни, слабые места. */
@Component({
  selector: 'app-stats',
  imports: [DatePipe, RouterLink, NgIcon, ErrorAlert, StatsChart, HlmButtonImports, HlmCardImports, HlmSpinnerImports],
  providers: [provideIcons({ lucideBookMarked, lucideFlame, lucideGauge, lucidePlay })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      --stage-learning: #86b6ef;
      --stage-passive: #3987e5;
      --stage-active: #1c5cab;
      --stage-none: var(--muted);
      display: block;
    }
    :host-context(.dark) {
      --stage-learning: #184f95;
      --stage-passive: #2a78d6;
      --stage-active: #86b6ef;
    }
  `,
  template: `
    <div class="flex flex-col gap-4">
      <h1 class="text-2xl font-semibold tracking-tight">Статистика</h1>

      @if (summary.error()) {
        <app-error-alert message="Не удалось загрузить статистику." />
      } @else if (summary.hasValue()) {
        @let s = summary.value();

        <section hlmCard>
          <div hlmCardContent class="flex flex-col gap-4">
            <div class="flex items-center gap-4">
              <span class="flex size-14 shrink-0 items-center justify-center rounded-2xl"
                    [class]="s.studiedToday ? 'bg-amber-500/15 text-amber-600 dark:text-amber-400' : 'bg-muted text-muted-foreground'">
                <ng-icon name="lucideFlame" class="text-3xl" />
              </span>
              <div class="flex-1">
                <p class="text-3xl font-semibold tabular-nums">{{ s.currentStreak }} {{ days(+s.currentStreak) }}</p>
                <p class="text-muted-foreground text-sm">
                  @if (+s.currentStreak === 0) {
                    Серия начнётся с первого занятия.
                  } @else if (s.studiedToday) {
                    подряд с занятиями — сегодня засчитан.
                  } @else {
                    подряд — позанимайтесь сегодня, чтобы не прервать.
                  }
                  @if (+s.bestStreak > +s.currentStreak) {
                    Рекорд: {{ s.bestStreak }}.
                  }
                </p>
              </div>
            </div>
            @if (!s.studiedToday) {
              <a hlmBtn class="h-12 w-full sm:w-auto sm:self-start" routerLink="/learn">
                <ng-icon name="lucidePlay" />
                Начать занятие
              </a>
            }
          </div>
        </section>

        <section aria-labelledby="week-title">
          <h2 id="week-title" class="text-muted-foreground mb-2 text-sm font-medium">За 7 дней</h2>
          <dl class="grid grid-cols-2 gap-2 sm:grid-cols-4">
            <div class="bg-card rounded-2xl border p-4">
              <dt class="text-muted-foreground text-xs">Ответов</dt>
              <dd class="text-2xl font-semibold tabular-nums">{{ s.reviewsWeek }}</dd>
            </div>
            <div class="bg-card rounded-2xl border p-4">
              <dt class="text-muted-foreground text-xs">Верно</dt>
              <dd class="text-2xl font-semibold tabular-nums">{{ +s.reviewsWeek > 0 ? percent(+s.accuracyWeek) + '%' : '—' }}</dd>
            </div>
            <div class="bg-card rounded-2xl border p-4">
              <dt class="text-muted-foreground text-xs">Новых слов</dt>
              <dd class="text-2xl font-semibold tabular-nums">{{ s.newWordsWeek }}</dd>
            </div>
            <div class="bg-card rounded-2xl border p-4">
              <dt class="text-muted-foreground text-xs">Время</dt>
              <dd class="text-2xl font-semibold tabular-nums">{{ s.minutesWeek }} <span class="text-base font-normal">мин</span></dd>
            </div>
          </dl>
        </section>

        <section hlmCard>
          <div hlmCardHeader>
            <h2 hlmCardTitle>Словарь</h2>
            <p hlmCardDescription>
              {{ s.totalWords }} {{ words(+s.totalWords) }}. Активный словарь — то, что вы вспоминаете сами, пассивный — то, что узнаёте.
            </p>
          </div>
          <div hlmCardContent class="flex flex-col gap-4">
            @if (+s.totalWords > 0) {
              <div class="flex h-4 w-full gap-[2px] overflow-hidden rounded-full" role="img"
                   [attr.aria-label]="segmentsLabel()">
                @for (seg of segments(); track seg.key) {
                  @if (seg.count > 0) {
                    <span class="h-full first:rounded-s-full last:rounded-e-full" [style.flex-grow]="seg.count" [style.background]="'var(--stage-' + stageVar(seg.key) + ')'"></span>
                  }
                }
              </div>
            }
            <ul class="grid gap-3 sm:grid-cols-2">
              @for (seg of segments(); track seg.key) {
                <li class="flex items-start gap-3">
                  <span class="mt-1 size-3 shrink-0 rounded-[4px]" [style.background]="'var(--stage-' + stageVar(seg.key) + ')'"
                        [class.border]="seg.key === 'notStarted'"></span>
                  <div class="min-w-0 flex-1">
                    <p class="flex items-baseline justify-between gap-2">
                      <span class="font-medium">{{ seg.label }}</span>
                      <span class="tabular-nums">{{ seg.count }}</span>
                    </p>
                    <p class="text-muted-foreground text-xs">{{ seg.hint }}</p>
                  </div>
                </li>
              }
            </ul>
            @if (+s.totalWords === 0) {
              <a hlmBtn variant="outline" class="h-11 w-full sm:w-auto sm:self-start" routerLink="/dictionary">Добавить слова</a>
            }
          </div>
        </section>

        <section hlmCard>
          <div hlmCardHeader>
            <h2 hlmCardTitle>Ответов в день</h2>
            <p hlmCardDescription>За последние {{ s.days.length }} дней, сегодня — {{ s.reviewsToday }}.</p>
          </div>
          <div hlmCardContent>
            <app-stats-chart [days]="s.days" />
          </div>
        </section>

        <section hlmCard>
          <div hlmCardHeader>
            <h2 hlmCardTitle class="flex items-center gap-2"><ng-icon name="lucideGauge" /> Уровень по навыкам</h2>
          </div>
          <div hlmCardContent class="flex flex-col gap-3">
            @for (level of s.skills; track level.skill) {
              <div class="flex items-center gap-3">
                <span class="w-24 shrink-0 text-sm">{{ skillLabel(level.skill) }}</span>
                <div class="bg-muted h-2 flex-1 overflow-hidden rounded-full">
                  <div class="bg-primary h-full rounded-full" [style.width.%]="levelPercent($any(level.level))"></div>
                </div>
                <span class="w-8 text-end text-sm font-semibold">{{ level.level }}</span>
              </div>
            } @empty {
              <p class="text-muted-foreground text-sm">Уровень ещё не определён.</p>
              <a hlmBtn variant="outline" class="h-11 w-full sm:w-auto sm:self-start" routerLink="/placement">Пройти тест уровня</a>
            }
          </div>
        </section>

        <section hlmCard>
          <div hlmCardHeader>
            <h2 hlmCardTitle>История занятий</h2>
          </div>
          <div hlmCardContent>
            <ul class="flex flex-col">
              @for (session of sessions.value()?.items ?? []; track session.id) {
                <li class="flex items-center gap-3 border-t py-3 first:border-t-0 first:pt-0">
                  <div class="min-w-0 flex-1">
                    <p class="font-medium">
                      {{ session.startedAt | date: 'dd.MM, HH:mm' }}
                      @if (session.kind === 'Mistakes') {
                        <span class="text-muted-foreground text-sm font-normal">· работа над ошибками</span>
                      }
                    </p>
                    <p class="text-muted-foreground text-sm">
                      {{ session.stats.answered }} {{ answers(+session.stats.answered) }}
                      @if (+session.stats.newWords > 0) {
                        · новых слов {{ session.stats.newWords }}
                      }
                      @if (+session.stats.durationSeconds > 0) {
                        · {{ duration(+session.stats.durationSeconds) }}
                      }
                    </p>
                  </div>
                  <span class="text-sm font-semibold tabular-nums" [attr.aria-label]="'Верно ' + accuracy(session.stats) + '%'">{{ accuracy(session.stats) }}%</span>
                </li>
              } @empty {
                @if (!sessions.isLoading()) {
                  <li class="text-muted-foreground text-sm">Занятий пока не было.</li>
                }
              }
            </ul>
            @if (hasMoreSessions()) {
              <button hlmBtn variant="ghost" size="sm" type="button" class="mt-2" [disabled]="sessions.isLoading()" (click)="historySize.set(historySize() + 10)">Показать ещё</button>
            }
          </div>
        </section>

        @if (weakTags().length > 0) {
          <section hlmCard>
            <div hlmCardHeader>
              <h2 hlmCardTitle>Слабые места</h2>
              <p hlmCardDescription>Какие ошибки вы делаете чаще всего за последние недели — сколько раз.</p>
            </div>
            <div hlmCardContent>
              <ul class="flex flex-col gap-3">
                @for (tag of weakTags(); track tag.code) {
                  <li class="flex items-center gap-3">
                    <span class="w-28 shrink-0 text-sm">{{ tag.title }}</span>
                    <div class="bg-muted h-2 flex-1 overflow-hidden rounded-full">
                      <div class="h-full rounded-full bg-amber-500" [style.width.%]="tag.width"></div>
                    </div>
                    <span class="text-muted-foreground w-10 text-end text-sm tabular-nums">{{ tag.count }}</span>
                    @if (tag.topicSlug) {
                      <a hlmBtn variant="ghost" size="sm" [routerLink]="['/grammar', tag.topicSlug]" [attr.aria-label]="'Правило: ' + tag.topicTitle">
                        <ng-icon name="lucideBookMarked" />
                      </a>
                    } @else {
                      <span class="size-8"></span>
                    }
                  </li>
                }
              </ul>
            </div>
          </section>
        }
      } @else {
        <div class="text-muted-foreground flex items-center gap-2"><hlm-spinner aria-label="Загрузка" /> Загружаем…</div>
      }
    </div>
  `,
})
export class Stats {
  private readonly api = inject(Api);

  protected readonly levelPercent = levelPercent;
  protected readonly summary = resource({ loader: () => this.api.invoke(getStatsSummary) });
  private readonly weak = resource({ loader: () => this.api.invoke(getWeakSpots) });
  protected readonly historySize = signal(10);
  protected readonly sessions = resource({
    params: () => ({ page: 1, pageSize: this.historySize() }),
    loader: ({ params }) => this.api.invoke(listSessions, params),
  });
  protected readonly hasMoreSessions = computed(() => {
    const v = this.sessions.value();
    return !!v && Number(v.totalCount) > v.items.length;
  });

  protected readonly segments = computed(() => (this.summary.hasValue() ? vocabularySegments(this.summary.value()) : []));
  protected readonly segmentsLabel = computed(() =>
    this.segments()
      .map((s) => `${s.label}: ${s.count}`)
      .join(', '),
  );
  protected readonly weakTags = computed(() => {
    const w = this.weak.value();
    if (!w || Number(w.totalAnswers) < 10) return [];
    const tags = w.tags.filter((t) => Number(t.count) > 0).slice(0, 6);
    const max = Math.max(1, ...tags.map((t) => Number(t.count)));
    return tags.map((t) => ({ ...t, width: (Number(t.count) / max) * 100 }));
  });

  protected skillLabel(skill: string): string {
    return SKILL_LABELS[skill as keyof typeof SKILL_LABELS] ?? skill;
  }

  protected stageVar(key: VocabularySegment['key']): string {
    return key === 'notStarted' ? 'none' : key;
  }

  protected percent(share: number): number {
    return Math.round(share * 100);
  }

  protected days(n: number): string {
    return pluralRu(n, 'день', 'дня', 'дней');
  }

  protected accuracy(stats: { answered: number | string; correct: number | string; almost: number | string }): number {
    const answered = Number(stats.answered);
    return answered > 0 ? Math.round(((Number(stats.correct) + Number(stats.almost) * 0.5) / answered) * 100) : 0;
  }

  protected answers(n: number): string {
    return pluralRu(n, 'ответ', 'ответа', 'ответов');
  }

  protected duration(seconds: number): string {
    return formatDuration(seconds);
  }

  protected words(n: number): string {
    return pluralRu(n, 'слово', 'слова', 'слов');
  }
}

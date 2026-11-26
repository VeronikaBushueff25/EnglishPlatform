import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, resource } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideBookMarked, lucideBookOpen, lucideGauge, lucidePlay, lucideTarget } from '@ng-icons/lucide';
import { HlmBadgeImports } from '@spartan-ng/helm/badge';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmSpinnerImports } from '@spartan-ng/helm/spinner';
import { Api } from '../../core/api/api';
import { getLearningPlan } from '../../core/api/functions';
import { problemMessage } from '../../core/http/problem';
import { ErrorAlert } from '../../shared/error-alert';
import { CEFR_LABELS, GOAL_TITLES, SKILL_LABELS, levelPercent } from './placement-labels';

/** Моя программа (/plan): уровни по навыкам, какие слова и темы грамматики сейчас в работе, дневная нагрузка. */
@Component({
  selector: 'app-plan',
  imports: [DatePipe, RouterLink, NgIcon, ErrorAlert, HlmBadgeImports, HlmButtonImports, HlmCardImports, HlmSpinnerImports],
  providers: [provideIcons({ lucideBookMarked, lucideBookOpen, lucideGauge, lucidePlay, lucideTarget })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4">
      <h1 class="text-2xl font-semibold tracking-tight">Моя программа</h1>

      @if (loadError(); as message) {
        <app-error-alert [message]="message" />
      } @else if (plan.hasValue()) {
        @let p = plan.value();
        @if (!p.hasPlacement) {
          <section hlmCard>
            <div hlmCardHeader>
              <h2 hlmCardTitle>Сначала определим уровень</h2>
              <p hlmCardDescription>5–7 минут — и программа подстроится под вас: слова нужного уровня, темы грамматики, нагрузка.</p>
            </div>
            <div hlmCardFooter>
              <a hlmBtn class="h-12 w-full sm:w-auto" routerLink="/placement">
                <ng-icon name="lucideGauge" />
                Пройти тест уровня
              </a>
            </div>
          </section>
        } @else {
          <section hlmCard>
            <div hlmCardHeader>
              <div class="flex items-start gap-3">
                <div class="flex-1">
                  <p class="text-muted-foreground text-sm">Общий уровень</p>
                  <p class="text-4xl font-semibold tracking-tight">{{ p.overall }}</p>
                  @if (p.overall) {
                    <p class="text-muted-foreground">{{ cefrLabels[p.overall] }}</p>
                  }
                </div>
                @if (p.lastPlacementAt) {
                  <span class="text-muted-foreground text-xs">тест {{ p.lastPlacementAt | date: 'dd.MM.yyyy' }}</span>
                }
              </div>
            </div>
            <div hlmCardContent class="flex flex-col gap-3">
              @for (level of p.levels; track level.skill) {
                <div class="flex items-center gap-3">
                  <span class="w-24 text-sm">{{ skillLabels[level.skill] }}</span>
                  <div class="bg-muted h-2.5 flex-1 overflow-hidden rounded-full">
                    <div class="bg-primary h-full rounded-full" [style.width.%]="levelPercent(level.level)"></div>
                  </div>
                  <span class="w-8 text-end font-semibold tabular-nums">{{ level.level }}</span>
                </div>
              }
              <p class="text-muted-foreground text-xs">Аудирование и говорение пока не оцениваются — для этого нужна проверка речи.</p>
            </div>
            <div hlmCardFooter>
              <a hlmBtn variant="ghost" size="sm" routerLink="/placement">Пройти тест заново</a>
            </div>
          </section>

          <section hlmCard>
            <div hlmCardHeader>
              <h2 hlmCardTitle class="flex items-center gap-2"><ng-icon name="lucideBookOpen" class="text-primary" /> Слова</h2>
              <p hlmCardDescription>
                Учим слова уровней {{ p.wordLevels.join(' и ') }}: свои закрепляем, следующего уровня — растём.
                В день: {{ p.dailyNewWords }} новых и до {{ p.dailyReviews }} повторений.
              </p>
            </div>
            <div hlmCardFooter class="flex flex-col gap-2 sm:flex-row">
              <a hlmBtn class="h-11 w-full sm:w-auto" routerLink="/learn">
                <ng-icon name="lucidePlay" />
                Занятие
              </a>
              <a hlmBtn variant="outline" class="h-11 w-full sm:w-auto" routerLink="/dictionary" [queryParams]="{ tab: 'base' }">Ещё слова</a>
              <a hlmBtn variant="ghost" class="h-11 w-full sm:w-auto" routerLink="/profile">Изменить нагрузку</a>
            </div>
          </section>

          @if (p.grammar.length > 0) {
            <section hlmCard>
              <div hlmCardHeader>
                <h2 hlmCardTitle class="flex items-center gap-2"><ng-icon name="lucideBookMarked" class="text-primary" /> Грамматика на сейчас</h2>
                <p hlmCardDescription>Темы вашего уровня и чуть выше.</p>
              </div>
              <div hlmCardContent>
                <ul class="flex flex-col gap-2">
                  @for (topic of p.grammar; track topic.slug) {
                    <li>
                      <a [routerLink]="['/grammar', topic.slug]" class="hover:bg-muted/50 flex flex-col gap-0.5 rounded-2xl border p-3 transition-colors">
                        <span class="flex items-center gap-2 font-medium">
                          {{ topic.title }}
                          @if (topic.level) {
                            <span hlmBadge variant="secondary">{{ topic.level }}</span>
                          }
                        </span>
                        <span class="text-muted-foreground line-clamp-2 text-sm">{{ topic.summary }}</span>
                      </a>
                    </li>
                  }
                </ul>
              </div>
            </section>
          }

          <section hlmCard>
            <div hlmCardHeader>
              <h2 hlmCardTitle class="flex items-center gap-2"><ng-icon name="lucideTarget" class="text-primary" /> Цели</h2>
              <p hlmCardDescription>
                @if (goals().length > 0) {
                  {{ goals().join(', ') }} — слова из этих тем идут первыми.
                } @else {
                  Цели не выбраны — укажите их в профиле, и подберём слова под них.
                }
              </p>
            </div>
            <div hlmCardFooter>
              <a hlmBtn variant="ghost" size="sm" routerLink="/profile">Изменить цели</a>
            </div>
          </section>
        }
      } @else {
        <div class="text-muted-foreground flex items-center gap-2"><hlm-spinner aria-label="Загрузка" /> Загружаем…</div>
      }
    </div>
  `,
})
export class Plan {
  private readonly api = inject(Api);

  protected readonly skillLabels = SKILL_LABELS;
  protected readonly cefrLabels = CEFR_LABELS;
  protected readonly levelPercent = levelPercent;

  protected readonly plan = resource({ loader: () => this.api.invoke(getLearningPlan) });
  protected readonly goals = computed(() => (this.plan.value()?.goals ?? []).map((g) => GOAL_TITLES[g] ?? g));
  protected readonly loadError = computed(() =>
    this.plan.error() ? problemMessage(this.plan.error(), 'Не удалось загрузить программу.') : null,
  );
}

import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, resource } from '@angular/core';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { Api } from '../core/api/api';
import { listMyWordProgress } from '../core/api/fn/my-words/list-my-word-progress';
import { WordProgress } from '../core/api/models/word-progress';

export type Stage = 'New' | 'Learning' | 'Passive' | 'Active';

/** Этапы — те же, что на странице статистики; цвет точки — одна шкала синего, «новое» — серое. */
export const STAGES: Record<Stage, { label: string; hint: string; dot: string }> = {
  New: { label: 'новое', hint: 'ещё не занимались', dot: 'bg-muted-foreground/40' },
  Learning: { label: 'учу', hint: 'только начали запоминать', dot: 'bg-[#86b6ef]' },
  Passive: { label: 'узнаю', hint: 'понимаете, увидев или услышав', dot: 'bg-[#3987e5]' },
  Active: { label: 'использую', hint: 'вспоминаете сами и надолго', dot: 'bg-[#1c5cab] dark:bg-[#86b6ef]' },
};

export function stageOf(progress: WordProgress | undefined): Stage {
  return (progress?.stage as Stage) ?? 'New';
}

/** Прогресс слова на его странице: этап, сколько раз отвечали и забывали, когда следующее повторение. */
@Component({
  selector: 'app-word-progress',
  imports: [DatePipe, HlmCardImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (progress(); as p) {
      @let stage = stages[stageOf(p)];
      <section hlmCard>
        <div hlmCardHeader>
          <h2 hlmCardTitle>Прогресс</h2>
        </div>
        <div hlmCardContent>
          <dl class="grid grid-cols-2 gap-3 sm:grid-cols-4">
            <div class="bg-muted/60 rounded-2xl p-3">
              <dt class="text-muted-foreground text-xs">Этап</dt>
              <dd class="flex items-center gap-2 font-semibold"><span class="size-2.5 rounded-full" [class]="stage.dot"></span>{{ stage.label }}</dd>
              <dd class="text-muted-foreground text-xs">{{ stage.hint }}</dd>
            </div>
            <div class="bg-muted/60 rounded-2xl p-3">
              <dt class="text-muted-foreground text-xs">Ответов</dt>
              <dd class="text-xl font-semibold tabular-nums">{{ p.reviews }}</dd>
            </div>
            <div class="bg-muted/60 rounded-2xl p-3">
              <dt class="text-muted-foreground text-xs">Забывали</dt>
              <dd class="text-xl font-semibold tabular-nums">{{ p.lapses }}</dd>
            </div>
            <div class="bg-muted/60 rounded-2xl p-3">
              <dt class="text-muted-foreground text-xs">Следующее повторение</dt>
              <dd class="font-semibold">{{ p.nextReview ? (p.nextReview | date: 'dd.MM, HH:mm') : 'в ближайшем занятии' }}</dd>
            </div>
          </dl>
        </div>
      </section>
    }
  `,
})
export class WordProgressCard {
  private readonly api = inject(Api);

  readonly wordId = input.required<string>();
  protected readonly stages = STAGES;
  protected readonly stageOf = stageOf;
  private readonly all = resource({ loader: () => this.api.invoke(listMyWordProgress) });
  protected readonly progress = computed(() => this.all.value()?.find((p) => p.userWordId === this.wordId()));
}

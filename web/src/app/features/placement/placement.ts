import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideArrowRight, lucideClock, lucideGauge, lucideSparkles } from '@ng-icons/lucide';
import { HlmBadgeImports } from '@spartan-ng/helm/badge';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmSpinnerImports } from '@spartan-ng/helm/spinner';
import { Api } from '../../core/api/api';
import { answerPlacement, finishPlacement, startPlacement } from '../../core/api/functions';
import { PlacementLength } from '../../core/api/models/placement-length';
import { PlacementResultDto } from '../../core/api/models/placement-result-dto';
import { PlacementStepDto } from '../../core/api/models/placement-step-dto';
import { problemMessage } from '../../core/http/problem';
import { ErrorAlert } from '../../shared/error-alert';
import { paragraphs } from '../../shared/text';
import { CEFR_LABELS, SKILL_LABELS, levelPercent } from './placement-labels';

type Phase = 'intro' | 'question' | 'result';

/**
 * Тест уровня: лексика, грамматика, чтение. Вопросы подстраиваются под ответы, правильность не показывается —
 * так тест быстрее и честнее. В конце — уровень по каждому навыку и стартовый набор слов.
 */
@Component({
  selector: 'app-placement',
  imports: [RouterLink, NgIcon, ErrorAlert, HlmBadgeImports, HlmButtonImports, HlmCardImports, HlmSpinnerImports],
  providers: [provideIcons({ lucideArrowRight, lucideClock, lucideGauge, lucideSparkles })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown)': 'onKey($event)' },
  template: `
    <div class="mx-auto flex w-full max-w-lg flex-col gap-4">
      @if (error(); as message) {
        <app-error-alert [message]="message" />
      }

      @switch (phase()) {
        @case ('intro') {
          <section hlmCard>
            <div hlmCardHeader>
              <span class="bg-primary/10 text-primary mb-2 flex size-12 items-center justify-center rounded-2xl">
                <ng-icon name="lucideGauge" class="text-2xl" />
              </span>
              <h1 hlmCardTitle class="text-2xl">Определим ваш уровень</h1>
              <p hlmCardDescription>Слова, грамматика и короткие тексты.</p>
            </div>
            <div hlmCardContent class="flex flex-col gap-4">
              <div class="grid gap-2 sm:grid-cols-2" role="radiogroup" aria-label="Длина теста">
                @for (mode of modes; track mode.value) {
                  <button
                    type="button"
                    role="radio"
                    [attr.aria-checked]="length() === mode.value"
                    class="flex flex-col items-start gap-1 rounded-2xl border p-4 text-start transition-colors"
                    [class]="length() === mode.value ? 'border-primary bg-primary/5 ring-primary/30 ring-2' : 'hover:border-primary/40'"
                    (click)="length.set(mode.value)"
                  >
                    <span class="font-semibold">{{ mode.title }}</span>
                    <span class="text-muted-foreground flex items-center gap-1.5 text-sm"><ng-icon name="lucideClock" /> {{ mode.time }}</span>
                    <span class="text-muted-foreground text-sm">{{ mode.note }}</span>
                  </button>
                }
              </div>
              <div class="text-muted-foreground flex flex-col gap-2 text-sm">
              <p>Вопросы подстраиваются под ответы: чем увереннее отвечаете, тем сложнее следующий.</p>
              <p>Не знаете ответ — так и нажмите «Не знаю»: угадывание только исказит результат.</p>
              <p>По итогам подберём слова вашего уровня и составим программу.</p>
              </div>
            </div>
            <div hlmCardFooter>
              <button hlmBtn type="button" class="h-12 w-full text-base" [disabled]="busy()" (click)="start()">
                @if (busy()) {
                  <hlm-spinner aria-label="Загрузка" />
                }
                Начать тест
                <ng-icon name="lucideArrowRight" />
              </button>
            </div>
          </section>
        }

        @case ('question') {
          @if (step(); as s) {
            <div class="flex items-center gap-3">
              <div class="bg-muted h-2.5 flex-1 overflow-hidden rounded-full" role="progressbar" aria-label="Прогресс теста"
                   [attr.aria-valuenow]="s.progress.answered" [attr.aria-valuemax]="s.progress.total">
                <div class="bg-primary h-full rounded-full transition-[width] duration-300" [style.width.%]="percent()"></div>
              </div>
              <span class="text-muted-foreground w-14 text-end text-sm tabular-nums">{{ s.progress.answered }}/{{ s.progress.total }}</span>
            </div>

            @if (s.question; as q) {
              <section hlmCard>
                <div hlmCardHeader>
                  <span hlmBadge variant="secondary" class="self-start">{{ skillLabels[q.skill] }}</span>
                  @if (q.passage) {
                    <div class="bg-muted/50 mt-3 flex flex-col gap-2 rounded-2xl p-4" lang="en">
                      @for (p of passage(); track $index) {
                        <p class="leading-relaxed">{{ p }}</p>
                      }
                    </div>
                  }
                  <h1 class="pt-3 text-xl font-semibold" [attr.lang]="q.skill === 'Vocabulary' ? null : 'en'">{{ q.prompt }}</h1>
                </div>
                <div hlmCardContent class="grid gap-2">
                  @for (option of q.options; track $index; let i = $index) {
                    <button
                      type="button"
                      class="hover:border-primary/50 hover:bg-primary/5 flex min-h-14 items-center gap-3 rounded-2xl border px-4 py-3 text-start text-base transition-colors active:scale-[0.99] disabled:opacity-60"
                      [class.border-primary]="chosen() === i"
                      [disabled]="busy()"
                      (click)="answer(i)"
                    >
                      <span class="text-muted-foreground bg-muted flex size-7 shrink-0 items-center justify-center rounded-lg text-sm tabular-nums">{{ i + 1 }}</span>
                      <span class="flex-1" [attr.lang]="q.skill === 'Vocabulary' ? null : 'en'">{{ option }}</span>
                      @if (busy() && chosen() === i) {
                        <hlm-spinner />
                      }
                    </button>
                  }
                  <button hlmBtn variant="ghost" type="button" class="h-11" [disabled]="busy()" (click)="answer(-1)">Не знаю</button>
                </div>
              </section>
            }
          }
        }

        @case ('result') {
          @if (result(); as r) {
            <section hlmCard>
              <div hlmCardHeader class="items-center text-center">
                <span class="bg-primary/10 text-primary mx-auto flex size-14 items-center justify-center rounded-full">
                  <ng-icon name="lucideSparkles" class="text-2xl" />
                </span>
                <p class="text-muted-foreground text-sm">Ваш уровень</p>
                <h1 class="text-5xl font-semibold tracking-tight">{{ r.overall }}</h1>
                <p class="text-muted-foreground">{{ cefrLabels[r.overall] }} · верных ответов {{ r.correct }} из {{ r.answered }}</p>
                @if (r.length === 'Short') {
                  <p class="text-muted-foreground mt-1 text-xs">Быстрый тест может ошибиться на ступень — точный уточнит.</p>
                }
              </div>
              <div hlmCardContent class="flex flex-col gap-3">
                @for (level of r.levels; track level.skill) {
                  <div class="flex items-center gap-3">
                    <span class="w-24 text-sm">{{ skillLabels[level.skill] }}</span>
                    <div class="bg-muted h-2.5 flex-1 overflow-hidden rounded-full">
                      <div class="bg-primary h-full rounded-full" [style.width.%]="levelPercent(level.level)"></div>
                    </div>
                    <span class="w-8 text-end font-semibold tabular-nums">{{ level.level }}</span>
                  </div>
                }
                @if (+r.starterWordsAdded > 0) {
                  <p class="bg-primary/5 border-primary/20 mt-2 rounded-2xl border p-3 text-sm">
                    В ваш словарь добавлено {{ r.starterWordsAdded }} слов вашего уровня — они начнут появляться в занятиях.
                  </p>
                }
              </div>
              <div hlmCardFooter class="flex flex-col gap-2 sm:flex-row">
                <a hlmBtn class="h-12 w-full sm:w-auto" routerLink="/plan">Моя программа</a>
                <a hlmBtn variant="outline" class="h-12 w-full sm:w-auto" routerLink="/learn">Начать занятие</a>
              </div>
            </section>
          }
        }
      }
    </div>
  `,
})
export class Placement {
  private readonly api = inject(Api);

  protected readonly skillLabels = SKILL_LABELS;
  protected readonly cefrLabels = CEFR_LABELS;
  protected readonly levelPercent = levelPercent;

  protected readonly phase = signal<Phase>('intro');
  protected readonly step = signal<PlacementStepDto | null>(null);
  protected readonly result = signal<PlacementResultDto | null>(null);
  protected readonly chosen = signal<number | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly percent = computed(() => {
    const p = this.step()?.progress;
    return p && Number(p.total) > 0 ? (Number(p.answered) / Number(p.total)) * 100 : 0;
  });
  protected readonly passage = computed(() => paragraphs(this.step()?.question?.passage));

  protected readonly modes: { value: PlacementLength; title: string; time: string; note: string }[] = [
    { value: 'Short', title: 'Быстрый', time: '18 вопросов · 5–7 минут', note: 'Чтобы начать заниматься. Может ошибиться на ступень.' },
    { value: 'Long', title: 'Точный', time: '36 вопросов · 12–15 минут', note: 'Вдвое больше вопросов — уровень почти всегда точный.' },
  ];
  protected readonly length = signal<PlacementLength>('Short');

  protected async start(): Promise<void> {
    await this.run(async () => this.show(await this.api.invoke(startPlacement, { body: { length: this.length() } })));
  }

  protected async answer(option: number): Promise<void> {
    const s = this.step();
    if (!s?.question || this.busy()) {
      return;
    }
    this.chosen.set(option);
    await this.run(async () => {
      try {
        this.show(await this.api.invoke(answerPlacement, { id: s.attemptId, body: { questionId: s.question!.questionId, option } }));
      } catch (e) {
        // Вопрос устарел (тест открыт во второй вкладке) — продолжаем с актуального.
        if (e instanceof HttpErrorResponse && e.status === 409) {
          this.show(await this.api.invoke(startPlacement));
          return;
        }
        throw e;
      }
    });
  }

  protected onKey(event: KeyboardEvent): void {
    if (this.phase() === 'question' && /^[1-6]$/.test(event.key)) {
      const index = Number(event.key) - 1;
      if (index < (this.step()?.question?.options.length ?? 0)) {
        void this.answer(index);
      }
    }
  }

  private async show(step: PlacementStepDto): Promise<void> {
    this.chosen.set(null);
    this.step.set(step);
    if (step.finished) {
      this.result.set(await this.api.invoke(finishPlacement, { id: step.attemptId }));
      this.phase.set('result');
    } else {
      this.phase.set('question');
    }
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await action();
    } catch (e) {
      this.chosen.set(null);
      this.error.set(problemMessage(e, 'Не удалось загрузить вопрос.'));
    } finally {
      this.busy.set(false);
    }
  }
}

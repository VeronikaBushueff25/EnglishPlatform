import { NgTemplateOutlet } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { afterNextRender, ChangeDetectionStrategy, Component, computed, effect, ElementRef, inject, Injector, signal, untracked, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideArrowRight,
  lucideBookMarked,
  lucideCircleCheck,
  lucideFlame,
  lucideCircleX,
  lucideCircleAlert,
  lucideLightbulb,
  lucidePlay,
  lucideSparkles,
  lucideVolume2,
  lucideVolumeX,
  lucideX,
} from '@ng-icons/lucide';
import { HlmBadgeImports } from '@spartan-ng/helm/badge';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { HlmSpinnerImports } from '@spartan-ng/helm/spinner';
import { Api } from '../../core/api/api';
import { answerTask, finishSession, getLearningToday, getNextTask, getWeakSpots, muteSessionAudio, startSession } from '../../core/api/functions';
import { toast } from '@spartan-ng/brain/sonner';
import { AnswerRequest } from '../../core/api/models/answer-request';
import { AnswerResultDto } from '../../core/api/models/answer-result-dto';
import { SessionProgress } from '../../core/api/models/session-progress';
import { SessionStats } from '../../core/api/models/session-stats';
import { TaskView } from '../../core/api/models/task-view';
import { TodayDto } from '../../core/api/models/today-dto';
import { WeakSpotsDto } from '../../core/api/models/weak-spots-dto';
import { problemMessage } from '../../core/http/problem';
import { ErrorAlert } from '../../shared/error-alert';
import { Pronunciation, SpeakButton } from '../../shared/pronunciation';
import { PetSignals } from '../../core/pet-signals';
import { SupportInvite } from '../../shared/support-invite';
import { formatTranscription } from '../dictionary/vocabulary-labels';
import { formatDuration, formatNextReview, pluralRu } from './learn-format';

type Phase = 'loading' | 'start' | 'task' | 'feedback' | 'finished';

const RETRIES = 2;

/**
 * Занятие: знакомство с новыми словами, выбор перевода, на слух, ввод слова, пропуск в предложении.
 * Удобно одной рукой: крупные варианты, клавиши 1–4 и Enter. Ответ несёт attemptId — при обрыве связи
 * отправляется ещё раз и не засчитывается дважды.
 */
@Component({
  selector: 'app-learn',
  imports: [SupportInvite, 
    NgTemplateOutlet,
    FormsModule,
    RouterLink,
    NgIcon,
    ErrorAlert,
    SpeakButton,
    HlmBadgeImports,
    HlmButtonImports,
    HlmCardImports,
    HlmInputImports,
    HlmSpinnerImports,
  ],
  providers: [
    provideIcons({
      lucideArrowRight,
      lucideBookMarked,
      lucideCircleAlert,
      lucideFlame,
      lucideCircleCheck,
      lucideCircleX,
      lucideLightbulb,
      lucidePlay,
      lucideSparkles,
      lucideVolume2,
      lucideVolumeX,
      lucideX,
    }),
  ],
  templateUrl: './learn.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown)': 'onKey($event)' },
})
export class Learn {
  private readonly api = inject(Api);
  private readonly petSignals = inject(PetSignals);
  private readonly pronunciation = inject(Pronunciation);
  private readonly injector = inject(Injector);

  protected readonly transcription = formatTranscription;
  protected readonly nextReview = formatNextReview;
  protected readonly duration = formatDuration;
  protected readonly Math = Math;

  protected readonly phase = signal<Phase>('loading');
  protected readonly today = signal<TodayDto | null>(null);
  protected readonly weak = signal<WeakSpotsDto | null>(null);
  /** Daily — обычное занятие, Mistakes — работа над ошибками. */
  protected readonly kind = signal<'Daily' | 'Mistakes'>('Daily');
  protected readonly sessionId = signal<string | null>(null);
  protected readonly task = signal<TaskView | null>(null);
  protected readonly progress = signal<SessionProgress>({ done: 0, total: 0 });
  protected readonly stats = signal<SessionStats | null>(null);
  protected readonly result = signal<AnswerResultDto | null>(null);
  protected readonly chosen = signal<number | null>(null);
  protected readonly typed = signal('');
  protected readonly hintShown = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  private readonly answerInput = viewChild<ElementRef<HTMLInputElement>>('answerInput');
  private readonly nextButton = viewChild<ElementRef<HTMLButtonElement>>('nextButton');
  private shownAt = 0;

  protected readonly percent = computed(() => {
    const p = this.progress();
    return Number(p.total) > 0 ? Math.round((Number(p.done) / Number(p.total)) * 100) : 0;
  });
  protected readonly isChoice = computed(() => ['ChooseTranslation', 'ListenChoose', 'ChooseWord'].includes(this.task()?.kind ?? ''));
  protected readonly problemWords = computed(() => Number(this.today()?.problemWords ?? 0));
  /** Слабые места показываем, когда ответов достаточно, чтобы доли что-то значили. */
  protected readonly weakTags = computed(() => {
    const w = this.weak();
    return w && Number(w.totalAnswers) >= 10 ? w.tags.filter((t) => Number(t.count) > 0).slice(0, 4) : [];
  });
  protected readonly isTyping = computed(() => ['TypeWord', 'FillGap'].includes(this.task()?.kind ?? ''));
  protected readonly correctOption = computed(() => {
    const r = this.result();
    const t = this.task();
    return r && t ? t.options.findIndex((o) => o === r.correctAnswer) : -1;
  });
  protected readonly accuracy = computed(() => {
    const s = this.stats();
    const answered = Number(s?.answered ?? 0);
    return answered > 0 ? Math.round(((Number(s!.correct) + Number(s!.almost)) / answered) * 100) : 0;
  });
  /** Сколько слов занятие добавит само (свои новые закончились, автопополнение включено). */
  protected readonly autoAdd = computed(() => Number(this.today()?.autoAddWords ?? 0));
  protected readonly todayCount = computed(() => Number(this.today()?.toStudyNow ?? 0) + this.autoAdd());
  protected readonly todayLabel = computed(() => {
    const n = Number(this.today()?.toStudyNow ?? 0);
    const cards = n > 0 ? `${n} ${pluralRu(n, 'карточка', 'карточки', 'карточек')}` : '';
    const added = this.autoAdd() > 0 ? `${this.autoAdd()} ${pluralRu(this.autoAdd(), 'новое слово', 'новых слова', 'новых слов')} вашего уровня добавится автоматически` : '';
    return [cards, added].filter((x) => x).join(', ');
  });

  constructor() {
    void this.loadToday();
    // Питомец радуется окончанию занятия (и проверяет, сколько угощений принесло занятие).
    effect(() => {
      if (this.phase() === 'finished') {
        untracked(() => this.petSignals.emit('finished'));
      }
    });
  }

  protected async loadToday(): Promise<void> {
    this.phase.set('loading');
    this.error.set(null);
    try {
      const [today, weak] = await Promise.all([this.api.invoke(getLearningToday), this.api.invoke(getWeakSpots)]);
      this.today.set(today);
      this.weak.set(weak);
    } catch (e) {
      this.error.set(problemMessage(e, 'Не удалось загрузить план на сегодня.'));
    }
    this.phase.set('start');
  }

  protected async start(kind: 'Daily' | 'Mistakes' = 'Daily'): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    this.kind.set(kind);
    try {
      const session = await this.api.invoke(startSession, { body: { kind } });
      this.sessionId.set(session.id);
      this.stats.set(session.stats);
      await this.loadNext();
    } catch (e) {
      this.error.set(problemMessage(e, 'Не удалось начать занятие.'));
    } finally {
      this.busy.set(false);
    }
  }

  protected async loadNext(): Promise<void> {
    const id = this.sessionId();
    if (!id) {
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    try {
      const step = await this.api.invoke(getNextTask, { id });
      this.progress.set(step.progress);
      this.stats.set(step.stats);
      if (step.finished || !step.task) {
        this.task.set(null);
        this.phase.set('finished');
        return;
      }
      this.showTask(step.task);
    } catch (e) {
      this.error.set(problemMessage(e, 'Не удалось загрузить задание.'));
    } finally {
      this.busy.set(false);
    }
  }

  /** «Не могу слушать»: на час без заданий на слух, текущее заменяется другим по тому же слову — без штрафа. */
  protected async muteAudio(): Promise<void> {
    const id = this.sessionId();
    if (!id || this.busy()) {
      return;
    }
    this.busy.set(true);
    try {
      const step = await this.api.invoke(muteSessionAudio, { id, minutes: 60 });
      toast.info('Час без заданий на слух', { description: 'Слово останется в занятии — в другом формате.' });
      this.progress.set(step.progress);
      this.stats.set(step.stats);
      if (step.finished || !step.task) {
        this.task.set(null);
        this.phase.set('finished');
      } else {
        this.showTask(step.task);
      }
    } catch (e) {
      this.error.set(problemMessage(e, 'Не удалось переключить задание.'));
    } finally {
      this.busy.set(false);
    }
  }

  protected choose(index: number): void {
    const t = this.task();
    if (this.phase() !== 'task' || !t || this.busy()) {
      return;
    }
    this.chosen.set(index);
    void this.submit(String(index));
  }

  protected check(): void {
    if (this.phase() === 'task' && this.isTyping() && !this.busy()) {
      void this.submit(this.typed());
    }
  }

  protected dontKnow(): void {
    if (this.phase() === 'task' && !this.busy()) {
      this.typed.set('');
      void this.submit('');
    }
  }

  protected showHint(): void {
    this.hintShown.set(true);
    this.focus(this.answerInput);
  }

  protected continueIntro(): void {
    if (this.phase() === 'task' && !this.busy()) {
      void this.submit('');
    }
  }

  protected speak(text: string | null | undefined, url?: string | null): void {
    if (text) {
      this.pronunciation.speak(text, url);
    }
  }

  protected async finish(): Promise<void> {
    const id = this.sessionId();
    if (!id) {
      return;
    }
    this.busy.set(true);
    try {
      const session = await this.api.invoke(finishSession, { id });
      this.stats.set(session.stats);
      this.phase.set('finished');
    } catch (e) {
      this.error.set(problemMessage(e, 'Не удалось завершить занятие.'));
    } finally {
      this.busy.set(false);
    }
  }

  protected async again(): Promise<void> {
    this.sessionId.set(null);
    await this.loadToday();
  }

  protected onKey(event: KeyboardEvent): void {
    if (event.target instanceof HTMLInputElement && event.key !== 'Enter') {
      return;
    }
    const phase = this.phase();
    if (phase === 'feedback' && event.key === 'Enter') {
      event.preventDefault();
      void this.loadNext();
    } else if (phase === 'task' && this.isChoice() && /^[1-4]$/.test(event.key)) {
      const index = Number(event.key) - 1;
      if (index < (this.task()?.options.length ?? 0)) {
        this.choose(index);
      }
    } else if (phase === 'task' && this.task()?.kind === 'Introduce' && event.key === 'Enter') {
      event.preventDefault();
      this.continueIntro();
    }
  }

  private showTask(task: TaskView): void {
    this.task.set(task);
    this.result.set(null);
    this.chosen.set(null);
    this.typed.set('');
    this.hintShown.set(false);
    this.phase.set('task');
    this.shownAt = performance.now();

    if (task.kind === 'Introduce' || task.kind === 'ListenChoose') {
      this.speak(task.audioText, task.audioUrl);
    }
    if (task.kind === 'TypeWord' || task.kind === 'FillGap') {
      this.focus(this.answerInput);
    }
  }

  private async submit(answer: string): Promise<void> {
    const id = this.sessionId();
    const task = this.task();
    if (!id || !task) {
      return;
    }

    const body: AnswerRequest = {
      attemptId: crypto.randomUUID(),
      taskId: task.taskId,
      answer,
      usedHint: this.hintShown(),
      elapsedMs: Math.round(performance.now() - this.shownAt),
    };

    this.busy.set(true);
    this.error.set(null);
    try {
      const result = await this.sendWithRetry(id, body);
      this.progress.set(result.progress);
      if (task.kind === 'Introduce') {
        this.busy.set(false);
        await this.loadNext();
        return;
      }
      this.result.set(result);
      this.petSignals.emit(result.verdict === 'Wrong' ? 'wrong' : 'correct');
      this.phase.set('feedback');
      this.focus(this.nextButton);
    } catch (e) {
      if (e instanceof HttpErrorResponse && e.status === 409) {
        // Задание устарело (например, занятие открыто во второй вкладке) — берём актуальное.
        this.busy.set(false);
        await this.loadNext();
        return;
      }
      this.chosen.set(null);
      this.error.set(problemMessage(e, 'Не удалось отправить ответ.'));
    } finally {
      this.busy.set(false);
    }
  }

  /** При обрыве связи отправляет тот же ответ ещё раз: сервер узнаёт attemptId и не считает его дважды. */
  private async sendWithRetry(id: string, body: AnswerRequest): Promise<AnswerResultDto> {
    for (let attempt = 0; ; attempt++) {
      try {
        return await this.api.invoke(answerTask, { id, body });
      } catch (e) {
        if (attempt >= RETRIES || !(e instanceof HttpErrorResponse) || e.status !== 0) {
          throw e;
        }
        await new Promise((resolve) => setTimeout(resolve, 800 * (attempt + 1)));
      }
    }
  }

  private focus(ref: () => ElementRef<HTMLElement> | undefined): void {
    afterNextRender(() => ref()?.nativeElement.focus(), { injector: this.injector });
  }
}

import { ChangeDetectionStrategy, Component, computed, inject, resource } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideBookOpen, lucideBrain, lucideGauge, lucidePlay, lucideRepeat, lucideSparkles, lucideTarget } from '@ng-icons/lucide';
import { HlmBadgeImports } from '@spartan-ng/helm/badge';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { Api } from '../../core/api/api';
import { getLearningToday } from '../../core/api/fn/learning/get-learning-today';
import { getLearningPlan } from '../../core/api/fn/placement/get-learning-plan';
import { AuthService } from '../../core/auth/auth.service';
import { HealthService } from '../../core/health/health.service';

@Component({
  selector: 'app-home',
  imports: [RouterLink, NgIcon, HlmBadgeImports, HlmButtonImports, HlmCardImports],
  providers: [provideIcons({ lucideBookOpen, lucideBrain, lucideGauge, lucidePlay, lucideRepeat, lucideSparkles, lucideTarget })],
  templateUrl: './home.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Home {
  protected readonly auth = inject(AuthService);
  protected readonly apiStatus = toSignal(inject(HealthService).ready());
  private readonly api = inject(Api);

  /** План на сегодня — только для вошедшего пользователя. */
  protected readonly today = resource({
    params: () => (this.auth.isAuthenticated() ? {} : undefined),
    loader: () => this.api.invoke(getLearningToday),
  });
  protected readonly toStudy = computed(() => Number(this.today.value()?.toStudyNow ?? 0));
  protected readonly plan = resource({
    params: () => (this.auth.isAuthenticated() ? {} : undefined),
    loader: () => this.api.invoke(getLearningPlan),
  });

  protected readonly features = [
    { icon: 'lucideTarget', title: 'Под ваши цели', text: 'Путешествия, работа, экзамены — слова и темы подбираются под задачу.' },
    { icon: 'lucideRepeat', title: 'Интервальное повторение', text: 'Слово возвращается ровно тогда, когда вы начинаете его забывать.' },
    { icon: 'lucideBrain', title: 'Работа над ошибками', text: 'Частые ошибки попадают в отдельные упражнения, пока не уйдут.' },
  ];
}

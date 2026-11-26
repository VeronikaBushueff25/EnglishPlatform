import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideBrain, lucideRepeat, lucideSparkles, lucideTarget } from '@ng-icons/lucide';
import { HlmBadgeImports } from '@spartan-ng/helm/badge';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { AuthService } from '../../core/auth/auth.service';
import { HealthService } from '../../core/health/health.service';

@Component({
  selector: 'app-home',
  imports: [RouterLink, NgIcon, HlmBadgeImports, HlmButtonImports, HlmCardImports],
  providers: [provideIcons({ lucideBrain, lucideRepeat, lucideSparkles, lucideTarget })],
  templateUrl: './home.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Home {
  protected readonly auth = inject(AuthService);
  protected readonly apiStatus = toSignal(inject(HealthService).ready());

  protected readonly features = [
    { icon: 'lucideTarget', title: 'Под ваши цели', text: 'Путешествия, работа, экзамены — слова и темы подбираются под задачу.' },
    { icon: 'lucideRepeat', title: 'Интервальное повторение', text: 'Слово возвращается ровно тогда, когда вы начинаете его забывать.' },
    { icon: 'lucideBrain', title: 'Работа над ошибками', text: 'Частые ошибки попадают в отдельные упражнения, пока не уйдут.' },
  ];
}

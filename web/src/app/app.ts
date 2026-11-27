import { ChangeDetectionStrategy, Component, effect, inject } from '@angular/core';
import { IsActiveMatchOptions, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideBookOpen, lucideChartColumn, lucideGamepad2, lucideLogOut, lucidePlay, lucideShieldCheck, lucideUserRound } from './core/header-icons';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmToasterImports } from '@spartan-ng/helm/sonner';
import { Api } from './core/api/api';
import { getMe } from './core/api/fn/profile/get-me';
import { AuthService } from './core/auth/auth.service';
import { I18n } from './core/i18n/i18n';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, NgIcon, HlmButtonImports, HlmToasterImports],
  providers: [provideIcons({ lucideBookOpen, lucideChartColumn, lucideGamepad2, lucideLogOut, lucidePlay, lucideShieldCheck, lucideUserRound })],
  templateUrl: './app.html',
  host: { class: 'flex min-h-dvh flex-col' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  protected readonly auth = inject(AuthService);
  private readonly api = inject(Api);
  private readonly i18n = inject(I18n);

  constructor() {
    // Язык интерфейса хранится в профиле: после входа берём его оттуда (выбран на другом устройстве — применится и здесь).
    effect(() => {
      if (this.auth.isAuthenticated()) {
        this.api.invoke(getMe).then(
          (me) => void this.i18n.use(me.settings.interfaceLanguage),
          () => undefined,
        );
      }
    });
  }

  /** «Админка» (/admin → /admin/users) подсвечена в любом её разделе. */
  protected readonly adminMatch: IsActiveMatchOptions = { paths: 'subset', queryParams: 'ignored', fragment: 'ignored', matrixParams: 'ignored' };

  protected logout(): void {
    void this.auth.logout();
  }
}

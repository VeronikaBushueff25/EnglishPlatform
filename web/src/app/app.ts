import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { IsActiveMatchOptions, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideBookOpen, lucideChartColumn, lucideGamepad2, lucideGraduationCap, lucideLogOut, lucidePlay, lucideShieldCheck, lucideUserRound } from './core/header-icons';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmToasterImports } from '@spartan-ng/helm/sonner';
import { AuthService } from './core/auth/auth.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, NgIcon, HlmButtonImports, HlmToasterImports],
  providers: [provideIcons({ lucideBookOpen, lucideChartColumn, lucideGamepad2, lucideGraduationCap, lucideLogOut, lucidePlay, lucideShieldCheck, lucideUserRound })],
  templateUrl: './app.html',
  host: { class: 'flex min-h-dvh flex-col' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  protected readonly auth = inject(AuthService);

  /** «Админка» (/admin → /admin/users) подсвечена в любом её разделе. */
  protected readonly adminMatch: IsActiveMatchOptions = { paths: 'subset', queryParams: 'ignored', fragment: 'ignored', matrixParams: 'ignored' };

  protected logout(): void {
    void this.auth.logout();
  }
}

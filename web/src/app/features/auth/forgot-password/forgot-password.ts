import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideMailCheck } from '@ng-icons/lucide';
import { HlmAlertImports } from '@spartan-ng/helm/alert';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmFieldImports } from '@spartan-ng/helm/field';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { HlmSpinnerImports } from '@spartan-ng/helm/spinner';
import { Api } from '../../../core/api/api';
import { forgotPassword } from '../../../core/api/functions';
import { problemMessage } from '../../../core/http/problem';
import { ErrorAlert } from '../../../shared/error-alert';

@Component({
  selector: 'app-forgot-password',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    NgIcon,
    ErrorAlert,
    HlmAlertImports,
    HlmButtonImports,
    HlmCardImports,
    HlmFieldImports,
    HlmInputImports,
    HlmSpinnerImports,
  ],
  providers: [provideIcons({ lucideMailCheck })],
  templateUrl: './forgot-password.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ForgotPassword {
  private readonly api = inject(Api);

  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
  });
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly sent = signal(false);

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    try {
      await this.api.invoke(forgotPassword, { body: { email: this.form.getRawValue().email.trim() } });
      this.sent.set(true);
    } catch (e) {
      this.error.set(problemMessage(e));
    } finally {
      this.submitting.set(false);
    }
  }
}

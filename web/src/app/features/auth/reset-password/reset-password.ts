import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCircleCheck } from '@ng-icons/lucide';
import { HlmAlertImports } from '@spartan-ng/helm/alert';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmFieldImports } from '@spartan-ng/helm/field';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { HlmSpinnerImports } from '@spartan-ng/helm/spinner';
import { Api } from '../../../core/api/api';
import { resetPassword } from '../../../core/api/functions';
import { problemMessage } from '../../../core/http/problem';
import { ErrorAlert } from '../../../shared/error-alert';
import { PASSWORD_HINT, passwordValidators, passwordsMatch } from '../password-rules';

@Component({
  selector: 'app-reset-password',
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
  providers: [provideIcons({ lucideCircleCheck })],
  templateUrl: './reset-password.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResetPassword {
  private readonly api = inject(Api);

  /** Query-параметры из ссылки в письме. */
  readonly email = input<string>();
  readonly token = input<string>();

  protected readonly linkValid = computed(() => !!this.email() && !!this.token());
  protected readonly passwordHint = PASSWORD_HINT;
  protected readonly form = inject(NonNullableFormBuilder).group(
    {
      password: ['', passwordValidators],
      confirm: ['', Validators.required],
    },
    { validators: passwordsMatch },
  );
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly done = signal(false);

  protected async submit(): Promise<void> {
    if (this.form.invalid || !this.linkValid()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    try {
      await this.api.invoke(resetPassword, {
        body: { email: this.email()!, token: this.token()!, newPassword: this.form.getRawValue().password },
      });
      this.done.set(true);
    } catch (e) {
      this.error.set(problemMessage(e));
    } finally {
      this.submitting.set(false);
    }
  }
}

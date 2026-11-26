import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmFieldImports } from '@spartan-ng/helm/field';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { HlmSpinnerImports } from '@spartan-ng/helm/spinner';
import { AuthService } from '../../../core/auth/auth.service';
import { problemMessage } from '../../../core/http/problem';
import { ErrorAlert } from '../../../shared/error-alert';
import { safeReturnUrl } from '../safe-return-url';

@Component({
  selector: 'app-login',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    ErrorAlert,
    HlmButtonImports,
    HlmCardImports,
    HlmFieldImports,
    HlmInputImports,
    HlmSpinnerImports,
  ],
  templateUrl: './login.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  /** Query-параметр: куда вернуться после входа. */
  readonly returnUrl = input<string>();

  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    password: ['', [Validators.required, Validators.maxLength(128)]],
  });
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    try {
      const { email, password } = this.form.getRawValue();
      await this.auth.login({ email: email.trim(), password });
      await this.router.navigateByUrl(safeReturnUrl(this.returnUrl()));
    } catch (e) {
      this.error.set(problemMessage(e));
    } finally {
      this.submitting.set(false);
    }
  }
}

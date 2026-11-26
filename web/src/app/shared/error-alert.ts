import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCircleAlert } from '@ng-icons/lucide';
import { HlmAlertImports } from '@spartan-ng/helm/alert';

/** Ошибка формы или загрузки: красный блок с иконкой. */
@Component({
  selector: 'app-error-alert',
  imports: [HlmAlertImports, NgIcon],
  providers: [provideIcons({ lucideCircleAlert })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div hlmAlert variant="destructive">
      <ng-icon name="lucideCircleAlert" />
      <p hlmAlertDescription>{{ message() }}</p>
    </div>
  `,
})
export class ErrorAlert {
  readonly message = input.required<string>();
}

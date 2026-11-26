import { ChangeDetectionStrategy, Component, booleanAttribute, input, output } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideTrash2 } from '@ng-icons/lucide';
import { HlmAlertDialogImports } from '@spartan-ng/helm/alert-dialog';
import { HlmButtonImports } from '@spartan-ng/helm/button';

/** Кнопка с подтверждением в диалоге: по умолчанию удаление, с destructive=false — любое действие. */
@Component({
  selector: 'app-confirm-delete',
  imports: [NgIcon, HlmAlertDialogImports, HlmButtonImports],
  providers: [provideIcons({ lucideTrash2 })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hlm-alert-dialog>
      @if (iconOnly()) {
        <button
          hlmAlertDialogTrigger
          hlmBtn
          variant="ghost"
          size="icon"
          type="button"
          class="text-destructive"
          [disabled]="disabled()"
          [attr.aria-label]="label()"
        >
          <ng-icon name="lucideTrash2" />
        </button>
      } @else if (destructive()) {
        <button hlmAlertDialogTrigger hlmBtn variant="outline" type="button" class="text-destructive h-11 w-full sm:w-auto" [disabled]="disabled()">
          <ng-icon name="lucideTrash2" />
          {{ label() }}
        </button>
      } @else {
        <button hlmAlertDialogTrigger hlmBtn variant="outline" type="button" [class]="triggerClass()" [disabled]="disabled()">
          {{ label() }}
        </button>
      }
      <hlm-alert-dialog-content *hlmAlertDialogPortal="let ctx">
        <hlm-alert-dialog-header>
          <h2 hlmAlertDialogTitle>{{ title() }}</h2>
          @if (description(); as text) {
            <p hlmAlertDialogDescription>{{ text }}</p>
          }
        </hlm-alert-dialog-header>
        <hlm-alert-dialog-footer>
          <button hlmAlertDialogCancel>Отмена</button>
          <button hlmAlertDialogAction [variant]="destructive() ? 'destructive' : 'default'" (click)="ctx.close(); confirmed.emit()">
            {{ confirmLabel() }}
          </button>
        </hlm-alert-dialog-footer>
      </hlm-alert-dialog-content>
    </hlm-alert-dialog>
  `,
})
export class ConfirmDelete {
  readonly label = input('Удалить');
  readonly title = input.required<string>();
  readonly description = input<string>();
  readonly disabled = input(false, { transform: booleanAttribute });
  readonly iconOnly = input(false, { transform: booleanAttribute });
  /** false — обычное подтверждение (кнопка без красного), например «Перенести». */
  readonly destructive = input(true, { transform: booleanAttribute });
  readonly confirmLabel = input('Удалить');
  readonly triggerClass = input('h-11 w-full sm:w-auto');
  readonly confirmed = output<void>();
}

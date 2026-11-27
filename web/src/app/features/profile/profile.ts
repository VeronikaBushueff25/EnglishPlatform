import {
  ChangeDetectionStrategy,
  Component,
  booleanAttribute,
  computed,
  effect,
  inject,
  input,
  resource,
  signal,
} from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucidePartyPopper } from '@ng-icons/lucide';
import { toast } from '@spartan-ng/brain/sonner';
import { HlmAlertImports } from '@spartan-ng/helm/alert';
import { HlmBadgeImports } from '@spartan-ng/helm/badge';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmCheckboxImports } from '@spartan-ng/helm/checkbox';
import { HlmFieldImports } from '@spartan-ng/helm/field';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { HlmNativeSelectImports } from '@spartan-ng/helm/native-select';
import { HlmSpinnerImports } from '@spartan-ng/helm/spinner';
import { Api } from '../../core/api/api';
import { getMe, updateMe, updateSettings } from '../../core/api/functions';
import { InterfaceLanguage } from '../../core/api/models/interface-language';
import { LearningGoal } from '../../core/api/models/learning-goal';
import { MeResponse } from '../../core/api/models/me-response';
import { problemMessage } from '../../core/http/problem';
import { Appearance } from '../../shared/appearance';
import { ErrorAlert } from '../../shared/error-alert';
import { I18n } from '../../core/i18n/i18n';
import { GOAL_LABELS, LANGUAGE_LABELS, PLAN_LABELS, allTimeZones, browserTimeZone } from './profile-labels';

@Component({
  selector: 'app-profile',
  imports: [
    Appearance,
    ReactiveFormsModule,
    NgIcon,
    ErrorAlert,
    HlmAlertImports,
    HlmBadgeImports,
    HlmButtonImports,
    HlmCardImports,
    HlmCheckboxImports,
    HlmFieldImports,
    HlmInputImports,
    HlmNativeSelectImports,
    HlmSpinnerImports,
  ],
  providers: [provideIcons({ lucidePartyPopper })],
  templateUrl: './profile.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Profile {
  private readonly api = inject(Api);
  private readonly fb = inject(NonNullableFormBuilder);

  /** ?welcome=1 после регистрации: показываем подсказку заполнить профиль. */
  readonly welcome = input(false, { transform: booleanAttribute });

  protected readonly goalLabels = GOAL_LABELS;
  protected readonly goals = Object.keys(GOAL_LABELS) as LearningGoal[];
  protected readonly languageLabels = LANGUAGE_LABELS;
  protected readonly languages = Object.keys(LANGUAGE_LABELS) as InterfaceLanguage[];
  protected readonly planLabels = PLAN_LABELS;
  protected readonly timeZones = allTimeZones();
  protected readonly browserZone = browserTimeZone();

  /** Профиль с сервера; после сохранения подменяется ответом PUT, без повторной загрузки. */
  private readonly i18n = inject(I18n);
  protected readonly me = resource({ loader: () => this.api.invoke(getMe) });
  protected readonly loadError = computed(() =>
    this.me.error() ? problemMessage(this.me.error(), 'Не удалось загрузить профиль.') : null,
  );

  protected readonly profileForm = this.fb.group({
    displayName: ['', [Validators.maxLength(64)]],
    timeZone: ['', [Validators.required, Validators.maxLength(64)]],
  });
  protected readonly profileSaving = signal(false);

  protected readonly settingsForm = this.fb.group({
    interfaceLanguage: this.fb.control<InterfaceLanguage>('Ru'),
    goals: this.fb.control<LearningGoal[]>([]),
    dailyNewWords: [10, [Validators.required, Validators.min(1), Validators.max(100)]],
    dailyReviews: [100, [Validators.required, Validators.min(10), Validators.max(1000)]],
    autoAddWords: [true],
  });
  protected readonly settingsSaving = signal(false);

  constructor() {
    // Пришли данные с сервера (загрузка или ответ на сохранение): заполняем формы и снимаем «изменено».
    effect(() => {
      const me = this.me.value();
      if (me) {
        this.fillForms(me);
      }
    });
  }

  protected hasGoal(goal: LearningGoal): boolean {
    return this.settingsForm.controls.goals.value.includes(goal);
  }

  protected toggleGoal(goal: LearningGoal, checked: boolean): void {
    const control = this.settingsForm.controls.goals;
    const next = checked ? [...control.value, goal] : control.value.filter((g) => g !== goal);
    control.setValue(this.goals.filter((g) => next.includes(g)));
    control.markAsDirty();
  }

  protected useBrowserZone(): void {
    if (this.browserZone) {
      this.profileForm.controls.timeZone.setValue(this.browserZone);
      this.profileForm.controls.timeZone.markAsDirty();
    }
  }

  protected async saveProfile(): Promise<void> {
    if (this.profileForm.invalid) {
      this.profileForm.markAllAsTouched();
      return;
    }

    this.profileSaving.set(true);
    try {
      const { displayName, timeZone } = this.profileForm.getRawValue();
      this.me.set(await this.api.invoke(updateMe, { body: { displayName: displayName.trim() || null, timeZone } }));
      toast.success('Профиль сохранён');
    } catch (e) {
      toast.error(problemMessage(e));
    } finally {
      this.profileSaving.set(false);
    }
  }

  protected async saveSettings(): Promise<void> {
    if (this.settingsForm.invalid) {
      this.settingsForm.markAllAsTouched();
      return;
    }

    this.settingsSaving.set(true);
    try {
      const value = this.settingsForm.getRawValue();
      this.me.set(
        await this.api.invoke(updateSettings, {
          body: {
            interfaceLanguage: value.interfaceLanguage,
            goals: value.goals,
            dailyNewWords: Number(value.dailyNewWords),
            dailyReviews: Number(value.dailyReviews),
            autoAddWords: value.autoAddWords,
          },
        }),
      );
      await this.i18n.use(value.interfaceLanguage);
      toast.success('Настройки сохранены');
    } catch (e) {
      toast.error(problemMessage(e));
    } finally {
      this.settingsSaving.set(false);
    }
  }

  private fillForms(me: MeResponse): void {
    this.profileForm.reset({ displayName: me.displayName ?? '', timeZone: me.timeZone });
    this.settingsForm.reset({
      interfaceLanguage: me.settings.interfaceLanguage,
      goals: [...me.settings.goals],
      dailyNewWords: Number(me.settings.dailyNewWords),
      dailyReviews: Number(me.settings.dailyReviews),
      autoAddWords: me.settings.autoAddWords ?? true,
    });
  }
}

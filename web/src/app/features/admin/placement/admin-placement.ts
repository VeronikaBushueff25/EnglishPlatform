import { ChangeDetectionStrategy, Component, computed, effect, inject, input, linkedSignal, resource, signal } from '@angular/core';
import { FormArray, FormControl, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideArrowLeft, lucidePlus, lucideX } from '@ng-icons/lucide';
import { toast } from '@spartan-ng/brain/sonner';
import { HlmBadgeImports } from '@spartan-ng/helm/badge';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmCheckboxImports } from '@spartan-ng/helm/checkbox';
import { HlmFieldImports } from '@spartan-ng/helm/field';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { HlmNativeSelectImports } from '@spartan-ng/helm/native-select';
import { HlmSpinnerImports } from '@spartan-ng/helm/spinner';
import { HlmTextareaImports } from '@spartan-ng/helm/textarea';
import { Api } from '../../../core/api/api';
import {
  adminCreatePlacementQuestion,
  adminDeletePlacementQuestion,
  adminGetPlacementQuestion,
  adminListPlacementQuestions,
  adminUpdatePlacementQuestion,
} from '../../../core/api/functions';
import { Cefr } from '../../../core/api/models/cefr';
import { PlacementQuestionDto } from '../../../core/api/models/placement-question-dto';
import { Skill } from '../../../core/api/models/skill';
import { problemMessage } from '../../../core/http/problem';
import { ConfirmDelete } from '../../../shared/confirm-delete';
import { ErrorAlert } from '../../../shared/error-alert';
import { Pager } from '../../../shared/pager';
import { CEFR_LEVELS } from '../../dictionary/vocabulary-labels';
import { SKILL_LABELS } from '../../placement/placement-labels';
import { AdminNav } from '../admin-nav';

const PAGE_SIZE = 30;
const SKILLS = Object.keys(SKILL_LABELS) as Skill[];

/** Банк вопросов теста уровня (/admin/placement): фильтры по навыку и уровню, сколько вопросов на каждом уровне. */
@Component({
  selector: 'app-admin-placement',
  imports: [RouterLink, NgIcon, AdminNav, ErrorAlert, Pager, HlmBadgeImports, HlmButtonImports, HlmCardImports, HlmNativeSelectImports, HlmSpinnerImports],
  providers: [provideIcons({ lucidePlus })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4">
      <app-admin-nav />

      <div class="flex flex-wrap items-center justify-between gap-2">
        <h1 class="text-2xl font-semibold tracking-tight">Тест уровня</h1>
        <a hlmBtn size="sm" routerLink="/admin/placement/new">
          <ng-icon name="lucidePlus" />
          Новый вопрос
        </a>
      </div>
      <p class="text-muted-foreground -mt-2 text-sm">
        Тест задаёт 7 вопросов по лексике, 7 по грамматике и 4 по чтению, поднимаясь и опускаясь по уровням.
        Чтобы вопросы не повторялись, на каждом уровне нужно хотя бы 2–3 вопроса.
      </p>

      <div class="grid grid-cols-2 gap-2">
        <hlm-native-select class="w-full" selectClass="h-10" aria-label="Навык" [value]="skill()" (valueChange)="skill.set($any($event ?? ''))">
          <option value="">Все навыки</option>
          @for (s of skills; track s) {
            <option [value]="s">{{ skillLabels[s] }}</option>
          }
        </hlm-native-select>
        <hlm-native-select class="w-full" selectClass="h-10" aria-label="Уровень" [value]="level()" (valueChange)="level.set($any($event ?? ''))">
          <option value="">Все уровни</option>
          @for (l of levels; track l) {
            <option [value]="l">{{ l }}</option>
          }
        </hlm-native-select>
      </div>

      @if (questions.error()) {
        <app-error-alert message="Не удалось загрузить вопросы." />
      }

      <p class="text-muted-foreground -mb-2 text-sm">Всего: {{ total() }}</p>
      <ul class="flex flex-col gap-2 transition-opacity" [class.opacity-60]="questions.isLoading()">
        @for (q of questions.value()?.items ?? []; track q.id) {
          <li>
            <a hlmCard [routerLink]="['/admin/placement', q.id]" class="hover:border-primary/40 gap-1 py-4 transition-colors" [class.opacity-60]="!q.isActive">
              <div class="flex flex-wrap items-center gap-1.5 px-5">
                <span hlmBadge variant="secondary">{{ q.level }}</span>
                <span hlmBadge variant="outline">{{ skillLabels[q.skill] }}</span>
                @if (!q.isActive) {
                  <span hlmBadge variant="outline" class="border-dashed">выключен</span>
                }
              </div>
              <p class="px-5 font-medium" lang="en">{{ q.prompt }}</p>
              <p class="text-muted-foreground px-5 text-sm">Ответ: {{ q.options[+q.correctOption] }}</p>
            </a>
          </li>
        } @empty {
          @if (!questions.isLoading()) {
            <li class="text-muted-foreground py-6 text-center">Вопросов нет.</li>
          }
        }
      </ul>
      <app-pager [(page)]="page" [totalPages]="totalPages()" [disabled]="questions.isLoading()" />
    </div>
  `,
})
export class AdminPlacement {
  private readonly api = inject(Api);

  protected readonly skills = SKILLS;
  protected readonly skillLabels = SKILL_LABELS;
  protected readonly levels = CEFR_LEVELS;
  protected readonly skill = signal<Skill | ''>('');
  protected readonly level = signal<Cefr | ''>('');
  protected readonly page = linkedSignal({ source: () => [this.skill(), this.level()], computation: () => 1 });

  protected readonly questions = resource({
    params: () => ({ skill: this.skill() || undefined, level: this.level() || undefined, page: this.page(), pageSize: PAGE_SIZE }),
    loader: ({ params }) => this.api.invoke(adminListPlacementQuestions, params),
  });
  protected readonly total = computed(() => Number(this.questions.value()?.totalCount ?? 0));
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.total() / PAGE_SIZE)));
}

/** Вопрос теста: создание (/admin/placement/new) и правка (/admin/placement/:id). */
@Component({
  selector: 'app-admin-placement-form',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    NgIcon,
    ConfirmDelete,
    ErrorAlert,
    HlmButtonImports,
    HlmCardImports,
    HlmCheckboxImports,
    HlmFieldImports,
    HlmInputImports,
    HlmNativeSelectImports,
    HlmSpinnerImports,
    HlmTextareaImports,
  ],
  providers: [provideIcons({ lucideArrowLeft, lucidePlus, lucideX })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4">
      <a hlmBtn variant="ghost" size="sm" routerLink="/admin/placement" class="self-start">
        <ng-icon name="lucideArrowLeft" />
        Тест уровня
      </a>

      @if (question.error()) {
        <app-error-alert message="Не удалось загрузить вопрос." />
      } @else if (isNew() || question.hasValue()) {
        <section hlmCard>
          <div hlmCardHeader>
            <h1 hlmCardTitle class="text-xl">{{ isNew() ? 'Новый вопрос' : 'Вопрос теста' }}</h1>
          </div>
          <div hlmCardContent>
            <form [formGroup]="form" (ngSubmit)="save()" novalidate>
              <div hlmFieldGroup>
                <div class="grid grid-cols-2 gap-4">
                  <div hlmField>
                    <label hlmFieldLabel for="skill">Навык</label>
                    <hlm-native-select selectId="skill" formControlName="skill" selectClass="h-11" class="w-full">
                      @for (s of skills; track s) {
                        <option [value]="s">{{ skillLabels[s] }}</option>
                      }
                    </hlm-native-select>
                  </div>
                  <div hlmField>
                    <label hlmFieldLabel for="level">Уровень</label>
                    <hlm-native-select selectId="level" formControlName="level" selectClass="h-11" class="w-full">
                      @for (l of levels; track l) {
                        <option [value]="l">{{ l }}</option>
                      }
                    </hlm-native-select>
                  </div>
                </div>

                @if (form.controls.skill.value === 'Reading') {
                  <div hlmField>
                    <label hlmFieldLabel for="passage">Текст для чтения</label>
                    <textarea hlmTextarea id="passage" formControlName="passage" rows="5" lang="en"></textarea>
                  </div>
                }

                <div hlmField>
                  <label hlmFieldLabel for="prompt">Вопрос</label>
                  <input hlmInput id="prompt" formControlName="prompt" class="h-11" autocomplete="off" placeholder="She ___ a teacher." />
                  <p hlmFieldDescription>Для лексики: «Что значит «слово»?», для грамматики — предложение с пропуском ___.</p>
                  <hlm-field-error>Введите вопрос</hlm-field-error>
                </div>

                <fieldset hlmFieldSet>
                  <legend hlmFieldLegend variant="label">Варианты (отметьте правильный)</legend>
                  <div formArrayName="options" class="flex flex-col gap-2">
                    @for (control of options.controls; track control; let i = $index) {
                      <div class="flex items-center gap-2">
                        <input
                          type="radio"
                          name="correct"
                          class="accent-primary size-5 shrink-0"
                          [attr.aria-label]="'Правильный вариант ' + (i + 1)"
                          [checked]="correct() === i"
                          (change)="setCorrect(i)"
                        />
                        <input hlmInput [formControlName]="i" class="h-11 flex-1" autocomplete="off" [attr.aria-label]="'Вариант ' + (i + 1)" />
                        @if (options.length > 2) {
                          <button hlmBtn variant="ghost" size="icon-lg" type="button" aria-label="Убрать вариант" (click)="removeOption(i)">
                            <ng-icon name="lucideX" />
                          </button>
                        }
                      </div>
                    }
                  </div>
                  @if (options.length < 6) {
                    <button hlmBtn variant="ghost" size="sm" type="button" class="self-start" (click)="addOption()">
                      <ng-icon name="lucidePlus" />
                      Ещё вариант
                    </button>
                  }
                </fieldset>

                <label class="flex items-center gap-3 text-sm">
                  <hlm-checkbox [checked]="form.controls.isActive.value" (checkedChange)="form.controls.isActive.setValue($event); form.markAsDirty()" />
                  Вопрос участвует в тесте
                </label>

                @if (error(); as message) {
                  <app-error-alert [message]="message" />
                }

                <div class="flex flex-col-reverse gap-2 sm:flex-row sm:justify-between">
                  @if (!isNew()) {
                    <app-confirm-delete label="Удалить вопрос" title="Удалить вопрос из банка?" description="Прошлые результаты тестов не изменятся." (confirmed)="remove()" />
                  }
                  <button hlmBtn type="submit" class="h-11 w-full sm:ms-auto sm:w-auto" [disabled]="saving() || (!isNew() && form.pristine)">
                    @if (saving()) {
                      <hlm-spinner aria-label="Сохраняем" />
                    }
                    {{ isNew() ? 'Добавить' : 'Сохранить' }}
                  </button>
                </div>
              </div>
            </form>
          </div>
        </section>
      } @else {
        <div class="text-muted-foreground flex items-center gap-2"><hlm-spinner aria-label="Загрузка" /> Загружаем…</div>
      }
    </div>
  `,
})
export class AdminPlacementForm {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly fb = inject(NonNullableFormBuilder);

  readonly id = input<string | undefined>(undefined);
  protected readonly isNew = computed(() => !this.id());
  protected readonly skills = SKILLS;
  protected readonly skillLabels = SKILL_LABELS;
  protected readonly levels = CEFR_LEVELS;

  protected readonly question = resource({
    params: () => (this.id() ? { id: this.id()! } : undefined),
    loader: ({ params }) => this.api.invoke(adminGetPlacementQuestion, params),
  });

  protected readonly form = this.fb.group({
    skill: this.fb.control<Skill>('Vocabulary'),
    level: this.fb.control<Cefr>('A2'),
    prompt: ['', [Validators.required, Validators.maxLength(500)]],
    passage: ['', Validators.maxLength(2000)],
    options: this.fb.array([this.option(''), this.option(''), this.option(''), this.option('')]),
    isActive: [true],
  });
  protected readonly correct = signal(0);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    effect(() => {
      if (this.question.hasValue()) {
        this.fill(this.question.value());
      }
    });
  }

  protected get options(): FormArray<FormControl<string>> {
    return this.form.controls.options;
  }

  protected setCorrect(index: number): void {
    this.correct.set(index);
    this.form.markAsDirty();
  }

  protected addOption(): void {
    this.options.push(this.option(''));
  }

  protected removeOption(index: number): void {
    this.options.removeAt(index);
    if (this.correct() >= this.options.length || this.correct() === index) {
      this.correct.set(0);
    }
    this.form.markAsDirty();
  }

  protected async save(): Promise<void> {
    this.form.markAllAsTouched();
    const v = this.form.getRawValue();
    if (this.form.invalid || v.options.some((o) => !o.trim())) {
      this.error.set('Заполните вопрос и все варианты.');
      return;
    }
    const body = {
      skill: v.skill,
      level: v.level,
      prompt: v.prompt,
      passage: v.skill === 'Reading' ? v.passage || null : null,
      options: v.options,
      correctOption: this.correct(),
      isActive: v.isActive,
    };
    this.saving.set(true);
    this.error.set(null);
    try {
      if (this.isNew()) {
        const created = await this.api.invoke(adminCreatePlacementQuestion, { body });
        toast.success('Вопрос добавлен');
        await this.router.navigate(['/admin/placement', created.id], { replaceUrl: true });
      } else {
        this.question.set(await this.api.invoke(adminUpdatePlacementQuestion, { id: this.id()!, body }));
        toast.success('Сохранено');
      }
    } catch (e) {
      this.error.set(problemMessage(e, 'Не удалось сохранить вопрос.'));
    } finally {
      this.saving.set(false);
    }
  }

  protected async remove(): Promise<void> {
    try {
      await this.api.invoke(adminDeletePlacementQuestion, { id: this.id()! });
      toast.success('Вопрос удалён');
      await this.router.navigate(['/admin/placement']);
    } catch (e) {
      toast.error(problemMessage(e, 'Не удалось удалить вопрос.'));
    }
  }

  private fill(q: PlacementQuestionDto): void {
    this.options.clear();
    for (const option of q.options) {
      this.options.push(this.option(option));
    }
    this.correct.set(Number(q.correctOption));
    this.form.patchValue({ skill: q.skill, level: q.level, prompt: q.prompt, passage: q.passage ?? '', isActive: q.isActive });
    this.form.markAsPristine();
  }

  private option(value: string): FormControl<string> {
    return this.fb.control(value, Validators.maxLength(200));
  }
}

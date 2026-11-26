import { ChangeDetectionStrategy, Component, computed, effect, inject, input, resource, signal } from '@angular/core';
import { FormArray, FormControl, FormGroup, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideArrowLeft, lucidePlus, lucideX } from '@ng-icons/lucide';
import { toast } from '@spartan-ng/brain/sonner';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmCheckboxImports } from '@spartan-ng/helm/checkbox';
import { HlmFieldImports } from '@spartan-ng/helm/field';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { HlmNativeSelectImports } from '@spartan-ng/helm/native-select';
import { HlmSpinnerImports } from '@spartan-ng/helm/spinner';
import { HlmTextareaImports } from '@spartan-ng/helm/textarea';
import { Api } from '../../../core/api/api';
import { adminCreateGrammarTopic, adminDeleteGrammarTopic, adminUpdateGrammarTopic, getGrammarTopic } from '../../../core/api/functions';
import { GrammarTopicDto } from '../../../core/api/models/grammar-topic-dto';
import { SaveGrammarTopicRequest } from '../../../core/api/models/save-grammar-topic-request';
import { problemMessage } from '../../../core/http/problem';
import { ConfirmDelete } from '../../../shared/confirm-delete';
import { ErrorAlert } from '../../../shared/error-alert';
import { CEFR_LEVELS } from '../../dictionary/vocabulary-labels';

type ExampleGroup = FormGroup<{ text: FormControl<string>; translation: FormControl<string>; isWrong: FormControl<boolean> }>;

/** Создание (/admin/grammar/new) и правка (/admin/grammar/:id) грамматической темы. */
@Component({
  selector: 'app-admin-grammar-form',
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
      <a hlmBtn variant="ghost" size="sm" routerLink="/admin/grammar" class="self-start">
        <ng-icon name="lucideArrowLeft" />
        Грамматика
      </a>

      @if (loadError(); as message) {
        <app-error-alert [message]="message" />
      } @else if (isNew() || topic.hasValue()) {
        <section hlmCard>
          <div hlmCardHeader>
            <h1 hlmCardTitle class="text-xl">{{ isNew() ? 'Новая тема' : form.controls.title.value }}</h1>
          </div>
          <div hlmCardContent>
            <form [formGroup]="form" (ngSubmit)="save()" novalidate>
              <div hlmFieldGroup>
                <div class="grid gap-4 sm:grid-cols-3">
                  <div hlmField class="sm:col-span-2">
                    <label hlmFieldLabel for="title">Название</label>
                    <input hlmInput id="title" formControlName="title" class="h-11" autocomplete="off" />
                    <hlm-field-error>Введите название</hlm-field-error>
                  </div>
                  <div hlmField>
                    <label hlmFieldLabel for="level">Уровень</label>
                    <hlm-native-select selectId="level" formControlName="level" selectClass="h-11" class="w-full">
                      <option value="">—</option>
                      @for (level of levels; track level) {
                        <option [value]="level">{{ level }}</option>
                      }
                    </hlm-native-select>
                  </div>
                </div>
                <div class="grid gap-4 sm:grid-cols-3">
                  <div hlmField class="sm:col-span-2">
                    <label hlmFieldLabel for="slug">Идентификатор</label>
                    <input hlmInput id="slug" formControlName="slug" class="h-11 font-mono" autocomplete="off" autocapitalize="none" placeholder="articles" />
                    <hlm-field-error>Латиница, цифры и дефис</hlm-field-error>
                  </div>
                  <div hlmField>
                    <label hlmFieldLabel for="sortOrder">Порядок</label>
                    <input hlmInput id="sortOrder" type="number" formControlName="sortOrder" class="h-11" />
                  </div>
                </div>
                <div hlmField>
                  <label hlmFieldLabel for="summary">Правило коротко</label>
                  <textarea hlmTextarea id="summary" formControlName="summary" rows="3"></textarea>
                  <p hlmFieldDescription>Показывается сразу после ошибки — два-три предложения.</p>
                  <hlm-field-error>Сформулируйте правило, до 600 символов</hlm-field-error>
                </div>
                <div hlmField>
                  <label hlmFieldLabel for="body">Подробно</label>
                  <textarea hlmTextarea id="body" formControlName="body" rows="8"></textarea>
                  <p hlmFieldDescription>Абзацы разделяйте пустой строкой.</p>
                </div>

                <fieldset hlmFieldSet>
                  <legend hlmFieldLegend variant="label">Примеры</legend>
                  <div formArrayName="examples" class="flex flex-col gap-3">
                    @for (group of examples.controls; track group; let i = $index) {
                      <div [formGroupName]="i" class="flex gap-2">
                        <div class="flex flex-1 flex-col gap-2">
                          <input hlmInput formControlName="text" class="h-11" lang="en" [attr.aria-label]="'Пример ' + (i + 1)" placeholder="Пример на английском" />
                          <input hlmInput formControlName="translation" class="h-11" [attr.aria-label]="'Перевод примера ' + (i + 1)" placeholder="Перевод или пояснение" />
                          <label class="flex items-center gap-2 text-sm">
                            <hlm-checkbox formControlName="isWrong" />
                            Так говорить нельзя
                          </label>
                        </div>
                        <button hlmBtn variant="ghost" size="icon-lg" type="button" aria-label="Убрать пример" (click)="removeExample(i)">
                          <ng-icon name="lucideX" />
                        </button>
                      </div>
                    }
                  </div>
                  <button hlmBtn variant="ghost" size="sm" type="button" class="self-start" (click)="addExample()">
                    <ng-icon name="lucidePlus" />
                    Добавить пример
                  </button>
                </fieldset>

                @if (error(); as message) {
                  <app-error-alert [message]="message" />
                }

                <div class="flex flex-col-reverse gap-2 sm:flex-row sm:justify-between">
                  @if (!isNew()) {
                    <app-confirm-delete
                      label="Удалить тему"
                      [title]="'Удалить тему «' + form.controls.title.value + '»?'"
                      description="Типы ошибок, которые на неё ссылались, останутся без правила."
                      (confirmed)="remove()"
                    />
                  }
                  <button hlmBtn type="submit" class="h-11 w-full sm:ms-auto sm:w-auto" [disabled]="saving() || (!isNew() && form.pristine)">
                    @if (saving()) {
                      <hlm-spinner aria-label="Сохраняем" />
                    }
                    {{ isNew() ? 'Создать' : 'Сохранить' }}
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
export class AdminGrammarForm {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly fb = inject(NonNullableFormBuilder);

  readonly id = input<string | undefined>(undefined);
  protected readonly isNew = computed(() => !this.id());
  protected readonly levels = CEFR_LEVELS;

  protected readonly topic = resource({
    params: () => (this.id() ? { slug: this.id()! } : undefined),
    loader: ({ params }) => this.api.invoke(getGrammarTopic, params),
  });
  protected readonly loadError = computed(() =>
    this.topic.error() ? problemMessage(this.topic.error(), 'Не удалось загрузить тему.') : null,
  );

  protected readonly form = this.fb.group({
    title: ['', [Validators.required, Validators.maxLength(128)]],
    slug: ['', [Validators.required, Validators.maxLength(64), Validators.pattern(/^[a-z0-9]+(-[a-z0-9]+)*$/)]],
    level: [''],
    sortOrder: [100],
    summary: ['', [Validators.required, Validators.maxLength(600)]],
    body: ['', Validators.maxLength(8000)],
    examples: this.fb.array<ExampleGroup>([]),
  });

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    effect(() => {
      if (this.topic.hasValue()) {
        this.fill(this.topic.value());
      }
    });
  }

  protected get examples(): FormArray<ExampleGroup> {
    return this.form.controls.examples;
  }

  protected addExample(): void {
    this.examples.push(this.exampleGroup('', '', false));
  }

  protected removeExample(index: number): void {
    this.examples.removeAt(index);
    this.form.markAsDirty();
  }

  protected async save(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.error.set('Проверьте поля формы.');
      return;
    }
    const v = this.form.getRawValue();
    const body: SaveGrammarTopicRequest = {
      title: v.title,
      slug: v.slug,
      level: v.level || null,
      sortOrder: Number(v.sortOrder) || 0,
      summary: v.summary,
      body: v.body || null,
      examples: v.examples.filter((e) => e.text.trim()).map((e) => ({ text: e.text, translation: e.translation || null, isWrong: e.isWrong })),
    };

    this.saving.set(true);
    this.error.set(null);
    try {
      if (this.isNew()) {
        const created = await this.api.invoke(adminCreateGrammarTopic, { body });
        toast.success(`Тема «${created.title}» создана`);
        await this.router.navigate(['/admin/grammar', created.id], { replaceUrl: true });
      } else {
        const updated = await this.api.invoke(adminUpdateGrammarTopic, { id: this.topic.value()!.id, body });
        this.topic.set(updated);
        toast.success('Сохранено');
      }
    } catch (e) {
      this.error.set(problemMessage(e, 'Не удалось сохранить тему.'));
    } finally {
      this.saving.set(false);
    }
  }

  protected async remove(): Promise<void> {
    try {
      await this.api.invoke(adminDeleteGrammarTopic, { id: this.topic.value()!.id });
      toast.success('Тема удалена');
      await this.router.navigate(['/admin/grammar']);
    } catch (e) {
      toast.error(problemMessage(e, 'Не удалось удалить тему.'));
    }
  }

  private fill(topic: GrammarTopicDto): void {
    this.examples.clear();
    for (const example of topic.examples) {
      this.examples.push(this.exampleGroup(example.text, example.translation ?? '', example.isWrong));
    }
    this.form.patchValue({
      title: topic.title,
      slug: topic.slug,
      level: topic.level ?? '',
      sortOrder: Number(topic.sortOrder),
      summary: topic.summary,
      body: topic.body,
    });
    this.form.markAsPristine();
  }

  private exampleGroup(text: string, translation: string, isWrong: boolean): ExampleGroup {
    return this.fb.group({
      text: [text, Validators.maxLength(300)],
      translation: [translation, Validators.maxLength(300)],
      isWrong: [isWrong],
    });
  }
}

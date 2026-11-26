import { ChangeDetectionStrategy, Component, computed, effect, inject, input, linkedSignal, resource, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideArrowLeft, lucideExternalLink, lucidePlus } from '@ng-icons/lucide';
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
import { adminCreateReadingText } from '../../../core/api/fn/admin-reading/admin-create-reading-text';
import { adminDeleteReadingText } from '../../../core/api/fn/admin-reading/admin-delete-reading-text';
import { adminGetReadingText } from '../../../core/api/fn/admin-reading/admin-get-reading-text';
import { adminListReadingTexts } from '../../../core/api/fn/admin-reading/admin-list-reading-texts';
import { adminUpdateReadingText } from '../../../core/api/fn/admin-reading/admin-update-reading-text';
import { CefrLevel } from '../../../core/api/models/cefr-level';
import { ReadingTextDto } from '../../../core/api/models/reading-text-dto';
import { problemMessage } from '../../../core/http/problem';
import { ConfirmDelete } from '../../../shared/confirm-delete';
import { ErrorAlert } from '../../../shared/error-alert';
import { Pager } from '../../../shared/pager';
import { CEFR_LEVELS } from '../../dictionary/vocabulary-labels';
import { readingMinutes } from '../../reading/reading-pages';
import { pluralRu } from '../../learn/learn-format';
import { AdminNav } from '../admin-nav';

const PAGE_SIZE = 30;

/** Слов в тексте — так же, как считает сервер. */
export function countWords(body: string): number {
  return body.split(/\s+/).filter((w) => w.length > 0).length;
}

function wordsLabel(n: number): string {
  return pluralRu(n, 'слово', 'слова', 'слов');
}

/** Тексты для чтения (/admin/reading): по уровням, скрытые отмечены. */
@Component({
  selector: 'app-admin-reading',
  imports: [RouterLink, NgIcon, AdminNav, ErrorAlert, Pager, HlmBadgeImports, HlmButtonImports, HlmCardImports, HlmNativeSelectImports],
  providers: [provideIcons({ lucidePlus })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4">
      <app-admin-nav />

      <div class="flex flex-wrap items-center justify-between gap-2">
        <h1 class="text-2xl font-semibold tracking-tight">Тексты для чтения</h1>
        <a hlmBtn size="sm" routerLink="/admin/reading/new">
          <ng-icon name="lucidePlus" />
          Новый текст
        </a>
      </div>
      <p class="text-muted-foreground -mt-2 text-sm">
        Тексты видят все пользователи в «Практика → Чтение». Пишите сами или берите тексты со свободной лицензией
        (с указанием источника): статьи СМИ и учебников копировать нельзя.
      </p>

      <hlm-native-select class="w-full sm:w-48" selectClass="h-10" aria-label="Уровень" [value]="level()" (valueChange)="level.set($any($event ?? ''))">
        <option value="">Все уровни</option>
        @for (l of levels; track l) {
          <option [value]="l">{{ l }}</option>
        }
      </hlm-native-select>

      @if (texts.error()) {
        <app-error-alert message="Не удалось загрузить тексты." />
      }
      <p class="text-muted-foreground -mb-2 text-sm">Всего: {{ total() }}</p>
      <ul class="flex flex-col gap-2 transition-opacity" [class.opacity-60]="texts.isLoading()">
        @for (t of texts.value()?.items ?? []; track t.id) {
          <li>
            <a hlmCard [routerLink]="['/admin/reading', t.id]" class="hover:border-primary/40 gap-1 py-4 transition-colors" [class.opacity-60]="!t.isPublished">
              <div class="flex flex-wrap items-center gap-1.5 px-5">
                <span hlmBadge variant="secondary">{{ t.level }}</span>
                @if (t.topic) {
                  <span hlmBadge variant="outline">{{ t.topic }}</span>
                }
                @if (!t.isPublished) {
                  <span hlmBadge variant="outline" class="border-dashed">скрыт</span>
                }
                <span class="text-muted-foreground ms-auto text-xs">{{ t.wordCount }} {{ wordsLabel(+t.wordCount) }}</span>
              </div>
              <p class="px-5 font-medium" lang="en">{{ t.title }}</p>
              <p class="text-muted-foreground line-clamp-1 px-5 text-sm" lang="en">{{ t.preview }}</p>
            </a>
          </li>
        } @empty {
          @if (!texts.isLoading()) {
            <li class="text-muted-foreground py-6 text-center">Текстов нет.</li>
          }
        }
      </ul>
      <app-pager [(page)]="page" [totalPages]="totalPages()" [disabled]="texts.isLoading()" />
    </div>
  `,
})
export class AdminReading {
  private readonly api = inject(Api);

  protected readonly levels = CEFR_LEVELS;
  protected readonly wordsLabel = wordsLabel;
  protected readonly level = signal<CefrLevel | ''>('');
  protected readonly page = linkedSignal({ source: () => this.level(), computation: () => 1 });
  protected readonly texts = resource({
    params: () => ({ level: this.level() || undefined, page: this.page(), pageSize: PAGE_SIZE }),
    loader: ({ params }) => this.api.invoke(adminListReadingTexts, params),
  });
  protected readonly total = computed(() => Number(this.texts.value()?.totalCount ?? 0));
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.total() / PAGE_SIZE)));
}

/** Текст: создание (/admin/reading/new) и правка (/admin/reading/:id). */
@Component({
  selector: 'app-admin-reading-form',
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
  providers: [provideIcons({ lucideArrowLeft, lucideExternalLink })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4">
      <a hlmBtn variant="ghost" size="sm" routerLink="/admin/reading" class="self-start">
        <ng-icon name="lucideArrowLeft" />
        Тексты
      </a>

      @if (text.error()) {
        <app-error-alert message="Не удалось загрузить текст." />
      } @else if (isNew() || text.hasValue()) {
        <section hlmCard>
          <div hlmCardHeader>
            <h1 hlmCardTitle class="text-xl">{{ isNew() ? 'Новый текст' : 'Текст для чтения' }}</h1>
          </div>
          <div hlmCardContent>
            <form [formGroup]="form" (ngSubmit)="save()" novalidate>
              <div hlmFieldGroup>
                <div hlmField>
                  <label hlmFieldLabel for="title">Заголовок</label>
                  <input hlmInput id="title" formControlName="title" class="h-11" lang="en" autocomplete="off" />
                  <hlm-field-error>Введите заголовок</hlm-field-error>
                </div>

                <div class="grid grid-cols-2 gap-4">
                  <div hlmField>
                    <label hlmFieldLabel for="level">Уровень</label>
                    <hlm-native-select selectId="level" formControlName="level" selectClass="h-11" class="w-full">
                      @for (l of levels; track l) {
                        <option [value]="l">{{ l }}</option>
                      }
                    </hlm-native-select>
                  </div>
                  <div hlmField>
                    <label hlmFieldLabel for="topic">Тема</label>
                    <input hlmInput id="topic" formControlName="topic" class="h-11" autocomplete="off" placeholder="Работа" />
                  </div>
                </div>

                <div hlmField>
                  <label hlmFieldLabel for="body">Текст</label>
                  <textarea hlmTextarea id="body" formControlName="body" rows="16" lang="en" class="text-base leading-relaxed"></textarea>
                  <p hlmFieldDescription>
                    Пустая строка — новый абзац. {{ words() }} {{ wordsLabel(words()) }}, около {{ minutes() }} мин чтения.
                  </p>
                  <hlm-field-error>Введите текст</hlm-field-error>
                </div>

                <label class="flex items-center gap-3 text-sm">
                  <hlm-checkbox [checked]="form.controls.isPublished.value" (checkedChange)="form.controls.isPublished.setValue($event); form.markAsDirty()" />
                  Опубликован — виден пользователям
                </label>

                @if (error(); as message) {
                  <app-error-alert [message]="message" />
                }

                <div class="flex flex-col-reverse gap-2 sm:flex-row sm:items-center">
                  @if (!isNew()) {
                    <app-confirm-delete label="Удалить текст" title="Удалить текст?" description="Он пропадёт из библиотеки у всех." (confirmed)="remove()" />
                    @if (form.controls.isPublished.value) {
                      <a hlmBtn variant="ghost" [routerLink]="['/reading', id()]" target="_blank">
                        <ng-icon name="lucideExternalLink" /> Открыть как читатель
                      </a>
                    }
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
export class AdminReadingForm {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly fb = inject(NonNullableFormBuilder);

  readonly id = input<string | undefined>(undefined);
  protected readonly isNew = computed(() => !this.id());
  protected readonly levels = CEFR_LEVELS;
  protected readonly wordsLabel = wordsLabel;

  protected readonly text = resource({
    params: () => (this.id() ? { id: this.id()! } : undefined),
    loader: ({ params }) => this.api.invoke(adminGetReadingText, params),
  });

  protected readonly form = this.fb.group({
    title: ['', [Validators.required, Validators.maxLength(160)]],
    level: this.fb.control<CefrLevel>('B1'),
    topic: ['', Validators.maxLength(64)],
    body: ['', [Validators.required, Validators.maxLength(20000)]],
    isPublished: [true],
  });
  private readonly body = toSignal(this.form.controls.body.valueChanges, { initialValue: '' });
  protected readonly words = computed(() => countWords(this.body()));
  protected readonly minutes = computed(() => readingMinutes(this.words()));
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    effect(() => {
      if (this.text.hasValue()) {
        this.fill(this.text.value());
      }
    });
  }

  protected async save(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      return;
    }
    const v = this.form.getRawValue();
    const body = { title: v.title, level: v.level, topic: v.topic || null, body: v.body, isPublished: v.isPublished };
    this.saving.set(true);
    this.error.set(null);
    try {
      if (this.isNew()) {
        const created = await this.api.invoke(adminCreateReadingText, { body });
        toast.success('Текст добавлен');
        await this.router.navigate(['/admin/reading', created.id], { replaceUrl: true });
      } else {
        this.text.set(await this.api.invoke(adminUpdateReadingText, { id: this.id()!, body }));
        toast.success('Сохранено');
      }
    } catch (e) {
      this.error.set(problemMessage(e, 'Не удалось сохранить текст.'));
    } finally {
      this.saving.set(false);
    }
  }

  protected async remove(): Promise<void> {
    try {
      await this.api.invoke(adminDeleteReadingText, { id: this.id()! });
      toast.success('Текст удалён');
      await this.router.navigate(['/admin/reading']);
    } catch (e) {
      toast.error(problemMessage(e, 'Не удалось удалить текст.'));
    }
  }

  private fill(t: ReadingTextDto): void {
    this.form.reset({ title: t.title, level: t.level, topic: t.topic ?? '', body: t.body, isPublished: t.isPublished });
  }
}

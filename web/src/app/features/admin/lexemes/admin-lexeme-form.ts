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
import { Api } from '../../../core/api/api';
import { adminCreateLexeme, adminDeleteLexeme, adminUpdateLexeme, getLexeme, listCategories } from '../../../core/api/functions';
import { CefrLevel } from '../../../core/api/models/cefr-level';
import { LexemeDto } from '../../../core/api/models/lexeme-dto';
import { LexemeType } from '../../../core/api/models/lexeme-type';
import { PartOfSpeech } from '../../../core/api/models/part-of-speech';
import { SaveLexemeRequest } from '../../../core/api/models/save-lexeme-request';
import { problemMessage } from '../../../core/http/problem';
import { ConfirmDelete } from '../../../shared/confirm-delete';
import { ErrorAlert } from '../../../shared/error-alert';
import { SpeakButton } from '../../../shared/pronunciation';
import {
  CEFR_LEVELS,
  LEXEME_TYPES,
  LEXEME_TYPE_LABELS,
  PARTS_OF_SPEECH,
  PART_OF_SPEECH_LABELS,
} from '../../dictionary/vocabulary-labels';

const MAX_ITEMS = 10;
const LANGUAGE = 'ru';

type ExampleGroup = FormGroup<{ text: FormControl<string>; translation: FormControl<string> }>;

/** Создание (/admin/lexemes/new) и правка (/admin/lexemes/:id) слова общей базы. */
@Component({
  selector: 'app-admin-lexeme-form',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    NgIcon,
    ConfirmDelete,
    ErrorAlert,
    SpeakButton,
    HlmButtonImports,
    HlmCardImports,
    HlmCheckboxImports,
    HlmFieldImports,
    HlmInputImports,
    HlmNativeSelectImports,
    HlmSpinnerImports,
  ],
  providers: [provideIcons({ lucideArrowLeft, lucidePlus, lucideX })],
  templateUrl: './admin-lexeme-form.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminLexemeForm {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly fb = inject(NonNullableFormBuilder);

  readonly id = input<string | undefined>(undefined);
  protected readonly isNew = computed(() => !this.id());

  protected readonly types = LEXEME_TYPES;
  protected readonly typeLabels = LEXEME_TYPE_LABELS;
  protected readonly parts = PARTS_OF_SPEECH;
  protected readonly partLabels = PART_OF_SPEECH_LABELS;
  protected readonly levels = CEFR_LEVELS;
  protected readonly maxItems = MAX_ITEMS;

  protected readonly categories = resource({ loader: () => this.api.invoke(listCategories) });
  protected readonly lexeme = resource({
    params: () => (this.id() ? { id: this.id()! } : undefined),
    loader: ({ params }) => this.api.invoke(getLexeme, params),
  });
  protected readonly loadError = computed(() =>
    this.lexeme.error() ? problemMessage(this.lexeme.error(), 'Не удалось загрузить слово.') : null,
  );

  protected readonly form = this.fb.group({
    text: ['', [Validators.required, Validators.maxLength(128)]],
    type: this.fb.control<LexemeType>('Word'),
    partOfSpeech: this.fb.control<PartOfSpeech | ''>(''),
    level: this.fb.control<CefrLevel | ''>(''),
    transcription: ['', Validators.maxLength(128)],
    audioUrl: ['', [Validators.maxLength(512), Validators.pattern(/^https?:\/\/\S+$/)]],
    translations: this.fb.array([this.translationControl('')]),
    examples: this.fb.array<ExampleGroup>([]),
  });

  protected readonly selectedCategories = signal<ReadonlySet<string>>(new Set());
  protected readonly saving = signal(false);
  protected readonly deleting = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    effect(() => {
      if (this.lexeme.hasValue()) {
        this.fill(this.lexeme.value());
      }
    });
  }

  protected get translations(): FormArray<FormControl<string>> {
    return this.form.controls.translations;
  }

  protected get examples(): FormArray<ExampleGroup> {
    return this.form.controls.examples;
  }

  protected addTranslation(): void {
    this.translations.push(this.translationControl(''));
  }

  protected addExample(): void {
    this.examples.push(this.exampleGroup('', ''));
  }

  protected removeAt(array: FormArray, index: number): void {
    array.removeAt(index);
    this.form.markAsDirty();
  }

  protected toggleCategory(id: string, checked: boolean): void {
    this.selectedCategories.update((set) => {
      const next = new Set(set);
      if (checked) {
        next.add(id);
      } else {
        next.delete(id);
      }
      return next;
    });
    this.form.markAsDirty();
  }

  protected async save(): Promise<void> {
    this.form.markAllAsTouched();
    const v = this.form.getRawValue();
    const translations = v.translations.map((t) => t.trim()).filter((t) => t);
    if (this.form.invalid || translations.length === 0) {
      this.error.set(translations.length === 0 ? 'Нужен хотя бы один перевод.' : 'Проверьте поля формы.');
      return;
    }

    const body: SaveLexemeRequest = {
      text: v.text,
      type: v.type,
      partOfSpeech: v.partOfSpeech || null,
      level: v.level || null,
      transcription: v.transcription || null,
      audioUrl: v.audioUrl || null,
      translations: translations.map((text) => ({ language: LANGUAGE, text })),
      examples: v.examples
        .filter((e) => e.text.trim())
        .map((e) => ({ text: e.text, translation: e.translation || null })),
      categoryIds: [...this.selectedCategories()],
    };

    this.saving.set(true);
    this.error.set(null);
    try {
      if (this.isNew()) {
        const created = await this.api.invoke(adminCreateLexeme, { body });
        toast.success(`«${created.text}» добавлено в общую базу`);
        await this.router.navigate(['/admin/lexemes', created.id], { replaceUrl: true });
      } else {
        const updated = await this.api.invoke(adminUpdateLexeme, { id: this.id()!, body });
        this.lexeme.set(updated);
        toast.success('Сохранено');
      }
    } catch (e) {
      this.error.set(problemMessage(e, 'Не удалось сохранить слово.'));
    } finally {
      this.saving.set(false);
    }
  }

  protected async remove(): Promise<void> {
    this.deleting.set(true);
    try {
      await this.api.invoke(adminDeleteLexeme, { id: this.id()! });
      toast.success('Слово удалено из общей базы');
      await this.router.navigate(['/admin/lexemes']);
    } catch (e) {
      toast.error(problemMessage(e, 'Не удалось удалить слово.'));
      this.deleting.set(false);
    }
  }

  private fill(lexeme: LexemeDto): void {
    this.translations.clear();
    const translations = lexeme.translations.filter((t) => t.language === LANGUAGE).map((t) => t.text);
    for (const text of translations.length > 0 ? translations : ['']) {
      this.translations.push(this.translationControl(text));
    }
    this.examples.clear();
    for (const example of lexeme.examples) {
      this.examples.push(this.exampleGroup(example.text, example.translation ?? ''));
    }
    this.form.patchValue({
      text: lexeme.text,
      type: lexeme.type,
      partOfSpeech: lexeme.partOfSpeech ?? '',
      level: lexeme.level ?? '',
      transcription: lexeme.transcription ?? '',
      audioUrl: lexeme.audioUrl ?? '',
    });
    this.selectedCategories.set(new Set(lexeme.categories.map((c) => c.id)));
    this.form.markAsPristine();
  }

  private translationControl(value: string): FormControl<string> {
    return this.fb.control(value, Validators.maxLength(256));
  }

  private exampleGroup(text: string, translation: string): ExampleGroup {
    return this.fb.group({
      text: [text, Validators.maxLength(512)],
      translation: [translation, Validators.maxLength(512)],
    });
  }
}

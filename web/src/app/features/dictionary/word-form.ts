import { ChangeDetectionStrategy, Component, computed, effect, inject, input, resource, signal } from '@angular/core';
import { FormArray, FormControl, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideArrowLeft, lucideInfo, lucidePlus, lucideX } from '@ng-icons/lucide';
import { toast } from '@spartan-ng/brain/sonner';
import { HlmAlertImports } from '@spartan-ng/helm/alert';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmFieldImports } from '@spartan-ng/helm/field';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { HlmNativeSelectImports } from '@spartan-ng/helm/native-select';
import { HlmSpinnerImports } from '@spartan-ng/helm/spinner';
import { HlmTextareaImports } from '@spartan-ng/helm/textarea';
import { Api } from '../../core/api/api';
import { addMyWord, deleteMyWord, getMyWord, updateMyWord } from '../../core/api/functions';
import { LexemeType } from '../../core/api/models/lexeme-type';
import { MyWordDto } from '../../core/api/models/my-word-dto';
import { problemMessage } from '../../core/http/problem';
import { ConfirmDelete } from '../../shared/confirm-delete';
import { WordProgressCard } from '../../shared/word-progress';
import { ErrorAlert } from '../../shared/error-alert';
import { SpeakButton } from '../../shared/pronunciation';
import {
  CEFR_LEVELS,
  CefrLevelValue,
  LEXEME_TYPES,
  LEXEME_TYPE_LABELS,
  PARTS_OF_SPEECH,
  PART_OF_SPEECH_LABELS,
  PartOfSpeechValue,
} from './vocabulary-labels';

const MAX_TRANSLATIONS = 10;

/** Своё слово: создание (/dictionary/words/new) и правка любого слова из личного словаря (/dictionary/words/:id). */
@Component({
  selector: 'app-word-form',
  imports: [
    WordProgressCard,
    ReactiveFormsModule,
    RouterLink,
    NgIcon,
    ConfirmDelete,
    ErrorAlert,
    SpeakButton,
    HlmAlertImports,
    HlmButtonImports,
    HlmCardImports,
    HlmFieldImports,
    HlmInputImports,
    HlmNativeSelectImports,
    HlmSpinnerImports,
    HlmTextareaImports,
  ],
  providers: [provideIcons({ lucideArrowLeft, lucideInfo, lucidePlus, lucideX })],
  templateUrl: './word-form.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WordForm {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly fb = inject(NonNullableFormBuilder);

  /** id слова из адреса; пусто для нового. */
  readonly id = input<string | undefined>(undefined);
  protected readonly isNew = computed(() => !this.id());

  protected readonly types = LEXEME_TYPES;
  protected readonly typeLabels = LEXEME_TYPE_LABELS;
  protected readonly levels = CEFR_LEVELS;
  protected readonly parts = PARTS_OF_SPEECH;
  protected readonly partLabels = PART_OF_SPEECH_LABELS;
  protected readonly maxTranslations = MAX_TRANSLATIONS;

  protected readonly word = resource({
    params: () => (this.id() ? { id: this.id()! } : undefined),
    loader: ({ params }) => this.api.invoke(getMyWord, params),
  });

  protected readonly loadError = computed(() =>
    this.word.error() ? problemMessage(this.word.error(), 'Не удалось загрузить слово.') : null,
  );

  protected readonly form = this.fb.group({
    text: ['', [Validators.required, Validators.maxLength(128)]],
    type: this.fb.control<LexemeType>('Word'),
    level: this.fb.control<CefrLevelValue | ''>(''),
    partOfSpeech: this.fb.control<PartOfSpeechValue | ''>(''),
    transcription: ['', Validators.maxLength(128)],
    translations: this.fb.array([this.translationControl('')]),
    example: ['', Validators.maxLength(512)],
    exampleTranslation: ['', Validators.maxLength(512)],
    note: ['', Validators.maxLength(1000)],
  });

  protected readonly saving = signal(false);
  protected readonly deleting = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    effect(() => {
      if (this.word.hasValue()) {
        this.fill(this.word.value());
      }
    });
  }

  protected get translations(): FormArray<FormControl<string>> {
    return this.form.controls.translations;
  }

  protected addTranslation(): void {
    if (this.translations.length < MAX_TRANSLATIONS) {
      this.translations.push(this.translationControl(''));
    }
  }

  protected removeTranslation(index: number): void {
    this.translations.removeAt(index);
    this.form.markAsDirty();
  }

  protected async save(): Promise<void> {
    this.form.markAllAsTouched();
    const value = this.form.getRawValue();
    const translations = value.translations.map((t) => t.trim()).filter((t) => t.length > 0);
    if (this.form.invalid || translations.length === 0) {
      this.error.set(translations.length === 0 ? 'Нужен хотя бы один перевод.' : null);
      return;
    }

    const body = {
      text: value.text,
      type: value.type,
      transcription: value.transcription || null,
      translations,
      example: value.example || null,
      exampleTranslation: value.exampleTranslation || null,
      note: value.note || null,
      level: value.level || null,
      partOfSpeech: value.partOfSpeech || null,
    };

    this.saving.set(true);
    this.error.set(null);
    try {
      if (this.isNew()) {
        const word = await this.api.invoke(addMyWord, { body: { ...body, lexemeId: null } });
        toast.success(`«${word.text}» в вашем словаре`);
        await this.router.navigate(['/dictionary']);
      } else {
        const word = await this.api.invoke(updateMyWord, { id: this.id()!, body });
        this.word.set(word);
        this.form.markAsPristine();
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
      await this.api.invoke(deleteMyWord, { id: this.id()! });
      toast.success('Слово удалено из словаря');
      await this.router.navigate(['/dictionary']);
    } catch (e) {
      toast.error(problemMessage(e, 'Не удалось удалить слово.'));
      this.deleting.set(false);
    }
  }

  private fill(word: MyWordDto): void {
    this.translations.clear();
    for (const translation of word.translations.length > 0 ? word.translations : ['']) {
      this.translations.push(this.translationControl(translation));
    }
    this.form.patchValue({
      text: word.text,
      type: word.type,
      level: word.level ?? '',
      partOfSpeech: word.partOfSpeech ?? '',
      transcription: word.transcription ?? '',
      example: word.example ?? '',
      exampleTranslation: word.exampleTranslation ?? '',
      note: word.note ?? '',
    });
    this.form.markAsPristine();
  }

  private translationControl(value: string): FormControl<string> {
    return this.fb.control(value, Validators.maxLength(256));
  }
}

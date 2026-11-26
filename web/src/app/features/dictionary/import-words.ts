import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideArrowLeft, lucideCircleCheck, lucideDownload, lucideFileUp } from '@ng-icons/lucide';
import { HlmAlertImports } from '@spartan-ng/helm/alert';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmCheckboxImports } from '@spartan-ng/helm/checkbox';
import { HlmFieldImports } from '@spartan-ng/helm/field';
import { HlmSpinnerImports } from '@spartan-ng/helm/spinner';
import { HlmTextareaImports } from '@spartan-ng/helm/textarea';
import { Api } from '../../core/api/api';
import { adminImportLexemes, importMyWords } from '../../core/api/functions';
import { ImportResult } from '../../core/api/models/import-result';
import { problemMessage } from '../../core/http/problem';
import { ErrorAlert } from '../../shared/error-alert';

const MAX_FILE_BYTES = 2_000_000;

const SAMPLES = {
  my: [
    'слово;перевод;уровень;тип;пример;перевод примера;заметка',
    'serendipity;счастливая случайность;C1;слово;It was pure serendipity.;Это была чистая случайность.;из книги',
    'bread;;;;;;',
    'break the ice;растопить лёд | разрядить обстановку;B2;идиома;;;',
    'bump into;случайно встретить;B1;;;;Фразовый глагол',
  ],
  admin: [
    'text;translation;type;part_of_speech;transcription;level;example;example_translation;categories',
    'apple;яблоко;word;noun;ˈæpl;A1;She eats an apple every day.;Она ест яблоко каждый день.;food',
    'give up;бросить | сдаться;phrasal verb;verb;ˌɡɪv ˈʌp;A2;Don\'t give up!;Не сдавайся!;everyday',
  ],
};

/**
 * Импорт CSV. Для пользователя — в личный словарь (/dictionary/import), для администратора — в общую базу (/admin/lexemes/import).
 * Файл читается в браузере и уходит текстом; разделитель и кодировку UTF-8 сервер ждёт как у Excel/Google Таблиц.
 */
@Component({
  selector: 'app-import-words',
  imports: [
    RouterLink,
    NgIcon,
    ErrorAlert,
    HlmAlertImports,
    HlmButtonImports,
    HlmCardImports,
    HlmCheckboxImports,
    HlmFieldImports,
    HlmSpinnerImports,
    HlmTextareaImports,
  ],
  providers: [provideIcons({ lucideArrowLeft, lucideCircleCheck, lucideDownload, lucideFileUp })],
  templateUrl: './import-words.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ImportWords {
  private readonly api = inject(Api);

  /**
   * Из данных маршрута: 'admin' — импорт в общую базу. Привязка маршрута к входам передаёт undefined,
   * если в маршруте нет data.mode, поэтому значение по умолчанию здесь не сработает — проверяем явно.
   */
  readonly mode = input<'my' | 'admin' | undefined>();
  protected readonly isAdmin = computed(() => this.mode() === 'admin');
  protected readonly backLink = computed(() => (this.isAdmin() ? '/admin/lexemes' : '/dictionary'));
  protected readonly sample = computed(() => (this.isAdmin() ? SAMPLES.admin : SAMPLES.my).join('\n'));

  protected readonly content = signal('');
  protected readonly fileName = signal<string | null>(null);
  protected readonly updateExisting = signal(false);
  /** Админский импорт: сразу положить загруженные слова и в свой словарь. */
  protected readonly addToMyWords = signal(true);
  protected readonly sending = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly result = signal<ImportResult | null>(null);
  protected readonly rows = computed(() => Math.max(0, this.content().split(/\r?\n/).filter((l) => l.trim()).length - 1));

  protected async pickFile(input: HTMLInputElement): Promise<void> {
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }
    if (file.size > MAX_FILE_BYTES) {
      this.error.set('Файл больше 2 МБ. Разбейте его на части.');
      return;
    }
    this.error.set(null);
    this.result.set(null);
    this.fileName.set(file.name);
    this.content.set(await file.text());
  }

  protected onPaste(text: string): void {
    this.fileName.set(null);
    this.result.set(null);
    this.content.set(text);
  }

  protected downloadSample(): void {
    // BOM нужен, чтобы Excel открыл файл в UTF-8, а не в кодировке Windows.
    const blob = new Blob(['﻿' + this.sample() + '\n'], { type: 'text/csv;charset=utf-8' });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = this.isAdmin() ? 'lexemes-sample.csv' : 'my-words-sample.csv';
    link.click();
    URL.revokeObjectURL(link.href);
  }

  protected async submit(): Promise<void> {
    if (!this.content().trim()) {
      this.error.set('Выберите файл или вставьте строки.');
      return;
    }
    this.sending.set(true);
    this.error.set(null);
    try {
      const body = { content: this.content(), updateExisting: this.updateExisting(), addToMyWords: this.addToMyWords() };
      const result = this.isAdmin()
        ? await this.api.invoke(adminImportLexemes, { body })
        : await this.api.invoke(importMyWords, { body });
      this.result.set(result);
    } catch (e) {
      this.error.set(problemMessage(e, 'Не удалось загрузить файл.'));
    } finally {
      this.sending.set(false);
    }
  }

  protected reset(): void {
    this.content.set('');
    this.fileName.set(null);
    this.result.set(null);
  }
}

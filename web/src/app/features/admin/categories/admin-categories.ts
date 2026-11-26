import { ChangeDetectionStrategy, Component, computed, inject, resource, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucidePencil, lucidePlus } from '@ng-icons/lucide';
import { toast } from '@spartan-ng/brain/sonner';
import { HlmBadgeImports } from '@spartan-ng/helm/badge';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmFieldImports } from '@spartan-ng/helm/field';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { HlmSpinnerImports } from '@spartan-ng/helm/spinner';
import { Api } from '../../../core/api/api';
import { adminCreateCategory, adminDeleteCategory, adminUpdateCategory, listCategories } from '../../../core/api/functions';
import { CategoryDto } from '../../../core/api/models/category-dto';
import { problemMessage } from '../../../core/http/problem';
import { ConfirmDelete } from '../../../shared/confirm-delete';
import { ErrorAlert } from '../../../shared/error-alert';
import { AdminNav } from '../admin-nav';

const SLUG_PATTERN = /^[a-z0-9]+(-[a-z0-9]+)*$/;

/** Категории общей базы: список со счётчиком слов, создание, правка на месте, удаление. */
@Component({
  selector: 'app-admin-categories',
  imports: [
    ReactiveFormsModule,
    NgIcon,
    AdminNav,
    ConfirmDelete,
    ErrorAlert,
    HlmBadgeImports,
    HlmButtonImports,
    HlmCardImports,
    HlmFieldImports,
    HlmInputImports,
    HlmSpinnerImports,
  ],
  providers: [provideIcons({ lucidePencil, lucidePlus })],
  templateUrl: './admin-categories.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminCategories {
  private readonly api = inject(Api);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly categories = resource({ loader: () => this.api.invoke(listCategories) });
  protected readonly loadError = computed(() =>
    this.categories.error() ? problemMessage(this.categories.error(), 'Не удалось загрузить категории.') : null,
  );

  /** null — форма создания, id — правка этой категории. */
  protected readonly editingId = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(64)]],
    slug: ['', [Validators.required, Validators.maxLength(64), Validators.pattern(SLUG_PATTERN)]],
    description: ['', Validators.maxLength(256)],
  });

  protected edit(category: CategoryDto): void {
    this.editingId.set(category.id);
    this.error.set(null);
    this.form.reset({ name: category.name, slug: category.slug, description: category.description ?? '' });
  }

  protected cancelEdit(): void {
    this.editingId.set(null);
    this.error.set(null);
    this.form.reset();
  }

  protected async save(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      return;
    }
    const v = this.form.getRawValue();
    const body = { name: v.name, slug: v.slug, description: v.description || null };
    const id = this.editingId();

    this.saving.set(true);
    this.error.set(null);
    try {
      if (id) {
        const updated = await this.api.invoke(adminUpdateCategory, { id, body });
        this.categories.update((list) => list?.map((c) => (c.id === id ? updated : c)));
        toast.success('Категория сохранена');
      } else {
        const created = await this.api.invoke(adminCreateCategory, { body });
        this.categories.update((list) => [...(list ?? []), created].sort((a, b) => a.name.localeCompare(b.name, 'ru')));
        toast.success(`Категория «${created.name}» создана`);
      }
      this.cancelEdit();
    } catch (e) {
      this.error.set(problemMessage(e, 'Не удалось сохранить категорию.'));
    } finally {
      this.saving.set(false);
    }
  }

  protected async remove(category: CategoryDto): Promise<void> {
    try {
      await this.api.invoke(adminDeleteCategory, { id: category.id });
      this.categories.update((list) => list?.filter((c) => c.id !== category.id));
      if (this.editingId() === category.id) {
        this.cancelEdit();
      }
      toast.success(`Категория «${category.name}» удалена`);
    } catch (e) {
      toast.error(problemMessage(e, 'Не удалось удалить категорию.'));
    }
  }
}

import { ChangeDetectionStrategy, Component, computed, inject, resource, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucidePlus } from '@ng-icons/lucide';
import { toast } from '@spartan-ng/brain/sonner';
import { HlmBadgeImports } from '@spartan-ng/helm/badge';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmNativeSelectImports } from '@spartan-ng/helm/native-select';
import { HlmSpinnerImports } from '@spartan-ng/helm/spinner';
import { Api } from '../../../core/api/api';
import { adminUpdateErrorTag, listErrorTags, listGrammarTopics } from '../../../core/api/functions';
import { ErrorTagDto } from '../../../core/api/models/error-tag-dto';
import { problemMessage } from '../../../core/http/problem';
import { ErrorAlert } from '../../../shared/error-alert';
import { AdminNav } from '../admin-nav';

/** Админка грамматики: темы и к какой теме ведёт каждый тип ошибки (правило в разборе ответа). */
@Component({
  selector: 'app-admin-grammar',
  imports: [RouterLink, NgIcon, AdminNav, ErrorAlert, HlmBadgeImports, HlmButtonImports, HlmCardImports, HlmNativeSelectImports, HlmSpinnerImports],
  providers: [provideIcons({ lucidePlus })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4">
      <app-admin-nav />

      <div class="flex flex-wrap items-center justify-between gap-2">
        <h1 class="text-2xl font-semibold tracking-tight">Грамматика</h1>
        <a hlmBtn size="sm" routerLink="/admin/grammar/new">
          <ng-icon name="lucidePlus" />
          Новая тема
        </a>
      </div>

      @if (topics.error() || tags.error()) {
        <app-error-alert message="Не удалось загрузить грамматику." />
      }

      <section hlmCard>
        <div hlmCardHeader>
          <h2 hlmCardTitle>Типы ошибок</h2>
          <p hlmCardDescription>Правило выбранной темы показывается в разборе, когда пользователь допускает такую ошибку.</p>
        </div>
        <div hlmCardContent>
          @if (tags.hasValue()) {
            <ul class="flex flex-col divide-y">
              @for (tag of tags.value(); track tag.code) {
                <li class="flex flex-col gap-2 py-3 sm:flex-row sm:items-center">
                  <div class="min-w-0 flex-1">
                    <div class="flex items-center gap-2">
                      <span class="font-medium">{{ tag.title }}</span>
                      <span class="text-muted-foreground font-mono text-xs">{{ tag.code }}</span>
                    </div>
                    <p class="text-muted-foreground text-sm">{{ tag.description }}</p>
                  </div>
                  <hlm-native-select
                    class="w-full sm:w-56"
                    selectClass="h-10"
                    [attr.aria-label]="'Тема для «' + tag.title + '»'"
                    [value]="tag.topicId ?? ''"
                    [disabled]="savingTag() === tag.code"
                    (valueChange)="setTopic(tag, $event ?? '')"
                  >
                    <option value="">Без темы</option>
                    @for (topic of topicList(); track topic.id) {
                      <option [value]="topic.id">{{ topic.title }}</option>
                    }
                  </hlm-native-select>
                </li>
              }
            </ul>
          } @else {
            <hlm-spinner aria-label="Загрузка" />
          }
        </div>
      </section>

      <h2 class="text-lg font-semibold">Темы</h2>
      <ul class="flex flex-col gap-2">
        @for (topic of topicList(); track topic.id) {
          <li>
            <a hlmCard [routerLink]="['/admin/grammar', topic.id]" class="hover:border-primary/40 gap-1 py-4 transition-colors">
              <div class="flex items-center gap-2 px-5">
                <span class="flex-1 font-semibold">{{ topic.title }}</span>
                <span class="text-muted-foreground font-mono text-xs">{{ topic.slug }}</span>
                @if (topic.level) {
                  <span hlmBadge variant="secondary">{{ topic.level }}</span>
                }
              </div>
              <p class="text-muted-foreground line-clamp-2 px-5 text-sm">{{ topic.summary }}</p>
            </a>
          </li>
        }
      </ul>
    </div>
  `,
})
export class AdminGrammar {
  private readonly api = inject(Api);

  protected readonly topics = resource({ loader: () => this.api.invoke(listGrammarTopics) });
  protected readonly tags = resource({ loader: () => this.api.invoke(listErrorTags) });
  protected readonly topicList = computed(() => this.topics.value() ?? []);
  protected readonly savingTag = signal<string | null>(null);

  protected async setTopic(tag: ErrorTagDto, topicId: string): Promise<void> {
    this.savingTag.set(tag.code);
    try {
      const updated = await this.api.invoke(adminUpdateErrorTag, {
        code: tag.code,
        body: { title: tag.title, description: tag.description, topicId: topicId || null },
      });
      this.tags.update((list) => list?.map((t) => (t.code === tag.code ? updated : t)));
      toast.success(`«${tag.title}»: ${updated.topicTitle ?? 'без темы'}`);
    } catch (e) {
      toast.error(problemMessage(e, 'Не удалось сохранить.'));
      this.tags.reload();
    } finally {
      this.savingTag.set(null);
    }
  }
}

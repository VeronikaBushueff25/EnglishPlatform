import { ChangeDetectionStrategy, Component, computed, inject, input, resource } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideArrowLeft, lucideCheck, lucideX } from '@ng-icons/lucide';
import { HlmBadgeImports } from '@spartan-ng/helm/badge';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmSpinnerImports } from '@spartan-ng/helm/spinner';
import { Api } from '../../core/api/api';
import { getGrammarTopic, listGrammarTopics } from '../../core/api/functions';
import { problemMessage } from '../../core/http/problem';
import { ErrorAlert } from '../../shared/error-alert';
import { SpeakButton } from '../../shared/pronunciation';
import { paragraphs } from '../../shared/text';


/** Список грамматических тем (/grammar). */
@Component({
  selector: 'app-grammar-list',
  imports: [RouterLink, ErrorAlert, HlmBadgeImports, HlmCardImports, HlmSpinnerImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4">
      <div>
        <h1 class="text-2xl font-semibold tracking-tight">Грамматика</h1>
        <p class="text-muted-foreground text-sm">Короткие правила к ошибкам, которые встречаются в занятиях.</p>
      </div>

      @if (topics.error()) {
        <app-error-alert message="Не удалось загрузить темы." />
      } @else if (topics.hasValue()) {
        <ul class="flex flex-col gap-2">
          @for (topic of topics.value(); track topic.id) {
            <li>
              <a hlmCard [routerLink]="['/grammar', topic.slug]" class="hover:border-primary/40 gap-1 py-4 transition-colors">
                <div class="flex items-center gap-2 px-5">
                  <h2 class="flex-1 font-semibold">{{ topic.title }}</h2>
                  @if (topic.level) {
                    <span hlmBadge variant="secondary">{{ topic.level }}</span>
                  }
                </div>
                <p class="text-muted-foreground line-clamp-2 px-5 text-sm">{{ topic.summary }}</p>
              </a>
            </li>
          }
        </ul>
      } @else {
        <div class="text-muted-foreground flex items-center gap-2"><hlm-spinner aria-label="Загрузка" /> Загружаем…</div>
      }
    </div>
  `,
})
export class GrammarList {
  private readonly api = inject(Api);
  protected readonly topics = resource({ loader: () => this.api.invoke(listGrammarTopics) });
}

/** Тема (/grammar/:slug): правило, подробности, примеры (неверные — зачёркнуты). */
@Component({
  selector: 'app-grammar-topic',
  imports: [RouterLink, NgIcon, ErrorAlert, SpeakButton, HlmBadgeImports, HlmButtonImports, HlmCardImports, HlmSpinnerImports],
  providers: [provideIcons({ lucideArrowLeft, lucideCheck, lucideX })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4">
      <a hlmBtn variant="ghost" size="sm" routerLink="/grammar" class="self-start">
        <ng-icon name="lucideArrowLeft" />
        Все темы
      </a>

      @if (loadError(); as message) {
        <app-error-alert [message]="message" />
      } @else if (topic.hasValue()) {
        @let t = topic.value();
        <article hlmCard>
          <div hlmCardHeader>
            <div class="flex items-start gap-2">
              <h1 hlmCardTitle class="flex-1 text-2xl">{{ t.title }}</h1>
              @if (t.level) {
                <span hlmBadge variant="secondary">{{ t.level }}</span>
              }
            </div>
          </div>
          <div hlmCardContent class="flex flex-col gap-4">
            <p class="bg-primary/5 border-primary/20 rounded-2xl border p-4 font-medium">{{ t.summary }}</p>
            @for (p of body(); track $index) {
              <p class="leading-relaxed">{{ p }}</p>
            }

            @if (t.examples.length > 0) {
              <div>
                <h2 class="text-muted-foreground mb-2 text-sm font-medium">Примеры</h2>
                <ul class="flex flex-col gap-2">
                  @for (example of t.examples; track $index) {
                    <li class="flex items-start gap-2 rounded-xl border p-3" [class.border-destructive/30]="example.isWrong">
                      @if (example.isWrong) {
                        <ng-icon name="lucideX" class="text-destructive mt-1 shrink-0" aria-label="Так нельзя" />
                      } @else {
                        <ng-icon name="lucideCheck" class="text-success mt-1 shrink-0" aria-label="Правильно" />
                      }
                      <div class="min-w-0 flex-1">
                        <p lang="en" [class.line-through]="example.isWrong" [class.text-muted-foreground]="example.isWrong">{{ example.text }}</p>
                        @if (example.translation) {
                          <p class="text-muted-foreground text-sm">{{ example.translation }}</p>
                        }
                      </div>
                      @if (!example.isWrong) {
                        <app-speak-button [text]="example.text" />
                      }
                    </li>
                  }
                </ul>
              </div>
            }
          </div>
        </article>
      } @else {
        <div class="text-muted-foreground flex items-center gap-2"><hlm-spinner aria-label="Загрузка" /> Загружаем…</div>
      }
    </div>
  `,
})
export class GrammarTopicPage {
  private readonly api = inject(Api);
  readonly slug = input.required<string>();

  protected readonly topic = resource({
    params: () => ({ slug: this.slug() }),
    loader: ({ params }) => this.api.invoke(getGrammarTopic, params),
  });
  protected readonly body = computed(() => paragraphs(this.topic.value()?.body));
  protected readonly loadError = computed(() =>
    this.topic.error() ? problemMessage(this.topic.error(), 'Не удалось загрузить тему.') : null,
  );
}

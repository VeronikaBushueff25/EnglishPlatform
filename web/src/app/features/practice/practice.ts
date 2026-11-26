import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideBookOpenText, lucideClipboardPaste, lucideLayoutGrid, lucidePlay, lucideShuffle, lucideZap } from '@ng-icons/lucide';
import { HlmCardImports } from '@spartan-ng/helm/card';

interface Activity {
  link: string;
  icon: string;
  title: string;
  text: string;
  tone: string;
}

/**
 * Практика (/practice): по желанию, в дополнение к ежедневному занятию. Чтение с переводом по нажатию и игры
 * на словах из личного словаря. Игры не меняют расписание повторений — это разминка, а не экзамен.
 */
@Component({
  selector: 'app-practice',
  imports: [NgTemplateOutlet, RouterLink, NgIcon, HlmCardImports],
  providers: [provideIcons({ lucideBookOpenText, lucideClipboardPaste, lucideLayoutGrid, lucidePlay, lucideShuffle, lucideZap })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4">
      <div>
        <h1 class="text-2xl font-semibold tracking-tight">Практика</h1>
        <p class="text-muted-foreground text-sm">Когда занятие на сегодня сделано, а хочется ещё. Игры — на ваших словах.</p>
      </div>

      <a hlmCard routerLink="/learn" class="border-primary/40 bg-primary/5 hover:border-primary flex-row items-center gap-3 px-5 py-4 transition-colors">
        <span class="bg-primary text-primary-foreground flex size-10 shrink-0 items-center justify-center rounded-2xl"><ng-icon name="lucidePlay" /></span>
        <span>
          <span class="block font-semibold">Ежедневное занятие</span>
          <span class="text-muted-foreground block text-sm">Главное — повторения по расписанию и новые слова.</span>
        </span>
      </a>

      <h2 class="text-muted-foreground mt-2 text-sm font-medium">Читать</h2>
      <div class="grid gap-3 sm:grid-cols-2">
        @for (a of reading; track a.link) {
          <ng-container *ngTemplateOutlet="card; context: { $implicit: a }" />
        }
      </div>

      <h2 class="text-muted-foreground mt-2 text-sm font-medium">Играть</h2>
      <div class="grid gap-3 sm:grid-cols-3">
        @for (a of games; track a.link) {
          <ng-container *ngTemplateOutlet="card; context: { $implicit: a }" />
        }
      </div>
    </div>

    <ng-template #card let-a>
      <a hlmCard [routerLink]="a.link" class="hover:border-primary/40 gap-2 px-5 py-4 transition-colors">
        <span class="flex size-11 items-center justify-center rounded-2xl" [class]="a.tone"><ng-icon [name]="a.icon" class="text-xl" /></span>
        <span class="font-semibold">{{ a.title }}</span>
        <span class="text-muted-foreground text-sm">{{ a.text }}</span>
      </a>
    </ng-template>
  `,
})
export class Practice {
  protected readonly reading: Activity[] = [
    { link: '/reading', icon: 'lucideBookOpenText', title: 'Тексты по уровням', text: 'Нажмите на слово — перевод, произношение и «в мои слова».', tone: 'bg-sky-500/15 text-sky-600 dark:text-sky-400' },
    { link: '/reading/own', icon: 'lucideClipboardPaste', title: 'Свой текст', text: 'Вставьте статью, письмо или субтитры — и читайте так же.', tone: 'bg-violet-500/15 text-violet-600 dark:text-violet-400' },
  ];

  protected readonly games: Activity[] = [
    { link: '/practice/match', icon: 'lucideLayoutGrid', title: 'Пары', text: 'Соедините слова с переводами на время.', tone: 'bg-emerald-500/15 text-emerald-600 dark:text-emerald-400' },
    { link: '/practice/blitz', icon: 'lucideZap', title: 'Блиц', text: '60 секунд: верный перевод или нет?', tone: 'bg-amber-500/15 text-amber-600 dark:text-amber-400' },
    { link: '/practice/scramble', icon: 'lucideShuffle', title: 'Собери слово', text: 'Буквы вперемешку — соберите слово.', tone: 'bg-rose-500/15 text-rose-600 dark:text-rose-400' },
  ];
}

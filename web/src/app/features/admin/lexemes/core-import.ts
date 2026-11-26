import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideDatabaseZap, lucideSquare } from '@ng-icons/lucide';
import { toast } from '@spartan-ng/brain/sonner';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmCheckboxImports } from '@spartan-ng/helm/checkbox';
import { HlmSpinnerImports } from '@spartan-ng/helm/spinner';
import { Api } from '../../../core/api/api';
import { cancelCoreImport } from '../../../core/api/fn/admin-vocabulary/cancel-core-import';
import { getCoreImport } from '../../../core/api/fn/admin-vocabulary/get-core-import';
import { startCoreImport } from '../../../core/api/fn/admin-vocabulary/start-core-import';
import { CefrLevel } from '../../../core/api/models/cefr-level';
import { CoreImportStatusDto } from '../../../core/api/models/core-import-status-dto';
import { problemMessage } from '../../../core/http/problem';

const LEVELS: CefrLevel[] = ['A1', 'A2', 'B1', 'B2', 'C1', 'C2'];

/**
 * Пополнение общей базы ядром лексики: слова списков CEFR выбранных уровней берутся из Викисловаря в фоне.
 * Пока идёт — статус обновляется каждые 3 секунды.
 */
@Component({
  selector: 'app-core-import',
  imports: [NgIcon, HlmButtonImports, HlmCardImports, HlmCheckboxImports, HlmSpinnerImports],
  providers: [provideIcons({ lucideDatabaseZap, lucideSquare })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section hlmCard>
      <div hlmCardHeader>
        <h2 hlmCardTitle class="flex items-center gap-2"><ng-icon name="lucideDatabaseZap" /> Пополнить базу</h2>
        <p hlmCardDescription>
          Слова из списков уровней CEFR (около 10 тысяч: CEFR-J и Octanove) — с переводами, транскрипцией и примерами
          из Викисловаря. Уже имеющиеся пропускаются; добавленные помечаются «проверить». Около 2 слов в секунду.
          @if (status(); as s) {
            Сейчас в базе: <b>{{ s.baseSize }}</b>.
          }
        </p>
      </div>
      <div hlmCardContent class="flex flex-col gap-3">
        @if (status(); as s) {
          @if (s.running) {
            <div class="flex items-center gap-3">
              <div class="bg-muted h-2 flex-1 overflow-hidden rounded-full">
                <div class="bg-primary h-full rounded-full transition-all" [style.width.%]="+s.total ? (+s.processed / +s.total) * 100 : 0"></div>
              </div>
              <span class="text-muted-foreground text-sm tabular-nums">{{ s.processed }} / {{ s.total }}</span>
            </div>
            <p class="text-muted-foreground flex items-center gap-2 text-sm">
              <hlm-spinner aria-label="Идёт" /> Добавлено {{ s.added }}, нет в словаре {{ s.missing }}
              @if (s.current) {
                · сейчас «{{ s.current }}»
              }
            </p>
            <button hlmBtn variant="outline" size="sm" type="button" class="self-start" (click)="cancel()">
              <ng-icon name="lucideSquare" /> Остановить
            </button>
          } @else {
            @if (s.finishedAt) {
              <p class="text-muted-foreground text-sm">Прошлый запуск: проверено {{ s.processed }} из {{ s.total }}, добавлено {{ s.added }}.</p>
            }
            <div class="flex flex-wrap gap-3" role="group" aria-label="Уровни">
              @for (level of levels; track level) {
                <label class="flex items-center gap-2 text-sm">
                  <hlm-checkbox [checked]="chosen().has(level)" (checkedChange)="toggle(level, $event)" />
                  {{ level }}
                </label>
              }
            </div>
            <button hlmBtn size="sm" type="button" class="self-start" [disabled]="chosen().size === 0 || busy()" (click)="start()">
              Пополнить
            </button>
          }
        }
      </div>
    </section>
  `,
})
export class CoreImport {
  private readonly api = inject(Api);

  protected readonly levels = LEVELS;
  protected readonly chosen = signal<ReadonlySet<CefrLevel>>(new Set<CefrLevel>(['A1', 'A2', 'B1', 'B2']));
  protected readonly status = signal<CoreImportStatusDto | null>(null);
  protected readonly busy = signal(false);
  private timer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    void this.refresh();
    inject(DestroyRef).onDestroy(() => clearTimeout(this.timer));
  }

  protected toggle(level: CefrLevel, on: boolean): void {
    this.chosen.update((set) => {
      const next = new Set(set);
      if (on) {
        next.add(level);
      } else {
        next.delete(level);
      }
      return next;
    });
  }

  protected async start(): Promise<void> {
    this.busy.set(true);
    try {
      await this.api.invoke(startCoreImport, { body: { levels: LEVELS.filter((l) => this.chosen().has(l)), limit: null } });
      toast.success('Пополнение базы запущено');
      await this.refresh();
    } catch (e) {
      toast.error(problemMessage(e, 'Не удалось запустить.'));
    } finally {
      this.busy.set(false);
    }
  }

  protected async cancel(): Promise<void> {
    await this.api.invoke(cancelCoreImport);
    await this.refresh();
  }

  private async refresh(): Promise<void> {
    clearTimeout(this.timer);
    try {
      const status = await this.api.invoke(getCoreImport);
      this.status.set(status);
      if (status.running) {
        this.timer = setTimeout(() => void this.refresh(), 3000);
      }
    } catch {
      this.timer = setTimeout(() => void this.refresh(), 10000);
    }
  }
}

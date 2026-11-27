import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, signal, untracked } from '@angular/core';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideX } from '@ng-icons/lucide';
import { filter } from 'rxjs';
import { PetSignals } from '../../core/pet-signals';
import { PetMood, toSpecies } from './pet-art';
import { Api } from '../../core/api/api';
import { getPracticeWords } from '../../core/api/fn/practice/get-practice-words';
import { Pronunciation } from '../../shared/pronunciation';
import { PetLine, PetStore, PetWord, baseMood, n, petLine, stageOf } from './pet-store';
import { PetView } from './pet-view';

const WALK_SPEED = 70; // px/с

/**
 * Питомец на страницах приложения. На широком экране живёт в левом поле: сидит, ходит туда-сюда, ночью спит.
 * На телефоне — маленький в углу. Время от времени сам напоминает: слово из словаря («как будет по-английски?»),
 * просьбу дня со ссылкой, что сегодня ещё не было занятия. В занятии радуется верным ответам и подбадривает
 * после ошибок, после занятия и выполненных просьб хвастается угощениями. Нажать — скажет что-нибудь.
 */
@Component({
  selector: 'app-pet-companion',
  imports: [PetView, RouterLink, NgIcon],
  providers: [provideIcons({ lucideX })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host { position: fixed; bottom: 12px; z-index: 30; pointer-events: none; }
    .walker { pointer-events: auto; transition-property: transform; transition-timing-function: linear; }
    .bubble { animation: pop .25s ease-out; }
    .bubble::after { content: ''; position: absolute; left: var(--tail, 40px); bottom: -7px; translate: -50% 0; border: 7px solid transparent; border-top-color: var(--card); border-bottom: 0; }
    @keyframes pop { from { opacity: 0; transform: translateY(6px) scale(.95); } }
    .hide-btn { opacity: 0; transition: opacity .2s; }
    .walker:hover .hide-btn, .hide-btn:focus-visible { opacity: 1; }
  `,
  host: {
    '[style.left.px]': 'wide() ? 0 : null',
    '[style.right.px]': 'wide() ? null : 8',
    '[style.display]': "visible() ? 'block' : 'none'",
  },
  template: `
    @if (store.pet(); as pet) {
      <div class="walker relative" [style.width.px]="size()" [style.transform]="'translateX(' + x() + 'px)'" [style.transition-duration.ms]="walkMs()">
        @if (bubble(); as b) {
          <div class="bubble bg-card text-card-foreground absolute bottom-full mb-1 w-52 rounded-2xl border px-3 py-2 text-center text-xs shadow-md"
               [style.left.px]="wide() ? 0 : null" [style.right.px]="wide() ? null : 0" [style.--tail]="wide() ? size() / 2 + 'px' : 'calc(100% - ' + size() / 2 + 'px)'">
            {{ b.text }}
            @if (b.word; as w) {
              <button type="button" class="text-primary font-semibold" translate="no" (click)="reveal(w)">
                @if (b.ask && !revealed()) { «{{ w.translation }}» } @else { <span lang="en">{{ w.text }}</span> }
              </button>
              @if (revealed()) { <span class="text-muted-foreground block" translate="no">{{ b.ask ? w.translation : w.translation }}</span> }
              @else { <span class="text-muted-foreground block">{{ b.ask ? 'нажмите, чтобы проверить' : 'нажмите — переведу' }}</span> }
            }
            @if (b.link; as link) {
              <a [routerLink]="link.url" class="text-primary mt-1 block font-medium underline-offset-2 hover:underline">{{ link.label }}</a>
            } @else if (b.quiz || b.feed || b.home) {
              <a routerLink="/pet" class="text-primary mt-1 block font-medium underline-offset-2 hover:underline">К питомцу</a>
            }
          </div>
        }
        <button type="button" class="hide-btn bg-card absolute -top-1 -right-1 z-10 flex size-6 items-center justify-center rounded-full border shadow-sm"
                aria-label="Спрятать питомца (вернуть можно на его странице)" title="Спрятать питомца (вернуть можно на его странице)" (click)="store.setRoaming(false)">
          <ng-icon name="lucideX" size="12" />
        </button>
        <app-pet-view [species]="species()" [palette]="pet.palette" [mood]="mood()" [stage]="stage()" [accessory]="pet.accessory"
                      [facingLeft]="facingLeft()" [label]="pet.name" (patted)="poke()" />
      </div>
    }
  `,
})
export class PetCompanion {
  protected readonly store = inject(PetStore);
  private readonly api = inject(Api);
  private readonly pronunciation = inject(Pronunciation);
  private readonly words = signal<PetWord[]>([]);
  protected readonly revealed = signal(false);
  private readonly signals = inject(PetSignals);
  private readonly router = inject(Router);

  private readonly url = signal(this.router.url);
  protected readonly wide = signal(false);
  private readonly gutter = signal(0);
  protected readonly x = signal(8);
  protected readonly walkMs = signal(0);
  protected readonly facingLeft = signal(false);
  private readonly walking = signal(false);
  private readonly reaction = signal<PetMood | null>(null);
  protected readonly bubble = signal<(PetLine & { home?: boolean }) | null>(null);
  private tick = 0;
  private timers: ReturnType<typeof setTimeout>[] = [];

  protected readonly size = computed(() => (this.wide() ? 120 : 76));
  protected readonly species = computed(() => toSpecies(this.store.pet()?.species));
  protected readonly stage = computed(() => (this.store.pet() ? stageOf(this.store.pet()!) : 'baby'));
  private readonly inLesson = computed(() => this.url().startsWith('/learn'));

  protected readonly visible = computed(() => {
    const pet = this.store.pet();
    const url = this.url();
    if (!pet || !this.store.roaming() || pet.mood === 'Away' || url.startsWith('/pet')) return false;
    // На телефоне в занятии кнопки внизу экрана важнее питомца.
    return this.wide() || !this.inLesson();
  });

  protected readonly mood = computed<PetMood>(() => {
    const pet = this.store.pet();
    if (!pet) return 'idle';
    return this.reaction() ?? (this.walking() ? 'walking' : baseMood(pet));
  });

  constructor() {
    void this.store.load();
    const destroy = inject(DestroyRef);
    this.router.events.pipe(filter((e) => e instanceof NavigationEnd)).subscribe((e) => this.url.set(e.urlAfterRedirects));

    const measure = () => {
      this.wide.set(window.innerWidth >= 1024);
      // Левое поле рядом с основной колонкой (max-w-3xl = 768px).
      this.gutter.set(Math.max(0, (window.innerWidth - 768) / 2));
      this.x.update((x) => Math.min(x, this.maxX()));
    };
    measure();
    window.addEventListener('resize', measure);
    destroy.onDestroy(() => {
      window.removeEventListener('resize', measure);
      this.timers.forEach(clearTimeout);
    });

    this.schedule(4000);
    this.remind(40_000);
    void this.store.load().then(() => {
      if (!this.store.pet()) return;
      this.api.invoke(getPracticeWords, { count: 10 }).then(
        (words) => this.words.set(words.filter((w) => w.translations[0]).map((w) => ({ text: w.text, translation: w.translations[0] }))),
        () => undefined,
      );
    });

    // Выполнена просьба (игра, текст) — благодарит.
    effect(() => {
      const done = this.store.completed();
      if (done.length === 0 || this.url().startsWith('/pet')) return;
      untracked(() => {
        const treats = done.reduce((sum, q) => sum + n(q.treats), 0);
        this.react('happy', 3000);
        this.say({ text: `Спасибо! Просьба выполнена — угощений: +${treats}.`, home: true }, 7000);
        this.store.completed.set([]);
      });
    });

    // События занятия.
    let lastTreats: number | null = null;
    effect(() => {
      const event = this.signals.last();
      if (!event) return;
      untracked(() => {
        if (event.kind === 'correct') {
          this.react('happy', 1300);
        } else if (event.kind === 'wrong') {
          this.react('sad', 1400);
          this.say({ text: 'Ничего, в следующий раз получится!' }, 2200);
        } else {
          lastTreats = n(this.store.pet()?.treats);
          void this.store.load(true).then(() => {
            const gained = n(this.store.pet()?.treats) - (lastTreats ?? 0);
            if (gained > 0) {
              this.react('happy', 3000);
              this.say({ text: `Ура! Угощений: +${gained}. Покормишь меня?`, home: true }, 6000);
            }
          });
        }
      });
    });
  }

  /** Нажали: погладить (без запроса — ласку считает страница питомца) и что-нибудь сказать. */
  protected poke(): void {
    const pet = this.store.pet();
    if (!pet) return;
    this.tick++;
    void this.store.pat().catch(() => undefined);
    this.say({ ...petLine(pet, this.words(), this.tick), home: true }, 7000);
  }

  private maxX(): number {
    return this.wide() ? Math.max(8, this.gutter() - this.size() - 24) : 0;
  }

  /** Жизнь питомца: посидеть, походить, снова посидеть. Ходит только по левому полю и только днём. */
  private schedule(delay: number): void {
    this.timers.push(
      setTimeout(() => {
        const pet = this.store.pet();
        const mood = pet ? baseMood(pet) : 'idle';
        const room = this.maxX();
        if (this.wide() && room > 40 && (mood === 'idle' || mood === 'hungry') && !this.reaction() && Math.random() < 0.6) {
          const target = Math.round(8 + Math.random() * (room - 8));
          const distance = Math.abs(target - this.x());
          const ms = Math.round((distance / WALK_SPEED) * 1000);
          this.facingLeft.set(target < this.x());
          this.walkMs.set(ms);
          this.walking.set(true);
          this.x.set(target);
          this.timers.push(setTimeout(() => this.walking.set(false), ms));
          this.schedule(ms + 4000 + Math.random() * 8000);
        } else {
          this.schedule(6000 + Math.random() * 8000);
        }
      }, delay),
    );
  }

  private react(mood: PetMood, ms: number): void {
    this.reaction.set(mood);
    this.timers.push(setTimeout(() => this.reaction.set(null), ms));
  }

  protected reveal(word: PetWord): void {
    this.revealed.set(true);
    this.pronunciation.speak(word.text);
  }

  /** Сам напоминает о себе раз в несколько минут: слово, просьба, занятие. Не в занятии — там не мешаем. */
  private remind(delay: number): void {
    this.timers.push(
      setTimeout(() => {
        const pet = this.store.pet();
        if (pet && this.visible() && !this.inLesson() && !this.bubble() && pet.mood !== 'Away') {
          this.tick++;
          this.say(petLine(pet, this.words(), this.tick), 9000);
        }
        this.remind(150_000 + Math.random() * 150_000);
      }, delay),
    );
  }

  private say(bubble: PetLine & { home?: boolean }, ms: number): void {
    this.revealed.set(false);
    this.bubble.set(bubble);
    this.timers.push(
      setTimeout(() => {
        if (this.bubble() === bubble) this.bubble.set(null);
      }, ms),
    );
  }
}

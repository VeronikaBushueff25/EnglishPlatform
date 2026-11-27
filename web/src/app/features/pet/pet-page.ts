import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCheck, lucideCookie, lucideDices, lucideFlame, lucideLock, lucideMessageCircleQuestion, lucideSparkles, lucideVolume2 } from '@ng-icons/lucide';
import { toast } from '@spartan-ng/brain/sonner';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmCheckboxImports } from '@spartan-ng/helm/checkbox';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { HlmSpinnerImports } from '@spartan-ng/helm/spinner';
import { Api } from '../../core/api/api';
import { getPracticeWords } from '../../core/api/fn/practice/get-practice-words';
import { PetDto } from '../../core/api/models/pet-dto';
import { PetQuestDto } from '../../core/api/models/pet-quest-dto';
import { PetSpecies as ApiSpecies } from '../../core/api/models/pet-species';
import { problemMessage } from '../../core/http/problem';
import { Pronunciation } from '../../shared/pronunciation';
import { ACCESSORIES, NAME_IDEAS, PALETTES, PetAccessory, PetMood, PetSpecies, SPECIES_LABELS, toSpecies } from './pet-art';
import { PetQuiz } from './pet-quiz';
import { PetRoom } from './pet-room';
import { DECOR, DECOR_COUNTERS, MOOD_LABELS, PetLine, PetStore, PetWord, QUESTS, STAGE_LABELS, baseMood, isNight, n, nextQuest, petLine, stageOf } from './pet-store';
import { PetView } from './pet-view';

const SPECIES: PetSpecies[] = ['cat', 'corgi', 'hamster'];
const API_SPECIES: Record<PetSpecies, ApiSpecies> = { cat: 'Cat', corgi: 'Corgi', hamster: 'Hamster' };
const SEEN_MOOD = 'owlish-pet-seen-mood';
const SEEN_DECOR = 'owlish-pet-seen-decor';

function readLocal(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function writeLocal(key: string, value: string): void {
  try {
    localStorage.setItem(key, value);
  } catch {
    // без localStorage просто не узнаем, что изменилось с прошлого раза
  }
}

/**
 * Вкладка «Питомец». Питомец — повод учиться: он просит сделать учебные дела (занятие, игра, текст, слова,
 * его вопросы), ест угощения, которые за них дают, а обстановка комнаты — достижения за учёбу.
 */
@Component({
  selector: 'app-pet-page',
  imports: [FormsModule, RouterLink, NgIcon, PetView, PetRoom, PetQuiz, HlmButtonImports, HlmCardImports, HlmCheckboxImports, HlmInputImports, HlmSpinnerImports],
  providers: [provideIcons({ lucideCheck, lucideCookie, lucideDices, lucideFlame, lucideLock, lucideMessageCircleQuestion, lucideSparkles, lucideVolume2 })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .bubble::after { content: ''; position: absolute; left: 50%; bottom: -7px; translate: -50% 0; border: 7px solid transparent; border-top-color: var(--card); border-bottom: 0; }
    .float-up { animation: float-up 1.2s ease-out forwards; }
    @keyframes float-up { from { opacity: 0; transform: translateY(6px); } 20% { opacity: 1; } to { opacity: 0; transform: translateY(-26px); } }
    .pop { animation: pop .5s ease-out; }
    @keyframes pop { 0% { transform: scale(.9); } 60% { transform: scale(1.04); } }
  `,
  template: `
    @if (store.pet() === undefined) {
      <div class="text-muted-foreground flex items-center gap-2"><hlm-spinner aria-label="Загрузка" /> Загружаем…</div>
    } @else if (store.pet() === null) {
      <!-- Знакомство -->
      <div class="flex flex-col gap-5">
        <div>
          <h1 class="text-2xl font-semibold tracking-tight">Заведите питомца</h1>
          <p class="text-muted-foreground mt-1">
            Он будет учиться вместе с вами: каждый день просить о маленьком учебном деле, есть угощения, которые вы за них получаете,
            обживать комнату с каждым достижением и скучать, если вы пропадёте.
          </p>
        </div>

        <section class="flex flex-col gap-2">
          <h2 class="font-medium">Кто это будет?</h2>
          <div class="grid grid-cols-3 gap-3" role="radiogroup" aria-label="Кто это будет?">
            @for (s of species; track s) {
              <button type="button" role="radio" [attr.aria-checked]="kind() === s" (click)="pickSpecies(s)"
                      class="bg-card flex flex-col items-center rounded-3xl border px-2 pt-2 pb-3 transition-shadow"
                      [class.ring-2]="kind() === s" [class.ring-primary]="kind() === s">
                <app-pet-view class="w-full max-w-36" [species]="s" [palette]="paletteFor(s)" [mood]="kind() === s ? 'happy' : 'idle'" [interactive]="false" />
                <span class="text-sm font-medium">{{ speciesLabels[s] }}</span>
              </button>
            }
          </div>
        </section>

        <section class="flex flex-col gap-2">
          <h2 class="font-medium">Окрас</h2>
          <div class="flex flex-wrap gap-2" role="radiogroup" aria-label="Окрас">
            @for (p of paletteKeys(kind()); track p) {
              <button type="button" role="radio" class="size-9 rounded-full border-[3px] border-white shadow-[0_0_0_1px_var(--border)]"
                      [class.shadow-[0_0_0_2px_var(--primary)]]="palette() === p" [style.background]="palettes[kind()][p].fur"
                      [attr.aria-checked]="palette() === p" [attr.aria-label]="palettes[kind()][p].label" [title]="palettes[kind()][p].label"
                      (click)="palette.set(p)"></button>
            }
          </div>
        </section>

        <section class="flex flex-col gap-2">
          <label for="pet-name" class="font-medium">Как зовут?</label>
          <div class="flex gap-2">
            <input hlmInput id="pet-name" class="h-11 flex-1" maxlength="24" autocomplete="off" [ngModel]="name()" (ngModelChange)="name.set($event)" />
            <button hlmBtn variant="outline" size="icon" type="button" class="size-11" aria-label="Придумать имя" title="Придумать имя" (click)="randomName()">
              <ng-icon name="lucideDices" />
            </button>
          </div>
          <div class="flex flex-wrap gap-1.5" translate="no">
            @for (idea of nameIdeas(); track idea) {
              <button type="button" class="hover:bg-muted rounded-full border px-3 py-1 text-sm" (click)="name.set(idea)">{{ idea }}</button>
            }
          </div>
        </section>

        <button hlmBtn size="lg" type="button" class="h-12 self-start" [disabled]="busy() || !name().trim()" (click)="adopt()">
          @if (busy()) { <hlm-spinner aria-label="Знакомимся" /> } @else { <ng-icon name="lucideSparkles" /> }
          Познакомиться
        </button>
      </div>
    } @else if (petData(); as pet) {
      <div class="flex flex-col gap-4">
        <div class="flex flex-wrap items-end justify-between gap-2">
          <div>
            <h1 class="text-2xl font-semibold tracking-tight" translate="no">{{ pet.name }}</h1>
            <p class="text-muted-foreground text-sm">
              {{ speciesLabels[speciesKey()] }} · уровень {{ pet.level }} · {{ stageLabels[pet.stage] }} · {{ moodLabels[pet.mood] }}
            </p>
          </div>
          <div class="flex flex-wrap gap-2 text-sm font-medium">
            <span class="flex items-center gap-1.5 rounded-full bg-orange-100 px-3 py-1.5 text-orange-900 dark:bg-orange-400/15 dark:text-orange-200"
                  [title]="'Рекорд: ' + pet.bestStreak">
              <ng-icon name="lucideFlame" /> {{ streakLabel(pet) }}
            </span>
            <span class="flex items-center gap-1.5 rounded-full bg-amber-100 px-3 py-1.5 text-amber-900 dark:bg-amber-400/15 dark:text-amber-200">
              <ng-icon name="lucideCookie" /> Угощений: {{ pet.treats }}
            </span>
          </div>
        </div>

        <!-- Комната -->
        <section class="relative overflow-hidden rounded-3xl border" aria-label="Комната питомца">
          <app-pet-room [decor]="unlockedDecor()" [night]="night()" label="Комната питомца" />
          <div class="absolute inset-x-0 bottom-[4%] flex flex-col items-center">
            @if (mood() !== 'away') {
              <div class="bubble bg-card text-card-foreground relative mb-1 max-w-[min(20rem,90%)] rounded-2xl border px-4 py-2 text-center text-sm shadow-sm">
                {{ line().text }}
                @if (line().word; as w) {
                  <button type="button" class="text-primary ms-1 inline-flex items-center gap-1 font-semibold" translate="no" (click)="reveal(w)">
                    @if (line().ask && !revealed()) { «{{ w.translation }}» } @else { <span lang="en">{{ w.text }}</span> }
                    <ng-icon name="lucideVolume2" size="14" />
                  </button>
                  @if (revealed()) { <span class="text-muted-foreground block text-xs" translate="no">{{ line().ask ? w.translation : w.text + ' — ' + w.translation }}</span> }
                }
                @if (line().link; as link) {
                  <a hlmBtn size="sm" class="mt-2 h-8 w-full" [routerLink]="link.url">{{ link.label }}</a>
                } @else if (line().quiz) {
                  <button hlmBtn size="sm" type="button" class="mt-2 h-8 w-full" (click)="openQuiz()">Ответить</button>
                } @else if (line().feed) {
                  <button hlmBtn size="sm" type="button" class="mt-2 h-8 w-full" (click)="feed()">Покормить</button>
                }
              </div>
            } @else {
              <div class="mb-10 max-w-72 -rotate-2 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-center text-sm text-amber-950 shadow-sm">
                <span class="font-semibold" translate="no">{{ pet.name }}</span> ушёл гулять. Вернётся, когда вы позанимаетесь.
                <a hlmBtn size="sm" routerLink="/learn" class="mt-2">Начать занятие</a>
              </div>
            }
            <div class="relative w-[38%] max-w-60 min-w-36">
              <app-pet-view plain [species]="speciesKey()" [palette]="pet.palette" [mood]="mood()" [stage]="stage()" [accessory]="pet.accessory"
                            [label]="speciesLabels[speciesKey()]" (patted)="pat()" />
              @for (f of floats(); track f.id) {
                <span class="float-up pointer-events-none absolute top-6 left-1/2 -translate-x-1/2 text-sm font-bold text-rose-500">{{ f.text }}</span>
              }
            </div>
          </div>
        </section>

        <!-- Действия: всё ведёт к учёбе -->
        <div class="flex flex-wrap gap-2">
          <button hlmBtn type="button" class="h-11" [disabled]="busy() || mood() === 'away'" (click)="feed()">
            <ng-icon name="lucideCookie" /> Покормить
          </button>
          <button hlmBtn variant="outline" type="button" class="h-11" [disabled]="mood() === 'away'" (click)="openQuiz()">
            <ng-icon name="lucideMessageCircleQuestion" /> Ответить на вопросы
          </button>
          <p class="text-muted-foreground self-center text-sm">Погладить — нажмите на питомца.</p>
        </div>

        @if (needTreats()) {
          <section class="pop rounded-3xl border border-amber-200 bg-amber-50 p-4 text-amber-950 dark:border-amber-400/30 dark:bg-amber-400/10 dark:text-amber-100">
            <p class="font-medium">Угощения закончились — их дают за учёбу</p>
            <ul class="mt-2 flex flex-col gap-2 text-sm">
              <li class="flex flex-wrap items-center justify-between gap-2">
                <span>Пройти занятие — 2–6 угощений</span>
                <a hlmBtn size="sm" routerLink="/learn">Начать занятие</a>
              </li>
              @for (q of openQuests(); track q.kind) {
                <li class="flex flex-wrap items-center justify-between gap-2">
                  <span><span>{{ quests[q.kind].request }}</span> <span class="opacity-70">+{{ q.treats }} угощ.</span></span>
                  @if (quests[q.kind].url; as url) {
                    <a hlmBtn variant="outline" size="sm" [routerLink]="url">{{ quests[q.kind].action }}</a>
                  } @else {
                    <button hlmBtn variant="outline" size="sm" type="button" (click)="openQuiz()">{{ quests[q.kind].action }}</button>
                  }
                </li>
              }
            </ul>
          </section>
        }

        @if (quizOpen()) {
          <app-pet-quiz (answered)="onAnswer($event)" (closed)="quizOpen.set(false)" />
        }

        <!-- Просьбы на сегодня -->
        <section hlmCard class="gap-3 py-4">
          <div class="px-4">
            <h2 class="font-semibold">Просьбы на сегодня</h2>
            <p class="text-muted-foreground text-sm">Каждый день — три учебных дела. За каждое — угощения и опыт.</p>
          </div>
          <ul class="flex flex-col divide-y px-4">
            @for (q of pet.quests; track q.kind) {
              <li class="flex flex-wrap items-center gap-3 py-3 first:pt-0 last:pb-0">
                <span class="flex size-8 shrink-0 items-center justify-center rounded-full border"
                      [class.bg-success]="q.done" [class.text-white]="q.done" [class.border-success]="q.done">
                  @if (q.done) { <ng-icon name="lucideCheck" /> } @else { <span class="text-muted-foreground text-xs tabular-nums">{{ q.progress }}/{{ q.target }}</span> }
                </span>
                <div class="min-w-0 flex-1">
                  <p class="text-sm" [class.text-muted-foreground]="q.done" [class.line-through]="q.done">{{ quests[q.kind].request }}</p>
                  <p class="text-muted-foreground text-xs">+{{ q.treats }} угощ. · +{{ q.xp }} опыта</p>
                </div>
                @if (!q.done) {
                  @if (quests[q.kind].url; as url) {
                    <a hlmBtn variant="outline" size="sm" [routerLink]="url">{{ quests[q.kind].action }}</a>
                  } @else {
                    <button hlmBtn variant="outline" size="sm" type="button" (click)="openQuiz()">{{ quests[q.kind].action }}</button>
                  }
                }
              </li>
            }
          </ul>
        </section>

        <!-- Шкалы -->
        <div class="grid gap-3 sm:grid-cols-3">
          @for (m of meters(); track m.label) {
            <div class="bg-card rounded-2xl border p-3">
              <div class="flex items-baseline justify-between text-sm"><span class="font-medium">{{ m.label }}</span><span class="text-muted-foreground tabular-nums">{{ m.value }}</span></div>
              <div class="bg-muted mt-2 h-2.5 overflow-hidden rounded-full" role="meter" [attr.aria-label]="m.label" aria-valuemin="0" aria-valuemax="100" [attr.aria-valuenow]="m.percent">
                <div class="h-full rounded-full transition-[width] duration-700" [class]="m.color" [style.width.%]="m.percent"></div>
              </div>
            </div>
          }
        </div>

        <!-- Обстановка — достижения -->
        <section hlmCard class="gap-3 py-4">
          <div class="px-4">
            <h2 class="font-semibold">Обстановка комнаты</h2>
            <p class="text-muted-foreground text-sm">Вещи появляются в комнате за учёбу. Открыто {{ unlockedDecor().length }} из {{ pet.decor.length }}.</p>
          </div>
          <ul class="grid gap-2 px-4 sm:grid-cols-2">
            @for (d of pet.decor; track d.key) {
              <li class="flex items-center gap-3 rounded-2xl border p-3" [class.bg-muted/50]="!d.unlocked" [class.border-primary]="isNew(d.key)">
                <span class="flex size-9 shrink-0 items-center justify-center rounded-full"
                      [class.bg-primary/15]="d.unlocked" [class.text-primary]="d.unlocked" [class.bg-muted]="!d.unlocked" [class.text-muted-foreground]="!d.unlocked">
                  <ng-icon [name]="d.unlocked ? 'lucideCheck' : 'lucideLock'" />
                </span>
                <div class="min-w-0 flex-1">
                  <p class="text-sm font-medium">{{ decorLabels[d.key] }}</p>
                  @if (d.unlocked) {
                    <p class="text-muted-foreground text-xs">{{ +d.target === 0 ? 'Есть с первого дня' : 'Открыто: ' + decorCounters[d.counter] + ' — ' + d.target }}</p>
                  } @else {
                    <p class="text-muted-foreground text-xs">{{ decorCounters[d.counter] }}: {{ d.progress }}/{{ d.target }}</p>
                    <div class="bg-muted mt-1 h-1.5 overflow-hidden rounded-full"><div class="bg-primary h-full rounded-full" [style.width.%]="(+d.progress / +d.target) * 100"></div></div>
                  }
                </div>
              </li>
            }
          </ul>
        </section>

        <!-- Гардероб -->
        <section hlmCard class="gap-3 py-4">
          <div class="px-4"><h2 class="font-semibold">Гардероб</h2><p class="text-muted-foreground text-sm">Новые вещи открываются с уровнями — опыт дают занятия и просьбы.</p></div>
          <div class="flex flex-wrap gap-2 px-4">
            <button type="button" class="rounded-full border px-3 py-1.5 text-sm" [class.border-primary]="!pet.accessory" [class.text-primary]="!pet.accessory" (click)="wear(null)">Без аксессуара</button>
            @for (a of accessoryKeys; track a) {
              @if (unlocked(pet, a)) {
                <button type="button" class="rounded-full border px-3 py-1.5 text-sm" [class.border-primary]="pet.accessory === a" [class.text-primary]="pet.accessory === a" (click)="wear(a)">{{ accessories[a].label }}</button>
              } @else if (a === 'heart') {
                <a routerLink="/support" class="text-muted-foreground hover:text-primary rounded-full border border-dashed px-3 py-1.5 text-sm">{{ accessories[a].label }} · за поддержку проекта</a>
              } @else {
                <span class="text-muted-foreground rounded-full border border-dashed px-3 py-1.5 text-sm">{{ accessories[a].label }} · с {{ accessories[a].level }} уровня</span>
              }
            }
          </div>
        </section>

        <!-- Окрас и имя -->
        <section hlmCard class="gap-3 py-4">
          <div class="px-4"><h2 class="font-semibold">Окрас и имя</h2></div>
          <div class="flex flex-wrap gap-2 px-4" role="radiogroup" aria-label="Окрас">
            @for (p of paletteKeys(speciesKey()); track p) {
              <button type="button" role="radio" class="size-9 rounded-full border-[3px] border-white shadow-[0_0_0_1px_var(--border)]"
                      [class.shadow-[0_0_0_2px_var(--primary)]]="pet.palette === p" [style.background]="palettes[speciesKey()][p].fur"
                      [attr.aria-checked]="pet.palette === p" [attr.aria-label]="palettes[speciesKey()][p].label" [title]="palettes[speciesKey()][p].label"
                      (click)="restyle(p)"></button>
            }
          </div>
          <form class="flex gap-2 px-4" (submit)="$event.preventDefault(); rename()">
            <input hlmInput class="h-10 flex-1" maxlength="24" aria-label="Имя" [ngModel]="newName()" (ngModelChange)="newName.set($event)" name="petName" />
            <button hlmBtn variant="outline" type="submit" class="h-10" [disabled]="!newName().trim() || newName().trim() === pet.name">Переименовать</button>
          </form>
          <label class="flex items-start gap-2 px-4 text-sm">
            <hlm-checkbox class="mt-0.5" [checked]="store.roaming()" (checkedChange)="store.setRoaming($event)" />
            <span>Питомец на страницах приложения: сидит сбоку, напоминает слова и просьбы, радуется вашим ответам в занятии</span>
          </label>
        </section>

        <!-- Вместе -->
        <div class="text-muted-foreground grid grid-cols-2 gap-3 text-center text-sm sm:grid-cols-4">
          <div class="bg-card rounded-2xl border p-3"><p class="text-foreground text-xl font-semibold tabular-nums">{{ daysTogether() }}</p>дней вместе</div>
          <div class="bg-card rounded-2xl border p-3"><p class="text-foreground text-xl font-semibold tabular-nums">{{ pet.lessonsCompleted }}</p>занятий</div>
          <div class="bg-card rounded-2xl border p-3"><p class="text-foreground text-xl font-semibold tabular-nums">{{ pet.questsCompleted }}</p>просьб выполнено</div>
          <div class="bg-card rounded-2xl border p-3"><p class="text-foreground text-xl font-semibold tabular-nums">{{ pet.bestStreak }}</p>рекорд серии</div>
        </div>
      </div>
    }
  `,
})
export class PetPage {
  protected readonly store = inject(PetStore);
  private readonly api = inject(Api);
  private readonly pronunciation = inject(Pronunciation);

  protected readonly species = SPECIES;
  protected readonly speciesLabels = SPECIES_LABELS;
  protected readonly palettes = PALETTES;
  protected readonly accessories = ACCESSORIES;
  protected readonly accessoryKeys = Object.keys(ACCESSORIES) as PetAccessory[];
  protected readonly moodLabels = MOOD_LABELS;
  protected readonly stageLabels = STAGE_LABELS;
  protected readonly quests = QUESTS;
  protected readonly decorLabels = DECOR;
  protected readonly decorCounters = DECOR_COUNTERS;

  // Знакомство
  protected readonly kind = signal<PetSpecies>('cat');
  protected readonly palette = signal('ginger');
  protected readonly name = signal(NAME_IDEAS.cat[0]);
  protected readonly nameIdeas = computed(() => NAME_IDEAS[this.kind()].slice(0, 5));

  protected readonly busy = signal(false);
  protected readonly quizOpen = signal(false);
  protected readonly needTreats = signal(false);
  protected readonly revealed = signal(false);
  /** Временное состояние поверх обычного (ест, ловит мячик, радуется). */
  private readonly action = signal<PetMood | null>(null);
  private actionTimer: ReturnType<typeof setTimeout> | undefined;
  protected readonly floats = signal<{ id: number; text: string }[]>([]);
  protected readonly newName = signal('');
  private readonly words = signal<PetWord[]>([]);
  private readonly tick = signal(0);
  protected readonly night = signal(isNight());
  private readonly newDecor = signal<ReadonlySet<string>>(new Set());

  protected readonly petData = computed(() => this.store.pet() ?? null);
  protected readonly speciesKey = computed(() => toSpecies(this.petData()?.species));
  protected readonly stage = computed(() => (this.petData() ? stageOf(this.petData()!) : 'baby'));
  protected readonly unlockedDecor = computed(() => (this.petData()?.decor ?? []).filter((d) => d.unlocked).map((d) => d.key));
  protected readonly openQuests = computed(() => (this.petData()?.quests ?? []).filter((q) => !q.done));
  /** Тронули ночью — просыпается на минуту. */
  private readonly awake = signal(false);
  protected readonly mood = computed<PetMood>(() => {
    const pet = this.petData();
    if (!pet) return 'idle';
    const base = baseMood(pet);
    if (base === 'away') return 'away';
    const action = this.action();
    if (action) return action;
    return base === 'sleeping' && this.awake() ? 'idle' : base;
  });
  /** Временная реплика в ответ на действие (например, «я сыт — давай лучше займёмся делом»). */
  private readonly hint = signal<PetLine | null>(null);
  private hintTimer: ReturnType<typeof setTimeout> | undefined;
  protected readonly line = computed<PetLine>(() => {
    const pet = this.petData();
    return this.hint() ?? (pet ? petLine(pet, this.words(), this.tick()) : { text: '' });
  });
  protected readonly meters = computed(() => {
    const pet = this.petData();
    if (!pet) return [];
    const levelSpan = Math.max(1, n(pet.nextLevelXp) - n(pet.levelXp));
    const levelDone = n(pet.xp) - n(pet.levelXp);
    return [
      { label: 'Сытость', value: `${pet.satiety}%`, percent: n(pet.satiety), color: 'bg-amber-400' },
      { label: 'Настроение', value: `${pet.affection}%`, percent: n(pet.affection), color: 'bg-rose-400' },
      { label: `Уровень ${pet.level}`, value: `${levelDone}/${levelSpan}`, percent: Math.round((levelDone / levelSpan) * 100), color: 'bg-violet-500' },
    ];
  });
  protected readonly daysTogether = computed(() => {
    const pet = this.petData();
    return pet ? Math.max(1, Math.ceil((Date.now() - Date.parse(pet.createdAt)) / 86_400_000)) : 0;
  });

  constructor() {
    void this.store.load(true).then(() => this.afterLoad());
    const timer = setInterval(() => {
      this.tick.update((t) => t + 1);
      this.revealed.set(false);
      this.night.set(isNight());
    }, 12000);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));

    // Просьба выполнена (вопросы питомцу) — радость и поздравление.
    effect(() => {
      const done = this.store.completed();
      if (done.length === 0) return;
      untracked(() => {
        this.celebrate(done);
        this.store.completed.set([]);
      });
    });
    // Новые вещи в комнате.
    effect(() => {
      const keys = this.unlockedDecor();
      if (keys.length === 0) return;
      untracked(() => this.checkDecor(keys));
    });
  }

  private afterLoad(): void {
    const pet = this.petData();
    if (!pet) return;
    this.newName.set(pet.name);
    void this.api.invoke(getPracticeWords, { count: 12 }).then(
      (words) => this.words.set(words.filter((w) => w.text.length <= 24 && w.translations[0]).map((w) => ({ text: w.text, translation: w.translations[0] }))),
      () => undefined,
    );
    const seen = readLocal(SEEN_MOOD);
    writeLocal(SEEN_MOOD, pet.mood);
    if (seen === 'Away' && pet.mood !== 'Away') {
      this.show('happy', 4000);
      toast.success('Питомец вернулся домой!', { description: 'Он соскучился. Покормите его угощениями за занятие.' });
    }
  }

  private checkDecor(keys: string[]): void {
    const raw = readLocal(SEEN_DECOR);
    writeLocal(SEEN_DECOR, keys.join(','));
    if (raw === null) return; // первый визит — всё «старое»
    const seen = new Set(raw.split(','));
    const fresh = keys.filter((k) => !seen.has(k));
    if (fresh.length === 0) return;
    this.newDecor.set(new Set(fresh));
    this.show('happy', 2500);
    toast.success('В комнате новая вещь!', { description: fresh.map((k) => DECOR[k]).join(', ') });
  }

  protected isNew(key: string): boolean {
    return this.newDecor().has(key);
  }

  protected streakLabel(pet: PetDto): string {
    const s = n(pet.streak);
    return s > 0 ? `${s} дн. подряд` : 'Серия: 0';
  }

  // ---------- Знакомство ----------

  protected paletteFor(s: PetSpecies): string {
    return s === this.kind() ? this.palette() : Object.keys(PALETTES[s])[0];
  }

  protected paletteKeys(s: PetSpecies): string[] {
    return Object.keys(PALETTES[s]);
  }

  protected pickSpecies(s: PetSpecies): void {
    if (s === this.kind()) return;
    const wasIdea = NAME_IDEAS[this.kind()].includes(this.name());
    this.kind.set(s);
    this.palette.set(Object.keys(PALETTES[s])[0]);
    if (wasIdea || !this.name().trim()) this.name.set(NAME_IDEAS[s][0]);
  }

  protected randomName(): void {
    const ideas = NAME_IDEAS[this.kind()].filter((x) => x !== this.name());
    this.name.set(ideas[Math.floor(Math.random() * ideas.length)]);
  }

  protected async adopt(): Promise<void> {
    this.busy.set(true);
    try {
      const pet = await this.store.adopt({ species: API_SPECIES[this.kind()], palette: this.palette(), name: this.name().trim() });
      this.afterLoad();
      this.show('happy', 3500);
      toast.success(`Знакомьтесь: ${pet.name}!`, { description: 'Вот три угощения для начала. Новые дают за занятия и просьбы питомца.' });
    } catch (e) {
      toast.error(problemMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  // ---------- Уход и учёба ----------

  /** Покормить: есть угощения — ест; нет — подсказка, какими учебными делами их заработать. */
  protected async feed(): Promise<void> {
    const pet = this.petData();
    if (!pet) return;
    if (n(pet.treats) <= 0) {
      this.needTreats.set(true);
      return;
    }
    if (n(pet.satiety) >= 95) {
      // Сытого не кормим — питомец зовёт к учебному делу из просьб.
      const quest = nextQuest(pet);
      const info = quest ? QUESTS[quest.kind] : null;
      this.say(
        info?.url
          ? { text: 'Я сыт! Давай лучше займёмся делом: ' + info.request, link: { label: info.action, url: info.url } }
          : info
            ? { text: 'Я сыт! Давай лучше займёмся делом: ' + info.request, quiz: true }
            : { text: 'Я сыт! Все просьбы на сегодня выполнены — ты молодец.' },
      );
      return;
    }
    this.needTreats.set(false);
    this.busy.set(true);
    try {
      await this.store.feed();
      this.show('eating', 4500, 'happy', 2000);
    } catch (e) {
      toast.error(problemMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected openQuiz(): void {
    this.quizOpen.set(true);
    this.needTreats.set(false);
  }

  protected onAnswer(correct: boolean): void {
    if (correct) {
      this.show('playing', 2800, 'happy', 1200);
      this.float('+♥');
    } else {
      this.show('sad', 1500);
    }
  }

  protected async pat(): Promise<void> {
    if (this.mood() === 'away') return;
    this.awake.set(true);
    setTimeout(() => this.awake.set(false), 60_000);
    const before = n(this.petData()?.patsLeft);
    try {
      const pet = await this.store.pat();
      this.float(n(pet.patsLeft) < before ? '+♥' : '♥');
    } catch {
      this.float('♥');
    }
  }

  protected reveal(word: PetWord): void {
    this.revealed.set(true);
    this.pronunciation.speak(word.text);
  }

  protected unlocked(pet: PetDto, accessory: PetAccessory): boolean {
    return pet.accessories.includes(accessory);
  }

  protected wear(accessory: PetAccessory | null): Promise<void> {
    return this.save({ accessory });
  }

  protected restyle(palette: string): Promise<void> {
    return this.save({ palette });
  }

  protected rename(): Promise<void> {
    return this.save({ name: this.newName().trim() });
  }

  private async save(change: { palette?: string; name?: string; accessory?: string | null }): Promise<void> {
    const pet = this.petData();
    if (!pet) return;
    try {
      await this.store.update({
        palette: change.palette ?? pet.palette,
        name: change.name ?? pet.name,
        accessory: change.accessory !== undefined ? change.accessory : pet.accessory,
      });
      if (change.accessory !== undefined) this.show('happy', 1800);
      if (change.name) toast.success('Имя сохранено');
    } catch (e) {
      toast.error(problemMessage(e));
    }
  }

  private say(line: PetLine): void {
    clearTimeout(this.hintTimer);
    this.hint.set(line);
    this.hintTimer = setTimeout(() => this.hint.set(null), 12000);
  }

  private celebrate(done: PetQuestDto[]): void {
    const treats = done.reduce((sum, q) => sum + n(q.treats), 0);
    this.show('happy', 3000);
    toast.success('Просьба выполнена!', { description: `Угощений: +${treats}. Питомец благодарит.` });
  }

  /** Показать временное состояние; можно сразу задать следующее (поел → радуется). */
  private show(mood: PetMood, ms: number, then?: PetMood, thenMs = 0): void {
    clearTimeout(this.actionTimer);
    this.action.set(mood);
    this.actionTimer = setTimeout(() => (then ? this.show(then, thenMs) : this.action.set(null)), ms);
  }

  private float(text: string): void {
    const id = Date.now() + Math.random();
    this.floats.update((f) => [...f, { id, text }]);
    setTimeout(() => this.floats.update((f) => f.filter((x) => x.id !== id)), 1300);
  }
}

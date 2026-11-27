import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { ACCESSORIES, PALETTES, PetAccessory, PetMood, PetSpecies, PetStage, SPECIES_LABELS } from './pet-art';
import { PetView } from './pet-view';

const MOODS: Record<PetMood, string> = {
  idle: 'Обычное',
  happy: 'Радуется',
  eating: 'Ест',
  hungry: 'Голодный',
  sad: 'Грустит',
  sleeping: 'Спит',
  playing: 'Играет',
  walking: 'Ходит',
  away: 'Ушёл гулять',
};

const STAGES: Record<PetStage, string> = { baby: 'Малыш', teen: 'Подросток', adult: 'Взрослый' };

/** Витрина рисунков: все звери, окрасы, состояния, рост и аксессуары. */
@Component({
  selector: 'app-pet-lab',
  imports: [PetView, HlmCardImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4">
      <div>
        <h1 class="text-2xl font-semibold tracking-tight">Питомцы</h1>
        <p class="text-muted-foreground mt-1">Наведите курсор — питомец следит глазами. Нажмите на него — погладить.</p>
      </div>

      @for (group of groups; track group.label) {
        <div class="flex flex-wrap items-center gap-2" role="group" [attr.aria-label]="group.label">
          @for (o of group.options; track o.key) {
            <button type="button" class="rounded-full border px-3 py-1.5 text-sm transition-colors"
                    [class.bg-primary]="group.value() === o.key" [class.text-primary-foreground]="group.value() === o.key" [class.border-primary]="group.value() === o.key"
                    [attr.aria-pressed]="group.value() === o.key" (click)="group.set(o.key)">{{ o.label }}</button>
          }
        </div>
      }

      <div class="grid gap-4 sm:grid-cols-3">
        @for (s of species; track s) {
          <section hlmCard class="gap-2 py-4">
            <div class="px-4"><h2 class="font-semibold">{{ speciesLabels[s] }}</h2></div>
            <app-pet-view class="px-3" [species]="s" [palette]="palette()[s]" [mood]="mood()" [stage]="stage()" [accessory]="accessory()" [label]="speciesLabels[s]" />
            <div class="flex flex-wrap gap-2 px-4" role="radiogroup" [attr.aria-label]="'Окрас: ' + speciesLabels[s]">
              @for (p of paletteKeys(s); track p) {
                <button type="button" role="radio" class="size-8 rounded-full border-[3px] border-white shadow-[0_0_0_1px_var(--border)]"
                        [class.shadow-[0_0_0_2px_var(--primary)]]="palette()[s] === p" [style.background]="palettes[s][p].fur"
                        [attr.aria-checked]="palette()[s] === p" [attr.aria-label]="palettes[s][p].label" [title]="palettes[s][p].label"
                        (click)="choose(s, p)"></button>
              }
            </div>
          </section>
        }
      </div>
    </div>
  `,
})
export class PetLab {
  protected readonly species = Object.keys(SPECIES_LABELS) as PetSpecies[];
  protected readonly speciesLabels = SPECIES_LABELS;
  protected readonly palettes = PALETTES;

  protected readonly mood = signal<PetMood>('idle');
  protected readonly stage = signal<PetStage>('adult');
  protected readonly accessory = signal<PetAccessory | ''>('');
  protected readonly palette = signal<Record<PetSpecies, string>>({ cat: 'ginger', corgi: 'red', hamster: 'golden' });

  protected readonly groups = [
    { label: 'Настроение', value: this.mood, set: (k: string) => this.mood.set(k as PetMood), options: Object.entries(MOODS).map(([key, label]) => ({ key, label })) },
    { label: 'Возраст', value: this.stage, set: (k: string) => this.stage.set(k as PetStage), options: Object.entries(STAGES).map(([key, label]) => ({ key, label })) },
    {
      label: 'Аксессуар',
      value: this.accessory,
      set: (k: string) => this.accessory.set(k as PetAccessory | ''),
      options: [{ key: '', label: 'Без аксессуара' }, ...Object.entries(ACCESSORIES).map(([key, a]) => ({ key, label: a.label }))],
    },
  ];

  protected paletteKeys(species: PetSpecies): string[] {
    return Object.keys(PALETTES[species]);
  }

  protected choose(species: PetSpecies, key: string): void {
    this.palette.update((p) => ({ ...p, [species]: key }));
  }
}

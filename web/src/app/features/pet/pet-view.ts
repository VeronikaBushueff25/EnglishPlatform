import { ChangeDetectionStrategy, Component, ElementRef, ViewEncapsulation, booleanAttribute, effect, inject, input, output, signal, viewChild } from '@angular/core';
import { PetAccessory, PetMood, PetSpecies, PetStage, petSvg } from './pet-art';

/**
 * Живой питомец: дышит, моргает, следит глазами за указателем, радуется, если погладить (нажать).
 * Настроение — mood; внешний вид — species, palette, stage, accessory. Логика ухода — на сервере, здесь только рисунок.
 */
@Component({
  selector: 'app-pet-view',
  template: `<div #stage class="pet-stage" [class.plain]="plain()"></div>`,
  styleUrl: './pet-view.css',
  // SVG вставляется как HTML, поэтому стили не изолируются (все селекторы начинаются с .pet).
  encapsulation: ViewEncapsulation.None,
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block select-none',
    '[class]': 'classes()',
    '[style.cursor]': "interactive() && mood() !== 'away' ? 'pointer' : null",
    role: 'img',
    '[attr.aria-label]': 'label() || null',
    '(click)': 'pet()',
    '(document:pointermove)': 'look($event)',
  },
})
export class PetView {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private petTimer: ReturnType<typeof setTimeout> | undefined;

  readonly species = input.required<PetSpecies>();
  readonly palette = input.required<string>();
  readonly mood = input<PetMood>('idle');
  readonly stage = input<PetStage>('adult');
  readonly accessory = input<PetAccessory | string | null>(null);
  readonly facingLeft = input(false, { transform: booleanAttribute });
  readonly interactive = input(true, { transform: booleanAttribute });
  /** Без пятна света под питомцем (в комнате на странице питомца свой фон). */
  readonly plain = input(false, { transform: booleanAttribute });
  readonly label = input('');
  /** Погладили (нажали). */
  readonly patted = output<void>();

  protected readonly petted = signal(false);
  private readonly stageEl = viewChild.required<ElementRef<HTMLElement>>('stage');

  protected classes(): string {
    const parts = [`is-${this.mood()}`, `stage-${this.stage()}`];
    if (this.accessory()) parts.push(`wear-${this.accessory()}`);
    if (this.petted()) parts.push('is-petted');
    if (this.facingLeft()) parts.push('facing-left');
    return parts.join(' ');
  }

  constructor() {
    // Рисунок собирается только из наших констант (pet-art.ts), пользовательских данных в нём нет, поэтому
    // вставляем его напрямую: [innerHTML] потянул бы в стартовый бандл санитайзер HTML (~7 КБ).
    effect(() => {
      this.stageEl().nativeElement.innerHTML = petSvg(this.species(), this.palette());
    });
  }

  protected pet(): void {
    if (!this.interactive() || this.mood() === 'away') return;
    this.petted.set(true);
    clearTimeout(this.petTimer);
    this.petTimer = setTimeout(() => this.petted.set(false), 1600);
    this.patted.emit();
  }

  /** Зрачки смещаются в сторону указателя — в пределах глаза. */
  protected look(event: PointerEvent): void {
    const r = this.host.getBoundingClientRect();
    if (r.width === 0) return;
    const flip = this.facingLeft() ? -1 : 1;
    const dx = Math.max(-1, Math.min(1, (event.clientX - (r.left + r.width * 0.45)) / (r.width * 1.2)));
    const dy = Math.max(-1, Math.min(1, (event.clientY - (r.top + r.height * 0.45)) / (r.height * 1.2)));
    this.host.style.setProperty('--look-x', `${(dx * 3.2 * flip).toFixed(2)}px`);
    this.host.style.setProperty('--look-y', `${(dy * 2.6).toFixed(2)}px`);
  }
}

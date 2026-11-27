import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * Комната питомца — SVG-сцена. Вещи появляются, когда открыты достижения за учёбу (decor — ключи открытых вещей).
 * Ночью темнеет, в окне луна и звёзды, торшер и гирлянда светятся. Сам питомец — поверх, в разметке страницы.
 */
@Component({
  selector: 'app-pet-room',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  styles: `
    svg { display: block; width: 100%; height: auto; }
    :host { --wall-a: #f3edff; --wall-b: #e9e1fb; --dots: #ddd2f6; --floor-a: #ecd6bb; --floor-b: #e2c6a4; --plank: #d2b08a; --board: #fff; --shade: #0000000f; }
    :host-context(.dark) { --wall-a: #262238; --wall-b: #1e1a2f; --dots: #2f2a45; --floor-a: #3a2f38; --floor-b: #322832; --plank: #2a2129; --board: #3b3550; --shade: #00000040; }
    .night-veil { fill: #0b1030; opacity: .28; }
    .glow { animation: glow 2.6s ease-in-out infinite; }
    .bulb { animation: twinkle 1.8s ease-in-out infinite; }
    .bulb:nth-child(2n) { animation-delay: .6s; } .bulb:nth-child(3n) { animation-delay: 1.2s; }
    .fish { animation: swim 5s ease-in-out infinite; transform-box: fill-box; transform-origin: center; }
    .leaf { animation: sway 4s ease-in-out infinite; transform-box: fill-box; transform-origin: bottom center; }
    @keyframes glow { 50% { opacity: .75; } }
    @keyframes twinkle { 50% { opacity: .45; } }
    @keyframes swim { 0%, 100% { transform: translateX(-7px); } 50% { transform: translateX(7px) scaleX(-1); } }
    @keyframes sway { 50% { transform: rotate(4deg); } }
    @media (prefers-reduced-motion: reduce) { * { animation: none !important; } }
  `,
  template: `
    <svg viewBox="0 0 400 260" role="img" [attr.aria-label]="label()">
      <defs>
        <linearGradient id="room-wall" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="var(--wall-a)" /><stop offset="1" stop-color="var(--wall-b)" /></linearGradient>
        <linearGradient id="room-floor" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="var(--floor-a)" /><stop offset="1" stop-color="var(--floor-b)" /></linearGradient>
        <linearGradient id="room-sky" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" [attr.stop-color]="night() ? '#1b2452' : '#7cc8ff'" /><stop offset="1" [attr.stop-color]="night() ? '#3b4685' : '#d6efff'" />
        </linearGradient>
        <radialGradient id="room-lamp-light"><stop offset="0" stop-color="#ffe9a8" stop-opacity=".75" /><stop offset="1" stop-color="#ffe9a8" stop-opacity="0" /></radialGradient>
        <pattern id="room-dots" width="18" height="18" patternUnits="userSpaceOnUse"><circle cx="4" cy="4" r="1.6" fill="var(--dots)" /><circle cx="13" cy="13" r="1.6" fill="var(--dots)" /></pattern>
      </defs>

      <!-- Стена, обои, пол -->
      <rect width="400" height="172" fill="url(#room-wall)" />
      <rect width="400" height="172" fill="url(#room-dots)" />
      <rect y="172" width="400" height="88" fill="url(#room-floor)" />
      @for (x of planks; track x) { <path [attr.d]="'M' + x + ' 172 L' + (x * 1.35 - 70) + ' 260'" stroke="var(--plank)" stroke-width="1.5" /> }
      <path d="M0 205 H400 M0 236 H400" stroke="var(--plank)" stroke-width="1" opacity=".5" />
      <rect y="166" width="400" height="7" fill="var(--board)" />
      <rect y="172" width="400" height="3" fill="var(--shade)" />

      <!-- Окно с занавесками -->
      <g>
        <rect x="26" y="26" width="92" height="88" rx="6" fill="url(#room-sky)" stroke="#fff" stroke-width="5" />
        @if (night()) {
          <circle cx="92" cy="48" r="10" fill="#fff4c9" /><circle cx="97" cy="45" r="9" fill="#2a3468" />
          <circle cx="44" cy="44" r="1.4" fill="#fff" /><circle cx="64" cy="62" r="1.2" fill="#fff" /><circle cx="50" cy="90" r="1.3" fill="#fff" /><circle cx="100" cy="86" r="1.1" fill="#fff" />
        } @else {
          <circle cx="94" cy="48" r="11" fill="#ffd34d" />
          <path d="M38 92 a8 8 0 0 1 14 -5 a10 10 0 0 1 18 4 a6 6 0 0 1 0 9 h-30 a5 5 0 0 1 -2 -8 Z" fill="#fff" opacity=".95" />
        }
        <path d="M72 26 V114 M26 70 H118" stroke="#fff" stroke-width="4" />
        <path d="M16 18 H128" stroke="#9a86d8" stroke-width="4" stroke-linecap="round" />
        <path d="M18 20 C 24 50, 20 90, 30 122 L 16 122 Z" fill="#b9a6ee" opacity=".9" />
        <path d="M126 20 C 120 50, 124 90, 114 122 L 128 122 Z" fill="#b9a6ee" opacity=".9" />
        <rect x="20" y="114" width="104" height="6" rx="3" fill="var(--board)" />
      </g>

      <!-- Гирлянда -->
      @if (has('lights')) {
        <path d="M140 10 Q 200 34 260 10 Q 320 34 392 10" fill="none" stroke="#6b5a8e" stroke-width="1.5" />
        <g>
          @for (b of bulbs; track b.x) { <circle class="bulb" [attr.cx]="b.x" [attr.cy]="b.y" r="4" [attr.fill]="b.c" [style.filter]="night() ? 'drop-shadow(0 0 5px ' + b.c + ')' : null" /> }
        </g>
      }

      <!-- Постер -->
      @if (has('poster')) {
        <g transform="translate(304 100) rotate(3) scale(.82)">
          <rect width="58" height="70" rx="4" fill="#fff" stroke="#d9cff2" stroke-width="2" />
          <rect x="6" y="6" width="46" height="32" rx="2" fill="#7c6cf2" />
          <path d="M6 30 L20 18 L30 28 L38 20 L52 34 V38 H6 Z" fill="#a99cff" />
          <text x="29" y="54" text-anchor="middle" font-family="system-ui,sans-serif" font-weight="800" font-size="11" fill="#4e3fc2">Hello!</text>
          <text x="29" y="64" text-anchor="middle" font-family="system-ui,sans-serif" font-size="6.5" fill="#8a7fb5">learn every day</text>
        </g>
      }

      <!-- Полка: книги, глобус, кубок -->
      @if (has('books') || has('globe') || has('trophy')) {
        <rect x="262" y="82" width="116" height="7" rx="2" fill="#b98a5e" />
        <path d="M272 89 v8 M368 89 v8" stroke="#9c714a" stroke-width="4" />
      }
      @if (has('books')) {
        <g>
          <rect x="300" y="50" width="11" height="32" rx="1.5" fill="#e5484d" /><rect x="312" y="56" width="9" height="26" rx="1.5" fill="#3e9b6e" />
          <rect x="322" y="46" width="12" height="36" rx="1.5" fill="#5b8def" /><rect x="335" y="58" width="9" height="24" rx="1.5" fill="#f2a65a" />
          <rect x="346" y="54" width="10" height="28" rx="1.5" fill="#7c6cf2" transform="rotate(8 351 82)" />
          <path d="M325 54 h6 M302 58 h7" stroke="#fff" stroke-width="1.5" opacity=".7" />
        </g>
      }
      @if (has('globe')) {
        <g>
          <path d="M284 82 h12 l-3 -5 h-6 Z" fill="#8a6a2b" /><path d="M290 77 v-4" stroke="#8a6a2b" stroke-width="2" />
          <circle cx="290" cy="62" r="12" fill="#6ec3f0" stroke="#3c8fbf" stroke-width="2" />
          <path d="M282 56 q5 -4 9 1 q2 5 -3 7 q-4 1 -6 -3 Z M293 64 q5 -2 7 3 q-2 4 -6 3 Z" fill="#59b26a" />
          <path d="M276 62 a14 14 0 0 1 28 0" fill="none" stroke="#c99a2e" stroke-width="1.6" />
        </g>
      }
      @if (has('trophy')) {
        <g>
          <path d="M361 82 h12 v-4 h-12 Z M364 78 h6 v-6 h-6 Z" fill="#c98a00" />
          <path d="M358 56 h18 v6 a9 9 0 0 1 -18 0 Z" fill="#ffc83d" stroke="#c98a00" stroke-width="1.5" />
          <path d="M358 58 h-4 a4 4 0 0 0 4 6 M376 58 h4 a4 4 0 0 1 -4 6" fill="none" stroke="#c98a00" stroke-width="1.5" />
          <path d="M367 60 l1.2 2.4 2.6 .4 -1.9 1.8 .5 2.6 -2.4 -1.3 -2.4 1.3 .5 -2.6 -1.9 -1.8 2.6 -.4 Z" fill="#fff6d0" />
        </g>
      }

      <!-- Торшер -->
      @if (has('lamp')) {
        <g>
          @if (night()) { <circle class="glow" cx="278" cy="128" r="60" fill="url(#room-lamp-light)" /> }
          <path d="M278 136 V226" stroke="#6b5a8e" stroke-width="3" />
          <ellipse cx="278" cy="228" rx="13" ry="4" fill="#6b5a8e" />
          <path d="M262 136 L268 110 H288 L294 136 Z" [attr.fill]="night() ? '#ffe39a' : '#f6d27a'" stroke="#c99a2e" stroke-width="1.5" stroke-linejoin="round" />
        </g>
      }

      <!-- Кресло для чтения -->
      @if (has('armchair')) {
        <g>
          <rect x="24" y="150" width="76" height="52" rx="16" fill="#e5737a" />
          <rect x="16" y="178" width="22" height="44" rx="10" fill="#d45d65" /><rect x="86" y="178" width="22" height="44" rx="10" fill="#d45d65" />
          <rect x="34" y="190" width="56" height="26" rx="8" fill="#f08a90" />
          <path d="M30 224 v10 M94 224 v10" stroke="#8a4a3a" stroke-width="4" stroke-linecap="round" />
          <rect x="50" y="182" width="22" height="9" rx="2" fill="#5b8def" transform="rotate(-8 61 186)" />
        </g>
      }

      <!-- Коврик -->
      @if (has('rug')) {
        <ellipse cx="206" cy="232" rx="112" ry="22" fill="#a99cff" opacity=".55" />
        <ellipse cx="206" cy="232" rx="92" ry="16" fill="none" stroke="#fff" stroke-width="2" stroke-dasharray="6 5" opacity=".7" />
      }

      <!-- Лежанка -->
      @if (has('bed')) {
        <ellipse cx="206" cy="230" rx="64" ry="15" fill="#7c6cf2" />
        <ellipse cx="206" cy="226" rx="52" ry="10" fill="#b8adff" />
      }

      <!-- Аквариум на табурете -->
      @if (has('fishbowl')) {
        <g>
          <rect x="300" y="200" width="40" height="8" rx="3" fill="#b98a5e" /><path d="M306 208 v26 M334 208 v26" stroke="#9c714a" stroke-width="4" stroke-linecap="round" />
          <path d="M304 198 a18 18 0 1 1 32 0 Z" fill="#bfe8ff" stroke="#8ccbee" stroke-width="2" opacity=".95" />
          <path d="M306 188 h28" stroke="#8ccbee" stroke-width="1.5" />
          <g class="fish"><path d="M314 186 q6 -5 11 0 q-5 5 -11 0 Z M325 186 l4 -3 v6 Z" fill="#ff8a3d" /></g>
          <path d="M312 196 q3 -6 0 -10" stroke="#59b26a" stroke-width="2" fill="none" />
        </g>
      }

      <!-- Цветок -->
      @if (has('plant')) {
        <g>
          <path d="M358 206 h30 l-4 28 h-22 Z" fill="#f08a5d" /><rect x="355" y="202" width="36" height="7" rx="3" fill="#e5734a" />
          <path class="leaf" d="M373 202 C 360 186, 356 170, 364 158 C 372 170, 376 186, 373 202 Z" fill="#4cae6e" />
          <path class="leaf" d="M374 202 C 384 184, 396 176, 398 162 C 386 166, 376 182, 374 202 Z" fill="#3e9b5e" />
          <path class="leaf" d="M372 202 C 362 194, 348 192, 342 182 C 356 180, 366 190, 372 202 Z" fill="#5bc27f" />
        </g>
      }

      @if (night()) { <rect class="night-veil" width="400" height="260" /> }
    </svg>
  `,
})
export class PetRoom {
  readonly decor = input<string[]>([]);
  readonly night = input(false);
  readonly label = input('');

  protected readonly planks = [40, 100, 160, 220, 280, 340, 400];
  protected readonly bulbs = [
    { x: 158, y: 17, c: '#ff6b6b' }, { x: 182, y: 23, c: '#ffd34d' }, { x: 208, y: 24, c: '#5bc27f' }, { x: 234, y: 18, c: '#5b8def' },
    { x: 282, y: 17, c: '#ff6b6b' }, { x: 308, y: 23, c: '#ffd34d' }, { x: 336, y: 23, c: '#5bc27f' }, { x: 364, y: 17, c: '#b86bf2' },
  ];
  private readonly set = computed(() => new Set(this.decor()));

  protected has(key: string): boolean {
    return this.set().has(key);
  }
}

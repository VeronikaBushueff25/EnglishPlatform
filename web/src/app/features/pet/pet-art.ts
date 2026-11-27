/**
 * Рисунки питомцев: SVG по слоям (хвост, тело, лапы, голова, уши, глаза, рот), окрас — CSS-переменными.
 * Движения и настроение — классами состояния на обёртке (pet-view.css), поэтому рисунок не перестраивается.
 * Все строки здесь — наши константы, без пользовательских данных: их можно вставлять как HTML.
 */

export type PetSpecies = 'cat' | 'corgi' | 'hamster';
export type PetMood = 'idle' | 'happy' | 'eating' | 'hungry' | 'sad' | 'sleeping' | 'playing' | 'walking' | 'away';
export type PetStage = 'baby' | 'teen' | 'adult';
export type PetAccessory = 'bow' | 'scarf' | 'glasses' | 'hat' | 'crown';

export interface PetPalette {
  label: string;
  fur: string;
  shade: string;
  belly: string;
  stripe: string;
  inner: string;
  nose: string;
  eye: string;
  pupil: string;
  line: string;
}

export const PALETTES: Record<PetSpecies, Record<string, PetPalette>> = {
  cat: {
    ginger: { label: 'Рыжий', fur: '#F2A65A', shade: '#DC8636', belly: '#FFF1DF', stripe: '#D47A2C', inner: '#F6B3AA', nose: '#EE8A8A', eye: '#2E2433', pupil: '#2E2433', line: '#8A4F1F' },
    gray: { label: 'Серый', fur: '#A9B3C1', shade: '#8893A4', belly: '#F1F3F7', stripe: '#8A95A6', inner: '#F2B8B5', nose: '#E98C93', eye: '#2E2433', pupil: '#2E2433', line: '#5C6676' },
    black: { label: 'Чёрный', fur: '#3D3B4A', shade: '#2C2A37', belly: '#575566', stripe: 'transparent', inner: '#B9868B', nose: '#D98A98', eye: '#F0C64E', pupil: '#1C1B22', line: '#1C1B22' },
    white: { label: 'Белый', fur: '#FBF8F3', shade: '#E7E0D6', belly: '#FFFFFF', stripe: 'transparent', inner: '#F6BDB8', nose: '#F09AA0', eye: '#5BA8D9', pupil: '#24303B', line: '#B8AC9C' },
    tabby: { label: 'Полосатый', fur: '#B8916A', shade: '#9C764F', belly: '#F4E9DC', stripe: '#6E5137', inner: '#EDB2A6', nose: '#D98478', eye: '#3A5B2B', pupil: '#1F2A19', line: '#5B4129' },
  },
  corgi: {
    red: { label: 'Рыжий', fur: '#E8914A', shade: '#CC742F', belly: '#FFF8F0', stripe: '#E8914A', inner: '#F4B8A0', nose: '#2C2530', eye: '#2C2530', pupil: '#2C2530', line: '#8E4E1C' },
    fawn: { label: 'Песочный', fur: '#E9BA79', shade: '#D29C55', belly: '#FFF9F0', stripe: '#E9BA79', inner: '#F4C4AE', nose: '#2C2530', eye: '#2C2530', pupil: '#2C2530', line: '#94683A' },
    tricolor: { label: 'Триколор', fur: '#3B3541', shade: '#2A2530', belly: '#FFF8F0', stripe: '#D78F4B', inner: '#C99A8E', nose: '#1E1A22', eye: '#2C2530', pupil: '#2C2530', line: '#1E1A22' },
  },
  hamster: {
    golden: { label: 'Золотистый', fur: '#E8AB62', shade: '#D08F45', belly: '#FFF3E1', stripe: '#E8AB62', inner: '#F4B3AE', nose: '#EE8F98', eye: '#2B2230', pupil: '#2B2230', line: '#93622B' },
    white: { label: 'Белый', fur: '#FBF7F1', shade: '#E6DDD1', belly: '#FFFFFF', stripe: '#FBF7F1', inner: '#F5BABA', nose: '#F09AA3', eye: '#8E2F45', pupil: '#5E1B2C', line: '#B6A897' },
    gray: { label: 'Серый', fur: '#9FA5B1', shade: '#838A98', belly: '#EEF0F4', stripe: '#9FA5B1', inner: '#EDB4B6', nose: '#E58F9A', eye: '#2B2230', pupil: '#2B2230', line: '#5A606D' },
    chocolate: { label: 'Шоколадный', fur: '#8C5F42', shade: '#734B32', belly: '#F2E2D2', stripe: '#8C5F42', inner: '#D9A39C', nose: '#D98390', eye: '#1F181C', pupil: '#1F181C', line: '#4A2F1F' },
  },
};

export const SPECIES_LABELS: Record<PetSpecies, string> = { cat: 'Котик', corgi: 'Корги', hamster: 'Хомяк' };

/** Аксессуары и уровень, с которого открываются (те же числа, что на сервере). */
export const ACCESSORIES: Record<PetAccessory, { label: string; level: number }> = {
  bow: { label: 'Бантик', level: 2 },
  scarf: { label: 'Шарф', level: 3 },
  glasses: { label: 'Очки', level: 4 },
  hat: { label: 'Колпачок', level: 5 },
  crown: { label: 'Корона', level: 7 },
};

/** Имена-подсказки при знакомстве. */
export const NAME_IDEAS: Record<PetSpecies, string[]> = {
  cat: ['Пушок', 'Барсик', 'Мурка', 'Персик', 'Симба', 'Луна', 'Тыковка', 'Кекс'],
  corgi: ['Бублик', 'Пончик', 'Бисквит', 'Рыжик', 'Тоша', 'Сырник', 'Чарли', 'Булочка'],
  hamster: ['Хома', 'Пирожок', 'Орешек', 'Зефирка', 'Пухляш', 'Крошка', 'Семечка', 'Хрустик'],
};

/** Вид с сервера («Cat») → ключ рисунка («cat»). */
export function toSpecies(value: string | null | undefined): PetSpecies {
  const key = (value ?? '').toLowerCase();
  return key === 'corgi' || key === 'hamster' ? key : 'cat';
}

const O = 'stroke="var(--line)" stroke-width="2.5" stroke-linejoin="round" stroke-linecap="round"';

interface Anchors {
  top: [number, number];
  bow: [number, number];
  eyes: [number, number, number, number];
  neck: [number, number];
}

const ANCHORS: Record<PetSpecies, Anchors> = {
  cat: { top: [100, 48], bow: [128, 54], eyes: [78, 122, 92, 10], neck: [134, 34] },
  corgi: { top: [100, 46], bow: [124, 52], eyes: [76, 124, 90, 9], neck: [136, 36] },
  hamster: { top: [100, 68], bow: [126, 74], eyes: [80, 120, 98, 7.5], neck: [134, 40] },
};

/** Глаза: открытые (со зрачком и бликом), «счастливые» дуги, закрытые (сон). */
function eyes(lx: number, rx: number, y: number, r: number): string {
  const one = (x: number, side: string) => `
    <g class="eye eye-${side}">
      <g class="eye-open">
        <circle cx="${x}" cy="${y}" r="${r}" fill="var(--eye)"/>
        <g class="pupil"><circle cx="${x}" cy="${y}" r="${r * 0.55}" fill="var(--pupil)"/>
          <circle cx="${x - r * 0.35}" cy="${y - r * 0.4}" r="${r * 0.32}" fill="#fff"/>
          <circle cx="${x + r * 0.35}" cy="${y + r * 0.35}" r="${r * 0.14}" fill="#fff" opacity=".8"/></g>
      </g>
      <path class="eye-happy" d="M${x - r} ${y + 2} Q${x} ${y - r - 2} ${x + r} ${y + 2}" fill="none" stroke="var(--pupil)" stroke-width="3.2" stroke-linecap="round"/>
      <path class="eye-closed" d="M${x - r} ${y} Q${x} ${y + r * 0.8} ${x + r} ${y}" fill="none" stroke="var(--pupil)" stroke-width="3" stroke-linecap="round"/>
    </g>`;
  return one(lx, 'l') + one(rx, 'r');
}

/** Рот: обычный, радостный (открытый), грустный; язычок — облизнуться после еды и в радости у корги. */
function mouth(x: number, y: number, w = 9): string {
  return `
    <g class="mouth">
      <path class="mouth-idle" d="M${x} ${y} q${-w * 0.5} ${w * 0.6} ${-w} ${w * 0.15} M${x} ${y} q${w * 0.5} ${w * 0.6} ${w} ${w * 0.15}" fill="none" stroke="var(--line)" stroke-width="2.4" stroke-linecap="round"/>
      <path class="mouth-happy" d="M${x - w} ${y + 1} Q${x} ${y + w * 1.5} ${x + w} ${y + 1} Z" fill="#E96B7B" stroke="var(--line)" stroke-width="2.2" stroke-linejoin="round"/>
      <path class="mouth-sad" d="M${x - w * 0.8} ${y + w * 0.7} Q${x} ${y - 1} ${x + w * 0.8} ${y + w * 0.7}" fill="none" stroke="var(--line)" stroke-width="2.4" stroke-linecap="round"/>
      <path class="tongue" d="M${x - 4.5} ${y + 5} q4.5 ${w + 3} 9 0 Z" fill="#EE7B8C" stroke="var(--line)" stroke-width="1.6" stroke-linejoin="round"/>
    </g>`;
}

const brows = (lx: number, rx: number, y: number) => `
  <g class="brows">
    <path d="M${lx - 8} ${y + 3} L${lx + 7} ${y - 3}" stroke="var(--line)" stroke-width="2.6" stroke-linecap="round"/>
    <path d="M${rx + 8} ${y + 3} L${rx - 7} ${y - 3}" stroke="var(--line)" stroke-width="2.6" stroke-linecap="round"/>
  </g>`;

const cheeks = (lx: number, rx: number, y: number) =>
  `<g class="cheeks"><ellipse cx="${lx}" cy="${y}" rx="8" ry="5" fill="#F48FA0" opacity=".45"/><ellipse cx="${rx}" cy="${y}" rx="8" ry="5" fill="#F48FA0" opacity=".45"/></g>`;

// ---------- Аксессуары ----------

function headAccessories(species: PetSpecies): string {
  const a = ANCHORS[species];
  const [tx, ty] = a.top;
  const [bx, by] = a.bow;
  const [lx, rx, ey, r] = a.eyes;
  const lens = r + 4.5;
  return `
    <g class="acc acc-glasses" fill="rgba(190,225,255,.28)" stroke="#3A3045" stroke-width="2.6">
      <circle cx="${lx}" cy="${ey}" r="${lens}"/><circle cx="${rx}" cy="${ey}" r="${lens}"/>
      <path d="M${lx + lens} ${ey - 1} Q100 ${ey - 6} ${rx - lens} ${ey - 1}" fill="none"/>
    </g>
    <g class="acc acc-bow" transform="translate(${bx} ${by}) rotate(18)">
      <path d="M0 0 C -6 -12 -20 -10 -18 1 C -16 11 -6 9 0 0 Z M0 0 C 6 -12 20 -10 18 1 C 16 11 6 9 0 0 Z" fill="#F25C9A" stroke="#B8306A" stroke-width="2" stroke-linejoin="round"/>
      <circle r="4" fill="#FF8DBB" stroke="#B8306A" stroke-width="2"/>
    </g>
    <g class="acc acc-hat" transform="translate(${tx + 6} ${ty + 4}) rotate(14)">
      <path d="M-16 0 L0 -38 L16 0 Q0 6 -16 0 Z" fill="#7C6CF2" stroke="#4E3FC2" stroke-width="2.2" stroke-linejoin="round"/>
      <path d="M-9 -14 l5 3 M2 -24 l5 3 M3 -8 l5 3" stroke="#FFD166" stroke-width="3" stroke-linecap="round"/>
      <circle cy="-39" r="5" fill="#FFD166" stroke="#C99A2E" stroke-width="2"/>
    </g>
    <g class="acc acc-crown" transform="translate(${tx} ${ty + 3})">
      <path d="M-20 0 L-21 -18 L-10 -8 L0 -23 L10 -8 L21 -18 L20 0 Q0 5 -20 0 Z" fill="#FFC83D" stroke="#C98A00" stroke-width="2.2" stroke-linejoin="round"/>
      <circle cx="0" cy="-7" r="3.2" fill="#F25C7A"/><circle cx="-11" cy="-4" r="2.4" fill="#5BA8D9"/><circle cx="11" cy="-4" r="2.4" fill="#5BA8D9"/>
    </g>`;
}

function scarf(species: PetSpecies): string {
  const [y, w] = ANCHORS[species].neck;
  return `
    <g class="acc acc-scarf">
      <path d="M${112} ${y + 4} l4 26 l11 -3 l-5 -25 Z" fill="#E5484D" stroke="#9E2A2E" stroke-width="2" stroke-linejoin="round"/>
      <path d="M${100 - w} ${y - 2} Q100 ${y + 12} ${100 + w} ${y - 2} L${100 + w - 2} ${y + 9} Q100 ${y + 23} ${100 - w + 2} ${y + 9} Z" fill="#E5484D" stroke="#9E2A2E" stroke-width="2" stroke-linejoin="round"/>
      <path d="M${100 - w + 12} ${y + 6} l0 9 M${100 - 6} ${y + 9} l0 9 M${100 + w - 16} ${y + 6} l0 9" stroke="#FFD9D9" stroke-width="3" stroke-linecap="round"/>
    </g>`;
}

// ---------- Звери ----------

function cat(): string {
  return `
  <g class="tail" style="transform-origin:136px 170px">
    <path d="M132 172 C 168 176, 182 146, 170 120 C 166 111, 156 113, 160 124 C 168 144, 156 160, 130 160" fill="var(--fur)" ${O}/>
    <path d="M166 128 q8 4 9 -4" fill="none" stroke="var(--stripe)" stroke-width="4" stroke-linecap="round"/>
  </g>
  <g class="body">
    <path d="M62 178 C 56 140, 72 118, 100 118 C 128 118, 144 140, 138 178 Z" fill="var(--fur)" ${O}/>
    <path d="M80 176 C 78 150, 88 136, 100 136 C 112 136, 122 150, 120 176 Z" fill="var(--belly)"/>
    <path d="M66 150 q6 2 8 8 M134 150 q-6 2 -8 8" fill="none" stroke="var(--stripe)" stroke-width="3.5" stroke-linecap="round"/>
    ${scarf('cat')}
  </g>
  <g class="paw paw-l"><ellipse cx="84" cy="179" rx="13" ry="8.5" fill="var(--fur)" ${O}/><path d="M81 182 v-3 M87 182 v-3" stroke="var(--line)" stroke-width="1.8" stroke-linecap="round" opacity=".5"/></g>
  <g class="paw paw-r"><ellipse cx="116" cy="179" rx="13" ry="8.5" fill="var(--fur)" ${O}/><path d="M113 182 v-3 M119 182 v-3" stroke="var(--line)" stroke-width="1.8" stroke-linecap="round" opacity=".5"/></g>
  <g class="head">
    <g class="ear ear-l" style="transform-origin:70px 66px">
      <path d="M52 76 L56 30 Q58 24 64 28 L92 54 Z" fill="var(--fur)" ${O}/>
      <path d="M60 64 L62 40 L80 56 Z" fill="var(--inner)"/>
    </g>
    <g class="ear ear-r" style="transform-origin:130px 66px">
      <path d="M148 76 L144 30 Q142 24 136 28 L108 54 Z" fill="var(--fur)" ${O}/>
      <path d="M140 64 L138 40 L120 56 Z" fill="var(--inner)"/>
    </g>
    <path d="M100 46 C 136 46, 156 64, 156 94 C 156 122, 132 136, 100 136 C 68 136, 44 122, 44 94 C 44 64, 64 46, 100 46 Z" fill="var(--fur)" ${O}/>
    <path d="M90 50 q10 10 0 18 M100 48 v18 M110 50 q-10 10 0 18" fill="none" stroke="var(--stripe)" stroke-width="3.5" stroke-linecap="round"/>
    <path d="M48 96 q8 -2 12 4 M152 96 q-8 -2 -12 4" fill="none" stroke="var(--stripe)" stroke-width="3.5" stroke-linecap="round"/>
    <ellipse cx="100" cy="114" rx="22" ry="15" fill="var(--belly)" opacity=".9"/>
    ${brows(78, 122, 76)}
    ${eyes(78, 122, 92, 10)}
    ${cheeks(64, 136, 110)}
    <path d="M94 104 h12 q-2 6 -6 7 q-4 -1 -6 -7 Z" fill="var(--nose)" stroke="var(--line)" stroke-width="1.6" stroke-linejoin="round"/>
    ${mouth(100, 111, 8)}
    <g class="whiskers" stroke="var(--line)" stroke-width="1.6" stroke-linecap="round" opacity=".55">
      <path d="M70 110 L44 106 M70 115 L46 118"/><path d="M130 110 L156 106 M130 115 L154 118"/>
    </g>
    ${headAccessories('cat')}
  </g>`;
}

function corgi(): string {
  return `
  <g class="tail" style="transform-origin:138px 160px">
    <path d="M134 162 C 150 150, 166 152, 164 166 C 162 176, 146 176, 136 170 Z" fill="var(--fur)" ${O}/>
    <path d="M150 158 c 8 0 12 6 9 11" fill="none" stroke="var(--belly)" stroke-width="5" stroke-linecap="round"/>
  </g>
  <g class="body">
    <path d="M58 178 C 52 142, 70 120, 100 120 C 130 120, 148 142, 142 178 Z" fill="var(--fur)" ${O}/>
    <path d="M76 178 C 74 150, 86 132, 100 132 C 114 132, 126 150, 124 178 Z" fill="var(--belly)"/>
    ${scarf('corgi')}
  </g>
  <g class="paw paw-l"><ellipse cx="84" cy="180" rx="13" ry="8" fill="var(--belly)" ${O}/></g>
  <g class="paw paw-r"><ellipse cx="116" cy="180" rx="13" ry="8" fill="var(--belly)" ${O}/></g>
  <g class="head">
    <g class="ear ear-l" style="transform-origin:66px 62px">
      <path d="M46 74 C 40 46, 44 22, 54 16 C 62 14, 82 40, 88 54 Z" fill="var(--fur)" ${O}/>
      <path d="M56 60 C 52 44, 54 30, 58 26 C 64 30, 74 44, 78 52 Z" fill="var(--inner)"/>
    </g>
    <g class="ear ear-r" style="transform-origin:134px 62px">
      <path d="M154 74 C 160 46, 156 22, 146 16 C 138 14, 118 40, 112 54 Z" fill="var(--fur)" ${O}/>
      <path d="M144 60 C 148 44, 146 30, 142 26 C 136 30, 126 44, 122 52 Z" fill="var(--inner)"/>
    </g>
    <path d="M100 44 C 140 44, 160 66, 158 96 C 156 124, 132 138, 100 138 C 68 138, 44 124, 42 96 C 40 66, 60 44, 100 44 Z" fill="var(--fur)" ${O}/>
    <path d="M60 96 C 58 82, 66 74, 78 78 C 72 86, 72 96, 66 104 Z M140 96 C 142 82, 134 74, 122 78 C 128 86, 128 96, 134 104 Z" fill="var(--stripe)" opacity=".95"/>
    <path d="M100 52 C 106 52, 108 64, 108 80 C 120 92, 132 102, 130 118 C 128 132, 114 138, 100 138 C 86 138, 72 132, 70 118 C 68 102, 80 92, 92 80 C 92 64, 94 52, 100 52 Z" fill="var(--belly)"/>
    ${brows(76, 124, 74)}
    ${eyes(76, 124, 90, 9)}
    ${cheeks(62, 138, 108)}
    <ellipse cx="100" cy="104" rx="9" ry="6.5" fill="var(--nose)"/>
    <ellipse cx="97" cy="102" rx="3" ry="1.8" fill="#fff" opacity=".6"/>
    ${mouth(100, 111, 9)}
    ${headAccessories('corgi')}
  </g>`;
}

function hamster(): string {
  return `
  <g class="body">
    <path d="M100 66 C 146 66, 168 100, 166 136 C 164 168, 136 184, 100 184 C 64 184, 36 168, 34 136 C 32 100, 54 66, 100 66 Z" fill="var(--fur)" ${O}/>
    <path d="M100 116 C 128 116, 144 136, 142 156 C 140 176, 122 184, 100 184 C 78 184, 60 176, 58 156 C 56 136, 72 116, 100 116 Z" fill="var(--belly)"/>
    ${scarf('hamster')}
  </g>
  <g class="paw paw-l"><ellipse cx="78" cy="183" rx="11" ry="6" fill="var(--inner)" ${O}/></g>
  <g class="paw paw-r"><ellipse cx="122" cy="183" rx="11" ry="6" fill="var(--inner)" ${O}/></g>
  <g class="head">
    <g class="ear ear-l" style="transform-origin:62px 76px">
      <circle cx="58" cy="72" r="15" fill="var(--fur)" ${O}/><circle cx="58" cy="72" r="8.5" fill="var(--inner)"/>
    </g>
    <g class="ear ear-r" style="transform-origin:138px 76px">
      <circle cx="142" cy="72" r="15" fill="var(--fur)" ${O}/><circle cx="142" cy="72" r="8.5" fill="var(--inner)"/>
    </g>
    <path d="M100 66 C 146 66, 168 100, 166 136 C 140 132, 60 132, 34 136 C 32 100, 54 66, 100 66 Z" fill="var(--fur)"/>
    <ellipse class="pouch pouch-l" cx="66" cy="118" rx="18" ry="15" fill="var(--belly)"/>
    <ellipse class="pouch pouch-r" cx="134" cy="118" rx="18" ry="15" fill="var(--belly)"/>
    <path d="M100 76 c 4 6 4 14 0 20 c -4 -6 -4 -14 0 -20 Z" fill="var(--shade)" opacity=".5"/>
    ${brows(80, 120, 84)}
    ${eyes(80, 120, 98, 7.5)}
    ${cheeks(64, 136, 120)}
    <path d="M96 110 h8 q-1 4 -4 5 q-3 -1 -4 -5 Z" fill="var(--nose)" stroke="var(--line)" stroke-width="1.4" stroke-linejoin="round"/>
    ${mouth(100, 115, 6)}
    <path class="teeth" d="M97 117 h6 v5 h-6 Z" fill="#fff" stroke="var(--line)" stroke-width="1.2"/>
    <g class="whiskers" stroke="var(--line)" stroke-width="1.4" stroke-linecap="round" opacity=".5">
      <path d="M84 114 L62 110 M84 118 L64 122"/><path d="M116 114 L138 110 M116 118 L136 122"/>
    </g>
    ${headAccessories('hamster')}
  </g>
  <g class="hands">
    <g class="seed"><ellipse cx="100" cy="140" rx="9" ry="12" fill="#F6E7BC" stroke="#8A6A2B" stroke-width="2"/><path d="M100 131 v18" stroke="#8A6A2B" stroke-width="1.6" opacity=".55"/></g>
    <ellipse class="hand hand-l" cx="88" cy="150" rx="8" ry="6" fill="var(--inner)" ${O}/>
    <ellipse class="hand hand-r" cx="112" cy="150" rx="8" ry="6" fill="var(--inner)" ${O}/>
  </g>`;
}

const DRAW: Record<PetSpecies, () => string> = { cat, corgi, hamster };

/** Сцена: тень, питомец, миска перед ним (с кормом и крошками), мячик, эффекты. */
export function petSvg(species: PetSpecies, palette: string): string {
  const p = PALETTES[species][palette] ?? Object.values(PALETTES[species])[0];
  const vars = Object.entries(p)
    .filter(([k]) => k !== 'label')
    .map(([k, v]) => `--${k}:${v}`)
    .join(';');
  return `
  <svg class="pet pet-${species}" viewBox="0 0 220 214" style="${vars}" aria-hidden="true">
    <ellipse class="shadow" cx="100" cy="191" rx="58" ry="7" fill="currentColor" opacity=".08"/>
    <g class="grow"><g class="figure"><g class="breath">${DRAW[species]()}</g></g></g>
    <g class="bowl">
      <g class="kibble">
        <circle class="k k1" cx="86" cy="186" r="5" fill="#E8A45C" stroke="#A8692A" stroke-width="1.4"/>
        <circle class="k k2" cx="97" cy="183" r="5" fill="#D98B3F" stroke="#A8692A" stroke-width="1.4"/>
        <circle class="k k3" cx="108" cy="186" r="5" fill="#E8A45C" stroke="#A8692A" stroke-width="1.4"/>
        <circle class="k k4" cx="116" cy="183" r="4.5" fill="#F0B776" stroke="#A8692A" stroke-width="1.4"/>
      </g>
      <path d="M68 188 h64 l-7 16 h-50 Z" fill="#7C6CF2" stroke="#4E3FC2" stroke-width="2.5" stroke-linejoin="round"/>
      <path d="M74 194 h52" stroke="#A99CFF" stroke-width="3" stroke-linecap="round" opacity=".7"/>
      <g class="crumbs" fill="#D98B3F"><circle class="c1" cx="88" cy="182" r="2.2"/><circle class="c2" cx="112" cy="182" r="2"/><circle class="c3" cx="100" cy="180" r="1.8"/></g>
    </g>
    <g class="fx-ball"><circle cx="0" cy="0" r="11" fill="#FF6B6B" stroke="#B83A3A" stroke-width="2.4"/><path d="M-10 -3 Q0 4 10 -3" fill="none" stroke="#FFE3E3" stroke-width="2.6"/></g>
    <g class="fx-hearts" fill="#F25C7A">
      <path class="h1" d="M58 50 c-6 -8 -16 -2 -10 6 l10 9 l10 -9 c6 -8 -4 -14 -10 -6 Z"/>
      <path class="h2" d="M150 40 c-5 -6 -13 -2 -8 5 l8 7 l8 -7 c5 -7 -3 -11 -8 -5 Z"/>
      <path class="h3" d="M104 26 c-4 -5 -10 -1 -6 4 l6 6 l6 -6 c4 -5 -2 -9 -6 -4 Z"/>
    </g>
    <g class="fx-zzz" fill="var(--zfill, #7C6CF2)" font-family="system-ui, sans-serif" font-weight="700">
      <text class="z1" x="146" y="60" font-size="16">z</text><text class="z2" x="158" y="44" font-size="20">z</text><text class="z3" x="172" y="26" font-size="24">Z</text>
    </g>
    <text class="fx-nom" x="150" y="150" font-family="system-ui, sans-serif" font-weight="800" font-size="15" fill="#F08A24">nom!</text>
    <g class="fx-hungry"><circle cx="160" cy="58" r="15" fill="#fff" stroke="#D9D4EA" stroke-width="2"/><circle cx="146" cy="76" r="4" fill="#fff" stroke="#D9D4EA" stroke-width="2"/>
      <path d="M154 58 a6 6 0 1 0 12 0 M160 52 v-4" fill="none" stroke="#E8A45C" stroke-width="3" stroke-linecap="round"/></g>
    <path class="fx-tear" d="M${species === 'hamster' ? 122 : 126} ${species === 'hamster' ? 106 : 102} q4 7 0 10 q-4 -3 0 -10 Z" fill="#6EC3F0"/>
  </svg>`;
}

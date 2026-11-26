import { PracticeWordDto } from '../../core/api/models/practice-word-dto';

/** Перемешивание Фишера — Йетса; <paramref name="random"/> подменяется в тестах. */
export function shuffle<T>(items: readonly T[], random: () => number = Math.random): T[] {
  const result = [...items];
  for (let i = result.length - 1; i > 0; i--) {
    const j = Math.floor(random() * (i + 1));
    [result[i], result[j]] = [result[j], result[i]];
  }
  return result;
}

/** Главный перевод — первый и без пояснений в скобках: «брать взаймы (у кого-то)» → «брать взаймы». */
export function mainTranslation(word: PracticeWordDto): string {
  return (word.translations[0] ?? '').replace(/\s*\(.*?\)\s*/g, ' ').trim();
}

export interface BlitzRound {
  word: PracticeWordDto;
  shown: string;
  correct: boolean;
}

/** Блиц: примерно половина пар верные, остальные — перевод другого слова (не совпадающий с правильным). */
export function blitzRounds(words: readonly PracticeWordDto[], random: () => number = Math.random): BlitzRound[] {
  return words.map((word, i) => {
    const right = mainTranslation(word);
    const others = words.filter((w, j) => j !== i && mainTranslation(w) !== right);
    if (others.length === 0 || random() < 0.5) {
      return { word, shown: right, correct: true };
    }
    const other = others[Math.floor(random() * others.length)];
    return { word, shown: mainTranslation(other), correct: false };
  });
}

/** Для «Собери слово» подходят одиночные слова из 3–12 букв. */
export function scrambleCandidates(words: readonly PracticeWordDto[]): PracticeWordDto[] {
  return words.filter((w) => /^[a-z]{3,12}$/i.test(w.text.trim()));
}

/** Буквы вперемешку, но не в исходном порядке (иначе нечего собирать). */
export function scrambled(text: string, random: () => number = Math.random): string[] {
  const letters = [...text.toLowerCase()];
  for (let attempt = 0; attempt < 10; attempt++) {
    const result = shuffle(letters, random);
    if (result.join('') !== letters.join('')) {
      return result;
    }
  }
  return [...letters].reverse();
}

/** Лучший результат игры в этом браузере. <c>lowerIsBetter</c> — для времени. */
export function bestScore(key: string, score: number, lowerIsBetter = false): { best: number; record: boolean } {
  let best: number | null = null;
  try {
    const saved = localStorage.getItem(key);
    best = saved === null ? null : Number(saved);
  } catch {
    best = null;
  }
  const record = best === null || (lowerIsBetter ? score < best : score > best);
  if (record) {
    try {
      localStorage.setItem(key, String(score));
    } catch {
      // приватный режим — без рекордов
    }
  }
  return { best: record ? score : best!, record };
}

/** Кусок текста: слово (кликабельное) или всё остальное — пробелы, знаки препинания, числа. */
export interface Token {
  text: string;
  word: boolean;
  /** Номер слова в абзаце — чтобы расширять выделение на следующие слова. */
  index: number;
}

const WORD = /[A-Za-z]+(?:['’][A-Za-z]+)*/g;

/** Абзац → слова и промежутки между ними. don't, it's, Kate's — одно слово. */
export function tokenize(paragraph: string): Token[] {
  const tokens: Token[] = [];
  let last = 0;
  let index = 0;
  for (const match of paragraph.matchAll(WORD)) {
    const start = match.index ?? 0;
    if (start > last) {
      tokens.push({ text: paragraph.slice(last, start), word: false, index: -1 });
    }
    tokens.push({ text: match[0], word: true, index: index++ });
    last = start + match[0].length;
  }
  if (last < paragraph.length) {
    tokens.push({ text: paragraph.slice(last), word: false, index: -1 });
  }
  return tokens;
}

/** Абзацы: пустая строка — новый абзац; переносы внутри абзаца — пробел. */
export function paragraphsOf(body: string): string[] {
  return body
    .replace(/\r\n/g, '\n')
    .split(/\n\s*\n/)
    .map((p) => p.replace(/\s*\n\s*/g, ' ').trim())
    .filter((p) => p.length > 0);
}

/** Как слово хранится в словаре: нижний регистр, обычный апостроф. */
export function normalizeWord(word: string): string {
  return word.toLowerCase().replace(/’/g, "'");
}

/** Простые начальные формы — чтобы подчеркнуть «знакомое», даже если в тексте другая форма (cats → cat, stopped → stop). */
export function baseForms(word: string): string[] {
  const w = normalizeWord(word);
  const forms = [w];
  const add = (f: string) => {
    if (f.length >= 2 && !forms.includes(f)) forms.push(f);
  };
  if (w.endsWith('ies')) add(w.slice(0, -3) + 'y');
  if (w.endsWith('es')) add(w.slice(0, -2));
  if (w.endsWith('s') && !w.endsWith('ss')) add(w.slice(0, -1));
  for (const suffix of ['ed', 'ing']) {
    if (w.endsWith(suffix) && w.length > suffix.length + 2) {
      const stem = w.slice(0, -suffix.length);
      if (suffix === 'ed' && stem.endsWith('i')) add(stem.slice(0, -1) + 'y');
      if (stem.length >= 3 && stem.at(-1) === stem.at(-2) && !'aeiou'.includes(stem.at(-1)!)) add(stem.slice(0, -1));
      add(stem);
      add(stem + 'e');
    }
  }
  return forms;
}

/** Выражение из слов абзаца с <paramref name="from"/> по <paramref name="to"/> включительно. */
export function phrase(tokens: Token[], from: number, to: number): string {
  return tokens
    .filter((t) => t.word && t.index >= from && t.index <= to)
    .map((t) => t.text)
    .join(' ');
}

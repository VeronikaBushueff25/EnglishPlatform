/**
 * Перевод интерфейса без переписывания шаблонов: исходный русский текст остаётся в коде и служит ключом,
 * а при английском языке переводчик подменяет текстовые узлы и атрибуты на странице по словарю (en.ts).
 *
 * - точное совпадение строки (пробелы схлопываются);
 * - шаблоны с местами для значений: «Ищем «{}» в словаре…», {n} — только число;
 * - «5 слов», «через 3 дня» — числа с русскими формами слова переводятся с английским множественным числом;
 * - всё, что внутри [translate="no"] (слова и переводы, которые пользователь учит), не трогается.
 *
 * Словарь грузится отдельным чанком только при выборе английского.
 */

export interface Dictionary {
  /** Русская строка → английская. */
  exact: Record<string, string>;
  /** Русский шаблон → английский. {} — любой текст, {n} — число; в переводе {} по порядку или {0}, {1}… */
  patterns: [string, string][];
  /** Формы русского слова после числа → [одно, много] по-английски. */
  plurals: Record<string, [string, string]>;
}

const CYRILLIC = /[А-Яа-яЁё]/;
const SKIP_TAGS = new Set(['SCRIPT', 'STYLE', 'TEXTAREA', 'CODE', 'PRE']);
const ATTRIBUTES = ['placeholder', 'aria-label', 'title', 'alt', 'aria-description'];

interface CompiledPattern {
  regex: RegExp;
  target: string;
}

let exact = new Map<string, string>();
let patterns: CompiledPattern[] = [];
let pluralRegex: RegExp | null = null;
let plurals: Record<string, [string, string]> = {};
let active = false;
const cache = new Map<string, string | null>();

function escapeRegex(s: string): string {
  return s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function normalize(s: string): string {
  return s.replace(/[\s ]+/g, ' ').trim();
}

export function loadDictionary(dictionary: Dictionary): void {
  exact = new Map(Object.entries(dictionary.exact).map(([ru, en]) => [normalize(ru), en]));
  patterns = dictionary.patterns.map(([ru, en]) => {
    const source = normalize(ru)
      .split(/(\{n\}|\{\})/)
      .map((part) => (part === '{n}' ? '(\\d+(?:[.,]\\d+)?)' : part === '{}' ? '(.+?)' : escapeRegex(part)))
      .join('');
    return { regex: new RegExp(`^${source}$`), target: en };
  });
  plurals = dictionary.plurals;
  const forms = Object.keys(plurals).sort((a, b) => b.length - a.length).map(escapeRegex);
  pluralRegex = forms.length > 0 ? new RegExp(`(\\d+)\\s(${forms.join('|')})(?=$|[\\s.,:;!?)»])`, 'g') : null;
  cache.clear();
}

/** Числа с русскими формами («5 слов», «1 карточка») → английские. */
function pluralPass(text: string): string {
  if (!pluralRegex) return text;
  return text.replace(pluralRegex, (_, n: string, form: string) => {
    const [one, many] = plurals[form];
    return `${n} ${n === '1' ? one : many}`;
  });
}

/** Кусок, подставленный в шаблон: переводим, если знаем, иначе оставляем как есть. */
function fragment(text: string): string {
  return exact.get(text) ?? translateCore(text) ?? text;
}

function translateCore(text: string): string | null {
  const hit = exact.get(text);
  if (hit !== undefined) return hit;

  const counted = pluralPass(text);
  if (counted !== text) {
    if (!CYRILLIC.test(counted)) return counted;
    const countedHit = exact.get(counted);
    if (countedHit !== undefined) return countedHit;
  }

  // Шаблон может содержать русскую форму («за последние {n} дней») — пробуем и исходный текст, и с переведёнными числами.
  return (counted !== text ? applyPatterns(counted) : null) ?? applyPatterns(text);
}

function applyPatterns(text: string): string | null {
  for (const { regex, target } of patterns) {
    const match = regex.exec(text);
    if (!match) continue;
    const groups = match.slice(1).map(fragment);
    let i = 0;
    return target.replace(/\{(\d*)\}/g, (_, index: string) => groups[index === '' ? i++ : Number(index)] ?? '');
  }
  return null;
}

/** Перевод строки на текущий язык; если перевода нет или язык русский — исходная строка. */
export function tr(text: string): string {
  if (!active || !CYRILLIC.test(text)) return text;
  return translateText(text) ?? text;
}

function translateText(raw: string): string | null {
  const text = normalize(raw);
  if (!CYRILLIC.test(text)) return null;
  let result = cache.get(text);
  if (result === undefined) {
    result = translateCore(text);
    cache.set(text, result);
  }
  return result;
}

// ---------- Работа со страницей ----------

const textOriginals = new WeakMap<Text, string>();
const textWritten = new WeakMap<Text, string>();
const attrOriginals = new WeakMap<Element, Map<string, string>>();
const attrWritten = new WeakMap<Element, Map<string, string>>();
const touchedTexts = new Set<WeakRef<Text>>();
const touchedElements = new Set<WeakRef<Element>>();
let observer: MutationObserver | null = null;

function skipped(element: Element | null): boolean {
  for (let el = element; el; el = el.parentElement) {
    if (SKIP_TAGS.has(el.tagName) || el.getAttribute('translate') === 'no' || (el as HTMLElement).isContentEditable) return true;
  }
  return false;
}

function processText(node: Text): void {
  const data = node.data;
  if (textWritten.get(node) === data || !CYRILLIC.test(data) || skipped(node.parentElement)) return;
  const translated = translateText(data);
  if (translated === null) return;
  const out = (data.match(/^[\s ]*/)?.[0] ?? '') + translated + (data.match(/[\s ]*$/)?.[0] ?? '');
  textOriginals.set(node, data);
  textWritten.set(node, out);
  touchedTexts.add(new WeakRef(node));
  node.data = out;
}

function processAttribute(element: Element, name: string): void {
  const value = element.getAttribute(name);
  // Атрибуты поля ввода (placeholder, aria-label) переводятся, даже если его содержимое — нет.
  if (value === null || attrWritten.get(element)?.get(name) === value || !CYRILLIC.test(value)) return;
  if (element.getAttribute('translate') === 'no' || skipped(element.parentElement)) return;
  const translated = translateText(value);
  if (translated === null) return;
  if (!attrOriginals.has(element)) {
    attrOriginals.set(element, new Map());
    attrWritten.set(element, new Map());
    touchedElements.add(new WeakRef(element));
  }
  attrOriginals.get(element)!.set(name, value);
  attrWritten.get(element)!.set(name, translated);
  element.setAttribute(name, translated);
}

function processTree(root: Node): void {
  if (root.nodeType === Node.TEXT_NODE) {
    processText(root as Text);
    return;
  }
  if (root.nodeType !== Node.ELEMENT_NODE && root.nodeType !== Node.DOCUMENT_NODE) return;
  const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT | NodeFilter.SHOW_ELEMENT);
  for (let node: Node | null = root; node; node = walker.nextNode()) {
    if (node.nodeType === Node.TEXT_NODE) {
      processText(node as Text);
    } else if (node.nodeType === Node.ELEMENT_NODE) {
      for (const name of ATTRIBUTES) {
        if ((node as Element).hasAttribute(name)) processAttribute(node as Element, name);
      }
    }
  }
}

function onMutations(records: MutationRecord[]): void {
  for (const record of records) {
    if (record.type === 'characterData') {
      processText(record.target as Text);
    } else if (record.type === 'attributes') {
      processAttribute(record.target as Element, record.attributeName!);
    } else {
      record.addedNodes.forEach(processTree);
    }
  }
}

/** Включает перевод страницы: переводит то, что уже есть, и всё, что появится. */
export function startTranslating(dictionary: Dictionary): void {
  loadDictionary(dictionary);
  active = true;
  processTree(document.documentElement);
  observer ??= new MutationObserver(onMutations);
  observer.observe(document.documentElement, {
    subtree: true,
    childList: true,
    characterData: true,
    attributes: true,
    attributeFilter: ATTRIBUTES,
  });
}

/** Выключает перевод и возвращает на странице исходные русские надписи. */
export function stopTranslating(): void {
  active = false;
  observer?.disconnect();
  for (const ref of touchedTexts) {
    const node = ref.deref();
    if (node && textWritten.get(node) === node.data) node.data = textOriginals.get(node)!;
  }
  for (const ref of touchedElements) {
    const element = ref.deref();
    if (!element) continue;
    for (const [name, value] of attrWritten.get(element) ?? []) {
      if (element.getAttribute(name) === value) element.setAttribute(name, attrOriginals.get(element)!.get(name)!);
    }
    attrWritten.delete(element);
    attrOriginals.delete(element);
  }
  touchedTexts.clear();
  touchedElements.clear();
}

/** Для тестов: перевод строки по загруженному словарю без включения перевода страницы. */
export function translateForTest(text: string): string | null {
  return translateText(text);
}

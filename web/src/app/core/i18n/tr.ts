/**
 * Перевод строки на текущий язык интерфейса. Сам переводчик и словарь грузятся лениво (только для английского),
 * поэтому здесь лишь ссылка на функцию: пока она не задана, строка возвращается как есть.
 */
let translate: ((text: string) => string) | null = null;

export function setTranslate(fn: ((text: string) => string) | null): void {
  translate = fn;
}

export function tr(text: string): string {
  return translate ? translate(text) : text;
}

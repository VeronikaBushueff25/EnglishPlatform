/** Абзацы текста: пустая строка — новый абзац, перенос внутри абзаца — пробел. */
export function paragraphs(body: string | null | undefined): string[] {
  return (body ?? '')
    .split(/\n\s*\n/)
    .map((p) => p.replace(/\s*\n\s*/g, ' ').trim())
    .filter((p) => p.length > 0);
}

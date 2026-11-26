import { AddMyWordRequest } from '../../core/api/models/add-my-word-request';
import { CefrLevel } from '../../core/api/models/cefr-level';
import { LexemeType } from '../../core/api/models/lexeme-type';
import { PartOfSpeech } from '../../core/api/models/part-of-speech';

export const LEXEME_TYPE_LABELS: Record<LexemeType, string> = {
  Word: 'Слово',
  Phrase: 'Фраза',
  Idiom: 'Идиома',
  PhrasalVerb: 'Фразовый глагол',
};

/** Части речи без null: сгенерированный тип допускает null, потому что поле у слова необязательное. */
export type PartOfSpeechValue = NonNullable<PartOfSpeech>;
export type CefrLevelValue = NonNullable<CefrLevel>;

export const PART_OF_SPEECH_LABELS: Record<PartOfSpeechValue, string> = {
  Noun: 'существительное',
  Verb: 'глагол',
  Adjective: 'прилагательное',
  Adverb: 'наречие',
  Pronoun: 'местоимение',
  Preposition: 'предлог',
  Conjunction: 'союз',
  Determiner: 'определитель',
  Interjection: 'междометие',
  Numeral: 'числительное',
};

export const LEXEME_TYPES = Object.keys(LEXEME_TYPE_LABELS) as LexemeType[];
export const PARTS_OF_SPEECH = Object.keys(PART_OF_SPEECH_LABELS) as PartOfSpeechValue[];
export const CEFR_LEVELS: CefrLevelValue[] = ['A1', 'A2', 'B1', 'B2', 'C1', 'C2'];

/** Транскрипция в привычных косых скобках. */
export function formatTranscription(transcription: string | null | undefined): string {
  return transcription ? `/${transcription}/` : '';
}

/** Тело запроса «добавить слово общей базы в свой словарь». */
export function fromBase(lexemeId: string, note: string | null = null): AddMyWordRequest {
  return {
    lexemeId,
    note,
    text: null,
    type: null,
    transcription: null,
    translations: null,
    example: null,
    exampleTranslation: null,
  };
}

/** «слово», «слова», «слов» по числу. */
export function plural(n: number): string {
  const mod10 = n % 10;
  const mod100 = n % 100;
  if (mod10 === 1 && mod100 !== 11) {
    return 'слово';
  }
  if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) {
    return 'слова';
  }
  return 'слов';
}

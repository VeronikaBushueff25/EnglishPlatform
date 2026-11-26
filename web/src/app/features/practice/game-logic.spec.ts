import { PracticeWordDto } from '../../core/api/models/practice-word-dto';
import { baseForms, paragraphsOf, phrase, tokenize } from '../reading/tokens';
import { blitzRounds, mainTranslation, scrambleCandidates, scrambled, shuffle } from './game-logic';

const word = (text: string, ...translations: string[]): PracticeWordDto => ({ id: text, text, translations, transcription: null, type: 'Word' });

/** Предсказуемый «случайный» генератор для тестов. */
function seq(...values: number[]): () => number {
  let i = 0;
  return () => values[i++ % values.length];
}

describe('games', () => {
  it('shuffle keeps all items', () => {
    expect(shuffle([1, 2, 3, 4, 5], seq(0.1, 0.9, 0.5)).sort()).toEqual([1, 2, 3, 4, 5]);
  });

  it('main translation drops notes in brackets', () => {
    expect(mainTranslation(word('borrow', 'брать взаймы (у кого-то)', 'занимать'))).toBe('брать взаймы');
  });

  it('blitz wrong pairs never show the right translation', () => {
    const words = [word('cat', 'кошка'), word('dog', 'собака'), word('kitty', 'кошка'), word('bird', 'птица')];
    const rounds = blitzRounds(words, seq(0.9, 0.1, 0.7, 0.3, 0.9, 0.6));
    for (const r of rounds) {
      expect(r.shown === mainTranslation(r.word)).toBe(r.correct);
    }
    expect(rounds.some((r) => !r.correct)).toBe(true);
  });

  it('scramble uses single words and never returns the word itself', () => {
    expect(scrambleCandidates([word('put off', 'x'), word('go', 'x'), word('harbour', 'x')]).map((w) => w.text)).toEqual(['harbour']);
    const letters = scrambled('stop', () => 0.99); // такой генератор оставил бы порядок как есть
    expect(letters.sort().join('')).toBe('opst');
    expect(scrambled('stop', () => 0.99).join('')).not.toBe('stop');
  });
});

describe('reading tokens', () => {
  it('splits words and keeps punctuation between them', () => {
    const tokens = tokenize("Don't put it off, Kate's friend!");
    expect(tokens.filter((t) => t.word).map((t) => t.text)).toEqual(["Don't", 'put', 'it', 'off', "Kate's", 'friend']);
    expect(tokens.map((t) => t.text).join('')).toBe("Don't put it off, Kate's friend!");
  });

  it('phrase joins words by their positions', () => {
    const tokens = tokenize('Jump to conclusions, again.');
    expect(phrase(tokens, 0, 2)).toBe('Jump to conclusions');
  });

  it('paragraphs are split by blank lines', () => {
    expect(paragraphsOf('One\nline.\n\nTwo.')).toEqual(['One line.', 'Two.']);
  });

  it('base forms cover common endings', () => {
    expect(baseForms('Stopped')).toContain('stop');
    expect(baseForms('studies')).toContain('study');
    expect(baseForms('making')).toContain('make');
    expect(baseForms('glass')).toEqual(['glass']);
  });
});

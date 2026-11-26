import { WordSuggestionDto } from '../../core/api/models/word-suggestion-dto';
import { changesOf, toApply } from './improve-words';

const suggestion: WordSuggestionDto = {
  userWordId: 'w1',
  text: 'buried',
  type: 'Phrase',
  level: 'B1',
  partOfSpeech: null,
  transcription: null,
  translations: ['похороненный'],
  suggestedText: 'bury',
  suggestedType: 'Word',
  suggestedLevel: 'A2',
  suggestedPartOfSpeech: 'Verb',
  suggestedTranscription: 'ˈberi',
  suggestedExample: null,
  suggestedTranslations: ['хоронить', 'зарывать'],
};

describe('improve words', () => {
  it('lists every suggested change once', () => {
    expect(changesOf(suggestion).map((c) => c.field)).toEqual(['text', 'type', 'level', 'partOfSpeech', 'transcription']);
    expect(changesOf(suggestion)[0].to).toBe('bury (хоронить, зарывать)');
  });

  it('applies only picked fields; translations travel with the base form', () => {
    const picked = toApply(suggestion, new Set(['text', 'level']));
    expect(picked.text).toBe('bury');
    expect(picked.translations).toEqual(['хоронить', 'зарывать']);
    expect(picked.level).toBe('A2');
    expect(picked.type).toBeNull();
    expect(picked.transcription).toBeNull();

    const levelOnly = toApply(suggestion, new Set(['level']));
    expect(levelOnly.text).toBeNull();
    expect(levelOnly.translations).toBeNull(); // свои переводы не трогаем
  });
});

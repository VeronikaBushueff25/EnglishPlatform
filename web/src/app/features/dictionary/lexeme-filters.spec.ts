import { ComponentFixture, TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { EMPTY_FILTER, LexemeFilters, filterQuery } from './lexeme-filters';
import { formatTranscription, fromBase, plural } from './vocabulary-labels';

describe('LexemeFilters', () => {
  let fixture: ComponentFixture<LexemeFilters>;

  beforeEach(() => {
    vi.useFakeTimers();
    fixture = TestBed.createComponent(LexemeFilters);
    fixture.detectChanges();
  });

  afterEach(() => vi.useRealTimers());

  function type(text: string): void {
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input[type=search]');
    input.value = text;
    input.dispatchEvent(new Event('input'));
  }

  it('passes the search on only after a pause, trimmed', () => {
    type('  appl');
    type('  apple ');
    expect(fixture.componentInstance.value().search).toBe('');

    vi.advanceTimersByTime(300);
    expect(fixture.componentInstance.value()).toEqual({ ...EMPTY_FILTER, search: 'apple' });
  });

  it('resets every filter at once', () => {
    fixture.componentInstance.value.set({ search: 'x', type: 'Idiom', level: 'B2', categoryId: '1' });
    fixture.detectChanges();

    const reset = [...fixture.nativeElement.querySelectorAll('button')].find((b: HTMLButtonElement) =>
      b.textContent?.includes('Сбросить'),
    ) as HTMLButtonElement;
    reset.click();

    expect(fixture.componentInstance.value()).toEqual(EMPTY_FILTER);
  });
});

describe('vocabulary helpers', () => {
  it('wraps transcription in slashes', () => {
    expect(formatTranscription('ˈæpl')).toBe('/ˈæpl/');
    expect(formatTranscription(null)).toBe('');
  });

  it('builds a request that adds a base word', () => {
    expect(fromBase('id-1')).toMatchObject({ lexemeId: 'id-1', text: null, translations: null, note: null });
  });
});

describe('filterQuery', () => {
  it('turns "no level" into the noLevel flag', () => {
    expect(filterQuery({ ...EMPTY_FILTER, level: 'none' })).toMatchObject({ level: undefined, noLevel: true });
    expect(filterQuery({ ...EMPTY_FILTER, level: 'B1' })).toMatchObject({ level: 'B1', noLevel: undefined });
  });
});

describe('plural', () => {
  it.each([
    [1, 'слово'],
    [3, 'слова'],
    [5, 'слов'],
    [11, 'слов'],
    [21, 'слово'],
    [253, 'слова'],
  ])('%i → %s', (n, word) => expect(plural(n)).toBe(word));
});

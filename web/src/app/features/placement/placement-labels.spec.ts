import { paragraphs } from '../../shared/text';
import { levelPercent } from './placement-labels';

describe('placement helpers', () => {
  it('maps CEFR levels to bar width', () => {
    expect(levelPercent('A1')).toBeCloseTo(16.67, 1);
    expect(levelPercent('B1')).toBe(50);
    expect(levelPercent('C2')).toBe(100);
    expect(levelPercent(null)).toBe(0);
  });

  it('splits text into paragraphs and joins wrapped lines', () => {
    expect(paragraphs('First line\ncontinues.\n\n  Second.  \n\n\n')).toEqual(['First line continues.', 'Second.']);
    expect(paragraphs(null)).toEqual([]);
  });
});

import { EN } from './en';
import { loadDictionary, startTranslating, stopTranslating, tr, translateForTest } from './translator';

const flush = () => new Promise((resolve) => setTimeout(resolve));

describe('translator', () => {
  beforeEach(() => loadDictionary(EN));
  afterEach(() => {
    stopTranslating();
    document.body.innerHTML = '';
  });

  it('translates exact strings ignoring extra whitespace', () => {
    expect(translateForTest('  Начать\n   занятие ')).toBe('Start lesson');
    expect(translateForTest('Нет аккаунта? ')).toBe('No account?');
  });

  it('fills patterns and translates known fragments', () => {
    expect(translateForTest('Ищем «pond» в словаре…')).toBe('Looking up “pond” in the dictionary…');
    expect(translateForTest('26.11, чт: ответов 5, верно 3, новых слов 1')).toBe('26.11, Thu: 5 answers, 3 correct, 1 new words');
  });

  it('uses English plurals after numbers', () => {
    expect(translateForTest('через 1 день')).toBe('in 1 day');
    expect(translateForTest('через 3 дня')).toBe('in 3 days');
    expect(translateForTest('Уровень B1: 21 слово')).toBe('Level B1: 21 words');
    expect(translateForTest('За последние 30 дней, сегодня — 4.')).toBe('Over the last 30 days, today — 4.');
  });

  it('leaves unknown text alone', () => {
    expect(translateForTest('счастливая случайность')).toBeNull();
    expect(translateForTest('serendipity')).toBeNull();
  });

  it('tr() translates only while English is on', () => {
    expect(tr('Неверный email или пароль')).toBe('Неверный email или пароль');
    startTranslating(EN);
    expect(tr('Неверный email или пароль')).toBe('Wrong email or password');
  });

  it('translates the page, skips translate="no", and restores Russian when switched off', async () => {
    document.body.innerHTML = `
      <button title="Закрыть"> Сохранить </button>
      <p translate="no">союз</p>
      <span id="dynamic"></span>`;
    startTranslating(EN);
    const button = document.querySelector('button')!;
    expect(button.textContent).toBe(' Save ');
    expect(button.title).toBe('Close');
    expect(document.querySelector('p')!.textContent).toBe('союз');

    document.getElementById('dynamic')!.textContent = 'Профиль сохранён';
    await flush();
    expect(document.getElementById('dynamic')!.textContent).toBe('Profile saved');

    stopTranslating();
    expect(button.textContent).toBe(' Сохранить ');
    expect(button.title).toBe('Закрыть');
    expect(document.getElementById('dynamic')!.textContent).toBe('Профиль сохранён');
  });
});

import { HttpErrorResponse } from '@angular/common/http';
import { problemMessage } from './problem';

describe('problemMessage', () => {
  it('uses the server detail', () => {
    const error = new HttpErrorResponse({ status: 401, error: { detail: 'Неверный email или пароль' } });
    expect(problemMessage(error)).toBe('Неверный email или пароль');
  });

  it('joins validation errors after the detail', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { detail: 'Пароль не подходит', errors: { newPassword: ['Нужна цифра.'] } },
    });
    expect(problemMessage(error)).toBe('Пароль не подходит Нужна цифра.');
  });

  it('parses a text body', () => {
    const error = new HttpErrorResponse({ status: 409, error: JSON.stringify({ detail: 'Занято' }) });
    expect(problemMessage(error)).toBe('Занято');
  });

  it('explains network and rate-limit failures', () => {
    expect(problemMessage(new HttpErrorResponse({ status: 0 }))).toContain('Нет связи');
    expect(problemMessage(new HttpErrorResponse({ status: 429 }))).toContain('Слишком много попыток');
  });

  it('falls back for unknown errors', () => {
    expect(problemMessage(new Error('boom'), 'запасной')).toBe('запасной');
  });
});

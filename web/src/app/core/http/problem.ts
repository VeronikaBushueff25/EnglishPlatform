import { HttpErrorResponse } from '@angular/common/http';

interface ProblemBody {
  title?: string | null;
  detail?: string | null;
  errors?: Record<string, string[]>;
}

/**
 * Текст ошибки для пользователя из ответа API (ProblemDetails / ValidationProblemDetails).
 * Сообщения сервера уже на русском, поэтому показываем их как есть.
 */
export function problemMessage(error: unknown, fallback = 'Что-то пошло не так. Попробуйте ещё раз.'): string {
  if (!(error instanceof HttpErrorResponse)) {
    return fallback;
  }
  if (error.status === 0) {
    return 'Нет связи с сервером. Проверьте подключение и попробуйте ещё раз.';
  }
  if (error.status === 429) {
    return 'Слишком много попыток. Подождите минуту и попробуйте снова.';
  }

  const body = parseBody(error.error);
  const fieldErrors = Object.values(body?.errors ?? {}).flat();
  if (fieldErrors.length > 0) {
    return [body?.detail, ...fieldErrors].filter(Boolean).join(' ');
  }
  return body?.detail || body?.title || fallback;
}

function parseBody(raw: unknown): ProblemBody | null {
  if (typeof raw === 'string') {
    try {
      return JSON.parse(raw) as ProblemBody;
    } catch {
      return null;
    }
  }
  return raw && typeof raw === 'object' ? (raw as ProblemBody) : null;
}

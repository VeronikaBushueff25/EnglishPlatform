import { HttpErrorResponse } from '@angular/common/http';
import { tr } from '../i18n/tr';

interface ProblemBody {
  title?: string | null;
  detail?: string | null;
  errors?: Record<string, string[]>;
}

/**
 * Текст ошибки для пользователя из ответа API (ProblemDetails / ValidationProblemDetails).
 * Сообщения сервера на русском; в английском интерфейсе каждое переводится по словарю (tr).
 */
export function problemMessage(error: unknown, fallback = 'Что-то пошло не так. Попробуйте ещё раз.'): string {
  return tr(rawProblemMessage(error, fallback));
}

function rawProblemMessage(error: unknown, fallback: string): string {
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
    return [body?.detail, ...fieldErrors].filter((m): m is string => !!m).map(tr).join(' ');
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

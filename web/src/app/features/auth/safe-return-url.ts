/** Возврат после входа только на свои страницы: без внешних адресов вида //evil.example. */
export function safeReturnUrl(url: string | undefined | null): string {
  return url && url.startsWith('/') && !url.startsWith('//') && !url.startsWith('/\\') ? url : '/';
}

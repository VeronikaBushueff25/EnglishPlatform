import { Routes } from '@angular/router';
import { adminGuard, authGuard, guestGuard } from './core/auth/auth.guards';

/**
 * Каждая область экранов (вход, тест уровня, словарь, занятие, статистика, админка)
 * живёт в своей папке features/* и грузится лениво. Области добавляются по этапам плана.
 */
export const routes: Routes = [
  {
    path: '',
    title: 'English Platform',
    loadComponent: () => import('./features/home/home').then((m) => m.Home),
  },
  {
    path: 'login',
    title: 'Вход',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login/login').then((m) => m.Login),
  },
  {
    path: 'register',
    title: 'Регистрация',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/register/register').then((m) => m.Register),
  },
  {
    path: 'forgot-password',
    title: 'Восстановление пароля',
    loadComponent: () => import('./features/auth/forgot-password/forgot-password').then((m) => m.ForgotPassword),
  },
  {
    // Ссылка из письма: /reset-password?email=...&token=...
    path: 'reset-password',
    title: 'Новый пароль',
    loadComponent: () => import('./features/auth/reset-password/reset-password').then((m) => m.ResetPassword),
  },
  {
    path: 'profile',
    title: 'Профиль',
    canActivate: [authGuard],
    loadComponent: () => import('./features/profile/profile').then((m) => m.Profile),
  },
  {
    path: 'dictionary',
    canActivate: [authGuard],
    children: [
      {
        path: '',
        title: 'Словарь',
        loadComponent: () => import('./features/dictionary/dictionary').then((m) => m.Dictionary),
      },
      {
        path: 'import',
        title: 'Импорт слов',
        loadComponent: () => import('./features/dictionary/import-words').then((m) => m.ImportWords),
      },
      {
        path: 'words/new',
        title: 'Новое слово',
        loadComponent: () => import('./features/dictionary/word-form').then((m) => m.WordForm),
      },
      {
        path: 'words/:id',
        title: 'Слово',
        loadComponent: () => import('./features/dictionary/word-form').then((m) => m.WordForm),
      },
      {
        path: 'lexemes/:id',
        title: 'Слово',
        loadComponent: () => import('./features/dictionary/lexeme-details').then((m) => m.LexemeDetails),
      },
    ],
  },
  { path: 'admin', pathMatch: 'full', redirectTo: 'admin/users' },
  {
    path: 'admin/lexemes',
    canActivate: [adminGuard],
    children: [
      {
        path: '',
        title: 'Слова общей базы',
        loadComponent: () => import('./features/admin/lexemes/admin-lexemes').then((m) => m.AdminLexemes),
      },
      {
        path: 'import',
        title: 'Импорт в общую базу',
        data: { mode: 'admin' },
        loadComponent: () => import('./features/dictionary/import-words').then((m) => m.ImportWords),
      },
      {
        path: 'new',
        title: 'Новое слово',
        loadComponent: () => import('./features/admin/lexemes/admin-lexeme-form').then((m) => m.AdminLexemeForm),
      },
      {
        path: ':id',
        title: 'Слово общей базы',
        loadComponent: () => import('./features/admin/lexemes/admin-lexeme-form').then((m) => m.AdminLexemeForm),
      },
    ],
  },
  {
    path: 'admin/categories',
    title: 'Категории',
    canActivate: [adminGuard],
    loadComponent: () => import('./features/admin/categories/admin-categories').then((m) => m.AdminCategories),
  },
  {
    path: 'admin/users',
    title: 'Пользователи',
    canActivate: [adminGuard],
    loadComponent: () => import('./features/admin/users/admin-users').then((m) => m.AdminUsers),
  },
  { path: '**', redirectTo: '' },
];

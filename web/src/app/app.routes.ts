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
    path: 'learn',
    title: 'Занятие',
    canActivate: [authGuard],
    loadComponent: () => import('./features/learn/learn').then((m) => m.Learn),
  },
  {
    path: 'stats',
    title: 'Статистика',
    canActivate: [authGuard],
    loadComponent: () => import('./features/stats/stats').then((m) => m.Stats),
  },
  {
    path: 'placement',
    title: 'Тест уровня',
    canActivate: [authGuard],
    loadComponent: () => import('./features/placement/placement').then((m) => m.Placement),
  },
  {
    path: 'plan',
    title: 'Моя программа',
    canActivate: [authGuard],
    loadComponent: () => import('./features/placement/plan').then((m) => m.Plan),
  },
  {
    path: 'grammar',
    canActivate: [authGuard],
    children: [
      { path: '', title: 'Грамматика', loadComponent: () => import('./features/grammar/grammar-pages').then((m) => m.GrammarList) },
      { path: ':slug', title: 'Правило', loadComponent: () => import('./features/grammar/grammar-pages').then((m) => m.GrammarTopicPage) },
    ],
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
    path: 'admin/placement',
    canActivate: [adminGuard],
    children: [
      { path: '', title: 'Тест уровня', loadComponent: () => import('./features/admin/placement/admin-placement').then((m) => m.AdminPlacement) },
      { path: 'new', title: 'Новый вопрос', loadComponent: () => import('./features/admin/placement/admin-placement').then((m) => m.AdminPlacementForm) },
      { path: ':id', title: 'Вопрос теста', loadComponent: () => import('./features/admin/placement/admin-placement').then((m) => m.AdminPlacementForm) },
    ],
  },
  {
    path: 'admin/grammar',
    canActivate: [adminGuard],
    children: [
      { path: '', title: 'Грамматика', loadComponent: () => import('./features/admin/grammar/admin-grammar').then((m) => m.AdminGrammar) },
      { path: 'new', title: 'Новая тема', loadComponent: () => import('./features/admin/grammar/admin-grammar-form').then((m) => m.AdminGrammarForm) },
      { path: ':id', title: 'Тема', loadComponent: () => import('./features/admin/grammar/admin-grammar-form').then((m) => m.AdminGrammarForm) },
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

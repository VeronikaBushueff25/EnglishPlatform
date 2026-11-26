import { expect, Locator, Page, test } from '@playwright/test';

/**
 * Главный путь нового пользователя: регистрация → тест уровня → слова в словарь → занятие → статистика.
 * Ответы в занятии не обязаны быть верными: тест проверяет, что путь проходится до конца и всё сохраняется.
 */
test('new user goes from registration to statistics', async ({ page }) => {
  const email = `e2e-${Date.now()}-${Math.floor(Math.random() * 1e6)}@example.com`;

  await test.step('registration', async () => {
    await page.goto('/register');
    await page.locator('#email').fill(email);
    await page.locator('#password').fill('secret123');
    await page.locator('#confirm').fill('secret123');
    await page.locator('button[type=submit]').click();
    // После регистрации — профиль: цели и нагрузка.
    await expect(page.getByRole('heading', { name: 'Аккаунт создан' })).toBeVisible();
    await page.getByRole('link', { name: 'English Platform' }).click();
    await expect(page.getByText('Определите свой уровень')).toBeVisible();
  });

  await test.step('placement test', async () => {
    await page.getByText('Определите свой уровень').click();
    await page.getByRole('button', { name: 'Начать тест' }).click();
    const dontKnow = page.getByRole('button', { name: 'Не знаю' });
    // Итог теста: «Начальный · верных ответов 0 из 18». Просто «ваш уровень» встречается и во вступлении.
    const result = page.getByText(/верных ответов \d+ из \d+/);
    for (let i = 0; i < 30 && !(await result.isVisible()); i++) {
      await dontKnow.or(result).first().waitFor();
      if (await dontKnow.isVisible()) {
        await tap(dontKnow);
      }
    }
    await expect(result).toBeVisible();
    await expect(page.getByText('A1', { exact: true }).first()).toBeVisible();
  });

  await test.step('add words', async () => {
    await page.goto('/dictionary');
    await page.getByRole('tab', { name: 'Общая база' }).click();
    for (const word of ['passport', 'luggage', 'receipt']) {
      await page.getByRole('searchbox').or(page.getByPlaceholder('Искать слово или перевод')).first().fill(word);
      const add = page.getByRole('button', { name: `Добавить в словарь: ${word}`, exact: true });
      const added = page.getByLabel('Уже в вашем словаре');
      await add.or(added).first().waitFor();
      if (await add.isVisible()) {
        await add.click();
      }
    }
  });

  await test.step('learning session', async () => {
    await page.goto('/learn');
    await page.getByRole('button', { name: /Начать занятие|Продолжить занятие/ }).click();
    await finishSession(page);
    await expect(page.getByText('Занятие завершено')).toBeVisible();
  });

  await test.step('statistics', async () => {
    await page.goto('/stats');
    await expect(page.getByRole('heading', { name: 'Статистика' })).toBeVisible();
    await expect(page.getByText('1 день')).toBeVisible();
    await expect(page.getByText('сегодня засчитан')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'История занятий' })).toBeVisible();
    await expect(page.locator('app-stats-chart li[aria-label*="ответов"]').last()).not.toHaveAttribute('aria-label', /ответов 0,/);
  });
});

/** Экран меняется после каждого ответа, поэтому кликаем с коротким таймаутом: кнопка исчезла — смотрим заново. */
function tap(locator: Locator): Promise<void> {
  return locator.click({ timeout: 3_000 }).catch(() => undefined);
}

/** Проходит занятие: знакомство — «Запомнил», выбор — первый вариант, ввод — «Не знаю», после разбора — «Дальше». */
async function finishSession(page: Page): Promise<void> {
  const done = page.getByText('Занятие завершено');
  const intro = page.getByRole('button', { name: /Запомнил, проверить|Помню, дальше/ });
  const next = page.getByRole('button', { name: 'Дальше' });
  const dontKnow = page.getByRole('button', { name: 'Не знаю' });
  const option = page.locator('section button.min-h-14').first();

  for (let step = 0; step < 120; step++) {
    await done.or(intro).or(next).or(dontKnow).or(option).first().waitFor();
    if (await done.isVisible()) return;
    if (await next.isVisible()) {
      await tap(next);
    } else if (await intro.isVisible()) {
      await tap(intro);
    } else if (await dontKnow.isVisible()) {
      await tap(dontKnow);
    } else {
      await tap(option);
    }
  }
  throw new Error('Занятие не закончилось за 120 шагов');
}

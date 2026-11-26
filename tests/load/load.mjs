// Нагрузочная проверка API без сторонних инструментов (Node 22+).
//
//   node tests/load/load.mjs [baseUrl] [виртуальных пользователей] [секунд]
//   node tests/load/load.mjs http://localhost:5080 50 60
//
// Заводит до 15 учебных аккаунтов (вход и регистрация ограничены 20 запросами в минуту с IP), каждому кладёт
// 15 слов, затем виртуальные пользователи по кругу ходят обычным путём: главная → словарь → поиск → занятие
// (следующее задание + ответ) → статистика. В конце — запросов в секунду и задержки p50/p95/p99 по каждому запросу.
// Запускать на тестовой базе: аккаунты load-*@example.com остаются в ней.

const [base = 'http://localhost:5080', vusArg = '30', secondsArg = '45'] = process.argv.slice(2);
const API = `${base.replace(/\/$/, '')}/api/v1`;
const VUS = Number(vusArg);
const SECONDS = Number(secondsArg);
const ACCOUNTS = Math.min(15, VUS);

const stats = new Map();
function record(name, ms, ok, conflict = false) {
  const s = stats.get(name) ?? { times: [], errors: 0, conflicts: 0 };
  s.times.push(ms);
  if (!ok) s.errors++;
  if (conflict) s.conflicts++;
  stats.set(name, s);
}

// expectConflict: 409 — штатный ответ (несколько виртуальных пользователей на одном аккаунте = две вкладки,
// сервер отклоняет устаревшее задание). Считаем отдельно, не как ошибку.
async function call(name, method, path, token, body, expectConflict = false) {
  const started = performance.now();
  let response;
  try {
    response = await fetch(API + path, {
      method,
      headers: { 'content-type': 'application/json', ...(token ? { authorization: `Bearer ${token}` } : {}) },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    const text = await response.text();
    const conflict = expectConflict && response.status === 409;
    record(name, performance.now() - started, response.ok || conflict, conflict);
    if (!response.ok) {
      if (conflict) return null;
      if (stats.get(name).errors <= 3) console.warn(`  ${name}: ${response.status} ${text.slice(0, 160)}`);
      return null;
    }
    return text ? JSON.parse(text) : {};
  } catch (e) {
    record(name, performance.now() - started, false);
    if (stats.get(name).errors <= 3) console.warn(`  ${name}: ${e.message}`);
    return null;
  }
}

async function register(i) {
  const email = `load-${Date.now()}-${i}@example.com`;
  const auth = await call('setup: register', 'POST', '/auth/register', null, { email, password: 'secret123' });
  if (!auth) throw new Error('Не удалось зарегистрировать учебный аккаунт — проверьте лимит запросов и адрес API.');
  return auth.accessToken;
}

async function addWords(i, token, lexemeIds) {
  for (const lexemeId of lexemeIds.slice((i * 7) % 40, ((i * 7) % 40) + 15)) {
    await call('setup: add word', 'POST', '/my-words', token, { lexemeId });
  }
}

const SEARCH = ['travel', 'work', 'book', 'time', 'house', 'water', 'friend', 'go', 'make', 'happy'];

async function virtualUser(token, deadline, n) {
  let session = null;
  let i = n;
  while (performance.now() < deadline) {
    i++;
    await call('GET /learning/today', 'GET', '/learning/today', token);
    await call('GET /my-words', 'GET', '/my-words?page=1&pageSize=20', token);
    await call('GET /lexemes?search', 'GET', `/lexemes?search=${SEARCH[i % SEARCH.length]}&pageSize=20`, token);

    session ??= (await call('POST /sessions', 'POST', '/sessions', token, {}))?.id ?? null;
    if (session) {
      const step = await call('GET /sessions/{id}/next', 'GET', `/sessions/${session}/next`, token);
      if (!step || step.finished) {
        session = null;
      } else {
        const task = step.task;
        const answer = task.kind === 'Introduce' ? '' : (task.options?.[i % Math.max(1, task.options.length)] ?? 'answer');
        await call('POST /sessions/{id}/answers', 'POST', `/sessions/${session}/answers`, token, {
          attemptId: crypto.randomUUID(),
          taskId: task.taskId,
          answer,
          usedHint: false,
          elapsedMs: 3000 + (i % 7) * 1000,
        }, true);
      }
    }
    if (i % 5 === 0) await call('GET /stats/summary', 'GET', '/stats/summary', token);
  }
}

function percentile(sorted, p) {
  return sorted[Math.min(sorted.length - 1, Math.floor((p / 100) * sorted.length))];
}

console.log(`API ${API}: ${VUS} виртуальных пользователей, ${SECONDS} с, учебных аккаунтов ${ACCOUNTS}`);
const health = await fetch(`${base.replace(/\/$/, '')}/health/ready`).catch(() => null);
if (!health?.ok) throw new Error(`API не готов: ${base}/health/ready`);

const tokens = [];
for (let i = 0; i < ACCOUNTS; i++) tokens.push(await register(i));
const lexemes = await call('setup: lexemes', 'GET', '/lexemes?page=1&pageSize=100', tokens[0]);
if (!lexemes?.items?.length) throw new Error('Не удалось получить список слов');
const lexemeIds = lexemes.items.map((l) => l.id);
for (let i = 0; i < ACCOUNTS; i++) await addWords(i, tokens[i], lexemeIds);
for (const key of [...stats.keys()]) if (key.startsWith('setup')) stats.delete(key);

const started = performance.now();
const deadline = started + SECONDS * 1000;
await Promise.all(Array.from({ length: VUS }, (_, n) => virtualUser(tokens[n % tokens.length], deadline, n)));
const elapsed = (performance.now() - started) / 1000;

let total = 0;
let errors = 0;
const rows = [...stats.entries()].map(([name, s]) => {
  const sorted = [...s.times].sort((a, b) => a - b);
  total += sorted.length;
  errors += s.errors;
  return {
    запрос: name,
    всего: sorted.length,
    ошибок: s.errors,
    'конфликт 409': s.conflicts,
    'p50, мс': Math.round(percentile(sorted, 50)),
    'p95, мс': Math.round(percentile(sorted, 95)),
    'p99, мс': Math.round(percentile(sorted, 99)),
    'макс, мс': Math.round(sorted.at(-1)),
  };
});
console.table(rows);
console.log(`Итого: ${total} запросов за ${elapsed.toFixed(1)} с — ${(total / elapsed).toFixed(0)} запросов/с, ошибок ${errors} (${((errors / Math.max(1, total)) * 100).toFixed(2)}%)`);
process.exit(errors / Math.max(1, total) > 0.01 ? 1 : 0);

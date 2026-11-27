# Owlish

Рабочее имя репозитория и пространств имён в коде — EnglishPlatform.

Веб-приложение для изучения английского: личный словарь, занятия с интервальным повторением (FSRS),
тест уровня CEFR, статистика. Модульный монолит на .NET 10 + Angular 22, PostgreSQL.

## Структура

```
src/Host/                    ASP.NET Core Web API: DI, middleware, OpenAPI, health
src/BuildingBlocks/          Result, события, пагинация; базовый DbContext, IModule, валидация
src/Modules/<Module>/        Domain, Application, Infrastructure, Contracts
tests/                       Architecture.Tests (NetArchTest), Integration.Tests (Testcontainers)
web/                         Angular
deploy/                      docker-compose, nginx.conf
openapi/openapi.json         генерируется при сборке Host
docs/decisions.md            журнал архитектурных решений
docs/progress.md             что сделано и что осталось по этапам
docs/roadmap.md              план, статус этапов, идеи на будущее
docs/deploy.md               выпуск и эксплуатация
web/e2e/                     сквозные тесты Playwright
tests/load/                  нагрузочный скрипт
```

Модуль видит у других модулей только `*.Contracts`, это проверяют архитектурные тесты.
Каждый модуль хранит данные в своей схеме PostgreSQL.

## Запуск одной командой (Docker)

```
cp deploy/.env.example deploy/.env      # задать POSTGRES_PASSWORD
docker compose -f deploy/docker-compose.yml up --build
```

Приложение: http://localhost:8080, API: http://localhost:8080/api/v1, готовность: /health/ready.

## Разработка без Docker

Нужны .NET SDK 10, Node 24 и PostgreSQL.

1. Строка подключения в user-secrets (в appsettings лежит только образец):
   ```
   dotnet user-secrets --project src/Host/EnglishPlatform.Host set ConnectionStrings:Database "Host=localhost;Database=english_platform;Username=...;Password=..."
   ```
2. API: `dotnet run --project src/Host/EnglishPlatform.Host` (http://localhost:5080, документация /scalar/v1).
   Миграции применяются при старте.
3. Фронтенд: `cd web && npm ci && npm start` (http://localhost:4200, /api проксируется на 5080).

Сборка решения: `dotnet build EnglishPlatform.slnx -m:1`.
Тесты: `dotnet test --solution EnglishPlatform.slnx`, фронтенд `cd web && npm run test:ci`.
Сквозной тест в браузере (Edge/Chrome, приложение запущено): `cd web && npm run e2e` (`E2E_BASE_URL` — другой адрес).
Нагрузка на API (тестовая база): `node tests/load/load.mjs http://localhost:5080 100 60`.
Выпуск на сервер, HTTPS, резервные копии, мониторинг: [docs/deploy.md](docs/deploy.md).
После изменения эндпоинтов: собрать Host, затем `cd web && npm run api:generate`.

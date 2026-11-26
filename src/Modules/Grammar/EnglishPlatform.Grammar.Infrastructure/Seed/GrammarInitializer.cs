using EnglishPlatform.BuildingBlocks.Infrastructure.Modules;
using EnglishPlatform.Grammar.Domain;
using EnglishPlatform.Grammar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnglishPlatform.Grammar.Infrastructure.Seed;

/// <summary>
/// Стартовое наполнение: шесть тем под самые частые ошибки при вводе слов и справочник типов ошибок.
/// Темы создаются только если их ещё нет (по идентификатору), типы ошибок — если нет кода; правки администратора не затираются.
/// </summary>
internal sealed class GrammarInitializer(GrammarDb db, TimeProvider time) : IModuleInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = time.GetUtcNow();
        HashSet<string> existing = (await db.Topics.Select(t => t.Slug).ToListAsync(cancellationToken)).ToHashSet();
        foreach (GrammarTopicData topic in StarterTopics.All.Where(t => !existing.Contains(t.Slug)))
        {
            db.Topics.Add(GrammarTopic.Create(topic, now));
        }

        await db.SaveChangesAsync(cancellationToken);

        Dictionary<string, Guid> bySlug = await db.Topics.ToDictionaryAsync(t => t.Slug, t => t.Id, cancellationToken);
        HashSet<string> codes = (await db.ErrorTags.Select(t => t.Code).ToListAsync(cancellationToken)).ToHashSet();
        foreach ((string code, string title, string description, string? slug) in StarterTopics.Tags.Where(t => !codes.Contains(t.Code)))
        {
            db.ErrorTags.Add(ErrorTagInfo.Create(code, title, description, slug is not null && bySlug.TryGetValue(slug, out Guid id) ? id : null));
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

internal static class StarterTopics
{
    public static readonly (string Code, string Title, string Description, string? Slug)[] Tags =
    [
        ("Typo", "Опечатки", "Слово узнано, но написано с ошибкой в одну-две буквы.", "spelling"),
        ("Article", "Артикли", "Пропущен или лишний a/an/the.", "articles"),
        ("WordForm", "Формы слова", "Нужное слово, но не в той форме: deny вместо denied.", "word-forms"),
        ("Preposition", "Предлоги", "Фраза верная, но предлог другой: depend of вместо depend on.", "prepositions"),
        ("WrongWord", "Не то слово", "Выбрано или написано другое слово.", null),
        ("Empty", "Нет ответа", "Ответ пропущен («Не знаю»).", null),
    ];

    private static GrammarExample Ok(string text, string? translation = null) => new(text, translation, false);

    private static GrammarExample No(string text, string? translation = null) => new(text, translation, true);

    public static readonly GrammarTopicData[] All =
    [
        new(
            "articles",
            "Артикли a/an и the",
            "A1",
            "a/an — «один, какой-то»: предмет упоминается впервые или он один из многих. the — «тот самый»: собеседник понимает, о каком предмете речь. an пишется перед гласным звуком: an apple, an hour, но a university.",
            """
            Перед исчисляемым существительным в единственном числе почти всегда нужен артикль. Без него можно только, если
            перед словом уже стоит другое определяющее слово: my, this, every, one.

            a/an — когда предмет называется впервые или неважно, какой именно: I need a pen. She is a teacher (профессия).
            Выбор a или an зависит от звука, а не от буквы: an hour (h не читается), a university (звук «ю»).

            the — когда понятно, о чём речь: предмет уже упоминали, он единственный (the sun) или уточнён (the book on the table).

            Во множественном числе и с неисчисляемыми словами a/an не бывает: I like apples. Money isn't everything.
            """,
            [
                Ok("I bought a book. The book is great.", "Я купил книгу. Книга отличная."),
                Ok("She is a teacher.", "Она учитель."),
                No("She is teacher."),
                Ok("an apple, an hour, a university"),
                Ok("Could we have the bill, please?", "Можно нам счёт, пожалуйста?"),
                No("a money", "money — неисчисляемое"),
            ],
            10),
        new(
            "word-forms",
            "Формы слова: -s, -ed, -ing",
            "A2",
            "Слово меняет окончание по смыслу фразы: he works (-s у глагола в 3-м лице настоящего времени), worked (прошлое), working (после be и после некоторых глаголов), books (множественное число). В пропуске нужна именно та форма, что подходит предложению.",
            """
            Глагол в настоящем времени с he/she/it получает -s: She works here. He denies it.

            Прошедшее время правильных глаголов — -ed: work → worked, deny → denied (y после согласной меняется на i).
            Неправильные глаголы нужно запомнить: take → took → taken, go → went → gone.

            -ing ставится после be (I am working), после предлогов (interested in learning) и после ряда глаголов
            (avoid, enjoy, keep, consider) — подробнее в теме «-ing или to».

            Существительное во множественном числе — -s или -es: book → books, box → boxes, city → cities.
            """,
            [
                Ok("He denied taking the money.", "Он отрицал, что взял деньги."),
                No("He deny taking the money."),
                Ok("She works from home.", "Она работает из дома."),
                No("She work from home."),
                Ok("I bumped into Tom yesterday.", "Вчера я случайно встретил Тома."),
            ],
            20),
        new(
            "spelling",
            "Трудные места в написании",
            "A2",
            "Частые ловушки: удвоение согласной (stop → stopped, big → bigger), y → i (study → studied), немые буквы (knee, write, hour), похожие окончания (-ible/-able, -ence/-ance). Пишите слово целиком несколько раз — так запоминается быстрее, чем по правилам.",
            """
            Удвоение: в коротком слове с одной гласной перед последней согласной она удваивается перед -ed, -ing, -er:
            stop → stopped, plan → planning, big → bigger.

            y → i: если перед y стоит согласная, перед окончанием y меняется на i: study → studied, happy → happier.
            Но play → played (перед y гласная).

            Немые буквы: k в knee, know; w в write, wrong; h в hour, honest; b в climb, doubt.

            Похожие пары, которые часто путают: their/there, lose/loose, advice (сущ.)/advise (глагол).
            """,
            [
                Ok("stop → stopped, plan → planning"),
                Ok("study → studied, but play → played"),
                No("enviroment", "правильно: environment"),
                Ok("environment, government, necessary"),
            ],
            30),
        new(
            "prepositions",
            "Предлоги после слов",
            "B1",
            "Многие слова требуют определённого предлога, и он не совпадает с русским: depend on (зависеть от), interested in (интересоваться), afraid of (бояться), good at (хорош в), responsible for (ответственный за). Учите слово сразу вместе с предлогом.",
            """
            Предлог здесь — часть выражения, логикой его не вывести. Поэтому в словаре удобно хранить сразу
            «depend on», а не «depend».

            Частые сочетания: depend on, rely on, interested in, good at, bad at, afraid of, proud of, capable of,
            responsible for, apply for, look forward to, get used to, deal with, cope with.

            После предлога глагол стоит в форме -ing: interested in learning, capable of doing, look forward to seeing.
            """,
            [
                Ok("It depends on the weather.", "Это зависит от погоды."),
                No("It depends of the weather."),
                Ok("She is interested in art.", "Она интересуется искусством."),
                No("She is interested at art."),
                Ok("I'm looking forward to seeing you.", "Жду встречи с тобой."),
            ],
            40),
        new(
            "gerund-infinitive",
            "-ing или to после глагола",
            "B1",
            "После avoid, enjoy, keep, consider, suggest, mind, finish нужен глагол с -ing: avoid making mistakes. После want, decide, plan, agree, refuse, manage, hope — to + глагол: decide to stay. После предлогов (включая to в look forward to) — всегда -ing.",
            """
            Глаголы, после которых идёт -ing: avoid, enjoy, keep, consider, suggest, mind, finish, deny, practise, give up.
            I keep forgetting his name. He denied taking the money.

            Глаголы, после которых идёт to: want, decide, plan, agree, refuse, manage, hope, promise, learn, need.
            We decided to stay at home.

            Ловушка: to бывает предлогом, и тогда после него -ing: look forward to meeting, be used to working,
            get used to living.

            С allow, ask, tell, want сначала идёт человек: allow someone to do something.
            """,
            [
                Ok("Try to avoid making the same mistake.", "Старайся не повторять ту же ошибку."),
                No("Try to avoid to make the same mistake."),
                Ok("We decided to stay at home.", "Мы решили остаться дома."),
                Ok("I'm used to working late.", "Я привык работать допоздна."),
                No("I'm used to work late."),
            ],
            50),
        new(
            "conditionals",
            "Условные предложения",
            "B1",
            "Second conditional — нереальное сейчас: If I had more time, I would travel. Third conditional — о прошлом, которое уже не изменить: If he had warned me, I would have left earlier. В части с if не ставится would.",
            """
            First conditional — реальное будущее: If it rains, we will stay at home. В части с if — настоящее время.

            Second conditional — воображаемое настоящее или будущее: If I had more time, I would learn Spanish.
            If I were you, I would call her (были — were, для всех лиц).

            Third conditional — прошлое, которое не случилось: If I had known, I would have helped.
            Схема: if + had + V3, would have + V3.

            Главная ошибка — would в обеих частях: «If I would have time» — так нельзя.
            """,
            [
                Ok("If I had more time, I would travel.", "Если бы у меня было больше времени, я бы путешествовал."),
                No("If I would have more time, I would travel."),
                Ok("If he had warned me, I would have left earlier.", "Если бы он предупредил, я бы ушёл раньше."),
                Ok("If I were you, I would call her.", "На твоём месте я бы ей позвонил."),
            ],
            60),
    ];
}

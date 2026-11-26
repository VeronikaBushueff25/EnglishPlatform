using EnglishPlatform.BuildingBlocks.Infrastructure.Modules;
using EnglishPlatform.LearningProfiles.Domain;
using EnglishPlatform.LearningProfiles.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnglishPlatform.LearningProfiles.Infrastructure.Seed;

/// <summary>
/// Стартовый банк вопросов: лексика и грамматика — по 3 вопроса на уровень A1–C2, чтение — по 2 текста на уровень.
/// Составлен для проекта. Загружается только в пустой банк, дальше вопросами управляет администратор.
/// Правильный вариант везде первый — при показе варианты перемешиваются.
/// </summary>
internal sealed class PlacementSeed(LearningProfilesDb db, TimeProvider time) : IModuleInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (await db.Questions.AnyAsync(cancellationToken))
        {
            return;
        }

        DateTimeOffset now = time.GetUtcNow();
        foreach (PlacementQuestionData question in Questions)
        {
            db.Questions.Add(PlacementQuestion.Create(question, now));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static PlacementQuestionData Word(Cefr level, string word, params string[] options) =>
        new(Skill.Vocabulary, level, $"Что значит «{word}»?", null, options, 0, true);

    private static PlacementQuestionData Gap(Cefr level, string sentence, params string[] options) =>
        new(Skill.Grammar, level, sentence, null, options, 0, true);

    private static PlacementQuestionData Read(Cefr level, string passage, string question, params string[] options) =>
        new(Skill.Reading, level, question, passage, options, 0, true);

    public static readonly PlacementQuestionData[] Questions =
    [
        // Лексика
        Word(Cefr.A1, "kitchen", "кухня", "спальня", "сад", "окно"),
        Word(Cefr.A1, "cheap", "дешёвый", "дорогой", "тяжёлый", "пустой"),
        Word(Cefr.A1, "tomorrow", "завтра", "вчера", "сегодня", "всегда"),
        Word(Cefr.A2, "borrow", "брать взаймы", "давать в долг", "терять", "покупать"),
        Word(Cefr.A2, "journey", "поездка", "журнал", "обед", "газета"),
        Word(Cefr.A2, "noisy", "шумный", "грязный", "весёлый", "тесный"),
        Word(Cefr.B1, "reliable", "надёжный", "известный", "удобный", "вежливый"),
        Word(Cefr.B1, "afford", "позволить себе (по деньгам)", "предложить", "обеспечить", "заработать"),
        Word(Cefr.B1, "disappointed", "разочарованный", "рассеянный", "удивлённый", "обиженный"),
        Word(Cefr.B2, "reluctant", "неохотный", "осторожный", "равнодушный", "решительный"),
        Word(Cefr.B2, "thorough", "тщательный", "сквозной", "грубый", "быстрый"),
        Word(Cefr.B2, "undermine", "подрывать", "недооценивать", "подчёркивать", "подписывать"),
        Word(Cefr.C1, "meticulous", "скрупулёзный", "мелочный", "медлительный", "суеверный"),
        Word(Cefr.C1, "scrutinize", "тщательно изучать", "ругать", "сокращать", "подозревать"),
        Word(Cefr.C1, "ubiquitous", "вездесущий", "единственный", "неоднозначный", "устаревший"),
        Word(Cefr.C2, "obfuscate", "запутывать, затемнять смысл", "разоблачать", "упрощать", "откладывать"),
        Word(Cefr.C2, "perfunctory", "сделанный для галочки", "безупречный", "обязательный", "торжественный"),
        Word(Cefr.C2, "recalcitrant", "непокорный, упрямый", "повторяющийся", "выздоравливающий", "расчётливый"),

        // Грамматика
        Gap(Cefr.A1, "She ___ a teacher.", "is", "are", "am", "be"),
        Gap(Cefr.A1, "I ___ coffee every morning.", "drink", "drinks", "drinking", "am drink"),
        Gap(Cefr.A1, "There ___ two cats in the garden.", "are", "is", "be", "has"),
        Gap(Cefr.A2, "Yesterday I ___ to the cinema.", "went", "go", "have gone", "was go"),
        Gap(Cefr.A2, "This book is ___ than that one.", "more interesting", "most interesting", "interestinger", "as interesting"),
        Gap(Cefr.A2, "We ___ dinner when the phone rang.", "were having", "have", "are having", "has had"),
        Gap(Cefr.B1, "I've lived here ___ 2015.", "since", "for", "from", "during"),
        Gap(Cefr.B1, "If it rains tomorrow, we ___ at home.", "will stay", "would stay", "stayed", "would have stayed"),
        Gap(Cefr.B1, "She avoided ___ the question.", "answering", "to answer", "answer", "answered"),
        Gap(Cefr.B2, "If I ___ more time, I would learn Spanish.", "had", "have", "would have", "will have"),
        Gap(Cefr.B2, "The report ___ by Friday.", "must be finished", "must finish", "must be finishing", "must finished"),
        Gap(Cefr.B2, "He suggested ___ a taxi.", "taking", "to take", "took", "taken"),
        Gap(Cefr.C1, "Had I known about the problem, I ___ earlier.", "would have acted", "would act", "had acted", "will have acted"),
        Gap(Cefr.C1, "Not only ___ late, but he also forgot the documents.", "was he", "he was", "he has been", "did he"),
        Gap(Cefr.C1, "It's high time we ___ a decision.", "made", "make", "will make", "have made"),
        Gap(Cefr.C2, "Little ___ that the plan was about to fail.", "did they realise", "they realised", "they did realise", "realised they"),
        Gap(Cefr.C2, "Were the proposal ___, the company would expand rapidly.", "to be accepted", "accepted", "be accepted", "being accepted"),
        Gap(Cefr.C2, "She is said ___ the company twenty years ago.", "to have founded", "to found", "having founded", "that she founded"),

        // Чтение
        Read(
            Cefr.A1,
            "Tom gets up at seven o'clock. He has breakfast and goes to work by bus. He works in a shop in the city centre.",
            "How does Tom go to work?",
            "By bus", "By car", "On foot", "By train"),
        Read(
            Cefr.A1,
            "Anna has a small flat in the city. It has one bedroom and a big kitchen. She likes cooking for her friends at the weekend.",
            "What does Anna like doing?",
            "Cooking for her friends", "Sleeping late", "Eating in restaurants", "Living in the country"),
        Read(
            Cefr.A2,
            "Last summer Maria travelled to Italy with her sister. They stayed in a hotel near the sea. It rained for two days, so they visited museums instead of going to the beach.",
            "Why did they visit museums?",
            "Because it was raining", "Because the beach was closed", "Because the hotel was far from the sea", "Because they don't like swimming"),
        Read(
            Cefr.A2,
            "The library is open from 9 am to 8 pm on weekdays and from 10 am to 4 pm on Saturdays. It is closed on Sundays. You need a library card to borrow books.",
            "Can you borrow books on Sunday?",
            "No, the library is closed", "Yes, if you have a card", "Yes, from 10 am to 4 pm", "Only in the morning"),
        Read(
            Cefr.B1,
            "Many people believe that working from home makes them more productive. However, a recent survey found that while employees spend less time commuting, they often work longer hours and find it harder to switch off at the end of the day.",
            "What problem with working from home does the survey mention?",
            "It is harder to stop working", "People spend more time commuting", "Employees work fewer hours", "Productivity always falls"),
        Read(
            Cefr.B1,
            "When Sam started learning the guitar, he practised for hours every day and soon felt bored. His teacher advised him to practise for just twenty minutes, but every day. Within a month, Sam noticed real progress.",
            "What helped Sam make progress?",
            "Short but regular practice", "Practising for hours", "Changing his teacher", "Playing with friends"),
        Read(
            Cefr.B2,
            "Although electric cars produce no exhaust emissions, critics point out that their environmental impact depends heavily on how the electricity is generated. In countries that rely on coal, the overall benefit is considerably smaller.",
            "What is the critics' main point?",
            "The benefit depends on how electricity is produced", "Electric cars produce exhaust emissions", "Electric cars are too expensive", "Coal is becoming cheaper"),
        Read(
            Cefr.B2,
            "The museum's decision to stay open late on Fridays was initially met with scepticism. Yet visitor numbers on those evenings have since doubled, with younger audiences in particular taking advantage of the extended hours.",
            "What happened after the museum started opening late?",
            "More people, especially young people, came", "The decision was cancelled", "Fewer people visited on Fridays", "Older visitors complained"),
        Read(
            Cefr.C1,
            "The author argues that our obsession with productivity has paradoxically made us less effective: by measuring every minute, we prioritise tasks that are easy to quantify over those that genuinely matter, such as reflection and creative thinking.",
            "Why, according to the author, have we become less effective?",
            "We focus on measurable tasks rather than important ones", "We do not measure our time carefully enough", "Creative work takes too much time", "We spend too much time reflecting"),
        Read(
            Cefr.C1,
            "Far from being a sign of weakness, the willingness to admit uncertainty is, the researchers suggest, a hallmark of expertise; novices, by contrast, tend to overestimate the reliability of their own judgements.",
            "What do the researchers suggest?",
            "Experts are more willing to admit uncertainty", "Novices admit uncertainty more often", "Admitting uncertainty shows weakness", "Experts are always certain"),
        Read(
            Cefr.C2,
            "Ostensibly a treatise on gardening, the book is in fact a thinly veiled meditation on mortality, its seasonal chapters mirroring the arc of a human life with an almost unbearable tenderness.",
            "What is the book really about?",
            "Life and death", "Practical gardening advice", "The history of gardens", "The changing seasons"),
        Read(
            Cefr.C2,
            "The minister's statement, while conciliatory in tone, conceded nothing of substance, leaving observers to conclude that the apparent change of heart was more cosmetic than real.",
            "How did observers interpret the statement?",
            "As a superficial change with no real concessions", "As a genuine change of policy", "As an aggressive attack on critics", "As an admission of guilt"),
    ];
}

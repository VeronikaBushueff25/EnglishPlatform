using EnglishPlatform.BuildingBlocks.Infrastructure.Modules;
using EnglishPlatform.LearningProfiles.Domain;
using EnglishPlatform.LearningProfiles.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnglishPlatform.LearningProfiles.Infrastructure.Seed;

/// <summary>
/// Стартовый банк вопросов, составлен для проекта. Первый пакет: лексика и грамматика — по 3 вопроса на уровень A1–C2,
/// чтение — по 2 текста на уровень. Второй пакет (для длинного теста) — ещё по 3 вопроса лексики и грамматики
/// и по тексту с двумя вопросами на уровень. Каждый пакет загружается один раз: если вопросов пакета в банке ещё нет.
/// Дальше вопросами управляет администратор. Правильный вариант везде первый — при показе варианты перемешиваются.
/// </summary>
internal sealed class PlacementSeed(LearningProfilesDb db, TimeProvider time) : IModuleInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = time.GetUtcNow();
        if (!await db.Questions.AnyAsync(cancellationToken))
        {
            foreach (PlacementQuestionData question in Questions)
            {
                db.Questions.Add(PlacementQuestion.Create(question, now));
            }
        }

        // Пакет 2 ставится один раз — по его первому вопросу. Удалённые администратором вопросы не возвращаются.
        string marker = SecondBatch[0].Prompt;
        if (!await db.Questions.AnyAsync(q => q.Prompt == marker, cancellationToken))
        {
            HashSet<string> existing = (await db.Questions.Select(q => q.Prompt + "|" + q.Passage).ToListAsync(cancellationToken)).ToHashSet();
            foreach (PlacementQuestionData question in SecondBatch.Where(q => !existing.Contains(q.Prompt + "|" + q.Passage)))
            {
                db.Questions.Add(PlacementQuestion.Create(question, now));
            }
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

    private const string LeoText = "My name is Leo. I am ten years old. I have a sister and a dog. The dog's name is Max. Max likes to run in the park every morning.";

    private const string EmmaText = "Dear Kate, thank you for the birthday present! I love the blue scarf. I wore it to school today and my friends liked it too. Next weekend I'm having a party at my house. Can you come on Saturday at 3 pm? Love, Emma";

    private const string CinemaText = "Our town's old cinema closed five years ago, and for a long time the building stood empty. Last year a group of volunteers raised money to repair it. Now it shows films three nights a week and hosts concerts and workshops at weekends. The volunteers say the project has brought people of all ages together.";

    private const string SleepText = "Sleep researchers increasingly warn that the common habit of catching up on sleep at weekends does little to repair the damage caused by chronic sleep deprivation during the week. While a long lie-in may temporarily reduce tiredness, measures of attention and metabolism often remain impaired.";

    private const string TrafficText = "The city's new congestion charge was intended to cut traffic, and in that narrow sense it has succeeded. What its architects did not anticipate, however, was the extent to which drivers would simply reroute through residential streets just outside the charging zone, shifting rather than solving the problem.";

    private const string NovelText = "To call the novel's ending ambiguous would be to understate the matter; the author seems to have gone out of her way to deny the reader any resolution, a choice some critics have dismissed as coyness and others have hailed as the book's most honest gesture.";

    /// <summary>Второй пакет: чтобы длинный тест (14 + 14 + 8 вопросов) не повторял вопросы на одном уровне.</summary>
    public static readonly PlacementQuestionData[] SecondBatch =
    [
        // Лексика
        Word(Cefr.A1, "window", "окно", "дверь", "стена", "пол"),
        Word(Cefr.A1, "hungry", "голодный", "уставший", "сердитый", "сонный"),
        Word(Cefr.A1, "buy", "покупать", "продавать", "платить", "искать"),
        Word(Cefr.A2, "neighbour", "сосед", "родственник", "коллега", "гость"),
        Word(Cefr.A2, "forget", "забывать", "прощать", "получать", "терять"),
        Word(Cefr.A2, "dangerous", "опасный", "осторожный", "смешной", "трудный"),
        Word(Cefr.B1, "advice", "совет", "предупреждение", "устройство", "реклама"),
        Word(Cefr.B1, "complain", "жаловаться", "объяснять", "сравнивать", "соглашаться"),
        Word(Cefr.B1, "income", "доход", "расход", "налог", "вход"),
        Word(Cefr.B2, "assess", "оценивать", "присваивать", "помогать", "утверждать"),
        Word(Cefr.B2, "inevitable", "неизбежный", "невидимый", "непредсказуемый", "незаменимый"),
        Word(Cefr.B2, "outcome", "исход, результат", "выход", "доход", "начало"),
        Word(Cefr.C1, "ambiguous", "двусмысленный", "честолюбивый", "двуличный", "неуверенный"),
        Word(Cefr.C1, "mitigate", "смягчать, уменьшать", "усугублять", "переселять", "ограничивать"),
        Word(Cefr.C1, "tenacious", "упорный, цепкий", "напряжённый", "нежный", "вспыльчивый"),
        Word(Cefr.C2, "ephemeral", "мимолётный", "эфирный", "воображаемый", "хрупкий"),
        Word(Cefr.C2, "sycophant", "льстец, подхалим", "предатель", "скептик", "хвастун"),
        Word(Cefr.C2, "obdurate", "упрямый, непреклонный", "обязанный", "опытный", "испуганный"),

        // Грамматика
        Gap(Cefr.A1, "___ you like pizza?", "Do", "Does", "Are", "Is"),
        Gap(Cefr.A1, "My brother ___ a red car.", "has", "have", "is", "having"),
        Gap(Cefr.A1, "They ___ from Spain.", "are", "is", "am", "be"),
        Gap(Cefr.A2, "___ you ever been to London?", "Have", "Did", "Were", "Has"),
        Gap(Cefr.A2, "She can't come to the party because she ___ a cold.", "has", "have", "is", "having"),
        Gap(Cefr.A2, "We usually go to work ___ bus.", "by", "on", "with", "in"),
        Gap(Cefr.B1, "I'm not used to ___ up so early.", "getting", "get", "got", "have got"),
        Gap(Cefr.B1, "The film ___ we watched last night was boring.", "that", "what", "who", "whose"),
        Gap(Cefr.B1, "You ___ wear a uniform at our school — it's optional.", "don't have to", "mustn't", "have to", "must"),
        Gap(Cefr.B2, "By the time we arrived, the film ___.", "had already started", "already started", "has already started", "was already starting"),
        Gap(Cefr.B2, "I wish I ___ so much at dinner last night.", "hadn't eaten", "didn't eat", "haven't eaten", "wouldn't eat"),
        Gap(Cefr.B2, "The more you practise, ___ you get.", "the better", "better", "the best", "more better"),
        Gap(Cefr.C1, "___ the bad weather, the match went ahead.", "Despite", "Although", "However", "In spite"),
        Gap(Cefr.C1, "Under no circumstances ___ the door unlocked.", "should you leave", "you should leave", "should leave you", "you leave"),
        Gap(Cefr.C1, "I'd rather you ___ anyone about this yet.", "didn't tell", "don't tell", "won't tell", "not to tell"),
        Gap(Cefr.C2, "So ___ was the storm that the old bridge collapsed.", "severe", "severely", "severity", "much severe"),
        Gap(Cefr.C2, "Scarcely ___ the station when the train pulled out.", "had we reached", "we had reached", "did we reached", "we reached"),
        Gap(Cefr.C2, "Come what ___, I'll stand by you.", "may", "might", "will", "would"),

        // Чтение: по тексту на уровень, к каждому два вопроса
        Read(Cefr.A1, LeoText, "What is the dog's name?", "Max", "Leo", "Sam", "Ben"),
        Read(Cefr.A1, LeoText, "When does Max run in the park?", "Every morning", "Every evening", "On Sundays", "Never"),
        Read(Cefr.A2, EmmaText, "Why is Emma writing to Kate?", "To thank her and invite her to a party", "To ask her for a present", "To say she lost the scarf", "To cancel her party"),
        Read(Cefr.A2, EmmaText, "When is the party?", "On Saturday at 3 pm", "Today after school", "On Sunday morning", "On Friday evening"),
        Read(Cefr.B1, CinemaText, "Who made it possible to reopen the cinema?", "Volunteers who raised money", "The town council", "A film company", "A famous actor"),
        Read(Cefr.B1, CinemaText, "What happens in the building at weekends?", "Concerts and workshops", "Films every night", "Nothing — it is closed", "Repair work"),
        Read(Cefr.B2, SleepText, "What is the researchers' main claim?", "Weekend lie-ins do not fully undo weekday sleep loss", "Sleeping late at weekends is harmful", "People should sleep less at weekends", "Tiredness is caused by metabolism"),
        Read(Cefr.B2, SleepText, "What can a long lie-in do, according to the text?", "Reduce tiredness for a while", "Fully restore attention", "Improve metabolism", "Cause sleep deprivation"),
        Read(Cefr.C1, TrafficText, "What unexpected effect did the charge have?", "Traffic moved to nearby residential streets", "Traffic in the centre increased", "Drivers stopped using cars entirely", "The charging zone was made smaller"),
        Read(Cefr.C1, TrafficText, "How does the writer view the policy overall?", "It worked in a narrow sense but moved the problem elsewhere", "It was a complete failure", "It was an unqualified success", "It was too expensive to run"),
        Read(Cefr.C2, NovelText, "How is the novel's ending described?", "Deliberately left unresolved", "Happy but predictable", "Rushed and careless", "Clear and conclusive"),
        Read(Cefr.C2, NovelText, "How have critics reacted to the ending?", "Their opinions are divided", "They have all praised it", "They have all criticised it", "They have ignored it"),
    ];
}

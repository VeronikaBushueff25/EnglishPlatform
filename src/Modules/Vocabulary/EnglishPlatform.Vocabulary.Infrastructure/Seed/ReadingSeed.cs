using EnglishPlatform.BuildingBlocks.Infrastructure.Modules;
using EnglishPlatform.Vocabulary.Domain;
using EnglishPlatform.Vocabulary.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnglishPlatform.Vocabulary.Infrastructure.Seed;

/// <summary>
/// Стартовая библиотека для чтения, написана для проекта. Первый пакет — 10 текстов A1–C1, второй — ещё 12 (A1–C2).
/// Каждый пакет загружается один раз (второй — по заголовку первого его текста), удалённые администратором тексты
/// не возвращаются. Дальше текстами управляет администратор.
/// </summary>
internal sealed class ReadingSeed(VocabularyDb db, TimeProvider time) : IModuleInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = time.GetUtcNow();
        if (!await db.ReadingTexts.AnyAsync(cancellationToken))
        {
            foreach (ReadingTextData text in Texts)
            {
                db.ReadingTexts.Add(ReadingText.Create(text, now));
            }
        }

        string marker = SecondBatch[0].Title;
        if (!await db.ReadingTexts.AnyAsync(t => t.Title == marker, cancellationToken))
        {
            HashSet<string> titles = (await db.ReadingTexts.Select(t => t.Title).ToListAsync(cancellationToken)).ToHashSet();
            foreach (ReadingTextData text in SecondBatch.Where(t => !titles.Contains(t.Title)))
            {
                db.ReadingTexts.Add(ReadingText.Create(text, now));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public static readonly ReadingTextData[] Texts =
    [
        new("My new neighbour", CefrLevel.A1, "Люди", """
            I have a new neighbour. Her name is Lucy and she is from Canada. She is twenty-five years old. Lucy is a nurse and she works at the hospital near our street.

            Lucy has a small black cat. The cat's name is Pepper. Every morning Pepper sits in the window and looks at the birds.

            On Saturday Lucy came to my flat. We drank tea and talked about our cities. She likes our town because it is quiet and green. I like her because she is friendly and funny. Next week we want to go to the park together.
            """, true),

        new("A day at the market", CefrLevel.A1, "Еда и покупки", """
            Every Sunday my mother and I go to the market. It opens at eight o'clock and it is always busy.

            First we buy fruit: apples, bananas and oranges. Then we buy vegetables for the week. My mother likes fresh tomatoes and carrots. I like the bread stall. The bread is warm and it smells very good.

            The market is not expensive. A big bag of apples is only two pounds. At the end we drink hot chocolate at a small café. Then we walk home with heavy bags, but we are happy.
            """, true),

        new("The lost phone", CefrLevel.A2, "Повседневная жизнь", """
            Last Friday I lost my phone. I was on the bus on my way to work, and I was reading the news. When I got off the bus, I put the phone in my jacket pocket. Or I thought I did.

            At the office I wanted to call a client, but my pocket was empty. I felt terrible. All my photos and contacts were on that phone.

            My colleague Sam had a good idea. He called my number from his phone. After a few rings, a woman answered. She was the bus driver! She found my phone on a seat and kept it safe. In the evening I went to the bus station and she gave it back to me. I bought her a box of chocolates to say thank you.
            """, true),

        new("Learning to cook", CefrLevel.A2, "Еда и покупки", """
            Two years ago I couldn't cook at all. I usually ate sandwiches or ordered pizza. It was expensive and not very healthy.

            Then I decided to learn. I started with simple dishes: pasta with tomato sauce, omelettes and soup. I watched short videos online and followed the recipes step by step. My first soup was too salty, and my first cake was hard as a stone!

            But I didn't give up. Now I cook dinner almost every day. My favourite dish is chicken curry with rice. Last month I invited my friends and cooked a dinner for six people. Everybody asked for more, and that made me very proud.
            """, true),

        new("Why we procrastinate", CefrLevel.B1, "Психология", """
            Almost everyone has put off an important task at least once. You need to write a report, but suddenly cleaning the kitchen seems like a great idea. Why does this happen?

            Researchers say that procrastination is not really about laziness. It is about feelings. When a task makes us feel bored, anxious or unsure, our brain looks for a quick way to feel better. Checking social media or making a cup of tea gives us that short relief.

            The good news is that there are simple ways to deal with it. One method is to break a big task into small steps. Instead of "write the report", the first step can be "open the document and write the title". Another trick is to work for just ten minutes. Once we start, it is often easier to keep going. Finally, it helps to be kind to yourself: people who forgive themselves for procrastinating usually procrastinate less next time.
            """, true),

        new("A weekend without screens", CefrLevel.B1, "Технологии", """
            Last month my family tried an experiment: one whole weekend without phones, computers or television. At first, everyone complained. My teenage son said it was impossible, and I was worried I would miss important messages from work.

            On Saturday morning the house felt strangely quiet. Nobody knew what to do. Then my daughter found an old board game in a cupboard, and we played it for two hours. In the afternoon we went for a long walk and actually talked to each other instead of looking at our screens.

            By Sunday evening something had changed. We were calmer, and I noticed that I had slept much better. Of course, on Monday we all went back to our devices. But now we have a new rule: no phones at dinner. It is a small change, but it has made a real difference.
            """, true),

        new("The four-day week", CefrLevel.B2, "Работа", """
            Over the past few years, a growing number of companies have experimented with a four-day working week, keeping salaries the same while cutting working hours. The results have surprised many sceptics.

            In several large trials, most participating firms reported that productivity stayed the same or even improved. Employees said they felt less stressed and were less likely to take sick days. Some companies also found it easier to attract and retain talented staff.

            However, the model is not without its drawbacks. In sectors such as healthcare or retail, where someone has to be present every day, a shorter week can be difficult to organise. Critics also point out that some workers end up squeezing five days of tasks into four, which can lead to longer, more exhausting days.

            Supporters argue that the key is not simply working fewer hours, but rethinking how time is used: fewer unnecessary meetings, clearer priorities and less constant checking of email. Whether the idea spreads will probably depend on whether managers are willing to measure results rather than hours.
            """, true),

        new("Why we believe fake news", CefrLevel.B2, "Медиа", """
            It is tempting to think that only gullible people fall for fake news. In reality, research suggests that almost anyone can be misled, especially when a story confirms what they already believe.

            Psychologists call this confirmation bias: we tend to accept information that fits our existing views and to question information that challenges them. A headline that makes us angry or excited is particularly dangerous, because strong emotions make us share content quickly, without checking where it came from.

            Repetition plays a role too. The more often we see a claim, the more familiar it feels, and familiarity is easily mistaken for truth. This is why false stories that spread widely on social media can be so hard to correct.

            Fortunately, a few simple habits can help. Before sharing something, ask who published it and whether other reliable sources report the same thing. Notice your emotional reaction: if a post makes you furious, that is a good reason to slow down. Being sceptical does not mean believing nothing; it means taking a moment before deciding what to believe.
            """, true),

        new("The paradox of choice", CefrLevel.C1, "Психология", """
            Conventional wisdom holds that more choice is always better: the wider the range of options, the more likely we are to find exactly what we want. Yet a substantial body of research suggests that, beyond a certain point, abundance becomes a burden rather than a benefit.

            In one frequently cited study, shoppers who were offered a sample of six varieties of jam were considerably more likely to make a purchase than those confronted with twenty-four. Faced with too many alternatives, people tend to delay decisions, or avoid them altogether. Even when they do choose, they are often less satisfied, haunted by the suspicion that one of the rejected options might have been superior.

            The psychologist Barry Schwartz distinguishes between "maximisers", who feel compelled to find the best possible option, and "satisficers", who settle for one that is good enough. Paradoxically, maximisers frequently make objectively better choices but report feeling worse about them.

            None of this implies that choice is inherently harmful. Rather, it suggests that well-designed defaults, curated selections and a willingness to accept "good enough" may contribute more to our wellbeing than an endless array of possibilities.
            """, true),

        new("Cities that sleep", CefrLevel.C1, "Города", """
            For much of the twentieth century, urban planners regarded the night as dead time: a period when streets emptied, shops shuttered and the city's economic engine idled until dawn. That view is now being reconsidered, as municipalities from Amsterdam to Sydney appoint "night mayors" charged with managing the after-dark economy.

            Proponents argue that a thriving nightlife generates jobs, draws tourism and lends a city its cultural vitality. Night-time workers, they point out, include not only bartenders and musicians but also nurses, cleaners and delivery drivers, whose needs for safe transport and affordable food are routinely overlooked.

            Yet the nocturnal city is also contested terrain. Residents living above popular venues complain of noise, and police resources are stretched thin at weekends. Night mayors must therefore act as mediators, negotiating compromises such as staggered closing times, better soundproofing and late-night public transport.

            Whether such initiatives succeed may hinge less on regulation than on a shift in perception: from seeing the night as a problem to be contained to recognising it as an integral part of urban life.
            """, true),
    ];

    /// <summary>Второй пакет: ещё по два текста на уровень, включая C2.</summary>
    public static readonly ReadingTextData[] SecondBatch =
    [
        new("My morning", CefrLevel.A1, "Повседневная жизнь", """
            I get up at seven o'clock every day. First, I open the window and drink a glass of water. Then I take a shower and get dressed.

            For breakfast I usually eat porridge with honey and a banana. My husband makes coffee. We sit in the kitchen and talk about our plans for the day.

            At eight o'clock I walk to the bus stop. The bus is often full, so I listen to music. I start work at nine. I like my mornings because they are calm and simple.
            """, true),

        new("At the hotel", CefrLevel.A1, "Путешествия", """
            Anna and Mark are on holiday in Rome. Their hotel is small but very clean. It is near a big square with a beautiful fountain.

            Their room is on the third floor. It has a big bed, a desk and a small balcony. From the balcony they can see old roofs and a church.

            Breakfast is from seven to ten. There is bread, cheese, fruit and very good coffee. The receptionist is friendly. Every morning she gives them a map and tells them about a new place to visit.
            """, true),

        new("A birthday surprise", CefrLevel.A2, "Люди", """
            Last Saturday was my grandmother's eightieth birthday. My family wanted to give her a surprise, so we planned everything in secret for two months.

            My uncle booked a room in her favourite restaurant. My cousins made a photo album with pictures from her childhood, her wedding and all her grandchildren. I wrote a short poem.

            We told Grandma that we were going to have a quiet dinner, just the two of us. When she opened the restaurant door, thirty people shouted "Happy birthday!" She was so surprised that she started to cry. Then she laughed and hugged everyone. Later she said it was the best day of her life.
            """, true),

        new("Moving to a new city", CefrLevel.A2, "Люди", """
            Six months ago I moved from a small town to a big city for a new job. At first it was difficult. I didn't know anyone, the streets were noisy, and I often got lost on the underground.

            In the evenings I felt lonely. I usually stayed at home and watched films. Then a colleague invited me to a running club. I was nervous, but I went.

            The people in the club were very kind. After the run we always go to a café together. Now I have new friends, I know the best parks for running, and I can use the underground without a map. The city doesn't feel strange any more. It feels like home.
            """, true),

        new("How to remember new words", CefrLevel.B1, "Учёба", """
            Many language learners write long lists of new words and then forget most of them a few days later. This is completely normal: our memory is designed to forget information that we don't use.

            Scientists have found that the best way to remember something is to review it just before you are about to forget it. Each time you remember a word successfully, you can wait a little longer before the next review. At first you might review a word after one day, then after three days, then after a week, and so on.

            It also helps to learn words in context rather than in isolation. Instead of memorising "borrow — брать взаймы", try to remember a whole sentence, like "Can I borrow your pen?" Finally, use new words as soon as you can: write them in a message, say them out loud, or try to explain something with them. Words that we actively use are much harder to forget.
            """, true),

        new("The city garden", CefrLevel.B1, "Природа", """
            Three years ago, the empty space behind our apartment block was full of rubbish and broken glass. Nobody used it, and most people just walked past without looking.

            Then one of our neighbours, a retired teacher called Mrs Patel, decided to change it. She asked the local council for permission to turn it into a community garden. At first only a few people helped her, but every weekend more neighbours joined in.

            Today the garden has vegetable beds, fruit trees, a few benches and even a small pond. Children from the nearby school come to learn how to grow tomatoes and beans. In summer we organise picnics where everyone brings food made from what we have grown. The garden has not only made our street greener; it has also turned strangers into friends.
            """, true),

        new("Is working from cafés a good idea?", CefrLevel.B2, "Работа", """
            For a growing number of freelancers and remote workers, the local café has become an unofficial office. With a laptop, a cup of coffee and a reliable internet connection, they argue, you can be just as productive as in a traditional workplace, if not more so.

            Supporters claim that the gentle background noise of a café actually helps concentration. Some studies suggest that a moderate level of ambient sound can boost creative thinking, while the presence of other people working quietly creates a sense of shared purpose that is missing at home.

            On the other hand, café owners are not always enthusiastic. A customer who buys one coffee and occupies a table for five hours can cost them money, especially at busy times. Some cafés have responded by limiting Wi-Fi access or introducing laptop-free areas.

            For workers themselves, the drawbacks include uncomfortable chairs, a lack of privacy for phone calls and the temptation to spend too much money on snacks. Perhaps the best approach is a compromise: a few hours in a café for creative tasks, and a quieter space for everything else.
            """, true),

        new("The hidden cost of cheap clothes", CefrLevel.B2, "Экология", """
            Over the last two decades, clothes have become remarkably cheap. Fashion brands now release new collections every few weeks, and many shoppers buy items they wear only a handful of times before throwing them away. This model, known as fast fashion, has a price that rarely appears on the label.

            The textile industry is responsible for a significant share of global water use and carbon emissions. Producing a single cotton T-shirt can require thousands of litres of water, while synthetic fabrics release tiny plastic fibres every time they are washed. Meanwhile, millions of tonnes of clothing end up in landfill each year.

            There are also human costs. To keep prices low, much of the production takes place in factories where wages are minimal and working conditions can be unsafe.

            Consumers are not powerless, however. Buying fewer, better-quality items, repairing clothes, swapping with friends and shopping second-hand can all reduce the impact. As one designer put it, the most sustainable garment is the one that is already in your wardrobe.
            """, true),

        new("The science of habits", CefrLevel.C1, "Психология", """
            We like to think of ourselves as deliberate decision-makers, yet a considerable proportion of our daily behaviour is governed by habit. Researchers estimate that roughly forty per cent of what we do each day is performed more or less automatically, triggered by cues in our environment rather than by conscious choice.

            According to the widely cited "habit loop" model, every habit consists of three elements: a cue that prompts the behaviour, the routine itself, and a reward that reinforces it. Over time, the brain comes to anticipate the reward as soon as it encounters the cue, which is precisely why habits are so difficult to break.

            This has practical implications for anyone hoping to change their behaviour. Rather than relying on sheer willpower, which tends to be depleted by stress and fatigue, it is often more effective to redesign one's environment: placing running shoes by the door, keeping biscuits out of sight, or attaching a new habit to an existing one, such as reviewing vocabulary while the kettle boils.

            Crucially, the research suggests that consistency matters more than intensity. A modest action repeated daily is far more likely to become automatic than an ambitious one attempted sporadically.
            """, true),

        new("Should museums return their treasures?", CefrLevel.C1, "Культура", """
            Many of the world's most celebrated museums display objects that were acquired during periods of colonial expansion, often through means that would now be regarded as coercive or outright illegal. In recent years, demands for the restitution of such artefacts to their countries of origin have grown increasingly difficult to ignore.

            Advocates of restitution argue that these objects are not merely aesthetic curiosities but integral parts of living cultural and religious traditions. Keeping them thousands of kilometres away, they contend, perpetuates an imbalance of power that the museums themselves claim to have left behind.

            Opponents, by contrast, frequently invoke the notion of the "universal museum": an institution in which visitors can encounter the achievements of many civilisations under one roof. They also raise practical concerns about conservation, although such arguments have been weakened by the construction of state-of-the-art museums in several of the countries requesting the return of their heritage.

            A number of institutions have begun to seek a middle path, through long-term loans, shared ownership or collaborative exhibitions. Whether such compromises will satisfy those who regard the objects as stolen property remains to be seen.
            """, true),

        new("On the art of doing nothing", CefrLevel.C2, "Философия", """
            There is a peculiar irony in the fact that idleness, once extolled by philosophers from Aristotle to Bertrand Russell as the very precondition of a cultivated life, should now be regarded with something approaching moral suspicion. In an economy that monetises attention and equates busyness with worth, to sit and do nothing is to invite the charge of squandering one's potential.

            Yet a growing body of research lends unexpected credence to the ancients. When the mind is released from goal-directed tasks, it does not simply switch off; rather, it engages a network of brain regions associated with memory consolidation, self-reflection and the kind of loose associative thinking from which creative insights tend to emerge. The proverbial idea that arrives in the shower is, it seems, no accident.

            None of this is to romanticise indolence or to deny that sustained effort is indispensable to any worthwhile achievement. The point is subtler: that periods of apparent unproductiveness may be not the antithesis of meaningful work but its hidden scaffolding. To reclaim the right to idle, then, is less an act of laziness than a quiet refusal to let every waking hour be colonised by the demand to be useful.
            """, true),

        new("The untranslatable", CefrLevel.C2, "Язык", """
            Every so often, a list of supposedly untranslatable words does the rounds online: the German "Schadenfreude", the Portuguese "saudade", the Japanese "komorebi", denoting sunlight filtering through the leaves of trees. Such lists are invariably charming, and invariably somewhat misleading.

            Strictly speaking, almost nothing is untranslatable; it is merely that some concepts resist being rendered by a single, tidy equivalent. "Saudade" can be glossed perfectly adequately as a melancholic longing for something or someone absent, perhaps irretrievably so. What is lost is not the meaning but the economy, and with it the sense that a culture has deemed this particular feeling worthy of its own word.

            This, arguably, is where the real interest lies. The lexicon of a language is not a neutral inventory of the world but a record of what its speakers have found it necessary or pleasurable to name. To learn a language well is therefore to acquire not simply a new set of labels but a subtly altered way of carving up experience, one in which certain distinctions become salient and others recede.

            Translators, accordingly, are less like engineers building bridges than like diplomats, perpetually negotiating between what can be said and what can merely be gestured towards.
            """, true),
    ];
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishPlatform.Vocabulary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OneLexemePerWord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            // Склеиваем повторы: одно слово с разными частями речи становится одной записью.
            // Главная — с самым базовым уровнем CEFR (при равенстве — проверенная, затем более ранняя);
            // переводы и примеры остальных дописываются следом без повторов (до 10), категории объединяются.
            // Слова пользователей переходят на главную запись; если у пользователя уже есть главная — его копия
            // становится своим словом (переводы в ней свои, ничего не теряется).
            migrationBuilder.Sql("""
                CREATE TEMP TABLE lexeme_merge AS
                WITH ranked AS (
                    SELECT id, normalized_text,
                           row_number() OVER (
                               PARTITION BY normalized_text
                               ORDER BY CASE level WHEN 'A1' THEN 1 WHEN 'A2' THEN 2 WHEN 'B1' THEN 3 WHEN 'B2' THEN 4
                                                   WHEN 'C1' THEN 5 WHEN 'C2' THEN 6 ELSE 7 END,
                                        needs_review, created_at, id) AS rn
                    FROM vocabulary.lexemes)
                SELECT d.id AS dup_id, k.id AS keeper_id, d.rn
                FROM ranked d JOIN ranked k ON k.normalized_text = d.normalized_text AND k.rn = 1
                WHERE d.rn > 1;

                UPDATE vocabulary.lexemes k SET
                    translations = (
                        SELECT COALESCE(jsonb_agg(t.value ORDER BY t.ord), '[]'::jsonb) FROM (
                            SELECT u.value, u.ord FROM (
                                SELECT DISTINCT ON (lower(x.value->>'Text')) x.value, x.ord
                                FROM (
                                    SELECT e.value, e.ordinality AS ord FROM jsonb_array_elements(COALESCE(k.translations, '[]')) WITH ORDINALITY e
                                    UNION ALL
                                    SELECT e.value, 1000 * m.rn + e.ordinality FROM lexeme_merge m
                                        JOIN vocabulary.lexemes d ON d.id = m.dup_id,
                                        jsonb_array_elements(COALESCE(d.translations, '[]')) WITH ORDINALITY e
                                    WHERE m.keeper_id = k.id) x
                                ORDER BY lower(x.value->>'Text'), x.ord) u
                            ORDER BY u.ord LIMIT 10) t),
                    examples = (
                        SELECT COALESCE(jsonb_agg(t.value ORDER BY t.ord), '[]'::jsonb) FROM (
                            SELECT u.value, u.ord FROM (
                                SELECT DISTINCT ON (lower(x.value->>'Text')) x.value, x.ord
                                FROM (
                                    SELECT e.value, e.ordinality AS ord FROM jsonb_array_elements(COALESCE(k.examples, '[]')) WITH ORDINALITY e
                                    UNION ALL
                                    SELECT e.value, 1000 * m.rn + e.ordinality FROM lexeme_merge m
                                        JOIN vocabulary.lexemes d ON d.id = m.dup_id,
                                        jsonb_array_elements(COALESCE(d.examples, '[]')) WITH ORDINALITY e
                                    WHERE m.keeper_id = k.id) x
                                ORDER BY lower(x.value->>'Text'), x.ord) u
                            ORDER BY u.ord LIMIT 10) t),
                    transcription = COALESCE(k.transcription, (
                        SELECT d.transcription FROM lexeme_merge m JOIN vocabulary.lexemes d ON d.id = m.dup_id
                        WHERE m.keeper_id = k.id AND d.transcription IS NOT NULL ORDER BY m.rn LIMIT 1)),
                    category_ids = (
                        SELECT COALESCE(array_agg(DISTINCT c), '{}') FROM (
                            SELECT unnest(k.category_ids) AS c
                            UNION
                            SELECT unnest(d.category_ids) FROM lexeme_merge m JOIN vocabulary.lexemes d ON d.id = m.dup_id
                            WHERE m.keeper_id = k.id) cs),
                    updated_at = now()
                WHERE k.id IN (SELECT keeper_id FROM lexeme_merge);

                -- Строка поиска: текст и переводы через перевод строки, нижний регистр, ё → е (как TextNormalizer.SearchKey).
                UPDATE vocabulary.lexemes k SET search_text = replace(lower(k.text), 'ё', 'е') || COALESCE((
                        SELECT string_agg(E'\n' || replace(lower(trim(e.value->>'Text')), 'ё', 'е'), '' ORDER BY e.ordinality)
                        FROM jsonb_array_elements(k.translations) WITH ORDINALITY e), '')
                WHERE k.id IN (SELECT keeper_id FROM lexeme_merge);

                -- Слова пользователей: на главную запись, а если она у пользователя уже есть — остаётся своим словом.
                UPDATE vocabulary.user_words w SET lexeme_id = NULL
                FROM lexeme_merge m
                WHERE w.lexeme_id = m.dup_id
                  AND EXISTS (SELECT 1 FROM vocabulary.user_words o WHERE o.user_id = w.user_id AND o.lexeme_id = m.keeper_id);
                UPDATE vocabulary.user_words w SET lexeme_id = m.keeper_id
                FROM lexeme_merge m
                WHERE w.lexeme_id = m.dup_id;

                DELETE FROM vocabulary.lexemes WHERE id IN (SELECT dup_id FROM lexeme_merge);
                DROP TABLE lexeme_merge;
                """);

            migrationBuilder.DropIndex(
                name: "ix_lexemes_normalized_text_part_of_speech",
                schema: "vocabulary",
                table: "lexemes");

            migrationBuilder.CreateIndex(
                name: "ix_lexemes_normalized_text",
                schema: "vocabulary",
                table: "lexemes",
                column: "normalized_text",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_lexemes_normalized_text",
                schema: "vocabulary",
                table: "lexemes");

            migrationBuilder.CreateIndex(
                name: "ix_lexemes_normalized_text_part_of_speech",
                schema: "vocabulary",
                table: "lexemes",
                columns: new[] { "normalized_text", "part_of_speech" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }
    }
}

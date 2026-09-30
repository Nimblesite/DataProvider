using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Migration;
using Migration.SQLite;

namespace ICD10AM.Api.Tests;

/// <summary>
/// WebApplicationFactory for ICD10AM.Api e2e testing.
/// Creates isolated temp database with seeded test data.
/// Requires embedding service running at localhost:8000 for RAG tests.
/// </summary>
public sealed class ICD10AMApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath;
    private readonly HttpClient _embeddingClient;

    /// <summary>
    /// Creates a new instance with an isolated temp database.
    /// </summary>
    public ICD10AMApiFactory()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"icd10am_test_{Guid.NewGuid()}.db");
        _embeddingClient = new HttpClient { BaseAddress = new Uri("http://localhost:8000") };
    }

    /// <summary>
    /// Gets the database path for direct access in tests if needed.
    /// </summary>
    public string DbPath => _dbPath;

    /// <summary>
    /// Gets whether the embedding service is available.
    /// </summary>
    public bool EmbeddingServiceAvailable { get; private set; }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("DbPath", _dbPath);

        var apiAssembly = typeof(Program).Assembly;
        var contentRoot = Path.GetDirectoryName(apiAssembly.Location)!;
        builder.UseContentRoot(contentRoot);

        // Seed test data including embeddings from real model
        SeedTestData();
    }

    private void SeedTestData()
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();

        // Create schema from YAML
        var yamlPath = Path.Combine(
            Path.GetDirectoryName(typeof(Program).Assembly.Location)!,
            "icd10am-schema.yaml"
        );
        var schema = SchemaYamlSerializer.FromYamlFile(yamlPath);

        foreach (var table in schema.Tables)
        {
            var ddl = SqliteDdlGenerator.Generate(new CreateTableOperation(table));
            using var cmd = conn.CreateCommand();
            cmd.CommandText = ddl;
            cmd.ExecuteNonQuery();
        }

        // Check if embedding service is available and seed ICD-10-CM codes with real embeddings
        SeedIcd10CmCodesWithEmbeddings(conn);

        // Seed test chapters
        InsertChapter(
            conn,
            id: "ch-01",
            chapterNumber: "I",
            title: "Certain infectious and parasitic diseases",
            codeRangeStart: "A00",
            codeRangeEnd: "B99"
        );

        InsertChapter(
            conn,
            id: "ch-18",
            chapterNumber: "XVIII",
            title: "Symptoms, signs and abnormal clinical and laboratory findings",
            codeRangeStart: "R00",
            codeRangeEnd: "R99"
        );

        // Seed test blocks
        InsertBlock(
            conn,
            id: "blk-a00",
            chapterId: "ch-01",
            blockCode: "A00-A09",
            title: "Intestinal infectious diseases",
            codeRangeStart: "A00",
            codeRangeEnd: "A09"
        );

        InsertBlock(
            conn,
            id: "blk-r00",
            chapterId: "ch-18",
            blockCode: "R00-R09",
            title: "Symptoms and signs involving the circulatory and respiratory systems",
            codeRangeStart: "R00",
            codeRangeEnd: "R09"
        );

        // Seed test categories
        InsertCategory(
            conn,
            id: "cat-a00",
            blockId: "blk-a00",
            categoryCode: "A00",
            title: "Cholera"
        );
        InsertCategory(
            conn,
            id: "cat-r07",
            blockId: "blk-r00",
            categoryCode: "R07",
            title: "Pain in throat and chest"
        );

        // Seed test codes
        InsertCode(
            conn,
            id: "code-a000",
            categoryId: "cat-a00",
            code: "A00.0",
            shortDescription: "Cholera due to Vibrio cholerae 01, biovar cholerae",
            longDescription: "Cholera due to Vibrio cholerae 01, biovar cholerae - classical cholera"
        );

        InsertCode(
            conn,
            id: "code-r074",
            categoryId: "cat-r07",
            code: "R07.4",
            shortDescription: "Chest pain, unspecified",
            longDescription: "Chest pain, unspecified - pain in the chest region"
        );

        InsertCode(
            conn,
            id: "code-r0789",
            categoryId: "cat-r07",
            code: "R07.89",
            shortDescription: "Other chest pain",
            longDescription: "Other chest pain including pleuritic pain"
        );

        InsertCode(
            conn,
            id: "code-r0600",
            categoryId: "cat-r07",
            code: "R06.00",
            shortDescription: "Dyspnea, unspecified",
            longDescription: "Dyspnea unspecified - shortness of breath"
        );

        // Seed respiratory chapter, block, category, and codes for pneumonia tests
        InsertChapter(
            conn,
            id: "ch-10",
            chapterNumber: "X",
            title: "Diseases of the respiratory system",
            codeRangeStart: "J00",
            codeRangeEnd: "J99"
        );

        InsertBlock(
            conn,
            id: "blk-j09",
            chapterId: "ch-10",
            blockCode: "J09-J18",
            title: "Influenza and pneumonia",
            codeRangeStart: "J09",
            codeRangeEnd: "J18"
        );

        InsertBlock(
            conn,
            id: "blk-j20",
            chapterId: "ch-10",
            blockCode: "J20-J22",
            title: "Other acute lower respiratory infections",
            codeRangeStart: "J20",
            codeRangeEnd: "J22"
        );

        InsertCategory(
            conn,
            id: "cat-j18",
            blockId: "blk-j09",
            categoryCode: "J18",
            title: "Pneumonia, unspecified organism"
        );
        InsertCategory(
            conn,
            id: "cat-j20",
            blockId: "blk-j20",
            categoryCode: "J20",
            title: "Acute bronchitis"
        );

        InsertCode(
            conn,
            id: "code-j189",
            categoryId: "cat-j18",
            code: "J18.9",
            shortDescription: "Pneumonia, unspecified organism",
            longDescription: "Pneumonia unspecified organism - lung infection"
        );

        InsertCode(
            conn,
            id: "code-j209",
            categoryId: "cat-j20",
            code: "J20.9",
            shortDescription: "Acute bronchitis, unspecified",
            longDescription: "Acute bronchitis unspecified - inflammation of bronchial tubes"
        );

        // Seed endocrine chapter, block, category, and codes for diabetes tests
        InsertChapter(
            conn,
            id: "ch-04",
            chapterNumber: "IV",
            title: "Endocrine, nutritional and metabolic diseases",
            codeRangeStart: "E00",
            codeRangeEnd: "E90"
        );

        InsertBlock(
            conn,
            id: "blk-e10",
            chapterId: "ch-04",
            blockCode: "E10-E14",
            title: "Diabetes mellitus",
            codeRangeStart: "E10",
            codeRangeEnd: "E14"
        );

        InsertCategory(
            conn,
            id: "cat-e11",
            blockId: "blk-e10",
            categoryCode: "E11",
            title: "Type 2 diabetes mellitus"
        );

        InsertCode(
            conn,
            id: "code-e119",
            categoryId: "cat-e11",
            code: "E11.9",
            shortDescription: "Type 2 diabetes mellitus without complications",
            longDescription: "Type 2 diabetes mellitus without complications - adult onset diabetes"
        );

        // Seed ACHI test data
        InsertAchiBlock(
            conn,
            id: "achi-blk-1",
            blockNumber: "1820",
            title: "Procedures on heart",
            codeRangeStart: "38200",
            codeRangeEnd: "38999"
        );

        InsertAchiCode(
            conn,
            id: "achi-code-1",
            blockId: "achi-blk-1",
            code: "38497-00",
            shortDescription: "Coronary angiography",
            longDescription: "Coronary angiography - imaging of coronary arteries"
        );
    }

    private static void InsertChapter(
        SqliteConnection conn,
        string id,
        string chapterNumber,
        string title,
        string codeRangeStart,
        string codeRangeEnd
    )
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO icd10am_chapter (Id, ChapterNumber, Title, CodeRangeStart, CodeRangeEnd, LastUpdated, VersionId)
            VALUES (@id, @chapterNumber, @title, @codeRangeStart, @codeRangeEnd, datetime('now'), 1)
            """;
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@chapterNumber", chapterNumber);
        cmd.Parameters.AddWithValue("@title", title);
        cmd.Parameters.AddWithValue("@codeRangeStart", codeRangeStart);
        cmd.Parameters.AddWithValue("@codeRangeEnd", codeRangeEnd);
        cmd.ExecuteNonQuery();
    }

    private static void InsertBlock(
        SqliteConnection conn,
        string id,
        string chapterId,
        string blockCode,
        string title,
        string codeRangeStart,
        string codeRangeEnd
    )
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO icd10am_block (Id, ChapterId, BlockCode, Title, CodeRangeStart, CodeRangeEnd, LastUpdated, VersionId)
            VALUES (@id, @chapterId, @blockCode, @title, @codeRangeStart, @codeRangeEnd, datetime('now'), 1)
            """;
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@chapterId", chapterId);
        cmd.Parameters.AddWithValue("@blockCode", blockCode);
        cmd.Parameters.AddWithValue("@title", title);
        cmd.Parameters.AddWithValue("@codeRangeStart", codeRangeStart);
        cmd.Parameters.AddWithValue("@codeRangeEnd", codeRangeEnd);
        cmd.ExecuteNonQuery();
    }

    private static void InsertCategory(
        SqliteConnection conn,
        string id,
        string blockId,
        string categoryCode,
        string title
    )
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO icd10am_category (Id, BlockId, CategoryCode, Title, LastUpdated, VersionId)
            VALUES (@id, @blockId, @categoryCode, @title, datetime('now'), 1)
            """;
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@blockId", blockId);
        cmd.Parameters.AddWithValue("@categoryCode", categoryCode);
        cmd.Parameters.AddWithValue("@title", title);
        cmd.ExecuteNonQuery();
    }

    private static void InsertCode(
        SqliteConnection conn,
        string id,
        string categoryId,
        string code,
        string shortDescription,
        string longDescription
    )
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO icd10am_code (Id, CategoryId, Code, ShortDescription, LongDescription, InclusionTerms, ExclusionTerms, CodeAlso, CodeFirst, Billable, EffectiveFrom, EffectiveTo, Edition, LastUpdated, VersionId)
            VALUES (@id, @categoryId, @code, @shortDescription, @longDescription, '', '', '', '', 1, '2025-07-01', '', 13, datetime('now'), 1)
            """;
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@categoryId", categoryId);
        cmd.Parameters.AddWithValue("@code", code);
        cmd.Parameters.AddWithValue("@shortDescription", shortDescription);
        cmd.Parameters.AddWithValue("@longDescription", longDescription);
        cmd.ExecuteNonQuery();
    }

    private static void InsertAchiBlock(
        SqliteConnection conn,
        string id,
        string blockNumber,
        string title,
        string codeRangeStart,
        string codeRangeEnd
    )
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO achi_block (Id, BlockNumber, Title, CodeRangeStart, CodeRangeEnd, LastUpdated, VersionId)
            VALUES (@id, @blockNumber, @title, @codeRangeStart, @codeRangeEnd, datetime('now'), 1)
            """;
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@blockNumber", blockNumber);
        cmd.Parameters.AddWithValue("@title", title);
        cmd.Parameters.AddWithValue("@codeRangeStart", codeRangeStart);
        cmd.Parameters.AddWithValue("@codeRangeEnd", codeRangeEnd);
        cmd.ExecuteNonQuery();
    }

    private static void InsertAchiCode(
        SqliteConnection conn,
        string id,
        string blockId,
        string code,
        string shortDescription,
        string longDescription
    )
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO achi_code (Id, BlockId, Code, ShortDescription, LongDescription, Billable, EffectiveFrom, EffectiveTo, Edition, LastUpdated, VersionId)
            VALUES (@id, @blockId, @code, @shortDescription, @longDescription, 1, '2025-07-01', '', 13, datetime('now'), 1)
            """;
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@blockId", blockId);
        cmd.Parameters.AddWithValue("@code", code);
        cmd.Parameters.AddWithValue("@shortDescription", shortDescription);
        cmd.Parameters.AddWithValue("@longDescription", longDescription);
        cmd.ExecuteNonQuery();
    }

    private void SeedIcd10CmCodesWithEmbeddings(SqliteConnection conn)
    {
        // Test codes for semantic search - diverse medical conditions
        var testCodes = new (string id, string code, string shortDesc, string longDesc)[]
        {
            (
                "cm-r074",
                "R07.4",
                "Chest pain, unspecified",
                "Chest pain, unspecified - pain in the thoracic region"
            ),
            (
                "cm-r0789",
                "R07.89",
                "Other chest pain",
                "Other chest pain including pleuritic pain and intercostal pain"
            ),
            (
                "cm-r0600",
                "R06.00",
                "Dyspnea, unspecified",
                "Dyspnea unspecified - shortness of breath, difficulty breathing"
            ),
            (
                "cm-r0602",
                "R06.02",
                "Shortness of breath",
                "Shortness of breath - breathlessness, respiratory distress"
            ),
            (
                "cm-i2111",
                "I21.11",
                "ST elevation myocardial infarction involving RCA",
                "ST elevation myocardial infarction involving right coronary artery - heart attack"
            ),
            (
                "cm-i2119",
                "I21.19",
                "ST elevation MI involving other coronary artery",
                "ST elevation myocardial infarction involving other coronary artery of inferior wall"
            ),
            (
                "cm-j189",
                "J18.9",
                "Pneumonia, unspecified organism",
                "Pneumonia unspecified organism - lung infection"
            ),
            (
                "cm-j209",
                "J20.9",
                "Acute bronchitis, unspecified",
                "Acute bronchitis unspecified - inflammation of bronchial tubes"
            ),
            (
                "cm-e119",
                "E11.9",
                "Type 2 diabetes mellitus without complications",
                "Type 2 diabetes mellitus without complications - adult onset diabetes"
            ),
            (
                "cm-i10",
                "I10",
                "Essential hypertension",
                "Essential primary hypertension - high blood pressure"
            ),
            ("cm-m545", "M54.5", "Low back pain", "Low back pain - lumbago, lower back ache"),
            (
                "cm-g43909",
                "G43.909",
                "Migraine, unspecified",
                "Migraine unspecified not intractable without status migrainosus - severe headache"
            ),
        };

        // Try to get real embeddings from the embedding service
        try
        {
            var healthResponse = _embeddingClient.GetAsync("/health").Result;
            EmbeddingServiceAvailable = healthResponse.IsSuccessStatusCode;
        }
        catch
        {
            EmbeddingServiceAvailable = false;
        }

        foreach (var (id, code, shortDesc, longDesc) in testCodes)
        {
            InsertIcd10CmCode(
                conn,
                id: id,
                code: code,
                shortDescription: shortDesc,
                longDescription: longDesc
            );

            if (EmbeddingServiceAvailable)
            {
                var embeddingText = $"{code} {shortDesc} {longDesc}";
                var embedding = GetRealEmbedding(embeddingText);
                if (embedding is not null)
                {
                    InsertIcd10CmEmbedding(conn, codeId: id, embedding: embedding);
                }
            }
        }
    }

    private string? GetRealEmbedding(string text)
    {
        try
        {
            var response = _embeddingClient.PostAsJsonAsync("/embed", new { text }).Result;

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = response.Content.ReadAsStringAsync().Result;
            using var doc = JsonDocument.Parse(json);
            var embeddingArray = doc.RootElement.GetProperty("embedding");
            return embeddingArray.GetRawText();
        }
        catch
        {
            return null;
        }
    }

    private static void InsertIcd10CmCode(
        SqliteConnection conn,
        string id,
        string code,
        string shortDescription,
        string longDescription
    )
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO icd10cm_code (Id, CategoryId, Code, ShortDescription, LongDescription, InclusionTerms, ExclusionTerms, CodeAlso, CodeFirst, Billable, EffectiveFrom, EffectiveTo, Edition, LastUpdated, VersionId)
            VALUES (@id, '', @code, @shortDescription, @longDescription, '', '', '', '', 1, '2025-07-01', '', 2025, datetime('now'), 1)
            """;
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@code", code);
        cmd.Parameters.AddWithValue("@shortDescription", shortDescription);
        cmd.Parameters.AddWithValue("@longDescription", longDescription);
        cmd.ExecuteNonQuery();
    }

    private static void InsertIcd10CmEmbedding(
        SqliteConnection conn,
        string codeId,
        string embedding
    )
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO icd10cm_code_embedding (Id, CodeId, Embedding, EmbeddingModel, LastUpdated)
            VALUES (@id, @codeId, @embedding, 'MedEmbed-small-v0.1', datetime('now'))
            """;
        cmd.Parameters.AddWithValue("@id", $"emb-{codeId}");
        cmd.Parameters.AddWithValue("@codeId", codeId);
        cmd.Parameters.AddWithValue("@embedding", embedding);
        cmd.ExecuteNonQuery();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _embeddingClient.Dispose();

            try
            {
                if (File.Exists(_dbPath))
                {
                    File.Delete(_dbPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}

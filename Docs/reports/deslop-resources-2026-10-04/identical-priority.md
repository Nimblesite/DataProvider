# Identical Deslop clusters, descending weight [DEDUP-IDENTICAL-PRIORITY]

Snapshot: after-yaml-roundtrip.json, Deslop 0.36.0. Mass is an AST weight, not removable LOC. All findings remain visible; scan scope is unchanged.

| Cluster | Mass | Copies | First occurrence | Decision |
| --- | ---: | ---: | --- | --- |
| `23a3a7713d8217a8` | 200 | 2 | `Lql/LqlWebsite-Eleventy/eleventy.config.js:36` | Untyped JavaScript; no measured coverage. Safety gate excludes edits. |
| `f5a2c96daa6df44c` | 180 | 7 | `Sync/Nimblesite.Sync.Tests/LqlExpressionEvaluatorTests.cs:292` | Attempt tested; increased whole-corpus duplication. Reverted. |
| `b93c97b99eab1199` | 164 | 2 | `Lql/LqlWebsite-Eleventy/eleventy.config.js:8` | Untyped JavaScript; no measured coverage. Safety gate excludes edits. |
| `b251e06d0c92578d` | 129 | 4 | `Migration/Nimblesite.DataProvider.Migration.Tests/SchemaYamlSerializerTests.cs:95` | Attempt tested; increased whole-corpus duplication. Reverted. |
| `65614d022056d7b0` | 123 | 2 | `Sync/Nimblesite.Sync.Http.Tests/HttpSyncE2ETests.cs:240` | Two copies, nine lines; below substantial-logic gate. |
| `0e2e10a6357706bd` | 104 | 3 | `Migration/Nimblesite.DataProvider.Migration.Tests/MigrateSchemaTests.cs:120` | Shared table checks; full suite validating. |
| `9bd5b9cb1f8c4164` | 102 | 4 | `DataProvider/Nimblesite.DataProvider.Core/CodeGeneration/DataAccessGenerator.cs:297` | Three trivial indexing/comma lines; no meaningful extraction. |
| `aa2bf198a073db7e` | 96 | 3 | `Sync/Nimblesite.Sync.SQLite.Tests/SqliteExtensionIntegrationTests.cs:359` | Next: three subscription insert/retrieve/assert roundtrips. |
| `b6a89bc4e8cd51b4` | 96 | 4 | `Migration/Nimblesite.DataProvider.Migration.Tests/RlsLqlExhaustiveTests.cs:99` | Two assertion lines; retain security checks and avoid trivial helper. |
| `e2dbd7b3272ff411` | 94 | 3 | `Sync/Nimblesite.Sync.Tests/SyncCoordinatorTests.cs:752` | Next: reuse existing TestDb operation parser. |
| `31e1e99b9a7a520d` | 93 | 2 | `DataProvider/DataProvider/PostgresCli.cs:969` | CLI has no measured coverage; no edits. |
| `726ecbcfa625825b` | 90 | 3 | `Migration/Nimblesite.DataProvider.Migration.Tests/SchemaYamlSerializerTests.cs:95` | Pending review |
| `79713bf4964420d6` | 90 | 2 | `Migration/Nimblesite.DataProvider.Migration.Tests/SqliteMigrationTests.cs:110` | Pending review |
| `dd9add425bedb1bc` | 84 | 3 | `Lql/lql-lsp-rust/crates/lql-analyzer/src/completion.rs:361` | Pending review |
| `15ca1dd359469bc6` | 82 | 3 | `Lql/Nimblesite.Lql.Postgres/PostgreSqlContext.cs:112` | Pending review |
| `fedeebeb0b321e11` | 82 | 2 | `Sync/Nimblesite.Sync.Tests/SyncCoordinatorTests.cs:688` | Pending review |
| `5249d0f3b5c4a207` | 77 | 2 | `Migration/Nimblesite.DataProvider.Migration.Tests/SchemaDiffTests.Destructive.cs:57` | Pending review |
| `bce033f2d0d5a1b3` | 77 | 2 | `Sync/Nimblesite.Sync.SQLite.Tests/SpecComplianceTests.cs:820` | Pending review |
| `136f057fc39d8134` | 76 | 3 | `DataProvider/Nimblesite.DataProvider.Parsing.Tests/SqlParserContractTests.cs:205` | Pending review |
| `617ea029d204622f` | 76 | 2 | `DataProvider/Nimblesite.DataProvider.Core/CodeGeneration/DataAccessGenerator.cs:634` | Pending review |
| `6a3df01b670de34f` | 76 | 3 | `Migration/Nimblesite.DataProvider.Migration.Tests/PostgresLqlOnlyE2ETests.cs:369` | Pending review |
| `52ff0aa068e19a21` | 74 | 3 | `DataProvider/DataProvider/SqliteProgram.cs:511` | Pending review |
| `fe36c7be676f7ffc` | 74 | 3 | `DataProvider/Nimblesite.DataProvider.Tests/LqlSqliteE2ETests.cs:341` | Pending review |
| `3e57a67ad5c33bff` | 73 | 2 | `DataProvider/Nimblesite.DataProvider.Core/CodeGeneration/DataAccessGenerator.cs:191` | Pending review |
| `04b13df603b48d45` | 72 | 2 | `Migration/Nimblesite.DataProvider.Migration.Tests/SqliteMigrationTests.cs:149` | Pending review |
| `38a7a4ea3fe46bad` | 72 | 3 | `Lql/Nimblesite.Lql.Postgres/PostgreSqlContext.cs:468` | Pending review |
| `95bb0b3251dbea36` | 72 | 3 | `DataProvider/DataProvider/SqliteProgram.cs:524` | Pending review |
| `e2226c806f89da54` | 71 | 2 | `Migration/Nimblesite.DataProvider.Migration.Tests/SqliteMigrationTests.cs:188` | Pending review |
| `4e7e61a46e685700` | 70 | 3 | `Lql/Nimblesite.Lql.Postgres/PostgreSqlContext.cs:119` | Pending review |
| `3a1dfc2b0eba86ed` | 69 | 2 | `DataProvider/Nimblesite.DataProvider.Core/CodeGeneration/DataAccessGenerator.cs:635` | Pending review |
| `7fcd5f0f73d744a0` | 69 | 2 | `DataProvider/Nimblesite.DataProvider.Core/CodeGeneration/CodeGenerationConfig.cs:154` | Pending review |
| `3f7cb94e24402922` | 68 | 3 | `Sync/Nimblesite.Sync.SQLite.Tests/SchemaAndTriggerTests.cs:840` | Pending review |
| `c4517f943ea6b55e` | 68 | 3 | `Migration/Nimblesite.DataProvider.Migration.Tests/RlsLqlExhaustiveTests.cs:168` | Pending review |
| `d1d2a34bddc08da3` | 68 | 3 | `DataProvider/Nimblesite.DataProvider.Tests/BulkOperationsTests.cs:159` | Pending review |
| `de05f8a408376f71` | 68 | 2 | `DataProvider/Nimblesite.DataProvider.Core/CodeGeneration/ModelGenerator.cs:58` | Pending review |
| `d5615919d5828e07` | 67 | 2 | `Sync/Nimblesite.Sync.SQLite.Tests/SpecComplianceTests.cs:457` | Pending review |
| `c1bca68d1871ddee` | 66 | 2 | `DataProvider/DataProvider/PostgresCli.cs:711` | Pending review |
| `a5a79e76698025f7` | 65 | 2 | `DataProvider/Nimblesite.DataProvider.Core/CodeGeneration/DataAccessGenerator.cs:342` | Pending review |
| `bcb0894356219bfb` | 65 | 2 | `DataProvider/Nimblesite.DataProvider.Tests/AdoNetDatabaseEffectsTests.cs:87` | Pending review |
| `3e1821752b12f0af` | 64 | 3 | `DataProvider/Nimblesite.DataProvider.Parsing.Tests/SqlParserContractTests.cs:179` | Pending review |
| `3ad5b90702aeca4e` | 62 | 3 | `Sync/Nimblesite.Sync.SQLite.Tests/SpecComplianceTests.cs:81` | Pending review |
| `5cb6e38e6a48995f` | 62 | 2 | `Lql/Nimblesite.Lql.Browser/Services/FileDialogService.cs:35` | Pending review |
| `a31e8fce563b8403` | 62 | 3 | `Migration/Nimblesite.DataProvider.Migration.Core/RlsPolicyPredicates.cs:138` | Pending review |
| `b1f3ec6686b12ddd` | 62 | 3 | `Migration/Nimblesite.DataProvider.Migration.Tests/SchemaDiffSupportTests.cs:87` | Pending review |
| `46b2f477cfc15b95` | 61 | 2 | `Sync/Nimblesite.Sync.SQLite.Tests/TombstoneIntegrationTests.cs:367` | Pending review |
| `5c818d55a8c0dbfd` | 61 | 2 | `Migration/Nimblesite.DataProvider.Migration.Tests/PostgresUniqueConstraintIssue55E2ETests.cs:112` | Pending review |
| `2947e6e2bfda0530` | 60 | 3 | `Migration/Nimblesite.DataProvider.Migration.Tests/PostgresMigrationTests.cs:172` | Pending review |
| `52bde7f1b778a871` | 60 | 2 | `Sync/Nimblesite.Sync.Http.Tests/HttpSyncE2ETests.cs:15` | Pending review |
| `636930235c288c0a` | 60 | 2 | `Migration/Nimblesite.DataProvider.Migration.Tests/PostgresUpgradeBlockersTests.cs:40` | Pending review |
| `75c4345f9f37d8a0` | 60 | 2 | `Migration/Nimblesite.DataProvider.Migration.Tests/MigrationCornerCaseTests.cs:775` | Pending review |
| `e6289cd8359cbd19` | 60 | 3 | `Lql/lql-lsp-rust/crates/lql-lsp/tests/lsp_protocol.rs:484` | Pending review |
| `7f9e196dcbb0b705` | 58 | 2 | `Sync/Nimblesite.Sync.Tests/ChangeApplierTests.cs:67` | Pending review |
| `d4d653f3c60c2ae6` | 58 | 2 | `Sync/Nimblesite.Sync.Postgres/PostgresSyncLogRepository.cs:166` | Pending review |
| `dd5b9eaa1d6e5c30` | 56 | 2 | `Migration/Nimblesite.DataProvider.Migration.Tests/SchemaIntegrityVerifierFunctionDriftTests.cs:77` | Pending review |
| `00ab33eae54abd9a` | 55 | 2 | `Lql/Nimblesite.Lql.Browser/Views/MainWindow.axaml.cs:96` | Pending review |
| `0ad1d0f13abca7e7` | 55 | 2 | `Migration/Nimblesite.DataProvider.Migration.SQLite/SqliteSchemaNormalizer.cs:14` | Pending review |
| `cd3ae70fc31516b6` | 54 | 2 | `Sync/Nimblesite.Sync.SQLite.Tests/SchemaAndTriggerTests.cs:827` | Pending review |
| `e565a1a0ddc8744d` | 53 | 2 | `Sync/Nimblesite.Sync.Tests/SyncErrorTests.cs:25` | Pending review |
| `35e392d6b5619836` | 52 | 2 | `DataProvider/Nimblesite.DataProvider.Tests/LqlSqliteE2ETests.cs:341` | Pending review |
| `80e121efbffe29fe` | 52 | 2 | `Lql/lql-lsp-rust/crates/lql-lsp/src/main.rs:1695` | Pending review |
| `32aed8ea9416396f` | 50 | 2 | `DataProvider/Nimblesite.DataProvider.Tests/SqlQueryableTests.cs:72` | Pending review |
| `57c8a8875d989c62` | 50 | 2 | `Migration/Nimblesite.DataProvider.Migration.Tests/SchemaDiffRlsTests.cs:54` | Pending review |
| `7bba45a353278de6` | 50 | 2 | `Migration/Nimblesite.DataProvider.Migration.Core/SchemaYamlSerializer.cs:61` | Pending review |
| `4b568b3511d19034` | 49 | 2 | `Migration/Nimblesite.DataProvider.Migration.Tests/RlsLqlExhaustiveTests.cs:168` | Pending review |
| `dbda781cde931dcd` | 49 | 2 | `Migration/Nimblesite.DataProvider.Migration.Postgres/PostgresSchemaInspector.cs:449` | Pending review |
| `fd00be5112ec7f1e` | 49 | 2 | `Sync/Nimblesite.Sync.Tests/LqlMappingCornerCaseTests.cs:241` | Pending review |
| `6d7a54bed46decb5` | 48 | 2 | `Lql/lql-lsp-rust/crates/lql-lsp/src/main.rs:1221` | Pending review |
| `83697a43812495df` | 48 | 2 | `Migration/Nimblesite.DataProvider.Migration.Tests/SchemaDiffSupportTests.cs:140` | Pending review |
| `8e29ddf7bb4f6dcd` | 48 | 2 | `Migration/Nimblesite.DataProvider.Migration.Postgres/PostgresDdlGenerator.cs:238` | Pending review |
| `9bfd72d05b8c664e` | 47 | 2 | `Migration/Nimblesite.DataProvider.Migration.Tests/SchemaDiffRlsTests.cs:117` | Pending review |
| `dc9f82e307a00105` | 45 | 2 | `DataProvider/Nimblesite.DataProvider.Tests/LqlSqliteE2ETests.cs:344` | Pending review |
| `bd43bb95fc032e97` | 43 | 2 | `Migration/Nimblesite.DataProvider.Migration.Tests/PostgresFunctionBodyLqlE2ETests.cs:139` | Pending review |
| `cf62f576e3eb1d27` | 42 | 2 | `Sync/Nimblesite.Sync.Core/LqlExpressionEvaluator.cs:39` | Pending review |
| `5acc443018e5c430` | 41 | 2 | `Lql/Lql/TranspileRunner.cs:148` | Pending review |
| `7b9750b77e753648` | 40 | 2 | `Sync/Nimblesite.Sync.Tests/MappingConfigParserTests.cs:82` | Pending review |
| `df98751c80eb27fc` | 40 | 2 | `Migration/Nimblesite.DataProvider.Migration.Tests/SchemaDiffSupportTests.cs:178` | Pending review |
| `0c66dade6e0fc6b3` | 39 | 2 | `Migration/Nimblesite.DataProvider.Migration.Tests/SchemaDiffTests.Destructive.cs:20` | Pending review |
| `96847da7a777258c` | 39 | 2 | `Sync/Nimblesite.Sync.Http.Tests/CrossDatabaseSyncBatchTests.cs:67` | Pending review |
| `bda29fd41644856d` | 39 | 2 | `DataProvider/Nimblesite.DataProvider.SQLite/SqliteCodeGenerator.cs:486` | Pending review |
| `6cd972a554e4b752` | 38 | 2 | `Migration/Nimblesite.DataProvider.Migration.Tests/PostgresTestDb.cs:99` | Pending review |
| `a6fc7476b26aa679` | 38 | 2 | `DataProvider/Nimblesite.DataProvider.Core/CodeGeneration/DataAccessGenerator.cs:737` | Pending review |
| `f2b4996d3fd2cd22` | 38 | 2 | `Sync/Nimblesite.Sync.SQLite.Tests/ChangeApplierIntegrationTests.cs:59` | Pending review |
| `1c97f6dcbc1d7b7f` | 37 | 2 | `Migration/Nimblesite.DataProvider.Migration.Tests/NativeAotMigrateSmokeTests.cs:56` | Pending review |
| `747e6823fc18a52d` | 37 | 2 | `Sync/Nimblesite.Sync.Integration.Tests/HttpMappingSyncTests.Mappings.cs:275` | Pending review |
| `86b1ff9d86e02398` | 37 | 2 | `Sync/Nimblesite.Sync.SQLite/MappingStateRepository.cs:33` | Pending review |
| `ec8770ca7e201ad1` | 37 | 2 | `DataProvider/Nimblesite.DataProvider.Tests/LqlSqliteE2ETests.cs:390` | Pending review |
| `555ad30bf34efca0` | 36 | 2 | `Lql/lql-lsp-rust/crates/lql-lsp/src/main.rs:319` | Pending review |
| `5e27b625e61d2905` | 36 | 2 | `Sync/Nimblesite.Sync.Tests/SubscriptionManagerTests.cs:384` | Pending review |
| `aba42ce1409c155a` | 36 | 2 | `Sync/Nimblesite.Sync.Tests/SyncCoordinatorTests.cs:133` | Pending review |
| `02c6a6361946440f` | 35 | 2 | `Migration/Nimblesite.DataProvider.Migration.Postgres/PostgresSchemaInspector.cs:625` | Pending review |
| `2889bc6207d2c487` | 35 | 2 | `Migration/Nimblesite.DataProvider.Migration.Tests/PostgresRlsE2ETests.cs:343` | Pending review |
| `5401df209e3637f7` | 35 | 2 | `Lql/lql-lsp-rust/crates/lql-lsp/src/ai.rs:622` | Pending review |
| `b90fbb9debdb8280` | 35 | 2 | `DataProvider/Nimblesite.DataProvider.Tests/QueryBuilderDatabaseContainers.cs:34` | Pending review |
| `dc627db320f89f60` | 35 | 2 | `Lql/LqlExtension/src/test/suite/lsp-protocol.test.ts:343` | Pending review |
| `c6c3422f2224e821` | 34 | 2 | `Sync/Nimblesite.Sync.SQLite.Tests/SpecConformanceTests.cs:695` | Pending review |
| `d6dd086f3cdf81b2` | 34 | 2 | `DataProvider/Nimblesite.DataProvider.Example.Tests/GeneratedOperationsPlatformTests.RelationsAssertions.cs:11` | Pending review |
| `3792ae8aace700b9` | 33 | 2 | `Migration/Nimblesite.DataProvider.Migration.Postgres/PostgresSchemaInspector.cs:99` | Pending review |
| `99384bcded5a604f` | 33 | 2 | `DataProvider/Nimblesite.DataProvider.Tests/LqlSqliteE2ETests.cs:384` | Pending review |
| `d085a105b4135026` | 33 | 2 | `Lql/Nimblesite.Lql.Core/Parsing/LqlCodeParser.cs:130` | Pending review |
| `f64b0119c3f0c1d3` | 33 | 2 | `Sync/Nimblesite.Sync.SQLite.Tests/SchemaAndTriggerTests.cs:244` | Pending review |
| `03851f9598d6ca57` | 32 | 2 | `Lql/LqlExtension/src/test/suite/vscode-e2e.test.ts:208` | Pending review |
| `739bb4265980bef2` | 32 | 2 | `Other/Nimblesite.Sql.Model/SelectStatementLinqExtensions.cs:406` | Pending review |
| `9a045a7811a22717` | 32 | 2 | `Migration/Nimblesite.DataProvider.Migration.Tests/SchemaDiffRlsTests.cs:151` | Pending review |
| `dd6c001d2d232433` | 32 | 2 | `Sync/Nimblesite.Sync.Core/MappingEngine.cs:297` | Pending review |
| `ea94a1976d0fca48` | 32 | 2 | `Sync/Nimblesite.Sync.SQLite/ChangeApplierSQLite.cs:72` | Pending review |
| `f8fe7d5c29451f42` | 32 | 2 | `Sync/Nimblesite.Sync.Http/SyncEndpointExtensions.cs:40` | Pending review |
| `80f900c0c6c0f5b4` | 31 | 2 | `Migration/Nimblesite.DataProvider.Migration.Tests/PostgresRlsE2ETests.cs:227` | Pending review |
| `e043b3a823f84b51` | 31 | 2 | `DataProvider/Nimblesite.DataProvider.Tests/DbTransactTests.cs:34` | Pending review |
| `e171c6c1f22a9864` | 31 | 2 | `Lql/lql-lsp-rust/crates/lql-analyzer/src/completion.rs:521` | Pending review |
| `fa5169364e65d864` | 31 | 2 | `Migration/Nimblesite.DataProvider.Migration.Tests/PostgresGrantRunAsE2ETests.cs:20` | Pending review |
| `01ef9a1175a36382` | 30 | 2 | `DataProvider/Nimblesite.DataProvider.Tests/JoinGraphTests.cs:28` | Pending review |
| `5cb38e20f78dd363` | 30 | 2 | `Migration/Nimblesite.DataProvider.Migration.Tests/PostgresMigrationTests.cs:1028` | Pending review |
| `71f4f2b5fbb60ce7` | 30 | 2 | `Migration/Nimblesite.DataProvider.Migration.Tests/SchemaDiffUniqueConstraintIssue55Tests.cs:45` | Pending review |
| `ac978bfb3a116859` | 30 | 2 | `Migration/DataProviderMigrate/Program.Export.cs:65` | Pending review |
| `ca5789d26f3c2e6e` | 30 | 2 | `DataProvider/Nimblesite.DataProvider.Tests/Fakes/FakeDataReader.cs:61` | Pending review |

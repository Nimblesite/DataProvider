global using System;
global using System.Collections.Immutable;
global using Generated;
global using Microsoft.Data.Sqlite;
global using Microsoft.Extensions.Logging;
global using Outcome;
global using GetAchiBlocksError = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetAchiBlocks>,
    Selecta.SqlError
>.Error<System.Collections.Immutable.ImmutableList<Generated.GetAchiBlocks>, Selecta.SqlError>;
// GetAchiBlocks query result type aliases
global using GetAchiBlocksOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetAchiBlocks>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetAchiBlocks>, Selecta.SqlError>;
global using GetAchiCodeByCodeError = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetAchiCodeByCode>,
    Selecta.SqlError
>.Error<System.Collections.Immutable.ImmutableList<Generated.GetAchiCodeByCode>, Selecta.SqlError>;
// GetAchiCodeByCode query result type aliases
global using GetAchiCodeByCodeOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetAchiCodeByCode>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetAchiCodeByCode>, Selecta.SqlError>;
global using GetAchiCodesByBlockError = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetAchiCodesByBlock>,
    Selecta.SqlError
>.Error<
    System.Collections.Immutable.ImmutableList<Generated.GetAchiCodesByBlock>,
    Selecta.SqlError
>;
// GetAchiCodesByBlock query result type aliases
global using GetAchiCodesByBlockOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetAchiCodesByBlock>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetAchiCodesByBlock>, Selecta.SqlError>;
global using GetAllCodeEmbeddingsError = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetAllCodeEmbeddings>,
    Selecta.SqlError
>.Error<
    System.Collections.Immutable.ImmutableList<Generated.GetAllCodeEmbeddings>,
    Selecta.SqlError
>;
// GetAllCodeEmbeddings query result type aliases
global using GetAllCodeEmbeddingsOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetAllCodeEmbeddings>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetAllCodeEmbeddings>, Selecta.SqlError>;
global using GetBlocksByChapterError = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetBlocksByChapter>,
    Selecta.SqlError
>.Error<System.Collections.Immutable.ImmutableList<Generated.GetBlocksByChapter>, Selecta.SqlError>;
// GetBlocksByChapter query result type aliases
global using GetBlocksByChapterOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetBlocksByChapter>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetBlocksByChapter>, Selecta.SqlError>;
global using GetCategoriesByBlockError = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetCategoriesByBlock>,
    Selecta.SqlError
>.Error<
    System.Collections.Immutable.ImmutableList<Generated.GetCategoriesByBlock>,
    Selecta.SqlError
>;
// GetCategoriesByBlock query result type aliases
global using GetCategoriesByBlockOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetCategoriesByBlock>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetCategoriesByBlock>, Selecta.SqlError>;
global using GetChaptersError = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetChapters>,
    Selecta.SqlError
>.Error<System.Collections.Immutable.ImmutableList<Generated.GetChapters>, Selecta.SqlError>;
// GetChapters query result type aliases
global using GetChaptersOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetChapters>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetChapters>, Selecta.SqlError>;
global using GetCodeByCodeError = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetCodeByCode>,
    Selecta.SqlError
>.Error<System.Collections.Immutable.ImmutableList<Generated.GetCodeByCode>, Selecta.SqlError>;
// GetCodeByCode query result type aliases
global using GetCodeByCodeOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetCodeByCode>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetCodeByCode>, Selecta.SqlError>;
global using GetCodesByCategoryError = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetCodesByCategory>,
    Selecta.SqlError
>.Error<System.Collections.Immutable.ImmutableList<Generated.GetCodesByCategory>, Selecta.SqlError>;
// GetCodesByCategory query result type aliases
global using GetCodesByCategoryOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetCodesByCategory>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetCodesByCategory>, Selecta.SqlError>;

global using ApiErrorResponseError = Outcome.HttpError<ErrorResponse>.ErrorResponseError;
global using ApiExceptionError = Outcome.HttpError<ErrorResponse>.ExceptionError;
global using ChaptersErrorResponse = Outcome.Result<
    System.Collections.Immutable.ImmutableArray<Chapter>,
    Outcome.HttpError<ErrorResponse>
>.Error<System.Collections.Immutable.ImmutableArray<Chapter>, Outcome.HttpError<ErrorResponse>>;
global using CodeErrorResponse = Outcome.Result<Icd10Code, Outcome.HttpError<ErrorResponse>>.Error<
    Icd10Code,
    Outcome.HttpError<ErrorResponse>
>;
global using CodesErrorResponse = Outcome.Result<
    System.Collections.Immutable.ImmutableArray<Icd10Code>,
    Outcome.HttpError<ErrorResponse>
>.Error<System.Collections.Immutable.ImmutableArray<Icd10Code>, Outcome.HttpError<ErrorResponse>>;
global using HealthErrorResponse = Outcome.Result<
    HealthResponse,
    Outcome.HttpError<ErrorResponse>
>.Error<HealthResponse, Outcome.HttpError<ErrorResponse>>;
global using OkChapters = Outcome.Result<
    System.Collections.Immutable.ImmutableArray<Chapter>,
    Outcome.HttpError<ErrorResponse>
>.Ok<System.Collections.Immutable.ImmutableArray<Chapter>, Outcome.HttpError<ErrorResponse>>;
global using OkCode = Outcome.Result<Icd10Code, Outcome.HttpError<ErrorResponse>>.Ok<
    Icd10Code,
    Outcome.HttpError<ErrorResponse>
>;
global using OkCodes = Outcome.Result<
    System.Collections.Immutable.ImmutableArray<Icd10Code>,
    Outcome.HttpError<ErrorResponse>
>.Ok<System.Collections.Immutable.ImmutableArray<Icd10Code>, Outcome.HttpError<ErrorResponse>>;
global using OkHealth = Outcome.Result<HealthResponse, Outcome.HttpError<ErrorResponse>>.Ok<
    HealthResponse,
    Outcome.HttpError<ErrorResponse>
>;
global using OkSearch = Outcome.Result<SearchResponse, Outcome.HttpError<ErrorResponse>>.Ok<
    SearchResponse,
    Outcome.HttpError<ErrorResponse>
>;
global using SearchErrorResponse = Outcome.Result<
    SearchResponse,
    Outcome.HttpError<ErrorResponse>
>.Error<SearchResponse, Outcome.HttpError<ErrorResponse>>;

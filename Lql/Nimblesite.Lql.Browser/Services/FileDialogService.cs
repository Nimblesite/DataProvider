using Avalonia.Platform.Storage;
using Outcome;

namespace Nimblesite.Lql.Browser.Services;

/// <summary>
/// Service for handling file dialogs
/// </summary>
public static class FileDialogService
{
    /// <summary>
    /// Shows a database file picker dialog
    /// </summary>
    /// <param name="storageProvider">Storage provider from the main window</param>
    /// <returns>Result containing selected file path or error message</returns>
    public static async Task<Result<string, string>> ShowDatabasePickerAsync(
        IStorageProvider storageProvider
    )
    {
        try
        {
            var dialog = new FilePickerOpenOptions
            {
                Title = "Select SQLite Database",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("SQLite Database")
                    {
                        Patterns = ["*.db", "*.sqlite", "*.sqlite3"],
                    },
                ],
            };

            var files = await storageProvider.OpenFilePickerAsync(dialog);
            if (files.Count > 0)
            {
                return new Result<string, string>.Ok<string, string>(files[0].Path.LocalPath);
            }

            return new Result<string, string>.Error<string, string>("No file selected");
        }
        catch (Exception ex)
        {
            return new Result<string, string>.Error<string, string>(
                $"Error selecting database: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// Shows a file open dialog for LQL/SQL files
    /// </summary>
    /// <param name="storageProvider">Storage provider from the main window</param>
    /// <returns>Result containing selected file path or error message</returns>
    public static async Task<Result<string, string>> ShowOpenFileDialogAsync(
        IStorageProvider storageProvider
    )
    {
        try
        {
            var dialog = new FilePickerOpenOptions
            {
                Title = "Open File",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("LQL Files") { Patterns = ["*.lql"] },
                    new FilePickerFileType("SQL Files") { Patterns = ["*.sql"] },
                ],
            };

            var files = await storageProvider.OpenFilePickerAsync(dialog);
            if (files.Count > 0)
            {
                return new Result<string, string>.Ok<string, string>(files[0].Path.LocalPath);
            }

            return new Result<string, string>.Error<string, string>("No file selected");
        }
        catch (Exception ex)
        {
            return new Result<string, string>.Error<string, string>(
                $"Error opening file: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// Shows a save file dialog
    /// </summary>
    /// <param name="storageProvider">Storage provider from the main window</param>
    /// <param name="suggestedFileName">Suggested file name</param>
    /// <returns>Result containing selected file path or error message</returns>
    public static async Task<Result<string, string>> ShowSaveFileDialogAsync(
        IStorageProvider storageProvider,
        string suggestedFileName
    )
    {
        var dialog = new FilePickerSaveOptions
        {
            Title = "Save File As",
            SuggestedFileName = suggestedFileName,
            FileTypeChoices =
            [
                new FilePickerFileType("LQL Files") { Patterns = ["*.lql"] },
                new FilePickerFileType("SQL Files") { Patterns = ["*.sql"] },
            ],
        };

        return await RunSavePickerAsync(
            storageProvider: storageProvider,
            dialog: dialog,
            errorContext: "Error saving file"
        );
    }

    /// <summary>
    /// Shows an export CSV dialog
    /// </summary>
    /// <param name="storageProvider">Storage provider from the main window</param>
    /// <returns>Result containing selected file path or error message</returns>
    public static async Task<Result<string, string>> ShowExportCsvDialogAsync(
        IStorageProvider storageProvider
    )
    {
        var dialog = new FilePickerSaveOptions
        {
            Title = "Export CSV",
            SuggestedFileName = "export.csv",
            FileTypeChoices = [new FilePickerFileType("CSV Files") { Patterns = ["*.csv"] }],
        };

        return await RunSavePickerAsync(
            storageProvider: storageProvider,
            dialog: dialog,
            errorContext: "Error selecting export file"
        );
    }

    /// <summary>
    /// Shows an export JSON dialog
    /// </summary>
    /// <param name="storageProvider">Storage provider from the main window</param>
    /// <returns>Result containing selected file path or error message</returns>
    public static async Task<Result<string, string>> ShowExportJsonDialogAsync(
        IStorageProvider storageProvider
    )
    {
        var dialog = new FilePickerSaveOptions
        {
            Title = "Export JSON",
            SuggestedFileName = "export.json",
            FileTypeChoices = [new FilePickerFileType("JSON Files") { Patterns = ["*.json"] }],
        };

        return await RunSavePickerAsync(
            storageProvider: storageProvider,
            dialog: dialog,
            errorContext: "Error selecting export file"
        );
    }

    private static async Task<Result<string, string>> RunSavePickerAsync(
        IStorageProvider storageProvider,
        FilePickerSaveOptions dialog,
        string errorContext
    )
    {
        try
        {
            var file = await storageProvider.SaveFilePickerAsync(dialog);
            return file != null
                ? new Result<string, string>.Ok<string, string>(file.Path.LocalPath)
                : new Result<string, string>.Error<string, string>("No file selected");
        }
        catch (Exception ex)
        {
            return new Result<string, string>.Error<string, string>(
                $"{errorContext}: {ex.Message}"
            );
        }
    }
}

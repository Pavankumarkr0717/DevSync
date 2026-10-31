using DevSync.Domain.Common;

namespace DevSync.Domain.Files;

/// <summary>Business-rule failures for project files (spec 003 FR-028). Invalid names carry the broken rule instead.</summary>
public static class FileErrors
{
    public const string InvalidNameCode = "invalid_name";

    public static readonly DomainError ItemNotFound = new("item_not_found", "File or folder not found.");
    public static readonly DomainError ParentNotFound = new("parent_not_found", "The target folder was not found.");
    public static readonly DomainError NameTaken = new("name_taken", "An item with this name already exists here.");
    public static readonly DomainError UnsupportedExtension = new(
        "unsupported_extension", "Supported: .cs .js .ts .tsx .jsx .json .html .css .py .java .md");
    public static readonly DomainError TooDeep = new("too_deep", $"Folders can be nested at most {ProjectLimits.MaxDepth} levels.");
    public static readonly DomainError PathTooLong = new(
        "path_too_long", "The full path would be longer than 1,024 characters.");
    public static readonly DomainError TooManyItems = new(
        "too_many_items", "This workspace has reached 5,000 files and folders.");
    public static readonly DomainError InvalidMove = new("invalid_move", "A folder cannot be moved into itself or one of its folders.");
    public static readonly DomainError InvalidContent = new("invalid_content", "The file contains characters that can't be saved.");
    public static readonly DomainError ContentTooLarge = new("content_too_large", "The file is larger than 1 MiB.");

    public static DomainError InvalidName(string message) => new(InvalidNameCode, message);
}

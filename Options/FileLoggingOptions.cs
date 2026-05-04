using Microsoft.Extensions.Logging;

namespace TopMail.Rest.Options;

public class FileLoggingOptions
{
    public const string SectionName = "FileLogging";

    public bool Enabled { get; set; } = true;
    public string DirectoryPath { get; set; } = "c:/temp/topmail-logs";
    public string FileNamePrefix { get; set; } = "topmail";
    public string FileNameDateFormat { get; set; } = "yyyyMMdd";
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
}

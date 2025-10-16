using System.Text.RegularExpressions;

public static class StringExtensions
{
    /// <summary>
    /// Sanitizes a string to be used as a filename by removing invalid characters
    /// </summary>
    public static string SanitizeFileName(this string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return "unnamed";

        // Remove or replace invalid filename characters
        string sanitized = Regex.Replace(fileName, @"[<>:""/\\|?*]", "_");
        
        // Remove leading/trailing spaces and dots
        sanitized = sanitized.Trim(' ', '.');
        
        // Ensure it's not empty after sanitization
        if (string.IsNullOrEmpty(sanitized))
            return "unnamed";

        return sanitized;
    }
}

using System.Text.RegularExpressions;

namespace TIRConnector.API.Validation;

/// <summary>
/// Validates SQL queries to ensure only read-only SELECT statements are executed.
/// Prevents DML, DDL, DCL operations and SQL injection attacks.
///
/// Uses a multi-step approach:
/// 1. Strips string literals, comments, and quoted identifiers to prevent
///    false positives (e.g., WHERE col = 'DELETE this') and obfuscation attacks
/// 2. Verifies the query starts with an allowed command (SELECT) or WITH (for CTEs)
/// 3. Blocks multiple statements (semicolons)
/// 4. Checks for dangerous keywords using word-boundary matching,
///    which correctly allows keywords as part of identifiers
///    (e.g., "delete_date", "updated_at", "created_by")
/// </summary>
public static class SqlQueryValidator
{
    /// <summary>
    /// Regex to match and strip "noise" tokens (string literals, comments, quoted identifiers).
    /// Processed left-to-right so overlapping constructs are handled correctly:
    /// e.g., a string containing -- is consumed as a string, not as a comment start.
    /// </summary>
    private static readonly Regex NoiseTokenRegex = new(
        @"'(?:[^']|'')*'" +         // single-quoted strings (handles escaped quotes: 'it''s')
        @"|""(?:[^""]|"""")*""" +    // double-quoted identifiers
        @"|/\*[\s\S]*?\*/" +         // block comments /* ... */
        @"|--[^\r\n]*" +             // line comments -- ...
        @"|\[(?:[^\]]|\]\])*\]",     // bracketed identifiers [delete_flag]
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Dangerous SQL keywords that indicate non-read operations.
    /// These are checked using word-boundary matching (\b) so they do NOT
    /// match when they appear as part of identifiers with underscores
    /// (e.g., DELETE does not match in delete_date, because _ is a word character).
    /// </summary>
    private static readonly string[] DangerousKeywords =
    [
        // DML (Data Manipulation Language)
        "INSERT", "UPDATE", "DELETE", "MERGE",
        // DDL (Data Definition Language)
        "CREATE", "ALTER", "DROP", "TRUNCATE", "RENAME",
        // DCL (Data Control Language)
        "GRANT", "REVOKE", "DENY",
        // Execution (stored procedures, dynamic SQL)
        "EXEC", "EXECUTE",
        // Variable declarations and cursors
        "DECLARE",
        // SELECT INTO (creates new tables)
        "INTO",
        // SQL Server specific dangerous operations
        "OPENROWSET", "OPENDATASOURCE", "OPENQUERY",
        "DBCC", "BACKUP", "RESTORE",
        "SHUTDOWN", "KILL", "RECONFIGURE",
        "WAITFOR", "BULK",
        "WRITETEXT", "UPDATETEXT",
        // Extended stored procedures: shell / file system / registry / OLE
        // (exact names, word-boundary matched: won't hit legit columns unless
        //  a column were named exactly like one of these — practically impossible)
        "XP_CMDSHELL", "XP_DIRTREE", "XP_FILEEXIST", "XP_FILESTATUS",
        "XP_SUBDIRS", "XP_GETFILEDETAILS", "XP_AVAILABLEMEDIA", "XP_ENUMDSN",
        "XP_REGREAD", "XP_REGWRITE", "XP_REGDELETEKEY", "XP_REGDELETEVALUE",
        "XP_REGENUMVALUES", "XP_REGADDMULTISTRING", "XP_INSTANCE_REGREAD",
        "XP_SERVICECONTROL", "XP_MSVER", "XP_LOGINCONFIG", "XP_ENUMGROUPS",
        // OLE Automation procedures (arbitrary code / file / shell via COM)
        "SP_OACREATE", "SP_OAMETHOD", "SP_OAGETPROPERTY", "SP_OASETPROPERTY",
        "SP_OADESTROY", "SP_OAGETERRORINFO",
        // Dynamic SQL / server configuration / extensibility
        "SP_EXECUTESQL", "SP_CONFIGURE", "SP_ADDEXTENDEDPROC", "SP_MAKEWEBTASK"
    ];

    /// <summary>
    /// Pre-compiled regex patterns for each dangerous keyword with word boundaries.
    /// </summary>
    private static readonly (string Keyword, Regex Pattern)[] KeywordPatterns =
        DangerousKeywords.Select(k => (k, new Regex($@"\b{Regex.Escape(k)}\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase)))
        .ToArray();

    /// <summary>
    /// Validates that a SQL query is a read-only SELECT statement.
    /// Throws InvalidOperationException if the query is not allowed.
    /// </summary>
    /// <param name="query">The SQL query to validate</param>
    /// <param name="allowedCommands">List of allowed SQL commands (e.g., ["SELECT"])</param>
    public static void ValidateReadOnlyQuery(string query, IEnumerable<string> allowedCommands)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be empty");
        }

        // Step 1: Strip string literals, comments, and quoted identifiers
        // This prevents false positives (keyword in a string/comment/identifier name)
        // and blocks obfuscation attacks (dangerous SQL hidden in comments)
        var cleaned = NoiseTokenRegex.Replace(query, " ");

        var normalized = cleaned.Trim();

        // Step 2: Verify query starts with an allowed command (or WITH for CTEs)
        var isAllowed = allowedCommands
            .Any(cmd => Regex.IsMatch(normalized, $@"^\s*{Regex.Escape(cmd)}\b", RegexOptions.IgnoreCase))
            || Regex.IsMatch(normalized, @"^\s*WITH\b", RegexOptions.IgnoreCase);

        if (!isAllowed)
        {
            throw new InvalidOperationException(
                $"Query must start with one of: {string.Join(", ", allowedCommands)}");
        }

        // Step 3: Block multiple statements (prevents stacked query injection like: SELECT 1; DROP TABLE...)
        if (normalized.Contains(';'))
        {
            throw new InvalidOperationException(
                "Multiple SQL statements are not allowed (semicolons are forbidden)");
        }

        // Step 4: Check for dangerous keywords using word-boundary regex.
        // Word boundaries (\b) ensure that keywords within identifiers are NOT matched:
        //   - "delete_date"  -> \bDELETE\b does NOT match (underscore is a word character)
        //   - "updated_at"   -> \bUPDATE\b does NOT match
        //   - "created_by"   -> \bCREATE\b does NOT match
        //   - "is_executed"  -> \bEXEC\b and \bEXECUTE\b do NOT match
        // But standalone keywords ARE matched:
        //   - "DELETE FROM"  -> \bDELETE\b matches
        //   - "DROP TABLE"   -> \bDROP\b matches
        foreach (var (keyword, pattern) in KeywordPatterns)
        {
            if (pattern.IsMatch(normalized))
            {
                throw new InvalidOperationException(
                    $"Query contains forbidden keyword: {keyword}");
            }
        }
    }
}

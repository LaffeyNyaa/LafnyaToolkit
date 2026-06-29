# C# Formatter

## Overview

The C# formatting tool is located in the `CSharpFormatter` directory. It accepts a target directory as a parameter and formats all C# files within that directory and its subdirectories.

### Execution Behavior

- **Invalid Input:** If the specified directory does not exist or no parameters are provided, the tool will output an error prompt.
- **Progress Logging:** During the formatting process, the tool will display whether each file is currently being formatted or skipped. A file is considered "skipped" if its content remains identical before and after the formatting process.
- **Summary Report:** Upon completion, a summary will be printed indicating the total number of files found, the number of files formatted, the number of files skipped, and the number of files that failed processing.
- **Error Handling:** If processing a file fails (e.g., I/O error), an `Error: <path>: <message>` message is printed to stderr and the file is counted as failed; processing continues with the remaining files.

## Formatting Rules

### Mandatory Braces for Control Statements

Curly braces `{}` are strictly required for all control flow statements, including `if`, `else`, `else if`, `for`, `while`, `foreach`, `do`, `lock`, `using`, `fixed`, `checked`, and `unchecked`, regardless of the block's length.

**Incorrect:**
```csharp
if (a == b) return true;
```

**Correct:**
```csharp
if (a == b)
{
    return true;
}
```

### Switch/Case Indentation

Inside a `switch` block, the body of each `case` (including `default`) label SHALL be indented one additional level (4 spaces) relative to the `case` label. This applies to all statements between a `case`/`default` label and the next `case`/`default` label or the closing `}` of the `switch`.

**Example:**
```csharp
switch (x)
{
    case 1:
        DoSomething();
        break;

    default:
        break;
}
```

### Blank Lines Around Code Blocks and Multi-line Statements

Maintain exactly one blank line above and below code blocks and multi-line statements. A **multi-line statement** is a statement that has been split across two or more physical lines due to line-length wrapping (see [Line Length Limit](#line-length-limit)): the first segment ends with a continuation operator (`,`, `+`, `-`, `*`, `/`, `%`, `=`, `=>`, `<`, `>`, `&&`, `||`, etc.) and the last segment ends with `;` or `}`.
- **Exception 1:** Do not add a blank line above if the block or statement is located at the very beginning of its parent block.
- **Exception 2:** Do not add a blank line below if the block or statement is located at the very end of its parent block.
- **Exception 3 (block-head):** Do not add a blank line above a multi-line statement if the previous non-blank line opens a block (`{` alone or ends with `{`).
- **Exception 4 (comment-attachment):** Do not add a blank line above a multi-line statement if the previous non-blank line is a comment (`//`, `/*`, `*`, or `///`); the comment is considered attached to the declaration.
- **Exception 5 (try/catch/finally):** Do not add a blank line between a `try`, `catch`, or `finally` clause and the closing brace of the preceding block. A `catch` or `finally` clause sits directly adjacent to the `}` of its `try` (or preceding `catch`) block, with no intervening blank line. Multiple consecutive `catch` clauses are likewise not separated by blank lines.

**Incorrect:**
```csharp
int i = 0;
for (i = 0; i < 10; i++)
{
    Console.WriteLine(i);
    if (i == 5)
    {
        Console.WriteLine("i is 5");
    }
}
```

**Correct:**
```csharp
int i = 0;

for (i = 0; i < 10; i++)
{
    Console.WriteLine(i);

    if (i == 5)
    {
        Console.WriteLine("i is 5");
    }
}
```

**try/catch/finally example:**
```csharp
try
{
    DoWork();
}
catch (Exception ex)
{
    Log.Error(ex);
}
finally
{
    Cleanup();
}
```

**Multi-line statement example:**
```csharp
Console.Error.WriteLine("Error: path does not exist or is not a directory: " +
    targetPath);

Environment.Exit(2);
```

### Indentation

Use exactly **4 spaces** for each level of indentation. Tab characters should not be used.

### Line Length Limit

The maximum allowed length for a single line of code is **80 characters**.
- Statements exceeding this limit must be broken down into multiple lines.
- For multi-line statements, all subsequent lines must be indented by one additional level relative to the first line.

Safe break points include the following operators (the line break occurs after the operator): `,`, `;`, `+`, `-`, `*`, `/`, `%`, `==`, `!=`, `<`, `>`, `<=`, `>=`, `=`, `+=`, `-=`, `=>`, `&&`, `||`. Breaks do not occur inside string, character, or comment tokens. The `.` (member access) operator is intentionally excluded as a break point due to style ambiguity.

**Incorrect:**
```csharp
if (currentCode.Length > 0)
{
    tokens.Add(new Token
        {Type = TokenType.Code, Value = currentCode.
        ToString()});
}
```

**Correct:**
```csharp
if (currentCode.Length > 0)
{
    tokens.Add(new Token
        {
            Type = TokenType.Code, 
            Value = currentCode.ToString()
        });
}
```

### Blank Lines Around Declarations

Maintain exactly one blank line above and below structural declarations, including methods, classes, enums, and namespaces.
- **Exception 1:** Do not add a blank line above if the declaration is at the very beginning of its parent block.
- **Exception 2:** Do not add a blank line below if the declaration is at the very end of its parent block.

### Empty Lines: Documentation Comments

- **General Rule**: Keep exactly one empty line above a documentation comment block (`///`) when the preceding non-blank line is a code statement. This separates the "doc comment + declaration" logical unit from the previous unit.
- **Exceptions** (no empty line added above the `///`):
  - The previous non-blank line is itself a `///` documentation comment (multi-line doc continuation).
  - The previous non-blank line is a regular comment (`//`, `/*`, or `*` continuation) attached to the declaration below.
  - The previous non-blank line is a block-opening brace (`{` alone or ending with `{`).
- *Incorrect*:
  ```csharp
  /// <summary>Number of spaces per indent level.</summary>
  private const int IndentSize = 4;
  /// <summary>Maximum line length.</summary>
  private const int MaxLineLength = 80;
  ```
- *Correct*:
  ```csharp
  /// <summary>Number of spaces per indent level.</summary>
  private const int IndentSize = 4;

  /// <summary>Maximum line length.</summary>
  private const int MaxLineLength = 80;
  ```

### Preserve blank lines between single-line statements

Beyond the blank-line rules above (which *insert* blanks around blocks, multi-line statements, and declarations), the formatter also **preserves** author-inserted blank lines between adjacent plain single-line statements. This rule runs last in `ApplyBlankLineRules`: after every other rule has decided `wantBlankAbove = false`, if the original input had a blank line above the current line (`entry.HadBlankAbove`) and both the current line and the previous non-blank line are "plain single-line statements", then `wantBlankAbove` is set to `true` and the blank is kept.

- **Preserve only, never insert:** The rule only retains blanks that already exist in the input (via `HadBlankAbove`). It never adds a new blank line where the original had none.
- **Idempotent:** Because the rule only preserves existing blanks, a second run sees the same blank and preserves it again — no duplication, no removal.
- **Definition of "plain single-line statement":** A line qualifies when **all** of the following hold (helper `IsPlainSingleLineStatement(trimmed, origIdx, isCodeLine, lineEndsStatement)`):
  - It is a code line (`isCodeLine[origIdx]` is true).
  - It ends a statement (`lineEndsStatement[origIdx]` is true, i.e., the last code character is `;` or `}`).
  - It is **not** a block-end line (`LineClassifier.IsBlockEndLine` returns false, i.e., not `}` or `};`).
  - It is **not** a block-start line (`LineClassifier.IsBlockStartLine` returns false).
  - It is **not** a comment line.

The blank is preserved only when **both** the current line and the previous non-blank line satisfy these conditions. Excluding block-start and block-end lines ensures this rule never interferes with the block-related blank-line rules.

**Example:**
```csharp
int[] lineStarts = new int[10];

var enumRanges = new List<KeyValuePair<int, int>>();
int depth = 0;
int enumDepth = -1;
int enumStart = -1;
bool pendingEnum = false;

for (int i = 0; i < lineStarts.Length; i++)
```

The blank between `lineStarts` and `enumRanges` is preserved (both are plain single-line statements). No blank is inserted between `enumRanges`, `depth`, `enumDepth`, `enumStart`, and `pendingEnum`, because none existed in the input. The blank between `pendingEnum` and the `for` loop is also preserved — the block-start line has its own blank-line rule, and this rule does not interfere with it.

### End-of-File Newline

Every file must end with exactly one newline character. 
- **Priority Rule:** In the event of a conflict with any other formatting rules, the requirement for a single trailing newline at the end of the file takes absolute precedence.

### Enum Formatting

Each enum value must be declared on a separate line.

**Incorrect:**
```csharp
private enum TokenType
{ Code, String, VerbatimString, Char, SingleLineComment, MultiLineComment }
```

**Correct:**
```csharp
private enum TokenType
{ 
    Code,
    String,
    VerbatimString,
    Char,
    SingleLineComment,
    MultiLineComment
}
```

### `using` Directive Ordering

`using` directives must be categorized into four distinct groups and ordered as follows:
1. System libraries
2. Third-party libraries
3. Other modules within the current project
4. The current module

Insert exactly one blank line between each group.

The "current module" RootNamespace is resolved dynamically: for each file, the formatter searches upward from the file's directory for the nearest `.csproj` file and reads its `<RootNamespace>` element. If no `.csproj` is found or the element is absent, the formatter falls back to the last path segment of the target root directory. This allows the tool to correctly classify `using` directives for any project, not just itself.

### C# Version Compatibility

The code must strictly adhere to the **C# 7.3** language syntax specifications.

### Property Declaration Formatting

Property accessors (`get`, `set`, etc.) must be formatted on separate lines.

**Incorrect:**
```csharp
public abstract double Area
{get;set;}
```

**Correct:**
```csharp
public abstract double Area
{
    get;
    set;
}
```

Accessors containing nested blocks (e.g., `get { return x; }`) are recursively expanded so that both the accessor keyword and its block body are placed on separate lines:

**Incorrect:**
```csharp
public int Foo {get {return x;} set {x = value;}}
```

**Correct:**
```csharp
public int Foo
{
    get
    {
        return x;
    }

    set
    {
        x = value;
    }
}
```

Accessor keywords (`get`, `set`, `init`, `add`, `remove`) are identified by word boundaries; substrings inside identifiers (e.g., `Budget`) are not treated as accessors.

### File Encoding

- **Read**: The formatter auto-detects the file encoding via byte order marks (BOM). Supported encodings: UTF-8 (with/without BOM), UTF-16 LE (with BOM), UTF-16 BE (with BOM), UTF-32 LE (with BOM), UTF-32 BE (with BOM). Files without a BOM are read as UTF-8.
- **Write**: After formatting, the file is always written as UTF-8 without BOM, regardless of the original encoding. If the formatted content is identical to the original (and the original was already UTF-8 without BOM), the file is skipped and not rewritten.

### Error Handling

If an error occurs while processing a file (e.g., read-only, locked, permission denied), the tool prints `Error: <relative path>: <message>` to stderr, increments the `Failed` counter, and continues processing the remaining files. The run does not abort on a single file failure.

## Token-Awareness Protection

All formatting transformations are **token-aware**: the formatter first tokenises the source into typed tokens (Code, String, VerbatimString, Char, SingleLineComment, MultiLineComment, Preprocessor), then builds a code mask marking which character positions belong to Code tokens. Formatting decisions consult this mask to ensure that the contents of strings and comments are never altered.

Protected operations include:

- **Tab replacement:** Tabs are expanded to 4 spaces only inside Code tokens. Tabs inside verbatim strings (`@"..."`) and comments are preserved verbatim.
- **Brace moving (Allman style):** Only braces in code regions are moved to their own line. Braces inside strings (e.g., `var x = "str {";`), interpolated strings (`$"{x}"`), verbatim strings, or comments are left untouched.
- **Blank-line insertion:** Blank lines are inserted before block-start keywords (`if`, `for`, `foreach`, etc.) only when the keyword is in a code region. Keywords appearing inside multi-line comments or verbatim strings do not trigger blank-line insertion.
- **Switch case detection:** `case`/`default` labels are recognised only when they appear in code regions, preventing false matches inside comments.
- **Line-length splitting:** Break points are chosen only at Code token boundaries, so strings and comments are never split mid-token.

## Idempotency Guarantee

The formatter is **idempotent**: running `Format` on its own output produces no further changes. That is, `Format(Format(source)) == Format(source)` for any input.

This guarantee holds because:

1. Each transformation pass either detects and skips already-formatted content or produces output that is stable under re-application.
2. The token mask and per-line code-region flags are recomputed from the current text at each run, so blank-line and indentation decisions always reflect the actual content rather than stale state.
3. Blank-line rules insert at most one blank line before block-starts and collapse multiple blanks to one, converging in a single pass.
4. Brace enforcement detects existing braces and does not add duplicates.
5. Line splitting recurses until every segment is within the 80-character limit, then re-running finds no lines exceeding the limit.
6. **Continuation detection coverage:** the `IsContinuationIndicator` test recognises the full set of break-point operators listed under [Line Length Limit](#line-length-limit) (`,`, `;`, `+`, `-`, `*`, `/`, `%`, `==`, `!=`, `<`, `>`, `<=`, `>=`, `=`, `+=`, `-=`, `=>`, `&&`, `||`). Because `==`/`!=`/`<=`/`>=`/`+=`/`-=` all end in `=`, and `=>` ends in `>`, the test only needs to check the trailing character against the set `{ ',', '+', '-', '*', '/', '%', '<', '>', '=', '&&', '||' }`. This keeps `IndentationProcessor` and `LineLengthProcessor` in agreement on where continuation indents apply, so a line split by the length limiter is recognised as a continuation on the next run and receives the same indent.
7. **Fixed continuation indent:** `LineLengthProcessor.SplitLongLine` computes the continuation indent once from the first segment's indent (first-segment indent + 4 spaces) and reuses that fixed value for every subsequent segment of the same statement. This avoids cascading indents (12 → 16 → 20 → …) that would otherwise diverge from `IndentationProcessor`'s single +4 application and break idempotency on statements requiring three or more segments.
8. **Pipeline ordering:** `LineLengthProcessor.ApplyLineLengthLimit` runs *before* `BlankLineProcessor.ApplyBlankLineRules`, and the text/tokens/code-mask/per-line flags are recomputed from the post-split lines before the blank-line rules consult them. This ensures the multi-line-statement blank-line rules see the same line structure on the first pass as on every subsequent pass, so no new blank line is inserted on a second run.

Files that are already in canonical form are reported as `Skipped` on every subsequent run.

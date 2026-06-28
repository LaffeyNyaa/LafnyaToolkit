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

Maintain exactly one blank line above and below code blocks and multi-line statements.
- **Exception 1:** Do not add a blank line above if the block or statement is located at the very beginning of its parent block.
- **Exception 2:** Do not add a blank line below if the block or statement is located at the very end of its parent block.

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

All files are read and written using UTF-8 encoding without BOM. Original file encodings and BOM markers are normalized to UTF-8 no BOM on write.

### Error Handling

If an error occurs while processing a file (e.g., read-only, locked, permission denied), the tool prints `Error: <relative path>: <message>` to stderr, increments the `Failed` counter, and continues processing the remaining files. The run does not abort on a single file failure.

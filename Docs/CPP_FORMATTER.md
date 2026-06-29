# C++ Formatter Tool

## Overview

- **Location**: The tool is located in the `CppFormatter` directory.
- **Parameters**: Accepts a directory path as an argument and formats all C++ files within that directory and its subdirectories.
- **Validation**: Outputs a prompt/warning if the specified directory does not exist or if no arguments are provided.
- **Execution Output**: Displays real-time status indicating whether a file is currently being formatted or skipped.
- **Skipping Logic**: A file is considered "skipped" if its content remains identical before and after the formatting process.
- **Summary**: Upon completion, outputs a summary showing the total number of files found, the number of files formatted, and the number of files skipped.

## Formatting Rules

### General Style & Standards

- **C++ Standard**: Strictly follows the **C++20** standard syntax.
- **Base Style**: Adheres to the **K&R** (Kernighan and Ritchie) coding style.

### Multi-line Token Content Protection

- **Protected Tokens**: The formatter preserves the internal content of multi-line raw string literals (`R"delim(...)delim"` and prefixed variants) and multi-line comments (`/* ... */`) exactly as-is.
- **No Modification**: Lines fully inside a multi-line raw string or multi-line comment are never modified — trailing whitespace, blank lines, line length limits, and indentation rules do not apply to them.
- **Line Ending Inside Token**: If a line's last character falls inside a multi-line token, trailing whitespace trimming is skipped for that line to avoid corrupting string content.
- **Brace Merging**: A `{` that appears alone on a line but is inside a comment or string literal is never merged to the previous line.

### Control Flow Statements

- **Mandatory Braces**: Enforces curly braces `{}` for all control structures (`if`, `else`, `else if`, `for`, `while`, etc.).
  - *Incorrect*:
    ```cpp
    if (a == b) return true;
    ```
  - *Correct*:
    ```cpp
    if (a == b) {
        return true;
    }
    ```

### Indentation

- **Indent Size**: Uses exactly **4 spaces** per indentation level.

### Line Length Limit & Wrapping

- **Maximum Length**: Each line of code must not exceed **80 characters**.
- **Line Wrapping**: Statements exceeding 80 characters must be broken into multiple lines.
- **Wrapped Line Indentation**: Continuation lines (all lines except the first) must be indented by one additional level relative to the starting line.

### Empty Lines: Code Blocks & Multi-line Statements

- **General Rule**: Keep exactly one empty line above and below code blocks and multi-line statements.
- **Exceptions**:
  - Do not add an empty line above if the block is at the very beginning of its parent block.
  - Do not add an empty line below if the block is at the very end of its parent block.
- *Incorrect*:
  ```cpp
  int i = 0;
  for (i = 0; i < 10; i++) {
      std::cout << i;
      if (i == 5) {
          std::cout << "i is 5";
      }
  }
  ```
- *Correct*:
  ```cpp
  int i = 0;

  for (i = 0; i < 10; i++) {
      std::cout << i;

      if (i == 5) {
          std::cout << "i is 5";
      }
  }
  ```

### Empty Lines: Methods, Classes, Enums, & Namespaces

- **General Rule**: Keep exactly one empty line above and below methods, classes, enums, and namespaces.
- **Exceptions**:
  - Do not add an empty line above if located at the beginning of the parent block.
  - Do not add an empty line below if located at the end of the parent block.
- **Namespace Specifics**: No empty lines are allowed immediately after the opening brace `{` or immediately before the closing brace `}` of a namespace.

### Empty Lines: Documentation Comments

- **General Rule**: Keep exactly one empty line above a documentation comment block (`///`) when the preceding non-blank line is a code statement. This separates the "doc comment + declaration" logical unit from the previous unit.
- **Exceptions** (no empty line added above the `///`):
  - The previous non-blank line is itself a `///` documentation comment (multi-line doc continuation).
  - The previous non-blank line is a regular comment (`//`, `/*`, or `*` continuation) attached to the declaration below.
  - The previous non-blank line is a block-opening brace (`{` alone or ending with `{`).
- *Incorrect*:
  ```cpp
  /// <summary>Indentation uses 4 spaces per level.</summary>
  private const int IndentSize = 4;
  /// <summary>Maximum length of a single line.</summary>
  private const int MaxLineLength = 80;
  ```
- *Correct*:
  ```cpp
  /// <summary>Indentation uses 4 spaces per level.</summary>
  private const int IndentSize = 4;

  /// <summary>Maximum length of a single line.</summary>
  private const int MaxLineLength = 80;
  ```

### Preserve blank lines between single-line statements

- **General Rule**: After all other blank-line rules in `ApplyBlankLineRules` (`CppFormatter/BlankLineProcessor.cs`) have decided *not* to insert a blank line above the current line, this fallback rule preserves an author-inserted blank line when both the current line and the previous non-blank line are plain single-line statements. It keeps author-inserted blank separators between adjacent single-line statements at the same indentation level.
- **Preserve Only**: The rule only **preserves** an existing blank line (detected via the original `HadBlankAbove` flag captured before collapsing); it never inserts a new blank line where the author did not place one.
- **Idempotent**: Because the rule only preserves existing blanks and never introduces new ones, running the formatter repeatedly yields the same output as a single run.
- **Definition of "Plain Single-line Statement"**: A line qualifies (helper `IsPlainSingleLineStatement(trimmed, isProtected)`) when **all** of the following hold:
  - The line is **not protected** (i.e., not a line inside a multi-line raw string or comment token, and not a protected `#include`/preprocessor line flagged by the tokenizer).
  - The trimmed line ends with `;`.
  - The line is **not** a block-end line (not `}` or `};`, per `IsBlockEndLine`).
  - The line is **not** a block-start line (per `IsBlockStartLine`).
  - The line is **not** a comment line (`//`, `/*`, or `*` continuation).
- *Example*:
  ```cpp
  int lineStarts[10];

  std::vector<std::pair<int, int>> enumRanges;
  int depth = 0;
  int enumDepth = -1;
  int enumStart = -1;
  bool pendingEnum = false;

  for (int i = 0; i < 10; i++) {
  ```
  The author-inserted blank between `lineStarts` and `enumRanges` is preserved. No blank is inserted between `enumRanges` / `depth` / `enumDepth` / `enumStart` / `pendingEnum`, because no blank existed there in the input.

### End of File (EOF) Newline

- **Newline Rule**: Every file must end with exactly **one newline character**.
- **Priority**: If this rule conflicts with any other spacing/empty-line rules, the EOF newline takes absolute priority, ensuring only a single newline exists at the very end of the file.

### Enumerations

- **Formatting**: Write exactly one enum value per line.
- *Incorrect*:
  ```cpp
  enum class TokenType {
      Code, String, VerbatimString, Char, SingleLineComment, MultiLineComment
  };
  ```
- *Correct*:
  ```cpp
  enum class TokenType { 
      Code,
      String,
      VerbatimString,
      Char,
      SingleLineComment,
      MultiLineComment
  };
  ```

### `#include` Directives Ordering

- **Categories**: Include directives are divided into four categories: System Libraries, Third-party Libraries, Other Project Modules, and the Current Module.
- **Ordering**: Sort `#include` statements strictly in the following order:
  1. System Libraries
  2. Third-party Libraries
  3. Other Project Modules
  4. Current Module
- **Spacing**: Separate each category with exactly one empty line.

---

### `#include` Directive Classification Heuristic

The four-group classification uses the following heuristic:

1. **System Libraries**: `#include <header>` where `header` contains no file extension (no `.`) and no path separator (`/` or `\`). Examples: `<iostream>`, `<vector>`, `<memory>`.

2. **Third-party Libraries**: `#include <header>` that does not qualify as System (i.e., contains `.` or path separators). Examples: `<windows.h>`, `<SDL2/SDL.h>`, `<boost/asio.hpp>`.

3. **Other Project Modules**: `#include "header"` where the path contains `..` (parent-directory reference), starts with `/` (absolute Unix path), or matches the Windows drive-letter pattern `[A-Za-z]:[\\/]`. Examples: `"../utils/logger.h"`, `"/abs/path/bar.h"`.

4. **Current Module**: `#include "header"` that does not qualify as Other Project (i.e., a simple relative path). Examples: `"foo.h"`, `"subdir/bar.hpp"`.

`#include` directives nested inside `#if`/`#ifdef`/`#ifndef` blocks are left untouched; only the top-level contiguous block is reordered.

---

### Mandatory Braces: `do-while` and `switch`

In addition to `if`/`else`/`else if`/`for`/`while`, the formatter also enforces mandatory braces for:

- **`do-while`**: The opening `{` is placed on the same line as `do`; the closing `}` is placed on the same line as the trailing `while`. Example:
  ```cpp
  do {
      stmt;
  } while (cond);
  ```

- **`do-while` Closing Brace**: The formatter also ensures that for existing `do-while` loops where `}` and `while` are on separate lines, the `while` is merged onto the same line as `}`. This applies only when the `}` matches the body of a `do` statement (verified via brace matching), preventing incorrect merges with standalone `while` loops.
  - *Before*:
    ```cpp
    do {
        stmt;
    }
    while (cond);
    ```
  - *After*:
    ```cpp
    do {
        stmt;
    } while (cond);
    ```

- **`switch`**: If the switch body is a single non-block statement, it is wrapped in `{ ... }`. Example:
  ```cpp
  switch (x) {
      stmt;
  }
  ```

---

### Line Wrapping: `<<` and `>>` Operators

The stream insertion (`<<`) and extraction (`>>`) operators are treated as safe break points for line wrapping, with the following constraint:

A break is permitted at `<<` or `>>` only when the preceding non-whitespace character is one of `)`, `]`, an identifier character (letter/digit/`_`), `"` (string close), or `'` (char close). This avoids breaking inside template parameter lists (e.g., `vector<vector<int>>`) where `>>` is part of a template closing bracket rather than a stream operator.

---

### Continuation Indicator: Exclusion Rules

When determining whether a line is a continuation of the previous line (for indentation purposes), the formatter treats lines ending with `,`, `+`, `(`, `=`, `?`, `:`, `&&`, or `||` as continuation indicators. However, the following `:`-terminated lines are **excluded** from being treated as continuations:

- **Access specifiers**: `public:`, `private:`, `protected:`
- **Case labels**: `case <expr>:`
- **Default label**: `default:`
- **Ordinary labels**: `<identifier>:`

This prevents incorrect over-indentation of statements following access specifiers, switch case labels, and goto labels. The `:` in a ternary expression (e.g., `cond ? a :`) still triggers continuation indentation.

---

### File Encoding

- **Read**: The formatter auto-detects the file encoding via byte order marks (BOM). Supported encodings: UTF-8 (with/without BOM), UTF-16 LE (with BOM), UTF-16 BE (with BOM), UTF-32 LE (with BOM), UTF-32 BE (with BOM). Files without a BOM are read as UTF-8.
- **Write**: After formatting, the file is always written as UTF-8 without BOM, regardless of the original encoding. If the formatted content is identical to the original (and the original was already UTF-8 without BOM), the file is skipped and not rewritten.

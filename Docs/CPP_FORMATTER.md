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
- **Preprocessor Conditionals**: Preprocessor conditional blocks (`#if`/`#ifdef`/`#ifndef` ... `#endif`) are treated as code blocks and follow the same blank-line rules as other blocks.
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
- **Namespace Specifics**:
  - No empty lines are allowed immediately after the opening brace `{` or immediately before the closing brace `}` of a namespace.
  - Consecutive namespace declarations must not have empty lines between them.
  - Consecutive namespace declarations share the same indentation level as the enclosing block's content (i.e., the inner namespace keyword is not additionally indented relative to the outer namespace's body). The `{` of a namespace declaration does not increase the nesting depth for subsequent content.

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

### Doc-comment + Single-line Statement as Multi-line Unit (Header Files)

- **General Rule**: In header files, a single-line statement (a line ending with `;`) that is immediately preceded by a documentation comment block (`/** ... */` or `///`) is treated as a multi-line statement unit. The doc comment is considered part of the statement for blank-line purposes.
- **Blank above**: A blank line is inserted above the doc comment when the preceding non-blank line is a regular code statement (not a doc comment, not a regular comment, and not a block-opening brace).
- **Blank below**: A blank line is inserted below the single-line statement when the following non-blank line is a new statement, unless the next line is a block-end (`}`, `};`) or an access specifier (`public:`, `protected:`, `private:`).
- **Rationale**: This prevents doc-commented declarations in class/struct bodies from being visually merged with adjacent declarations, ensuring consistent vertical spacing similar to multi-line constructs.
- *Incorrect*:
  ```cpp
  int getValue() const;
  /// @brief Get the name.
  const std::string& getName() const;
  /// @brief Get the value.
  int getValue() const;
  ```
- *Correct*:
  ```cpp
  int getValue() const;

  /// @brief Get the name.
  const std::string& getName() const;

  /// @brief Get the value.
  int getValue() const;
  ```

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

- **Idempotency**: The closing `};` of an enum declaration is treated as part of the enum body for indentation purposes. This prevents the backward continuation-indicator scan from misinterpreting the preceding enum member's trailing comma as a continuation and incorrectly indenting `};` by one extra level on subsequent formatting passes.

### `#include` Directives Ordering

- **Categories**: Include directives are divided into four categories: System Libraries, Third-party Libraries, Other Project Modules, and the Current Module.
- **Ordering**: Sort `#include` statements strictly in the following order:
  1. System Libraries
  2. Third-party Libraries
  3. Other Project Modules
  4. Current Module
- **Spacing**: Separate each category with exactly one empty line.
- **Pragma Once Handling**: A blank line is automatically inserted between a `#pragma once` directive (or any other non-include preprocessor directive) and the first `#include` directive, ensuring visual separation between the include guard and the include block.
- **Include-less Files**: Files without any `#include` directives are left completely untouched by the include sorting pass.

---

### `#include` Directive Classification Heuristic

The four-group classification uses the following heuristic:

1. **System Libraries**: `#include <header>` where `header` contains no file extension (no `.`) and no path separator (`/` or `\`). Examples: `<iostream>`, `<vector>`, `<memory>`.

2. **Third-party Libraries**: `#include <header>` that does not qualify as System (i.e., contains `.` or path separators). Examples: `<windows.h>`, `<SDL2/SDL.h>`, `<boost/asio.hpp>`.

3. **Other Project Modules**: `#include "header"` where the path contains `..` (parent-directory reference), starts with `/` (absolute Unix path), or matches the Windows drive-letter pattern `[A-Za-z]:[\\/]`. Examples: `"../utils/logger.h"`, `"/abs/path/bar.h"`.

4. **Current Module**: `#include "header"` that does not qualify as Other Project (i.e., a simple relative path). Examples: `"foo.h"`, `"subdir/bar.hpp"`.

`#include` directives nested inside `#if`/`#ifdef`/`#ifndef` blocks are left untouched. However, ALL top-level `#include` directives across the entire file are collected, categorized, and sorted as a single unified block. The `#if`/`#ifdef`/`#ifndef` wrapper (including the `#define` and `#endif` lines) is preserved as part of the wrapped include's unit.

In addition to sorting includes, any non-include preprocessor directives (such as `#ifndef`, `#define`, `#endif`, `#pragma`, `#error`) that appear between includes are extracted and placed at the very top of the file, before the first `#include` line.

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

---

### `#endif` Comment Appending

Each `#endif` directive is automatically annotated with a `// <macro_name>` comment that references the matching opening directive's macro name. This is done via stack-based matching to correctly handle nested conditionals.

- **`#ifdef`/`#ifndef`**: The macro name following the directive is used.
- **`#if`**: The first identifier in the condition is used (skipping `!`, `(`, and `defined(...)` wrappers).
- **`#else`/`#elif`**: These do not affect the stack.
- **Nested conditionals**: Each `#endif` references its own matching opening directive's macro.

*Example*:
```cpp
#ifndef CPPHTTPLIB_MBEDTLS_SUPPORT
#define CPPHTTPLIB_MBEDTLS_SUPPORT 1
#endif  // CPPHTTPLIB_MBEDTLS_SUPPORT
```

---

### Empty Lines: Blank Line Before `return` at End of Block

When a `return` statement is the last non-blank statement before the closing `}` of a block, and the block contains at least one other statement before the `return`, a blank line is inserted above the `return`.

- **Multi-statement block**: A blank line is added before the `return`.
  ```cpp
  if (!result) {
      spdlog::warn("request failed");

      return response;
  }
  ```
- **Single-statement block**: No blank line is added (the `return` is the only statement).
  ```cpp
  if (x == 0) {
      return nullptr;
  }
  ```

---
### Constructor Initializer List Formatting

Constructor initializer lists are formatted with the following rules:

- **Colon placement**: The colon `:` must immediately follow the closing `)` of the constructor signature on the next line, with no blank line between them.
- **Colon indentation**: The `:` starts at the base indentation of the constructor (not continuation indent), so it aligns with the constructor's opening level.
- **Continuation indentation**: Continuation lines in the initializer list are indented one additional level from the colon.
- **Blank line exclusion**: The multi-line statement end rule does not insert a blank line before a line starting with `:` (constructor initializer list continuation).

*Example*:
```cpp
HttpServer::HttpServer(const Config& config,
    Repository& repo,
    QqVerifier& verifier,
    CodeGenerator& code_gen)
: impl_(std::make_unique<Impl>(config, repo, verifier, code_gen)) {
    impl_->configure();
}
```

---

### Access Specifier Indentation

Access specifiers (`public:`, `protected:`, `private:`) inside class and struct definitions are indented at the same level as the enclosing class/struct keyword itself (not indented by an extra level).

*Example*:
```cpp
class CodeGenerator {
public:
    std::string generate() const;
};
```

### Reference: Formatted Example

After formatting, `http_client.cpp` produces the following output which demonstrates all the rules above:

```cpp
#ifndef CPPHTTPLIB_MBEDTLS_SUPPORT
#define CPPHTTPLIB_MBEDTLS_SUPPORT 1
#endif  // CPPHTTPLIB_MBEDTLS_SUPPORT

#include <chrono>
#include <cstdio>
#include <optional>
#include <string>

#include <httplib.h>
#include <spdlog/spdlog.h>

#include "lafnya/http_client.hpp"

namespace lafnya {
namespace {
struct UrlParts {
    std::string host;
    std::string path;
};
```

The full formatted output should be verified with the latest formatter version.

---

### Skip Build Directory

The formatter automatically skips any C++ files located under a `build` directory (case-insensitive) during file discovery. This prevents formatting auto-generated files produced by CMake or other build systems.

- **Detection**: The formatter checks whether the file's absolute path contains a path segment named `build` (e.g., `\build\` on Windows, `/build/` on Linux/macOS).
- **Case-insensitive**: Both `build`, `Build`, and `BUILD` directory names are recognized.
- **Scope**: All subdirectories named `build` at any depth are skipped.

*Example: Given a project tree with:*
```
project/
  src/
    main.cpp
  build/
    CMakeCache.txt
    src/
      main.cpp   (generated copy)
```
The formatter will format `project/src/main.cpp` but skip `project/build/src/main.cpp`.

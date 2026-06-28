# Java Formatter

## Overview

- **Location**: The tool is located in the `JavaFormatter` directory.
- **Usage**: Accepts a directory path as an argument and formats all `.java` files within it, including all subdirectories.
- **Error Handling**: Outputs a prompt if the specified directory does not exist or if no arguments are provided.
- **Execution Output**:
  - Displays real-time status indicating whether a file is being formatted or skipped.
  - A file is considered "skipped" if the formatted content is identical to the original file.
  - Upon completion, outputs a summary: total files found, number of files formatted, and number of files skipped.

## Formatting Rules

### Mandatory Braces

All control flow statements (`if`, `else`, `else if`, `for`, `while`, `do`, `synchronized`, `try`, etc.) must be enclosed in curly braces.

- **Incorrect Example**:
  ```java
  if (a == b) return true;
  ```
- **Correct Example**:
  ```java
  if (a == b) {
      return true;
  }
  ```

### Blank Lines for Code Blocks and Multi-line Statements

Maintain exactly one blank line above and below all code blocks and multi-line statements.

- **Exceptions**:
  - Do **not** add a blank line above if the block/statement is the very first element inside a parent block.
  - Do **not** add a blank line below if the block/statement is the very last element inside a parent block.

- **Incorrect Example**:
  ```java
  int i = 0;
  for (i = 0; i < 10; i++) {
      System.out.println(i);
      if (i == 5) {
          System.out.println("i is 5");
      }
  }
  ```
- **Correct Example**:
  ```java
  int i = 0;

  for (i = 0; i < 10; i++) {
      System.out.println(i);

      if (i == 5) {
          System.out.println("i is 5");
      }
  }
  ```

### Blank Lines for Declarations

Maintain exactly one blank line above and below declarations such as classes, methods, enums, and packages.
- **Exceptions**:
  - Do **not** add a blank line above if the declaration is the very first element inside a parent block.
  - Do **not** add a blank line below if the declaration is the very last element inside a parent block.

### Indentation

Use exactly **4 spaces** for each indentation level.

### Switch/Case Indentation

Inside a `switch` block, `case` and `default` labels are indented one level relative to the `switch` keyword. The body of each case is at the same indentation level as the case label (Oracle style), not indented further.

- **Example**:
  ```java
  switch (x)
  {
      case 1:
          doSomething();
          break;
      default:
          break;
  }
  ```

### Annotations

Annotation lines (lines starting with `@`) are considered attached to the declaration that follows them. No blank line shall be inserted between an annotation and the declaration it annotates, nor between consecutive annotations.

- **Incorrect Example**:
  ```java
  @Override

  public void foo()
  {
  }
  ```
- **Correct Example**:
  ```java
  @Override
  public void foo()
  {
  }
  ```

### Line Length Limit

- The maximum length for a single line of code is **80 characters**.
- Statements exceeding 80 characters must be split into multiple lines.
- **Safe Break Points**: Line breaks occur after the following operators: `,`, `;`, `+`, `-`, `*`, `/`, `%`, `==`, `!=`, `<`, `>`, `<=`, `>=`, `=`, `+=`, `-=`, `&&`, `||`. Breaks do not occur inside string, text block, character, or comment tokens. The `.` (member access) operator is intentionally excluded as a break point.
- **Wrapping Rule**: The first line of a multi-line statement retains its standard indentation, while all subsequent wrapped lines must be indented by one additional level relative to the first line.

### End of File (EOF) Newline

- Every file must end with exactly **one** newline character.
- **Priority Rule**: If this rule conflicts with any other spacing rules, the EOF newline takes precedence, and exactly one newline must be preserved.

### File Encoding

All files are read and written using UTF-8 encoding without BOM. Original file encodings and BOM markers are normalized to UTF-8 no BOM on write.

### Error Handling

If an error occurs while processing a file (e.g., read-only, locked, permission denied), the tool prints `Error: <relative path>: <message>` to stderr, increments the `Failed` counter, and continues processing the remaining files. The run does not abort on a single file failure.

### Enum Declarations

Each enum value must be written on a separate line.

- **Incorrect Example**:
  ```java
  enum TokenType {
      Code, String, VerbatimString, Char, SingleLineComment, MultiLineComment;
  }
  ```
- **Correct Example**:
  ```java
  enum TokenType { 
      Code,
      String,
      VerbatimString,
      Char,
      SingleLineComment,
      MultiLineComment;
  }
  ```

### Import Statements Order

Import statements are categorized into four groups and must be ordered as follows:
1. System libraries (e.g., `java.*`, `javax.*`)
2. Third-party libraries
3. Other modules within the current project
4. The current module

Each category must be separated by exactly one blank line.

The classification of imports into groups is determined based on the file's `package` declaration:
- **Current module**: The import namespace matches the file's `package` declaration exactly.
- **Other modules within the current project**: The import namespace starts with the project root prefix (the `package` declaration with its last segment removed; if the package has only one segment, the project root equals the package itself) but is not the current module.
- **System libraries**: Imports starting with `java.` or `javax.`.
- **Third-party libraries**: All remaining imports.
If the file has no `package` declaration, the project root falls back to the target directory name, and there is no "current module" group.

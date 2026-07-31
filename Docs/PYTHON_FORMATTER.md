# Python Formatter

## Overview

- **Location**: The tool is located in the `PythonFormatter` directory.
- **Usage**: Accepts a directory path as an argument and formats all `.py` and `.pyw` files within it, including all subdirectories.
- **Error Handling**: Outputs a prompt if the specified directory does not exist or if no arguments are provided.
- **Execution Output**:
  - Displays the relative path of each file that is being reformatted. Files whose content remains unchanged after formatting are not printed individually; only their total count appears in the summary line.
  - A file is considered "skipped" if the formatted content is identical to the original file. Skipped files do not produce a per-file log line.
  - Upon completion, outputs a single summary line `Total: N, Formatted: M, Skipped: K, Failed: L`, showing the number of files found, reformatted, skipped, and failed.
- **Encoding**: All output files are written as UTF-8 without BOM.

## Formatting Rules

### PEP 8 Baseline Style

The formatter follows [PEP 8](https://peps.python.org/pep-0008/) as the baseline style guide:

- **4 spaces** per indentation level; no tabs in code regions.
- Maximum line length of **80 characters**.
- Exactly **one** trailing newline at end of file.
- All line endings are normalized to LF (`\n`).
- Runs of 3 or more consecutive blank lines are collapsed to a single blank line.

### Indentation

- All indent levels use exactly **4 spaces**.
- Tabs inside code regions are replaced with 4 spaces; tabs inside strings, comments, and docstrings are preserved verbatim.
- Continuation lines of a previous statement (lines following an unclosed bracket, a backslash, or a binary operator) retain at least 4 spaces of hanging indent relative to the statement's start.
- Lines inside a multi-line string or multi-line comment preserve their original leading whitespace.

- **Incorrect Example**:
  ```python
  def foo():
       return 1
  ```
- **Correct Example**:
  ```python
  def foo():
      return 1
  ```

### Line Length

- No output line exceeds **80 characters**, except for lines inside a multi-line string or comment token.
- Long lines are wrapped at safe break points (after `,`, `+`, `-`, `*`, `/`, `%`, `=`, comparison operators, or boolean operators like `and`/`or`).
- Wrapped lines are indented 4 spaces deeper than the statement's start.

- **Incorrect Example**:
  ```python
  def long_call():
      result = some_function_with_a_very_long_name(argument_one, argument_two, argument_three, argument_four)
      return result
  ```
- **Correct Example**:
  ```python
  def long_call():
      result = some_function_with_a_very_long_name(
          argument_one, argument_two, argument_three, argument_four
      )
      return result
  ```

### Trailing Whitespace

- Every output line has no trailing whitespace, except for lines whose last non-whitespace character is inside a multi-line string or comment token.

### Blank Lines for Top-Level Definitions (PEP 8 Strict)

- **Two** blank lines between any two top-level `def` or `class` statements.
- **No** blank line above a top-level `def` or `class` if it is the very first non-blank line of the file.
- **No** blank line below a top-level `def` or `class` if it is the very last non-blank line of the file.
- Decorator lines (`@...`) immediately preceding a `def` or `class` are treated as part of the same logical unit: no blank line is inserted between consecutive decorator lines or between the last decorator and the `def`/`class` it decorates.

- **Incorrect Example**:
  ```python
  def foo():
      return 1
  def bar():
      return 2
  ```
- **Correct Example**:
  ```python
  def foo():
      return 1


  def bar():
      return 2
  ```

### Blank Lines for Methods (PEP 8 Strict)

- **One** blank line between two `def` statements that are methods of the same class.
- **One** blank line between a `class` keyword line and the first method it contains.
- The first method inside a class body is preceded by exactly one blank line.

- **Incorrect Example**:
  ```python
  class Calculator:
      def add(self, a, b):
          return a + b
      def subtract(self, a, b):
          return a - b
  ```
- **Correct Example**:
  ```python
  class Calculator:
      def add(self, a, b):
          return a + b

      def subtract(self, a, b):
          return a - b
  ```

### Blank Lines for Multi-line Code Blocks (Project Rule)

In addition to the PEP 8 strict definition separators above, the formatter applies the project's house blank-line rule: any multi-line statement or code block is surrounded by exactly one blank line above and below, with the following exceptions:

- **No blank line above** if the multi-line code/block is the first non-blank line at its current indentation level.
- **No blank line below** if the multi-line code/block is the last non-blank line at its current indentation level (followed by a `def`/`class` boundary or the end of the file).

A "multi-line statement" includes:

- A line that ends with a continuation indicator (an unclosed `(`/`[`/`{`, a backslash, an assignment, or a binary operator).
- A function call or definition whose argument list is split across multiple lines.
- A multi-line collection literal (`[...]`, `{...}`, or `(...)` that closes on a later line).

A "code block" is the body of a `def`, `class`, `if`, `elif`, `else`, `for`, `while`, `try`, `except`, `finally`, `with`, or `match`/`case` statement.

### Import Sorting

The formatter collects every top-level `import` and `from ... import ...` statement, classifies each into one of three groups, sorts each group alphabetically (case-insensitive), and reassembles them at the top of the file. The three groups, in order, are:

1. **Standard library**: modules bundled with Python (e.g., `os`, `sys`, `pathlib`, `collections`, `typing`), detected by a lowercase module name with no `.` separator.
2. **Third-party**: any non-stdlib, non-local import (e.g., `numpy`, `pandas`, `requests`).
3. **Local/relative**: any import whose top-level module starts with `.` (relative) or matches a local package prefix.

Behavior details:

- The three groups are separated by exactly one blank line.
- Combined imports on a single line (e.g., `import os, sys`) are split into separate lines.
- Inline `# comment` text and `as` aliases are preserved.
- Comments and blank lines inside the import block are preserved as group boundaries.
- A blank line is automatically inserted between the import block and the first non-import top-level statement.
- Files with no imports are left unchanged by the import-sorting pass.

- **Incorrect Example**:
  ```python
  import requests
  import os
  from collections import OrderedDict

  def main():
      pass
  ```
- **Correct Example**:
  ```python
  from collections import OrderedDict
  import os
  import sys
  import requests

  def main():
      pass
  ```

### Token-Aware Processing

The formatter tokenizes the source character stream before applying any structural formatting. The following token kinds are recognized:

- **Code**: ordinary Python source code.
- **SingleLineComment**: `# ...` to end of line.
- **String**: `"..."` or `'...'` with backslash escape handling.
- **MultiLineString**: `"""..."""` or `'''...'''` triple-quoted strings.
- **RawString / FormatString / Bytes**: prefixes `r`, `R`, `f`, `F`, `b`, `B`, `rb`, `br`, etc., in any combination are honored.

The content inside string, multi-line string, and comment tokens is never modified (except for the trim-trailing-whitespace pass, which leaves any line whose last non-whitespace character is inside a string or comment token untouched). Indentation and blank-line passes use the code mask so that `# comments`, docstrings, and string content do not affect the indentation or blank-line decisions for surrounding code.

- **Scenario**: A line `s = "if x: pass"` is preserved verbatim; the `if`/`pass` keywords inside the string do not trigger any block-start rules.
- **Scenario**: A multi-line docstring `"""..."""` with internal blank lines and indentation is preserved exactly as written.

### Indentation Recomputation

The formatter recomputes the leading whitespace of each line based on the actual nesting depth of `def`, `class`, `if`/`elif`/`else`, `for`, `while`, `try`/`except`/`finally`, `with`, and `match`/`case` blocks. Inconsistent indentation in the input (e.g., 2 spaces in some places, 4 in others) is normalized to 4 spaces per level. Continuation lines (those that follow a line ending in an unclosed bracket, a backslash, or a binary operator) keep their relative indentation.

### Idempotency

The formatter is idempotent: running it on the output of a previous run produces no changes. This holds for all transformations in the pipeline (whitespace normalization, tab expansion, line-ending normalization, import sorting, indentation, blank-line rules, trailing whitespace removal, EOF newline enforcement).

### EOF Newline

Every output file ends with exactly one `\n` and no other trailing whitespace.

### File Encoding

All output files are written as UTF-8 without BOM.

## Skipping Logic

A file is considered "skipped" when the formatted content is byte-for-byte identical to the original. The following directory names are pruned from recursive discovery (case-insensitive) and any file inside them is left untouched:

- `build` (universal)
- `venv` (Python virtual environment)
- `.venv` (Python virtual environment)

## Out of Scope (v1)

- No import normalization beyond grouping and sorting (no removal of unused imports, no rewriting `from x import a, b` into separate lines unless they were originally combined).
- No reformatting of type annotations or dataclass field declarations.
- No conversion between string-quote styles (single ↔ double).
- No reformatting of f-string contents.
- No insertion of trailing commas.
- No enforcement of `import` placement relative to `from ... import ...` (both are allowed within the same group, sorted alphabetically).
- No reformatting of `__all__`, `if __name__ == "__main__":` blocks, or assertion statements.
- No reformatting of `async def` or `await` statements beyond treating `async def` as a block-start keyword equivalent to `def`.
- No reformatting of `*args`, `**kwargs`, or `*`, `/` separator parameters.
- No restructuring of comprehensions, generator expressions, or lambdas.
- No comment-formatting (e.g., shebang `#!/usr/bin/env python`, encoding declarations) beyond preserving them.

## Snapshot Test Cases

The formatter is verified by the following snapshot test cases under `LafnyaToolkit.Tests/Snapshots/Python/`:

- `01-basic.in` / `.expected` — top-level imports, two top-level functions separated by 2 blank lines.
- `02-class.in` / `.expected` — a class with 3 methods (separated by 1 blank line each), and a top-level function after the class (separated by 2 blank lines).
- `03-control-flow.in` / `.expected` — `if/elif/else`, `for`, `while`, `try/except/finally`, `with`, `match/case`.
- `04-imports.in` / `.expected` — mixed-order stdlib/third-party/local imports, combined imports, comments inside the import block.
- `05-comments-docstrings.in` / `.expected` — module docstring, function docstrings (single-line and multi-line), `#` comments.
- `06-multiline-statements.in` / `.expected` — multi-line statements (split argument lists, list/dict/set literals, backslash continuation) and a nested `def` at the bottom.
- `07-line-length.in` / `.expected` — long lines wrapped at 80 characters.

## Reference Formatted Example

**Input**:
```python
"""Basic Python module with top-level imports and functions."""


import os
import sys
from pathlib import Path


def foo():
  return 1


def bar():
  return 2
```

**Output**:
```python
"""Basic Python module with top-level imports and functions."""

from pathlib import Path
import os
import sys

def foo():
    return 1


def bar():
    return 2
```

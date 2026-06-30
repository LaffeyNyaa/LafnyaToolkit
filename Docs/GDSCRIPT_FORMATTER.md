# GDScript Formatter

## Target Version

This formatter targets **GDScript as shipped with Godot 4.4.1** (GDScript 2.x syntax: `@export`/`@onready`/`@tool` annotations, `class_name`/`extends`, type annotations, property setter/getter blocks, `match` statements, string interpolation, etc.).

## Overview

The GDScript formatting tool is located in the `GDScriptFormatter` directory.
- **Arguments**: Accepts a target directory as an argument and formats all GDScript files within it, including its subdirectories.
- **Validation**: If no argument is provided or the specified directory does not exist, the tool will output a prompt/error message.
- **addons Exclusions**: Directories named `addons` (case-insensitive) and all their contents are automatically excluded from formatting. This prevents accidental modification of third-party Godot plugin code.

## Execution Logs

- **Progress**: Displays messages indicating whether a file is currently being formatted or skipped.
- **Skip Condition**: A file is considered "skipped" if its content remains identical before and after the formatting process.
- **Summary**: Upon completion, the tool outputs a summary showing the total number of files found, the number of files formatted, and the number of files skipped.

## Formatting Rules

### Blank Lines for Code Blocks and Multi-line Statements

- Maintain exactly **one blank line** above and below code blocks and multi-line statements.
- **Exceptions**:
  - Do not add a blank line above a code block or multi-line statement if it is at the very beginning of its parent block.
  - Do not add a blank line below a code block or multi-line statement if it is at the very end of its parent block.

**Incorrect Example:**
```gdscript
int i = 0;
for n in numbers:
    print(n)
    if i == 5:
        print("i is 5")
```

**Correct Example:**
```gdscript
int i = 0;

for n in numbers:
    print(n)

    if i == 5:
        print("i is 5")
```

### Indentation

- Use exactly **4 spaces** for each level of indentation.

### Line Length Limit

- The maximum line length is **80 characters**.
- Statements exceeding 80 characters must be split into multi-line statements.
- Continuation lines (all lines except the first one) must be indented one additional level deeper than the first line.

### Blank Lines for Classes and Methods

- Maintain exactly **two blank lines** above and below class definitions and method declarations.

### Empty Lines: Documentation Comments

- **General Rule**: Keep exactly one empty line above a documentation comment block (`##`) when the preceding non-blank line is a code statement. This separates the "doc comment + declaration" logical unit from the previous unit.
- **Exceptions** (no empty line added above the `##`):
  - The previous non-blank line is itself a `##` documentation comment (multi-line doc continuation).
  - The previous non-blank line is a regular comment (`#` without `##`) attached to the declaration below.
  - The previous non-blank line is a file header line (`@tool`, `@icon`, `class_name`, `extends`).
- *Incorrect*:
  ```gdscript
  ## Doc for x.
  var x := 0
  ## Doc for y.
  var y := 1
  ```
- *Correct*:
  ```gdscript
  ## Doc for x.
  var x := 0

  ## Doc for y.
  var y := 1
  ```

### End-of-File (EOF) Newline

- Every file must end with exactly **one newline character**.
- If this rule conflicts with other spacing rules, the EOF newline takes priority, and there must be exactly one trailing newline.

### Preserve blank lines between single-line statements

- Preserves author-inserted blank lines between adjacent plain single-line statements that share the same indentation level. In `BlankLineProcessor.ComputeDesiredBlanksAbove`, after every other rule has left the desired count at `0`, if the original input had a blank line above the current line (`nonBlank[curIdx].HadBlankAbove`), the previous and current lines have equal indentation, and both are plain single-line statements, the desired count is set to `1`.
- **Only preserves, never adds**: the rule fires only when `HadBlankAbove` is already true. It never inserts a blank where the author placed none; it solely prevents the "align downward" logic from stripping an existing author blank.
- **Idempotent**: running the formatter again produces the same output, because the decision depends only on the original `HadBlankAbove` flag and the (unchanged) line contents.
- **Plain single-line statement** — `IsPlainSingleLineStatement(trimmed)` returns `true` when the trimmed line is:
  - non-empty;
  - not a comment (does not start with `#`);
  - not a block-start line (`TextUtils.IsBlockStartLine` returns `false`; in GDScript, block-starts end with `:`), so `func`/`class` declarations are excluded;
  - not a file-header line (`TextUtils.IsFileHeaderLine` returns `false`; covers `class_name`, `extends`, etc.);
  - not an annotation line (does not start with `@`).
  - `var`/`const`/`signal`/`enum` declarations **are** treated as plain single-line statements, so author-inserted blanks between them are preserved (consistent with C# field initializers). `func`/`class` declarations end with `:` and are excluded as block-starts.

**Example:**
```gdscript
func run():
    var lineStarts = [0, 0, 0]

    var enumRanges = []
    var depth = 0
    var enumDepth = -1
    var enumStart = -1
    var pendingEnum = false

    for i in lineStarts:
        print(i)
    var a = 1
    var b = 2
    var c = 3
```
The blank between `lineStarts` and `enumRanges` is preserved. No blank is inserted between `enumRanges`/`depth`/`enumDepth`/`enumStart`/`pendingEnum`, nor between `var a`/`var b`/`var c`.

### Enum Formatting

- Write each enum value on a separate line.
- Always include a trailing comma after the last enum value.

**Incorrect Example:**
```gdscript
enum TokenType {
    CODE, STRING, VERBATIM_STRING, CHAR, SINGLE_LINE_COMMENT, MULTI_LINE_COMMENT
}
```

**Correct Example:**
```gdscript
enum TokenType {
    CODE,
    STRING,
    VERBATIM_STRING,
    CHAR,
    SINGLE_LINE_COMMENT,
    MULTI_LINE_COMMENT,
}
```

## Class Member Organization

Organize class members in the following strict order:

1. Engine annotations (`@tool`, `@icon`, `@static_unload`)
2. `class_name`
3. `extends`
4. Doc comments
5. Signals
6. Enums
7. Constants
8. Static variables
9. `@export` variables
10. Remaining regular variables
11. `@onready` variables
12. Private variables
13. `_static_init()`
14. Remaining static methods
15. Overridden built-in virtual methods (in this exact order):
    - `_init()`
    - `_enter_tree()`
    - `_ready()`
    - `_process()`
    - `_physics_process()`
16. Remaining virtual methods
17. Overridden custom methods
18. Public methods
19. Setter/Getter methods
20. Callback methods (prefixed with `on_`)
21. Private methods (prefixed with `_`)

### Blank Lines Between Class Members

- Insert exactly **one blank line** between each of the following variable groups:
  - Signals
  - Enums
  - Constants
  - Static variables
  - `@export` variables
  - Remaining regular variables
  - `@onready` variables
  - Private variables
- If a property specifies a custom `getter` or `setter`, it must always be surrounded by **one blank line** above and below.

**Example:**
```gdscript
class_name StateMachine
extends Node

## Hierarchical State machine for the player.
##
## Initializes states and delegates engine callbacks ([method Node._physics_process],
## [method Node._unhandled_input]) to the state.

signal state_changed(previous, new)

@export var initial_state: Node

var is_active = true:
	set = set_is_active

@onready var _state = initial_state:
	set = set_state

@onready var _state_name = _state.name
```

## Implementation Notes

### Processing Pipeline

`Format` applies rules in a fixed order: (1) normalize line endings (`\r\n`/`\r` → `\n`); (2) tab normalization restricted to `Code` regions (see below); (3) `ExpandEnums`; (4) re-tokenize once and reuse the resulting tokens and code mask across re-indentation and line-length splitting; (5) apply blank-line rules; (6) collapse excessive blank lines; (7) trim trailing whitespace; (8) apply line-length splitting; (9) ensure a single trailing newline. Line-ending normalization happens before `ExpandEnums` to avoid producing mixed line endings after enum expansion.

### Lexical Analysis & Prefixed String Literals

The tokenizer recognizes four token kinds: `Code`, `String`, `TripleString`, and `Comment`. In addition to ordinary string literals, it recognizes GDScript 2.x prefixed string literals:
- **Raw strings**: `r"..."`, `r'...'`, `r"""..."""`, `R"..."` — the prefix character (`r`/`R`) is emitted as a separate `Code` token, and the following string is scanned with the standard string/triple-string logic. The prefix is only recognized when the preceding character is not a word character, so identifiers ending in `r` are not misread.
- **StringName literals**: `&"..."`, `&'...'` — the `&` prefix is emitted as `Code`, the string as `String`/`TripleString`.
- **NodePath literals**: `^"..."`, `^'...'` — the `^` prefix is emitted as `Code`, the string as `String`/`TripleString`.

As a result, `#`, `"`, and `'` inside these prefixed literals are never misinterpreted as lexical boundaries (comment starts or string delimiters), so the contents of raw strings, StringNames, and NodePaths are preserved exactly.

### Tab Normalization

Tabs are replaced with 4 spaces **only** at positions marked as `Code` by the tokenizer's code mask. Tabs inside string literals (including triple-quoted and raw strings) and inside comments are preserved verbatim, so string contents are never modified.

### Class Member Reordering

The formatter does **not** physically reorder class members. The 21-category ordering above is a style recommendation for authors. The formatter only enforces blank-line spacing between different member groups — it inserts one blank line between adjacent top-level members that belong to different groups, but never moves members. This avoids breaking `@onready`/`@export` initialization-order dependencies.

### Inline Construct Expansion

The formatter does **not** expand inline single-line constructs. `if x: y()`, `for n in arr: f(n)`, inline property setters (`var x: set = set_x`), and similar one-liners are preserved as-is, subject only to indentation, trailing-whitespace, and EOF rules.

### Colon-Based Block Detection

Indentation depth is recomputed from the source structure: a code line whose last non-whitespace character is `:` opens a new block (depth +1 for subsequent lines until a dedent). Colons inside `(...)`, `[...]`, or `{...}` (e.g., dict literals, slice syntax, type annotations) do **not** open blocks.

### Continuation Lines

A line is treated as a continuation (not a new statement) when:
- The running bracket depth (parentheses and square brackets only; braces are handled via the block stack) across the file is greater than zero at the start of that line, or
- The previous line ended with a trailing `\` **and** that backslash is located in a `Code` region.

Backslashes inside comments or string literals do **not** trigger continuation. A doubled backslash (`\\`) in `Code` is treated as a non-continuation. Continuation lines are indented one additional level beyond the statement's base indentation. Blank-line rules are not applied between a line and its continuation.

### Line Length Splitting

Lines exceeding 80 characters are split, in priority order, by attempting:
1. **Unclosed-bracket comma split**: if brackets are still open at the end of the line, split after the last comma inside brackets such that the first segment is ≤ 80 characters.
2. **Closed-bracket comma split**: even when all brackets on the line are balanced, if a comma exists inside brackets, split after the last such comma that keeps the first segment ≤ 80 characters.
3. **Top-level `=` wrap**: if a top-level `=` exists (excluding `==`, `!=`, `<=`, `>=`, `+=`, `-=`, `*=`, `/=`, `:=`) and the right-hand side is not already parenthesized, wrap the RHS in `(...)` and recursively split.
4. **No safe split**: if none of the above applies safely (e.g., a single over-long string literal), the line is left unchanged rather than emit invalid GDScript.

Split continuation lines are indented one additional level, and splitting recurses until all segments are ≤ 80 characters or no safe split point remains.

### Top-Level Member Classification

Both `static var` and `static func` are recognized as top-level members for blank-line spacing. Classification follows a first-match-wins rule:
1. `signal` → Signals
2. `enum` → Enums
3. `const` → Constants
4. `static var` → Static variables
5. `@export` → Export variables
6. `@onready` → Onready variables
7. Name starts with `_` → Private
8. Otherwise → Regular

`static func` falls through the variable-group checks and is classified by name (private if it starts with `_`, otherwise regular), the same as a non-static `func`. This guarantees that one blank line is inserted between a `static var` and an adjacent `static func` (they belong to different groups), while two adjacent `static func`s of the same name-privacy group are not separated by an extra group-break blank line.

For example, `@export var _hidden: int` is classified as **@export** (rule 5 takes precedence over rule 7), and `@onready var _state: Node` is classified as **@onready** (rule 6 takes precedence over rule 7).

### Move File-Level Doc Comments

When `##` doc-comment blocks appear at the very top of a file — before any file header line (`@tool`, `@icon`, `@static_unload`, `class_name`, `extends`) — they are moved to immediately after the last file header line. This places class-level documentation in its logical position.

- If no file headers are present, the doc comments remain in place and attach to the first declaration as usual.
- If the doc comments are already positioned after the file headers, no change is made (idempotent).
- Only `##` doc-comment lines are moved; regular `#` comments at the top of the file are unaffected.

**Example** — before formatting:
```gdscript
## @brief Player node spawner that creates and removes player nodes
## by network peer ID, keeping game state in sync.
class_name PlayerSpawner
extends Node3D

@export var game := Game.new()
```

After formatting:
```gdscript
class_name PlayerSpawner
extends Node3D
## @brief Player node spawner that creates and removes player nodes
## by network peer ID, keeping game state in sync.

@export var game := Game.new()
```

### Doc Comment Attachment

`##` doc-comment blocks (consecutive non-blank lines starting with `##`) are **always** attached to the immediately following declaration, even when a blank line originally separated the doc comment from the declaration. This preserves the doc-to-declaration association.

**Exception — File-level doc comments**: When a `##` doc-comment block appears at file level (its nearest preceding non-blank, non-doc-comment line is a file header such as `class_name`, `extends`, `@tool`, `@icon`, or `@static_unload`), it is treated as a class-level documentation comment rather than being force-attached to the declaration below. In this case:
- No blank line is inserted between the file header and the `##` block (the doc comment stays immediately below `extends`).
- One blank line is inserted between the `##` block and the following `const`/`var`/`signal`/`enum` declaration.
- Two blank lines are inserted between the `##` block and the following `func`/`class` declaration.

**Example** — file-level doc comment:

Before formatting:
```gdscript
class_name MapGenerator
extends RefCounted

## Generates province-based map data using Voronoi tessellation and layered
## noise. Produces warped polygon meshes with terrain types, height values,
## and shared noise textures for rendering.

const TERRAIN_OCEAN_THRESHOLD: float = -0.1
```

After formatting:
```gdscript
class_name MapGenerator
extends RefCounted
## Generates province-based map data using Voronoi tessellation and layered
## noise. Produces warped polygon meshes with terrain types, height values,
## and shared noise textures for rendering.

const TERRAIN_OCEAN_THRESHOLD: float = -0.1
```

Ordinary `#` comments retain the original-attachment behavior: they attach to the following declaration only when no blank line originally separated them.

### File I/O Behavior

- **Encoding (Read)**: The formatter auto-detects the file encoding via byte order marks (BOM). Supported encodings: UTF-8 (with/without BOM), UTF-16 LE (with BOM), UTF-16 BE (with BOM), UTF-32 LE (with BOM), UTF-32 BE (with BOM). Files without a BOM are read as UTF-8.
- **Encoding (Write)**: After formatting, the file is always written as UTF-8 without BOM, regardless of the original encoding. If the formatted content is identical to the original (and the original was already UTF-8 without BOM), the file is skipped and not rewritten.
- **Atomic writes**: Output is first written to a temporary file in the same directory, then atomically swapped into place via `File.Replace` (with a `Delete` + `Move` fallback when `File.Replace` is unsupported). A failed write does not corrupt the original file.
- **Per-file resilience**: If reading or writing a single file raises an exception, the tool prints an error message naming the file and continues processing the remaining files.
- **Exit code**: The process exit code equals the number of files that failed (0 = all files processed successfully).
- **Summary line**: `Total: N, Formatted: F, Skipped: S`; when there are failures, `, Failed: W` is appended.

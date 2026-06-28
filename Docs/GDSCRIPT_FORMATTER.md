# GDScript Formatter

## Target Version

This formatter targets **GDScript as shipped with Godot 4.4.1** (GDScript 2.x syntax: `@export`/`@onready`/`@tool` annotations, `class_name`/`extends`, type annotations, property setter/getter blocks, `match` statements, string interpolation, etc.).

## Overview

The GDScript formatting tool is located in the `GDScriptFormatter` directory.
- **Arguments**: Accepts a target directory as an argument and formats all GDScript files within it, including its subdirectories.
- **Validation**: If no argument is provided or the specified directory does not exist, the tool will output a prompt/error message.

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

### End-of-File (EOF) Newline

- Every file must end with exactly **one newline character**.
- If this rule conflicts with other spacing rules, the EOF newline takes priority, and there must be exactly one trailing newline.

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

### Class Member Reordering

The formatter does **not** physically reorder class members. The 21-category ordering above is a style recommendation for authors. The formatter only enforces blank-line spacing between different member groups — it inserts one blank line between adjacent top-level members that belong to different groups, but never moves members. This avoids breaking `@onready`/`@export` initialization-order dependencies.

### Inline Construct Expansion

The formatter does **not** expand inline single-line constructs. `if x: y()`, `for n in arr: f(n)`, inline property setters (`var x: set = set_x`), and similar one-liners are preserved as-is, subject only to indentation, trailing-whitespace, and EOF rules.

### Colon-Based Block Detection

Indentation depth is recomputed from the source structure: a code line whose last non-whitespace character is `:` opens a new block (depth +1 for subsequent lines until a dedent). Colons inside `(...)`, `[...]`, or `{...}` (e.g., dict literals, slice syntax, type annotations) do **not** open blocks.

### Continuation Lines

A line is treated as a continuation (not a new statement) when:
- The running bracket depth across the file is greater than zero at the start of that line, or
- The previous line ended with a trailing `\`.

Continuation lines are indented one additional level beyond the statement's base indentation. Blank-line rules are not applied between a line and its continuation.

### Line Length Splitting

Lines exceeding 80 characters are split using GDScript-valid continuation:
- After commas inside already-open brackets (implicit continuation).
- For long expressions without open brackets, the right-hand side is wrapped in `(...)` and then split after operators or commas. The parenthesized form is semantically identical in GDScript.
- If no safe split point exists (e.g., a single over-long string literal), the line is left unchanged rather than emit invalid GDScript.

### Variable-Group Classification Precedence

When classifying top-level members for blank-line spacing, the first-match-wins rule applies:
1. `signal` → Signals
2. `enum` → Enums
3. `const` → Constants
4. `static var` → Static variables
5. `@export` → Export variables
6. `@onready` → Onready variables
7. Name starts with `_` → Private
8. Otherwise → Regular

For example, `@export var _hidden: int` is classified as **@export** (rule 5 takes precedence over rule 7), and `@onready var _state: Node` is classified as **@onready** (rule 6 takes precedence over rule 7).

using System;

using GDScriptFormatter;

using LafnyaToolkit.Tests;

namespace LafnyaToolkit.Tests.Idempotency
{
    /// <summary>
    /// Idempotency property tests for the GDScript formatter. For each
    /// representative input, the test asserts that
    /// <c>Format(Format(x)) == Format(x)</c>.
    /// </summary>
    public sealed class GDScriptIdempotencyTests
    {
        /// <summary>
        /// Runs all idempotency assertions for the GDScript formatter.
        /// </summary>
        public void TestIdempotency(bool unused)
        {
            RunCase("empty", string.Empty);
            RunCase("comment only", "# just a comment\n");
            RunCase("doc comment", "## doc comment\n");

            RunCase("class with member",
                "class_name Foo\nextends Node\nvar x:int=0\nfunc _ready()->void:x=1\n");
            RunCase("if else", "if x>0:\n    a()\nelse:\n    b()\n");
            RunCase("for loop", "for i in range(10):\n    print(i)\n");
            RunCase("while loop", "while x>0:\n    x-=1\n");

            RunCase("func with args",
                "func add(a:int,b:int)->int:\n    return a+b\n");

            RunCase("signal",
                "signal pressed\nsignal value_changed(new_value:int)\n");
            RunCase("enum", "enum State{IDLE,RUNNING,STOPPED}\n");

            RunCase("const",
                "const MAX_SPEED:int=100\nconst PI:float=3.14159\n");

            RunCase("annotation export",
                "@export var speed:int=10\n@onready var sprite:Sprite=$Sprite\n");
            RunCase("string", "var s:String=\"hello\\nworld\"\n");
            RunCase("raw string", "var s:String=r\"raw\\nstring\"\n");

            RunCase("triple quote",
                "var s:String=\"\"\"\nmulti\nline\n\"\"\"\n");

            RunCase("dictionary",
                "var d:Dictionary={\"a\":1,\"b\":2,\"c\":3}\n");
            RunCase("array", "var a:Array=[1,2,3,4,5]\n");

            RunCase("match statement",
                "match x:\n    1:\n        a()\n    2:\n        b()\n    _:\n        c()\n");

            RunCase("nested func",
                "func outer()->void:\n    func inner()->void:\n        pass\n");
            RunCase("trailing whitespace", "var x:int=1   \nvar y:int=2\t\n");

            RunCase("arg list splitting",
                "class_name Foo\nextends Node\n" +
                "func _apply_compositions_recursive(active_compositions, states, compositions, layers_info):\n" +
                "    pass\n");

            RunCase("operator chain",
                "class_name Foo\nextends Node\n" +
                "func _ready() -> void:\n" +
                "    avatars_container = Node.new()\n" +
                "    if (\n" +
                "        avatars_container\n\n" +
                "        and not avatars_container.goto_sub_location_requested.is_connected(\n" +
                "            _on_goto_sub_location_requested\n" +
                "        )\n\n" +
                "    ):\n" +
                "        avatars_container.goto_sub_location_requested.connect(_on_goto_sub_location_requested)\n");

            RunCase("top level equals wrap",
                "class_name Foo\nextends Node\n" +
                "func _create_player() -> void:\n" +
                "    player_view = (\n" +
                "        (await AsyncResourceLoader.load_resource_async(\n" +
                "                \"res://gameplay/player/player_view/player_view.tscn\"\n" +
                "    ))\n\n" +
                "        . instantiate()\n" +
                "    )\n");
        }

        private static void RunCase(string name, string input)
        {
            string first = Formatter.Instance.Format(input);
            string second = Formatter.Instance.Format(first);
            TestHarness.AssertEqual(first, second);
        }
    }
}

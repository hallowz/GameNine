using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Voidborne.Automation
{
    /// <summary>
    /// Parses and executes simple automation rules written in the Terminal's Scripts tab.
    ///
    /// Supported commands:
    ///   IF [item_name] > [amount] THEN PAUSE [machine_id]
    ///   IF [item_name] < [amount] THEN ACTIVATE [machine_id]
    ///   SEND [item_name] TO [machine_id] WHEN [item_name] < [amount]
    ///   SET [machine_id] RECIPE [recipe_name]
    ///   ALERT WHEN [item_name] < [amount]
    ///
    /// Scripts run on every automation tick.
    /// Errors display in the Scripts tab with line number.
    ///
    /// Lore note: this syntax is the same underlying language VORD uses for Kin directive control.
    /// </summary>
    public class AutomationScriptEngine
    {
        // ── Public results ─────────────────────────────────────────────────

        /// <summary>Error messages from last compile. Key = 1-based line number.</summary>
        public Dictionary<int, string> CompileErrors { get; } = new Dictionary<int, string>();

        /// <summary>Runtime alerts generated this tick.</summary>
        public List<string> RuntimeAlerts { get; } = new List<string>();

        // ── Private ────────────────────────────────────────────────────────

        private readonly List<ParsedInstruction> _instructions = new List<ParsedInstruction>();
        private ComputerTerminal _terminal;

        // ── API ────────────────────────────────────────────────────────────

        public void SetTerminal(ComputerTerminal terminal) => _terminal = terminal;

        /// <summary>Compile a script string. Returns true if error-free.</summary>
        public bool Compile(string script)
        {
            _instructions.Clear();
            CompileErrors.Clear();

            if (string.IsNullOrWhiteSpace(script)) return true;

            string[] lines = script.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("//")) continue;
                if (!ParseLine(line, i + 1, out var instruction))
                    CompileErrors[i + 1] = $"Line {i + 1}: Syntax error — \"{line}\"";
                else
                    _instructions.Add(instruction);
            }

            return CompileErrors.Count == 0;
        }

        /// <summary>Execute all compiled instructions. Call once per automation tick.</summary>
        public void Execute()
        {
            RuntimeAlerts.Clear();
            if (_terminal == null) return;
            foreach (var inst in _instructions)
                ExecuteInstruction(inst);
        }

        // ── Parsing ────────────────────────────────────────────────────────

        private bool ParseLine(string line, int lineNum, out ParsedInstruction result)
        {
            result = default;
            string[] tokens = line.Split(new char[]{' '}, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) return false;

            try
            {
                // IF [item] > [amount] THEN PAUSE [machine_id]
                // IF [item] < [amount] THEN ACTIVATE [machine_id]
                if (tokens[0].Equals("IF", StringComparison.OrdinalIgnoreCase))
                {
                    // Rebuild item name (may have spaces) — find operator token
                    int opIdx = FindOperatorIndex(tokens, 1);
                    if (opIdx < 0) return false;
                    string itemName  = JoinTokens(tokens, 1, opIdx - 1);
                    string op        = tokens[opIdx];          // > or <
                    if (!int.TryParse(tokens[opIdx + 1], out int amount)) return false;
                    // THEN keyword
                    int thenIdx = opIdx + 2;
                    if (thenIdx >= tokens.Length || !tokens[thenIdx].Equals("THEN", StringComparison.OrdinalIgnoreCase)) return false;
                    string action    = tokens[thenIdx + 1].ToUpperInvariant(); // PAUSE or ACTIVATE
                    string machineId = JoinTokens(tokens, thenIdx + 2, tokens.Length - 1);

                    InstructionType type = action == "PAUSE"    ? InstructionType.IfPause    :
                                          action == "ACTIVATE"  ? InstructionType.IfActivate : InstructionType.Invalid;
                    if (type == InstructionType.Invalid) return false;

                    result = new ParsedInstruction
                    {
                        type      = type,
                        itemName  = itemName,
                        op        = op,
                        amount    = amount,
                        machineId = machineId
                    };
                    return true;
                }

                // SEND [item] TO [machine_id] WHEN [item] < [amount]
                if (tokens[0].Equals("SEND", StringComparison.OrdinalIgnoreCase))
                {
                    int toIdx = FindKeyword(tokens, "TO", 1);
                    if (toIdx < 0) return false;
                    string itemName = JoinTokens(tokens, 1, toIdx - 1);
                    int whenIdx = FindKeyword(tokens, "WHEN", toIdx + 1);
                    if (whenIdx < 0) return false;
                    string machineId = JoinTokens(tokens, toIdx + 1, whenIdx - 1);
                    int opIdx2 = FindOperatorIndex(tokens, whenIdx + 1);
                    if (opIdx2 < 0) return false;
                    string condItem = JoinTokens(tokens, whenIdx + 1, opIdx2 - 1);
                    if (!int.TryParse(tokens[opIdx2 + 1], out int amount2)) return false;
                    result = new ParsedInstruction
                    {
                        type       = InstructionType.Send,
                        itemName   = itemName,
                        machineId  = machineId,
                        condItem   = condItem,
                        op         = tokens[opIdx2],
                        amount     = amount2
                    };
                    return true;
                }

                // SET [machine_id] RECIPE [recipe_name]
                if (tokens[0].Equals("SET", StringComparison.OrdinalIgnoreCase))
                {
                    int recipeIdx = FindKeyword(tokens, "RECIPE", 1);
                    if (recipeIdx < 0) return false;
                    string machineId  = JoinTokens(tokens, 1, recipeIdx - 1);
                    string recipeName = JoinTokens(tokens, recipeIdx + 1, tokens.Length - 1);
                    result = new ParsedInstruction
                    {
                        type       = InstructionType.SetRecipe,
                        machineId  = machineId,
                        recipeName = recipeName
                    };
                    return true;
                }

                // ALERT WHEN [item] < [amount]
                if (tokens[0].Equals("ALERT", StringComparison.OrdinalIgnoreCase)
                    && tokens.Length > 1 && tokens[1].Equals("WHEN", StringComparison.OrdinalIgnoreCase))
                {
                    int opIdx3 = FindOperatorIndex(tokens, 2);
                    if (opIdx3 < 0) return false;
                    string itemName = JoinTokens(tokens, 2, opIdx3 - 1);
                    if (!int.TryParse(tokens[opIdx3 + 1], out int amount3)) return false;
                    result = new ParsedInstruction
                    {
                        type     = InstructionType.Alert,
                        itemName = itemName,
                        op       = tokens[opIdx3],
                        amount   = amount3
                    };
                    return true;
                }
            }
            catch { /* fall through to return false */ }

            return false;
        }

        // ── Execution ──────────────────────────────────────────────────────

        private void ExecuteInstruction(ParsedInstruction inst)
        {
            switch (inst.type)
            {
                case InstructionType.IfPause:
                case InstructionType.IfActivate:
                {
                    int count = _terminal.QueryStoredAmount(inst.itemName);
                    bool condition = inst.op == ">" ? count > inst.amount : count < inst.amount;
                    if (!condition) break;
                    var node = _terminal.FindNetworkNode(inst.machineId);
                    if (node == null) break;
                    node.IsPausedByScript = (inst.type == InstructionType.IfPause);
                    break;
                }

                case InstructionType.Send:
                {
                    int condCount = _terminal.QueryStoredAmount(inst.condItem ?? inst.itemName);
                    bool condition = inst.op == ">" ? condCount > inst.amount : condCount < inst.amount;
                    if (!condition) break;
                    var node = _terminal.FindNetworkNode(inst.machineId);
                    if (node == null) break;
                    // Extract one item from storage and push to machine
                    ItemStack stack = _terminal.PullFromStorage(inst.itemName, 1);
                    if (!stack.IsEmpty && node is IAutomationNode autoNode)
                    {
                        if (!autoNode.TryInsert(stack))
                            _terminal.PushToStorage(stack); // return if machine rejected
                    }
                    break;
                }

                case InstructionType.SetRecipe:
                {
                    var node = _terminal.FindNetworkNode(inst.machineId);
                    node?.TrySetRecipe(inst.recipeName);
                    break;
                }

                case InstructionType.Alert:
                {
                    int count = _terminal.QueryStoredAmount(inst.itemName);
                    bool condition = inst.op == ">" ? count > inst.amount : count < inst.amount;
                    if (condition)
                        RuntimeAlerts.Add($"ALERT: {inst.itemName} {inst.op} {inst.amount} (current: {count})");
                    break;
                }
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private static int FindOperatorIndex(string[] tokens, int start)
        {
            for (int i = start; i < tokens.Length; i++)
                if (tokens[i] == ">" || tokens[i] == "<") return i;
            return -1;
        }

        private static int FindKeyword(string[] tokens, string keyword, int start)
        {
            for (int i = start; i < tokens.Length; i++)
                if (tokens[i].Equals(keyword, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        private static string JoinTokens(string[] tokens, int from, int to)
        {
            var sb = new StringBuilder();
            for (int i = from; i <= to && i < tokens.Length; i++)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(tokens[i]);
            }
            return sb.ToString();
        }

        // ── Inner types ────────────────────────────────────────────────────

        private enum InstructionType { Invalid, IfPause, IfActivate, Send, SetRecipe, Alert }

        private struct ParsedInstruction
        {
            public InstructionType type;
            public string          itemName;
            public string          op;
            public int             amount;
            public string          machineId;
            public string          condItem;
            public string          recipeName;
        }
    }
}

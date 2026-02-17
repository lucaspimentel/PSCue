using System;
using System.Collections.Generic;
using System.Linq;

namespace PSCue.Module;

public enum ArgumentType
{
    Verb,
    Flag,
    Parameter,
    ParameterValue,
    Standalone
}

public class ParsedArgument
{
    public string Text { get; set; } = string.Empty;
    public ArgumentType Type { get; set; }
    public ParsedArgument? BoundParameter { get; set; }

    public override string ToString() => $"{Type}: {Text}";
}

public class ParsedCommand
{
    public string Command { get; set; } = string.Empty;
    public List<ParsedArgument> Arguments { get; set; } = new();

    public IEnumerable<(string parameter, string value)> GetParameterValuePairs()
    {
        var pairs = new List<(string, string)>();

        for (int i = 0; i < Arguments.Count; i++)
        {
            var arg = Arguments[i];
            if (arg.Type == ArgumentType.Parameter && i + 1 < Arguments.Count)
            {
                var nextArg = Arguments[i + 1];
                if (nextArg.Type == ArgumentType.ParameterValue)
                {
                    pairs.Add((arg.Text, nextArg.Text));
                }
            }
        }

        return pairs;
    }

    public IEnumerable<string> GetFlags()
    {
        return Arguments.Where(a => a.Type == ArgumentType.Flag).Select(a => a.Text);
    }

    public IEnumerable<string> GetVerbs()
    {
        return Arguments.Where(a => a.Type == ArgumentType.Verb).Select(a => a.Text);
    }

    public override string ToString() => $"{Command} [{string.Join(", ", Arguments)}]";
}

public class CommandParser
{
    private readonly HashSet<string> _knownParametersRequiringValues = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _knownFlags = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterParameterRequiringValue(string parameter)
    {
        _knownParametersRequiringValues.Add(parameter);
    }

    public void RegisterFlag(string flag)
    {
        _knownFlags.Add(flag);
    }

    public ParsedCommand Parse(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return new ParsedCommand();
        }

        var parts = SplitCommandLine(commandLine);
        if (parts.Count == 0)
        {
            return new ParsedCommand();
        }

        var result = new ParsedCommand { Command = parts[0] };
        var arguments = parts.Skip(1).ToList();
        var parsedArgs = new List<ParsedArgument>();

        for (int i = 0; i < arguments.Count; i++)
        {
            var arg = arguments[i];

            if (arg.StartsWith("--") && arg.Contains('='))
            {
                var equalIndex = arg.IndexOf('=');
                var param = arg.Substring(0, equalIndex);
                var value = arg.Substring(equalIndex + 1);

                var paramArg = new ParsedArgument { Text = param, Type = ArgumentType.Parameter };
                parsedArgs.Add(paramArg);
                parsedArgs.Add(new ParsedArgument
                {
                    Text = value,
                    Type = ArgumentType.ParameterValue,
                    BoundParameter = paramArg
                });
                continue;
            }

            var parsed = new ParsedArgument { Text = arg };

            if (IsFlag(arg))
            {
                if (IsKnownParameterRequiringValue(arg))
                {
                    parsed.Type = ArgumentType.Parameter;
                }
                else if (IsKnownFlag(arg))
                {
                    parsed.Type = ArgumentType.Flag;
                }
                else if (i + 1 < arguments.Count && !IsFlag(arguments[i + 1]))
                {
                    parsed.Type = ArgumentType.Parameter;
                }
                else
                {
                    parsed.Type = ArgumentType.Flag;
                }
            }
            else
            {
                if (parsedArgs.Count > 0 && parsedArgs[^1].Type == ArgumentType.Parameter)
                {
                    parsed.Type = ArgumentType.ParameterValue;
                    parsed.BoundParameter = parsedArgs[^1];
                }
                else if (parsedArgs.Count == 0)
                {
                    parsed.Type = ArgumentType.Verb;
                }
                else
                {
                    parsed.Type = ArgumentType.Standalone;
                }
            }

            parsedArgs.Add(parsed);
        }

        result.Arguments = parsedArgs;
        return result;
    }

    public ArgumentType DetermineExpectedType(string commandLine)
    {
        var parsed = Parse(commandLine);

        if (parsed.Arguments.Count == 0)
        {
            return ArgumentType.Verb;
        }

        var lastArg = parsed.Arguments[^1];

        if (lastArg.Type == ArgumentType.Parameter)
        {
            return ArgumentType.ParameterValue;
        }

        return ArgumentType.Flag;
    }

    private static bool IsFlag(string arg)
    {
        return arg.StartsWith('-') && arg.Length > 1;
    }

    private bool IsKnownParameterRequiringValue(string arg)
    {
        return _knownParametersRequiringValues.Contains(arg);
    }

    private bool IsKnownFlag(string arg)
    {
        return _knownFlags.Contains(arg);
    }

    private static List<string> SplitCommandLine(string commandLine)
    {
        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        var escapeNext = false;

        for (int i = 0; i < commandLine.Length; i++)
        {
            var c = commandLine[i];

            if (escapeNext)
            {
                current.Append(c);
                escapeNext = false;
                continue;
            }

            if (c == '\\')
            {
                // Only treat backslash as escape if followed by special character
                // This preserves Windows paths like C:\Users\... while still allowing \"
                if (i + 1 < commandLine.Length)
                {
                    var next = commandLine[i + 1];
                    if (next == '\\' || next == '"' || next == '\'')
                    {
                        // Escape sequence - consume the backslash
                        escapeNext = true;
                        continue;
                    }
                }
                // Not an escape sequence - treat backslash as literal
                current.Append(c);
                continue;
            }

            if (c == '"' || c == '\'')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (c == ' ' && !inQuotes)
            {
                if (current.Length > 0)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
        {
            parts.Add(current.ToString());
        }

        return parts;
    }
}

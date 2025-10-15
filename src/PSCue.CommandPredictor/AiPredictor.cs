using System.Management.Automation.Subsystem.Prediction;
using Microsoft.ML.OnnxRuntimeGenAI;

namespace PSCue.CommandPredictor;

public class AiPredictor : ICommandPredictor, IDisposable
{
    private static Model? _model;
    private static Tokenizer? _tokenizer;
    private static readonly object _lock = new();
    private static bool _initialized = false;
    private static bool _initializationFailed = false;

    /// <summary>
    /// Gets the unique identifier for a subsystem implementation.
    /// </summary>
    public Guid Id { get; } = new("a3f8e2c7-9b41-4df3-b178-ac2e55232999");

    /// <summary>
    /// Gets the name of a subsystem implementation.
    /// </summary>
    public string Name => "Lucas AI Predictor";

    /// <summary>
    /// Gets the description of a subsystem implementation.
    /// </summary>
    public string Description => "A PowerShell predictor that uses local AI (Phi-3) for intelligent command suggestions.";

    public AiPredictor()
    {
        // Initialize the model lazily on first use
        EnsureModelLoaded();
    }

    private static void EnsureModelLoaded()
    {
        if (_initialized || _initializationFailed)
        {
            return;
        }

        lock (_lock)
        {
            if (_initialized || _initializationFailed)
            {
                return;
            }

            try
            {
                // Model path - user needs to download Phi-3 model
                // Expected location: %USERPROFILE%\.powershell-predictor\models\phi3
                var modelPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".powershell-predictor",
                    "models",
                    "phi3"
                );

                if (!Directory.Exists(modelPath))
                {
                    Console.WriteLine($"AI Predictor: Model not found at {modelPath}");
                    Console.WriteLine("Download Phi-3 ONNX model from: https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-onnx");
                    _initializationFailed = true;
                    return;
                }

                _model = new Model(modelPath);
                _tokenizer = new Tokenizer(_model);
                _initialized = true;

                Console.WriteLine("AI Predictor: Model loaded successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AI Predictor: Failed to load model: {ex.Message}");
                _initializationFailed = true;
            }
        }
    }

    public SuggestionPackage GetSuggestion(PredictionClient client, PredictionContext context, CancellationToken cancellationToken)
    {
        if (!_initialized || _model == null || _tokenizer == null)
        {
            return default;
        }

        var input = context.InputAst.Extent.Text.AsSpan().TrimEnd();

        if (input.Length == 0 || input.Length < 3)
        {
            return default;
        }

        try
        {
            // Build a prompt that asks the AI to complete the PowerShell command
            var prompt = BuildPrompt(input.ToString(), context);

            // Tokenize the prompt
            var sequences = _tokenizer.Encode(prompt);

            // Generate parameters - optimized for fast suggestions
            using var generatorParams = new GeneratorParams(_model);
            generatorParams.SetSearchOption("max_length", 100); // Keep it short
            generatorParams.SetSearchOption("temperature", 0.3); // Lower temperature for more deterministic output
            generatorParams.SetSearchOption("top_p", 0.9);

            // Create generator and append input sequences
            using var generator = new Generator(_model, generatorParams);
            generator.AppendTokenSequences(sequences);

            // Generate tokens
            var output = new System.Text.StringBuilder();
            using var tokenizerStream = _tokenizer.CreateStream();

            while (!generator.IsDone())
            {
                generator.GenerateNextToken();
                var newToken = generator.GetSequence(0)[^1];
                output.Append(tokenizerStream.Decode(newToken));
            }

            // Parse the output to extract command suggestions
            var suggestions = ParseSuggestions(output.ToString(), input.ToString());

            if (suggestions.Count == 0)
            {
                return default;
            }

            return new SuggestionPackage(suggestions);
        }
        catch (Exception ex)
        {
            // Silently fail - don't disrupt the user's workflow
            Console.WriteLine($"AI Predictor error: {ex.Message}");
            return default;
        }
    }

    private static string BuildPrompt(string input, PredictionContext context)
    {
        // Build a focused prompt for PowerShell command completion
        var promptBuilder = new System.Text.StringBuilder();

        promptBuilder.AppendLine("<|system|>");
        promptBuilder.AppendLine("You are a PowerShell command completion assistant. Given a partial PowerShell command, suggest the most likely completions.");
        promptBuilder.AppendLine("Output only the completed command(s), one per line. Do not include explanations.");
        promptBuilder.AppendLine("<|end|>");
        promptBuilder.AppendLine("<|user|>");
        promptBuilder.Append("Complete this PowerShell command: ");
        promptBuilder.AppendLine(input);
        promptBuilder.AppendLine("<|end|>");
        promptBuilder.Append("<|assistant|>");

        return promptBuilder.ToString();
    }

    private static List<PredictiveSuggestion> ParseSuggestions(string output, string originalInput)
    {
        var suggestions = new List<PredictiveSuggestion>();

        // Extract suggestions from the AI output
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            // Skip lines that look like system/user/assistant markers
            if (line.StartsWith("<|") || line.Contains("Complete this") || line.Contains("PowerShell command"))
            {
                continue;
            }

            // Clean up the suggestion
            var cleaned = line.Trim();

            if (cleaned.Length > 0 && cleaned.Length < 200)
            {
                suggestions.Add(new PredictiveSuggestion(cleaned));

                // Limit to top 3 suggestions
                if (suggestions.Count >= 3)
                {
                    break;
                }
            }
        }

        return suggestions;
    }

    public void Dispose()
    {
        // Note: We keep the model loaded across instances for performance
        // Actual cleanup happens when the PowerShell session ends
    }

    // Static cleanup method to be called on module unload
    public static void Cleanup()
    {
        lock (_lock)
        {
            _tokenizer?.Dispose();
            _model?.Dispose();
            _tokenizer = null;
            _model = null;
            _initialized = false;
        }
    }
}

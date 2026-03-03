namespace PSCue.Shared.KnownCompletions;

using Completions;

internal static class ChafaCommand
{
    public static Command Create() =>
        new("chafa", "Character Art Facsimile — terminal graphics/character art generator")
        {
            Parameters =
            [
                // General options
                new("--files", "Read list of files from PATH") { RequiresValue = true },
                new("--files0", "Read NUL-separated list of files from PATH") { RequiresValue = true },
                new("--help", "Show help (-h)") { Alias = "-h" },
                new("--probe", "Probe terminal capabilities")
                {
                    StaticArguments =
                    [
                        new("auto", "Automatically probe"),
                        new("on", "Enable probing"),
                        new("off", "Disable probing")
                    ]
                },
                new("--probe-mode", "Probe mode")
                {
                    StaticArguments =
                    [
                        new("any", "Any mode"),
                        new("ctty", "Controlling TTY"),
                        new("stdio", "Standard I/O")
                    ]
                },
                new("--version", "Show version"),
                new("--verbose", "Be verbose (-v)") { Alias = "-v" },

                // Output encoding
                new("--format", "Output format (-f)")
                {
                    Alias = "-f",
                    StaticArguments =
                    [
                        new("iterm", "iTerm inline image protocol"),
                        new("kitty", "Kitty terminal graphics protocol"),
                        new("sixels", "Sixel graphics"),
                        new("symbols", "Unicode/ASCII character art")
                    ]
                },
                new("--optimize", "Compress output [0-9] (-O)") { Alias = "-O", RequiresValue = true },
                new("--relative", "Use relative positioning")
                {
                    StaticArguments =
                    [
                        new("on", "Enable relative positioning"),
                        new("off", "Disable relative positioning")
                    ]
                },
                new("--passthrough", "Passthrough mode")
                {
                    StaticArguments =
                    [
                        new("auto", "Automatically detect"),
                        new("none", "No passthrough"),
                        new("screen", "GNU Screen passthrough"),
                        new("tmux", "tmux passthrough")
                    ]
                },
                new("--polite", "Polite mode")
                {
                    StaticArguments =
                    [
                        new("on", "Enable polite mode"),
                        new("off", "Disable polite mode")
                    ]
                },

                // Size and layout
                new("--align", "Horizontal/vertical alignment") { RequiresValue = true },
                new("--clear", "Clear screen before each file"),
                new("--exact-size", "Use exact image size")
                {
                    StaticArguments =
                    [
                        new("auto", "Automatically decide"),
                        new("on", "Enable exact size"),
                        new("off", "Disable exact size")
                    ]
                },
                new("--fit-width", "Fit images to view width"),
                new("--font-ratio", "Font width/height ratio") { RequiresValue = true },
                new("--grid", "Grid layout CxR (-g)") { RequiresValue = true },
                new("--label", "Show file labels")
                {
                    StaticArguments =
                    [
                        new("on", "Enable labels"),
                        new("off", "Disable labels")
                    ]
                },
                new("--link", "Hyperlink mode")
                {
                    StaticArguments =
                    [
                        new("auto", "Automatically detect support"),
                        new("on", "Enable hyperlinks"),
                        new("off", "Disable hyperlinks")
                    ]
                },
                new("--margin-bottom", "Bottom margin in rows") { RequiresValue = true },
                new("--margin-right", "Right margin in columns") { RequiresValue = true },
                new("--scale", "Scale image (number or \"max\")") { RequiresValue = true },
                new("--size", "Max output dimensions WxH (-s)") { Alias = "-s", RequiresValue = true },
                new("--stretch", "Stretch image to fit, ignoring aspect ratio"),
                new("--view-size", "View size WxH") { RequiresValue = true },

                // Animation and timing
                new("--animate", "Enable animation")
                {
                    StaticArguments =
                    [
                        new("on", "Enable animation"),
                        new("off", "Disable animation")
                    ]
                },
                new("--duration", "Duration in seconds (-d)") { Alias = "-d", RequiresValue = true },
                new("--speed", "Animation speed (multiplier or Nfps)") { RequiresValue = true },
                new("--watch", "Watch file for changes"),

                // Colors and processing
                new("--bg", "Background color (name or hex)") { RequiresValue = true },
                new("--colors", "Color mode (-c)")
                {
                    Alias = "-c",
                    StaticArguments =
                    [
                        new("none", "No colors"),
                        new("2", "2 colors (monochrome)"),
                        new("8", "8 ANSI colors"),
                        new("16/8", "16 foreground + 8 background colors"),
                        new("16", "16 ANSI colors"),
                        new("240", "240 colors"),
                        new("256", "256 colors"),
                        new("full", "Full 24-bit color")
                    ]
                },
                new("--color-extractor", "Color extraction method")
                {
                    StaticArguments =
                    [
                        new("average", "Use average color"),
                        new("median", "Use median color")
                    ]
                },
                new("--color-space", "Color space for processing")
                {
                    StaticArguments =
                    [
                        new("rgb", "RGB color space"),
                        new("din99d", "DIN99d perceptual color space")
                    ]
                },
                new("--dither", "Dithering algorithm")
                {
                    StaticArguments =
                    [
                        new("none", "No dithering"),
                        new("ordered", "Ordered (Bayer) dithering"),
                        new("diffusion", "Error diffusion dithering"),
                        new("noise", "Noise dithering")
                    ]
                },
                new("--dither-grain", "Dither grain dimensions WxH (1,2,4,8)") { RequiresValue = true },
                new("--dither-intensity", "Dither intensity multiplier [0.0-inf]") { RequiresValue = true },
                new("--fg", "Foreground color (name or hex)") { RequiresValue = true },
                new("--invert", "Swap --fg and --bg colors"),
                new("--preprocess", "Preprocessing (-p)")
                {
                    Alias = "-p",
                    StaticArguments =
                    [
                        new("on", "Enable preprocessing"),
                        new("off", "Disable preprocessing")
                    ]
                },
                new("--threshold", "Transparency threshold [0.0-1.0] (-t)") { Alias = "-t", RequiresValue = true },

                // Resource allocation
                new("--threads", "Number of CPU threads to use") { RequiresValue = true },
                new("--work", "Work level [1-9] (-w)") { Alias = "-w", RequiresValue = true },

                // Symbol encoding extras
                new("--fg-only", "Use foreground colors only"),
                new("--fill", "Symbols to use for fill/gradients") { RequiresValue = true },
                new("--glyph-file", "Font file for glyph metrics") { RequiresValue = true },
                new("--symbols", "Symbol classes for output") { RequiresValue = true }
            ]
        };
}

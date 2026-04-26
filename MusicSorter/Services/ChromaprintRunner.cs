using System.Diagnostics;
using System.IO;

namespace MusicSorter.Services;

/// <summary>
/// Wraps fpcalc.exe (Chromaprint).  fpcalc analyses an audio file and emits
/// FINGERPRINT=... and DURATION=... which AcoustID needs.
/// </summary>
public sealed class ChromaprintRunner
{
    private readonly string _fpcalcPath;
    public bool Available => !string.IsNullOrEmpty(_fpcalcPath);

    public ChromaprintRunner(string? overridePath)
    {
        _fpcalcPath = ResolvePath(overridePath) ?? "";
    }

    public async Task<(string Fingerprint, int Duration)?> FingerprintAsync(string filePath, CancellationToken ct)
    {
        if (!Available) return null;

        var psi = new ProcessStartInfo(_fpcalcPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-json");
        psi.ArgumentList.Add(filePath);

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Could not launch fpcalc");
        var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
        try
        {
            await proc.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            throw;
        }
        var stdout = await stdoutTask;
        if (proc.ExitCode != 0) return null;

        // fpcalc -json produces { "duration": 213.42, "fingerprint": "AQABz..." }
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(stdout);
            var dur = (int)doc.RootElement.GetProperty("duration").GetDouble();
            var fp  = doc.RootElement.GetProperty("fingerprint").GetString() ?? "";
            return string.IsNullOrEmpty(fp) ? null : (fp, dur);
        }
        catch
        {
            // Fallback: parse the plain DURATION=, FINGERPRINT= text format.
            int duration = 0; string fingerprint = "";
            foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith("DURATION=", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(line[9..].Trim(), out duration);
                else if (line.StartsWith("FINGERPRINT=", StringComparison.OrdinalIgnoreCase))
                    fingerprint = line[12..].Trim();
            }
            return string.IsNullOrEmpty(fingerprint) ? null : (fingerprint, duration);
        }
    }

    private static string? ResolvePath(string? overridePath)
    {
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
            return overridePath;

        // Next to the .exe?
        var exeDir = AppContext.BaseDirectory;
        var candidate = Path.Combine(exeDir, "fpcalc.exe");
        if (File.Exists(candidate)) return candidate;

        // On PATH?
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(Path.PathSeparator))
        {
            try
            {
                var p = Path.Combine(dir, "fpcalc.exe");
                if (File.Exists(p)) return p;
            }
            catch { }
        }
        return null;
    }
}

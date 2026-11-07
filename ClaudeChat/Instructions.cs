using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ClaudeChat;

/// <summary>
/// Instructions for Claude (author, 2026-11-07): the Settings tab "Instructions for Claude" edits
/// two files Claude Code reads itself — the personal CLAUDE.md (~\.claude\CLAUDE.md, every
/// conversation and project, the desktop app's Code too) and CLAUDE.md of the Chat folder (plain
/// chats only). A file that does not exist yet starts from a template in the language file, so a
/// new user gets sensible defaults. Per-project instructions stay in the project's own CLAUDE.md
/// (a button opens it in Notepad). Separately, every start through this window appends a short
/// system prompt: History is a plain text field read by a screen reader, so no markdown.
/// </summary>
internal sealed partial class MainWindow
{
    private static string PersonalInstructionsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "CLAUDE.md");

    private string ChatInstructionsPath => Path.Combine(ChatFolderPath, "CLAUDE.md");

    private string ProjectInstructionsPath => Path.Combine(_project, "CLAUDE.md");

    /// <summary>Appended to Claude Code's system prompt on every start through this window.</summary>
    private const string WindowSystemPrompt =
        "The user is talking to you through an accessible desktop window built for screen-reader users. " +
        "Your replies appear in a plain text field that does not render markdown, and a screen reader reads them aloud. " +
        "Write plain text: short paragraphs, lists with numbers or dashes, no bold or italics, no headings, no tables, " +
        "no horizontal rules, no emoji. Use a code block only when the user has to copy a command or code.";

    private string SystemPromptForWindow =>
        _settings.WindowSystemPrompt ?? WindowSystemPrompt;

    /// <summary>The file's text for an EDIT control (CRLF), or null when it does not exist.</summary>
    private static string? ReadInstructions(string path)
    {
        try { return File.Exists(path) ? ToEdit(File.ReadAllText(path, Encoding.UTF8)) : null; }
        catch { return null; }
    }

    private static string ToEdit(string s) => s.Replace("\r\n", "\n").Replace("\n", "\r\n");
    private static string FromEdit(string s) => s.Replace("\r\n", "\n");

    /// <summary>Writes the text if it differs from the file (or the file is missing). Returns an
    /// error message, or null.</summary>
    private static string? WriteInstructions(string path, string editText, string? onDisk)
    {
        if (onDisk != null && onDisk == editText) return null;
        if (onDisk == null && editText.Trim().Length == 0) return null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, FromEdit(editText), new UTF8Encoding(false));
            return null;
        }
        catch (Exception e) { return e.Message; }
    }

    /// <summary>Opens the current project's CLAUDE.md in Notepad (created with a heading if missing).</summary>
    private void EditProjectInstructions()
    {
        try
        {
            string path = ProjectInstructionsPath;
            if (!File.Exists(path))
                File.WriteAllText(path, "# " + Path.GetFileName(_project.TrimEnd('\\', '/')) + "\n\n", new UTF8Encoding(false));
            Process.Start(new ProcessStartInfo("notepad.exe") { ArgumentList = { path }, UseShellExecute = false });
        }
        catch (Exception e) { AddEntry(_loc["role.system"], e.Message); }
    }
}

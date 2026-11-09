using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ClaudeChat.Native;

namespace ClaudeChat;

/// <summary>
/// Each project's own program (author, 2026-11-08): Session → Run the project's program (F5) and
/// the same in Manage projects and conversations, instead of a desktop shortcut per project. The
/// first time the program is chosen with the Open dialog and remembered in
/// %APPDATA%\ClaudeChat\programs.json (folder → path): the paths belong to this computer, so they
/// are not written into the projects, which are often git repositories.
/// </summary>
internal sealed partial class MainWindow
{
    private const int IdmRunProgram = 243, IdmChangeProgram = 244;

    private static string ProgramsFile => Path.Combine(Settings.AppDataFolder, "programs.json");

    private static Dictionary<string, string> LoadPrograms()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (File.Exists(ProgramsFile) && Json.Read<Dictionary<string, string>>(File.ReadAllText(ProgramsFile)) is { } read)
                foreach (var kv in read) map[kv.Key] = kv.Value;
        }
        catch { }
        return map;
    }

    private static string ProgramKey(string folder) => Path.GetFullPath(folder).TrimEnd('\\');

    /// <summary>The Open dialog for a project's program; remembers and returns the choice, or null.</summary>
    private string? ChooseProgram(IntPtr owner, string folder)
    {
        string? path = PickFiles(owner, folder, _loc.Format("prog.pickTitle", Path.GetFileName(ProgramKey(folder))))?.FirstOrDefault();
        if (path == null) return null;
        var map = LoadPrograms();
        map[ProgramKey(folder)] = path;
        try
        {
            Directory.CreateDirectory(Settings.AppDataFolder);
            File.WriteAllText(ProgramsFile, Json.Write(map));
        }
        catch (Exception e) { Notice(owner, _loc.Format("prog.failed", e.Message)); }
        return path;
    }

    /// <summary>Starts the folder's program, asking for it when it is not known or is gone.</summary>
    private void RunProgram(IntPtr owner, string folder)
    {
        if (SameFolder(folder, ChatFolderPath)) { Notice(owner, _loc["prog.chat"]); return; }
        LoadPrograms().TryGetValue(ProgramKey(folder), out string? path);
        if (path != null && !File.Exists(path))
        {
            Notice(owner, _loc.Format("prog.missing", path));
            path = null;
        }
        path ??= ChooseProgram(owner, folder);
        if (path == null) return;
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(path) ?? folder });
        }
        catch (Exception e) { Notice(owner, _loc.Format("prog.failed", e.Message)); }
    }

    private void ChangeProgram()
    {
        if (SameFolder(_project, ChatFolderPath)) { Notice(_hwnd, _loc["prog.chat"]); return; }
        ChooseProgram(_hwnd, _project);
    }
}

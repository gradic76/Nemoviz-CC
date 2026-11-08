using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using ClaudeChat.Native;

namespace ClaudeChat;

internal static class Program
{
    /// <summary>
    /// ClaudeChat.exe [project folder] [--project DIR] [--config FILE] [--lang CODE]
    ///                [--theme system|light|dark] [--claude EXE] [--model NAME] [--new]
    /// Command-line values override every settings file.
    /// </summary>
    [STAThread]
    private static int Main(string[] args)
    {
        Win32.CoInitializeEx(IntPtr.Zero, Win32.COINIT_APARTMENTTHREADED);
        var icc = new INITCOMMONCONTROLSEX
        {
            dwSize = (uint)Marshal.SizeOf<INITCOMMONCONTROLSEX>(),
            dwICC = Win32.ICC_STANDARD_CLASSES | Win32.ICC_WIN95_CLASSES,
        };
        Win32.InitCommonControlsEx(ref icc);

        var cli = new JsonObject();
        string? project = null, config = null, defaultProject = null;
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (a.StartsWith("--wait-pid=", StringComparison.OrdinalIgnoreCase))
            {
                // A portable update starts the new exe before the old one has closed.
                if (int.TryParse(a[11..], out int pid))
                    try { System.Diagnostics.Process.GetProcessById(pid).WaitForExit(20000); } catch { }
                continue;
            }
            string? Next() => i + 1 < args.Length ? args[++i] : null;
            switch (a.ToLowerInvariant())
            {
                case "--project": project = Next(); break;
                case "--config": config = Next(); break;
                case "--lang": cli["language"] = Next(); break;
                case "--theme": cli["theme"] = Next(); break;
                case "--claude": cli["claudePath"] = Next(); break;
                case "--model": cli["model"] = Next(); break;
                case "--new": cli["startMode"] = "new"; break;
                case "--restart-command": cli["restartCommand"] = Next(); break;
                case "--default-project": defaultProject = Next(); break;
                default: if (!a.StartsWith("--")) project ??= a; break;
            }
        }

        var state = AppState.Load();
        string userFile = Settings.ResolveUserFile(config);
        var pre = Settings.Load(userFile, null, cli);
        var loc = new Localization();
        loc.Load(pre.Language);

        project ??= pre.ProjectFolder;
        // Order: explicit folder → settings → the folder last worked in → the launcher's default → ask.
        if (project == null && state.LastProject != null && Directory.Exists(state.LastProject)) project = state.LastProject;
        if (project == null && defaultProject != null && Directory.Exists(defaultProject)) project = defaultProject;
        // The very first start (no folder remembered): where projects and chats live.
        if (project == null && state.LastProject == null) project = FirstRun(pre, loc);
        project ??= PickFolder(IntPtr.Zero, loc["dialog.pickFolder"], state.LastProject);
        if (project == null || !Directory.Exists(project)) return 0;
        project = Path.GetFullPath(project);

        var window = new MainWindow(userFile, cli, state, loc, project);
        return window.Run();
    }

    /// <summary>First start: the projects folder (DocumentsClaude unless the user picks another),
    /// its Chat subfolder, and the window opens in Chat.</summary>
    private static string? FirstRun(Settings pre, Localization loc)
    {
        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string root = pre.ProjectsRoot ?? Path.Combine(docs, "Claude");
        if (pre.ProjectsRoot == null)
        {
            int r = Win32.MessageBoxW(IntPtr.Zero, loc.Format("first.intro", pre.AppName, root).Replace("\n", "\r\n"), pre.AppName,
                0x03 /* YESNOCANCEL */ | 0x40 /* INFORMATION */);
            if (r == 2) return null; // Cancel
            if (r == 7) // No: choose another folder
            {
                string? picked = PickFolder(IntPtr.Zero, loc["first.pickRoot"], docs);
                if (picked == null) return null;
                root = picked;
            }
            pre.SaveUserValue("projectsRoot", root);
        }
        string chat = Path.Combine(root, pre.ChatFolder);
        try { Directory.CreateDirectory(chat); } catch { return null; }
        return chat;
    }

    public static string? PickFolder(IntPtr owner, string title, string? current) =>
        FolderPicker.Pick(owner, title, current);
}

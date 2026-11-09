using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace System.Runtime.CompilerServices
{
    /// <summary>The compiler needs this type for "init" and records; .NET Framework does not have it.</summary>
    internal static class IsExternalInit { }
}

namespace ClaudeChat
{
    /// <summary>What .NET Framework 4.8 lacks and the program uses.</summary>
    internal static class Compat
    {
        public static void Deconstruct<K, V>(this KeyValuePair<K, V> pair, out K key, out V value)
        {
            key = pair.Key;
            value = pair.Value;
        }

        public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
        public static double Clamp(double value, double min, double max) => value < min ? min : value > max ? max : value;
        public static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;

        public static bool IsAsciiLetterOrDigit(char c) => c < 128 && char.IsLetterOrDigit(c);
        public static bool StartsWith(this string s, char c) => s.Length > 0 && s[0] == c;
        public static bool EndsWith(this string s, char c) => s.Length > 0 && s[s.Length - 1] == c;
        public static bool Contains(this string s, string value, StringComparison comparison) => s.IndexOf(value, comparison) >= 0;

        public static string ProcessPath { get; } = Process.GetCurrentProcess().MainModule!.FileName!;
        public static int ProcessId { get; } = Process.GetCurrentProcess().Id;

        /// <summary>One argument, quoted so that CommandLineToArgvW and the C runtime read it back unchanged.</summary>
        public static string QuoteArgument(string arg)
        {
            if (arg.Length > 0 && arg.IndexOfAny(new[] { ' ', '\t', '\n', '\r', '\v', '"' }) < 0) return arg;
            var o = new StringBuilder(arg.Length + 2).Append('"');
            int backslashes = 0;
            foreach (char c in arg)
            {
                if (c == '\\') { backslashes++; continue; }
                // Backslashes count double only before a quote.
                o.Append('\\', c == '"' ? backslashes * 2 + 1 : backslashes);
                backslashes = 0;
                o.Append(c);
            }
            return o.Append('\\', backslashes * 2).Append('"').ToString();
        }

        public static string JoinArguments(IEnumerable<string> args) => string.Join(" ", args.Select(QuoteArgument));

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileExW(string from, string to, uint flags);

        /// <summary>Renames a file over an existing one.</summary>
        public static void MoveOverwrite(string from, string to)
        {
            const uint ReplaceExisting = 1;
            if (!MoveFileExW(from, to, ReplaceExisting))
                throw new IOException($"{to}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
        }

        public static string Sha256Hex(Stream stream)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }

        /// <summary>The zlib format (RFC 1950): a header, deflate data, and an Adler-32 of the input.</summary>
        public static void WriteZlib(Stream output, byte[] data)
        {
            output.WriteByte(0x78);
            output.WriteByte(0x9C);
            using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
                deflate.Write(data, 0, data.Length);
            uint a = 1, b = 0;
            foreach (byte x in data)
            {
                a = (a + x) % 65521;
                b = (b + a) % 65521;
            }
            uint adler = (b << 16) | a;
            output.WriteByte((byte)(adler >> 24));
            output.WriteByte((byte)(adler >> 16));
            output.WriteByte((byte)(adler >> 8));
            output.WriteByte((byte)adler);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct PROCESSENTRY32W
        {
            public uint dwSize, cntUsage, th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID, cntThreads, th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile;
        }

        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32FirstW(IntPtr snapshot, ref PROCESSENTRY32W entry);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32NextW(IntPtr snapshot, ref PROCESSENTRY32W entry);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);

        private static List<(int Id, int Parent)> ProcessList()
        {
            const uint SnapProcess = 2;
            var result = new List<(int, int)>();
            IntPtr snapshot = CreateToolhelp32Snapshot(SnapProcess, 0);
            if (snapshot == new IntPtr(-1)) return result;
            try
            {
                var e = new PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32W)) };
                for (bool more = Process32FirstW(snapshot, ref e); more; more = Process32NextW(snapshot, ref e))
                    result.Add(((int)e.th32ProcessID, (int)e.th32ParentProcessID));
            }
            finally { CloseHandle(snapshot); }
            return result;
        }

        private static DateTime? StartTime(Process p)
        {
            try { return p.StartTime; }
            catch { return null; }
        }

        /// <summary>Ends a process and everything it started that is still its descendant. The
        /// whole tree is listed before anything is ended, because a child whose parent has gone
        /// can no longer be found. A process id Windows has reused is told apart by its start time.</summary>
        public static void KillTree(Process root)
        {
            var tree = new List<Process> { root };
            var all = ProcessList();
            for (int i = 0; i < tree.Count; i++)
            {
                Process parent = tree[i];
                DateTime? parentStart = StartTime(parent);
                foreach (var (id, parentId) in all)
                {
                    if (parentId != parent.Id || id == 0 || tree.Any(t => t.Id == id)) continue;
                    try
                    {
                        var child = Process.GetProcessById(id);
                        DateTime? childStart = StartTime(child);
                        if (parentStart == null || childStart == null || childStart >= parentStart) tree.Add(child);
                        else child.Dispose();
                    }
                    catch (ArgumentException) { } // already gone
                }
            }
            foreach (var p in tree)
                try { if (!p.HasExited) p.Kill(); }
                catch (Exception e) when (e is Win32Exception or InvalidOperationException or NotSupportedException) { }
        }
    }
}

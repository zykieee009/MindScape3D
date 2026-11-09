using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Mindscape.Data
{
    // A minimal SQLite binding with no plugin files. It loads the SQLite library the operating system
    // already ships: libsqlite3 on macOS / Linux, winsqlite3.dll on Windows 10+. A sqlite3.dll copied into
    // Assets/Plugins is picked up first. WebGL has no native SQLite, so the database falls back to local storage.
    public sealed class SqliteConnection : IDisposable
    {
        const int Ok = 0, Row = 100, Done = 101;
        const int OpenReadWrite = 0x2, OpenCreate = 0x4, OpenFullMutex = 0x10000;
        static readonly IntPtr Transient = new IntPtr(-1);

        IntPtr db;
        readonly INative native;

        public string Library => native.Name;

        SqliteConnection(INative native, IntPtr db) { this.native = native; this.db = db; }

        // Returns null (and the reason) when no SQLite library can be loaded on this platform.
        public static SqliteConnection TryOpen(string path, out string error)
        {
            error = null;
#if UNITY_WEBGL && !UNITY_EDITOR
            error = "SQLite is not available in WebGL builds.";
            return null;
#else
            foreach (var native in new INative[] { new SystemSqlite(), new WindowsSqlite() })
            {
                try
                {
                    if (native.LibVersionNumber() <= 0) continue;
                    int rc = native.Open(Utf8(path), out IntPtr handle, OpenReadWrite | OpenCreate | OpenFullMutex, IntPtr.Zero);
                    if (rc != Ok)
                    {
                        error = "sqlite3_open_v2 failed (" + rc + ") for " + path;
                        if (handle != IntPtr.Zero) native.Close(handle);
                        return null;
                    }
                    native.BusyTimeout(handle, 2000);
                    return new SqliteConnection(native, handle);
                }
                catch (DllNotFoundException e) { error = e.Message; }
                catch (EntryPointNotFoundException e) { error = e.Message; }
                catch (BadImageFormatException e) { error = e.Message; }
            }
            return null;
#endif
        }

        // Runs a statement that returns no rows. Parameters bind to ?1, ?2, ... in order.
        public int Execute(string sql, params object[] args)
        {
            IntPtr statement = Prepare(sql, args);
            try
            {
                int rc;
                while ((rc = native.Step(statement)) == Row) { }
                if (rc != Done) throw Error("step");
                return native.Changes(db);
            }
            finally { native.Finalize(statement); }
        }

        // Runs a query and returns every row as text columns (NULL becomes null).
        public List<string[]> Query(string sql, params object[] args)
        {
            var rows = new List<string[]>();
            IntPtr statement = Prepare(sql, args);
            try
            {
                int rc;
                while ((rc = native.Step(statement)) == Row)
                {
                    int count = native.ColumnCount(statement);
                    var row = new string[count];
                    for (int i = 0; i < count; i++) row[i] = ReadText(statement, i);
                    rows.Add(row);
                }
                if (rc != Done) throw Error("step");
            }
            finally { native.Finalize(statement); }
            return rows;
        }

        public string Scalar(string sql, params object[] args)
        {
            var rows = Query(sql, args);
            return rows.Count > 0 && rows[0].Length > 0 ? rows[0][0] : null;
        }

        IntPtr Prepare(string sql, object[] args)
        {
            if (db == IntPtr.Zero) throw new ObjectDisposedException(nameof(SqliteConnection));
            byte[] text = Utf8(sql);
            if (native.Prepare(db, text, text.Length, out IntPtr statement, IntPtr.Zero) != Ok) throw Error("prepare");
            for (int i = 0; args != null && i < args.Length; i++)
            {
                int rc;
                object value = args[i];
                if (value == null) rc = native.BindNull(statement, i + 1);
                else if (value is int || value is bool || value is long)
                    rc = native.BindInt64(statement, i + 1, value is bool b ? (b ? 1 : 0) : Convert.ToInt64(value));
                else
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
                    rc = native.BindText(statement, i + 1, bytes, bytes.Length, Transient);
                }
                if (rc != Ok) { native.Finalize(statement); throw Error("bind"); }
            }
            return statement;
        }

        string ReadText(IntPtr statement, int column)
        {
            IntPtr pointer = native.ColumnText(statement, column);
            if (pointer == IntPtr.Zero) return null;
            int length = native.ColumnBytes(statement, column);
            var bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }

        Exception Error(string stage)
        {
            IntPtr message = native.ErrorMessage(db);
            string text = message == IntPtr.Zero ? "unknown error" : Marshal.PtrToStringAnsi(message);
            return new InvalidOperationException("SQLite " + stage + ": " + text);
        }

        static byte[] Utf8(string value)
        {
            // SQLite expects NUL-terminated UTF-8.
            var bytes = Encoding.UTF8.GetBytes(value);
            Array.Resize(ref bytes, bytes.Length + 1);
            return bytes;
        }

        public void Dispose()
        {
            if (db == IntPtr.Zero) return;
            native.Close(db);
            db = IntPtr.Zero;
        }

        interface INative
        {
            string Name { get; }
            int LibVersionNumber();
            int Open(byte[] path, out IntPtr db, int flags, IntPtr vfs);
            int Close(IntPtr db);
            int BusyTimeout(IntPtr db, int ms);
            int Prepare(IntPtr db, byte[] sql, int bytes, out IntPtr statement, IntPtr tail);
            int Step(IntPtr statement);
            int Finalize(IntPtr statement);
            int BindText(IntPtr statement, int index, byte[] value, int bytes, IntPtr destructor);
            int BindInt64(IntPtr statement, int index, long value);
            int BindNull(IntPtr statement, int index);
            int ColumnCount(IntPtr statement);
            IntPtr ColumnText(IntPtr statement, int column);
            int ColumnBytes(IntPtr statement, int column);
            int Changes(IntPtr db);
            IntPtr ErrorMessage(IntPtr db);
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        sealed class SystemSqlite : INative
        {
            const string L = "sqlite3";
            public string Name => L;
            [DllImport(L, EntryPoint = "sqlite3_libversion_number", CallingConvention = CallingConvention.Cdecl)] static extern int V();
            [DllImport(L, EntryPoint = "sqlite3_open_v2", CallingConvention = CallingConvention.Cdecl)] static extern int O(byte[] p, out IntPtr d, int f, IntPtr v);
            [DllImport(L, EntryPoint = "sqlite3_close_v2", CallingConvention = CallingConvention.Cdecl)] static extern int C(IntPtr d);
            [DllImport(L, EntryPoint = "sqlite3_busy_timeout", CallingConvention = CallingConvention.Cdecl)] static extern int B(IntPtr d, int ms);
            [DllImport(L, EntryPoint = "sqlite3_prepare_v2", CallingConvention = CallingConvention.Cdecl)] static extern int P(IntPtr d, byte[] s, int n, out IntPtr st, IntPtr t);
            [DllImport(L, EntryPoint = "sqlite3_step", CallingConvention = CallingConvention.Cdecl)] static extern int S(IntPtr st);
            [DllImport(L, EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Cdecl)] static extern int F(IntPtr st);
            [DllImport(L, EntryPoint = "sqlite3_bind_text", CallingConvention = CallingConvention.Cdecl)] static extern int BT(IntPtr st, int i, byte[] v, int n, IntPtr x);
            [DllImport(L, EntryPoint = "sqlite3_bind_int64", CallingConvention = CallingConvention.Cdecl)] static extern int BI(IntPtr st, int i, long v);
            [DllImport(L, EntryPoint = "sqlite3_bind_null", CallingConvention = CallingConvention.Cdecl)] static extern int BN(IntPtr st, int i);
            [DllImport(L, EntryPoint = "sqlite3_column_count", CallingConvention = CallingConvention.Cdecl)] static extern int CC(IntPtr st);
            [DllImport(L, EntryPoint = "sqlite3_column_text", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr CT(IntPtr st, int c);
            [DllImport(L, EntryPoint = "sqlite3_column_bytes", CallingConvention = CallingConvention.Cdecl)] static extern int CB(IntPtr st, int c);
            [DllImport(L, EntryPoint = "sqlite3_changes", CallingConvention = CallingConvention.Cdecl)] static extern int CH(IntPtr d);
            [DllImport(L, EntryPoint = "sqlite3_errmsg", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr E(IntPtr d);
            public int LibVersionNumber() => V();
            public int Open(byte[] p, out IntPtr d, int f, IntPtr v) => O(p, out d, f, v);
            public int Close(IntPtr d) => C(d);
            public int BusyTimeout(IntPtr d, int ms) => B(d, ms);
            public int Prepare(IntPtr d, byte[] s, int n, out IntPtr st, IntPtr t) => P(d, s, n, out st, t);
            public int Step(IntPtr st) => S(st);
            public int Finalize(IntPtr st) => F(st);
            public int BindText(IntPtr st, int i, byte[] v, int n, IntPtr x) => BT(st, i, v, n, x);
            public int BindInt64(IntPtr st, int i, long v) => BI(st, i, v);
            public int BindNull(IntPtr st, int i) => BN(st, i);
            public int ColumnCount(IntPtr st) => CC(st);
            public IntPtr ColumnText(IntPtr st, int c) => CT(st, c);
            public int ColumnBytes(IntPtr st, int c) => CB(st, c);
            public int Changes(IntPtr d) => CH(d);
            public IntPtr ErrorMessage(IntPtr d) => E(d);
        }

        // Windows 10 and later include SQLite as winsqlite3.dll in System32.
        sealed class WindowsSqlite : INative
        {
            const string L = "winsqlite3";
            public string Name => L;
            [DllImport(L, EntryPoint = "sqlite3_libversion_number")] static extern int V();
            [DllImport(L, EntryPoint = "sqlite3_open_v2")] static extern int O(byte[] p, out IntPtr d, int f, IntPtr v);
            [DllImport(L, EntryPoint = "sqlite3_close_v2")] static extern int C(IntPtr d);
            [DllImport(L, EntryPoint = "sqlite3_busy_timeout")] static extern int B(IntPtr d, int ms);
            [DllImport(L, EntryPoint = "sqlite3_prepare_v2")] static extern int P(IntPtr d, byte[] s, int n, out IntPtr st, IntPtr t);
            [DllImport(L, EntryPoint = "sqlite3_step")] static extern int S(IntPtr st);
            [DllImport(L, EntryPoint = "sqlite3_finalize")] static extern int F(IntPtr st);
            [DllImport(L, EntryPoint = "sqlite3_bind_text")] static extern int BT(IntPtr st, int i, byte[] v, int n, IntPtr x);
            [DllImport(L, EntryPoint = "sqlite3_bind_int64")] static extern int BI(IntPtr st, int i, long v);
            [DllImport(L, EntryPoint = "sqlite3_bind_null")] static extern int BN(IntPtr st, int i);
            [DllImport(L, EntryPoint = "sqlite3_column_count")] static extern int CC(IntPtr st);
            [DllImport(L, EntryPoint = "sqlite3_column_text")] static extern IntPtr CT(IntPtr st, int c);
            [DllImport(L, EntryPoint = "sqlite3_column_bytes")] static extern int CB(IntPtr st, int c);
            [DllImport(L, EntryPoint = "sqlite3_changes")] static extern int CH(IntPtr d);
            [DllImport(L, EntryPoint = "sqlite3_errmsg")] static extern IntPtr E(IntPtr d);
            public int LibVersionNumber() => V();
            public int Open(byte[] p, out IntPtr d, int f, IntPtr v) => O(p, out d, f, v);
            public int Close(IntPtr d) => C(d);
            public int BusyTimeout(IntPtr d, int ms) => B(d, ms);
            public int Prepare(IntPtr d, byte[] s, int n, out IntPtr st, IntPtr t) => P(d, s, n, out st, t);
            public int Step(IntPtr st) => S(st);
            public int Finalize(IntPtr st) => F(st);
            public int BindText(IntPtr st, int i, byte[] v, int n, IntPtr x) => BT(st, i, v, n, x);
            public int BindInt64(IntPtr st, int i, long v) => BI(st, i, v);
            public int BindNull(IntPtr st, int i) => BN(st, i);
            public int ColumnCount(IntPtr st) => CC(st);
            public IntPtr ColumnText(IntPtr st, int c) => CT(st, c);
            public int ColumnBytes(IntPtr st, int c) => CB(st, c);
            public int Changes(IntPtr d) => CH(d);
            public IntPtr ErrorMessage(IntPtr d) => E(d);
        }
#else
        sealed class SystemSqlite : Unavailable { }
        sealed class WindowsSqlite : Unavailable { }
        abstract class Unavailable : INative
        {
            public string Name => "none";
            public int LibVersionNumber() => 0;
            public int Open(byte[] p, out IntPtr d, int f, IntPtr v) { d = IntPtr.Zero; return 1; }
            public int Close(IntPtr d) => 0;
            public int BusyTimeout(IntPtr d, int ms) => 0;
            public int Prepare(IntPtr d, byte[] s, int n, out IntPtr st, IntPtr t) { st = IntPtr.Zero; return 1; }
            public int Step(IntPtr st) => 1;
            public int Finalize(IntPtr st) => 0;
            public int BindText(IntPtr st, int i, byte[] v, int n, IntPtr x) => 1;
            public int BindInt64(IntPtr st, int i, long v) => 1;
            public int BindNull(IntPtr st, int i) => 1;
            public int ColumnCount(IntPtr st) => 0;
            public IntPtr ColumnText(IntPtr st, int c) => IntPtr.Zero;
            public int ColumnBytes(IntPtr st, int c) => 0;
            public int Changes(IntPtr d) => 0;
            public IntPtr ErrorMessage(IntPtr d) => IntPtr.Zero;
        }
#endif
    }
}

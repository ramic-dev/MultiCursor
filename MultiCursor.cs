// MultiCursor - cursori aggiuntivi che seguono il mouse e replicano click e rotella.
// Compilazione: build.cmd (usa il csc.exe di .NET Framework gia' presente in Windows).
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

static class Native
{
    public const int WH_MOUSE_LL = 14;
    public const int WM_HOTKEY = 0x0312;
    public const int WM_MOUSEMOVE = 0x200, WM_LBUTTONDOWN = 0x201, WM_LBUTTONUP = 0x202,
        WM_RBUTTONDOWN = 0x204, WM_RBUTTONUP = 0x205, WM_MBUTTONDOWN = 0x207, WM_MBUTTONUP = 0x208,
        WM_MOUSEWHEEL = 0x20A, WM_XBUTTONDOWN = 0x20B, WM_XBUTTONUP = 0x20C, WM_MOUSEHWHEEL = 0x20E;
    public const uint LLMHF_INJECTED = 1;
    public const uint MOVE = 0x0001, LEFTDOWN = 0x0002, LEFTUP = 0x0004, RIGHTDOWN = 0x0008, RIGHTUP = 0x0010,
        MIDDLEDOWN = 0x0020, MIDDLEUP = 0x0040, XDOWN = 0x0080, XUP = 0x0100, WHEEL = 0x0800, HWHEEL = 0x1000,
        ABSOLUTE = 0x8000, VIRTUALDESK = 0x4000;
    public const uint SWP_NOSIZE = 0x1, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10;

    public delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr extra; }

    // MOUSEINPUT e' il membro piu' grande dell'unione, quindi basta questo layout.
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public uint type; public MOUSEINPUT mi; }

    [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint ms);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] public static extern IntPtr LoadImage(IntPtr hInst, IntPtr name, uint type, int cx, int cy, uint load);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr hMod, uint threadId);
    [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] public static extern IntPtr GetModuleHandle(string name);
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] public static extern uint SendInput(uint n, INPUT[] inputs, int size);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr BeginDeferWindowPos(int n);
    [DllImport("user32.dll")] public static extern IntPtr DeferWindowPos(IntPtr hdwp, IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool EndDeferWindowPos(IntPtr hdwp);
}

// Esegue le raffiche su un thread dedicato, in ordine di arrivo.
static class Clicker
{
    public static int HoldMs, GapMs;

    struct Job { public Point Origin; public Point[] Pts; public uint Down, Up, Data; }

    static readonly BlockingCollection<Job> queue = new BlockingCollection<Job>();
    static int pending;

    public static bool Busy { get { return pending != 0; } }

    static Clicker()
    {
        Thread t = new Thread(Loop);
        t.IsBackground = true;
        t.Start();
    }

    static void Loop()
    {
        foreach (Job j in queue.GetConsumingEnumerable())
        {
            try { Fire(j.Origin, j.Pts, j.Down, j.Up, j.Data); }
            catch { }
            finally { Interlocked.Decrement(ref pending); }
        }
    }

    // down/up sono i flag di SendInput; up = 0 per gli eventi senza rilascio (rotella).
    public static void Enqueue(Point origin, Point[] pts, uint down, uint up, uint data)
    {
        Job j = new Job();
        j.Origin = origin; j.Pts = pts; j.Down = down; j.Up = up; j.Data = data;
        Interlocked.Increment(ref pending);
        queue.Add(j);
    }

    static void Fire(Point origin, Point[] pts, uint down, uint up, uint data)
    {
        Rectangle vs = SystemInformation.VirtualScreen;
        List<Native.INPUT> buf = new List<Native.INPUT>();
        Step(buf, vs, origin, down, up, data);
        foreach (Point p in pts)
            if (vs.Contains(p)) Step(buf, vs, p, down, up, data);
        buf.Add(Move(vs, origin));
        Flush(buf);
    }

    static void Step(List<Native.INPUT> buf, Rectangle vs, Point p, uint down, uint up, uint data)
    {
        buf.Add(Move(vs, p));
        buf.Add(Button(down, data));
        if (up != 0)
        {
            if (HoldMs > 0) { Flush(buf); Thread.Sleep(HoldMs); }
            buf.Add(Button(up, data));
        }
        // Scatti di rotella consecutivi verso la stessa finestra verrebbero fusi da Windows
        // in un unico scorrimento sull'ultimo punto: serve almeno una pausa minima.
        int gap = up == 0 && GapMs == 0 ? 1 : GapMs;
        if (gap > 0) { Flush(buf); Thread.Sleep(gap); }
    }

    static Native.INPUT Move(Rectangle vs, Point p)
    {
        Native.INPUT i = new Native.INPUT();
        i.mi.dx = (int)Math.Round((p.X - vs.Left) * 65535.0 / (vs.Width - 1));
        i.mi.dy = (int)Math.Round((p.Y - vs.Top) * 65535.0 / (vs.Height - 1));
        i.mi.dwFlags = Native.MOVE | Native.ABSOLUTE | Native.VIRTUALDESK;
        return i;
    }

    static Native.INPUT Button(uint flag, uint data)
    {
        Native.INPUT i = new Native.INPUT();
        i.mi.dwFlags = flag;
        i.mi.mouseData = data;
        return i;
    }

    static void Flush(List<Native.INPUT> buf)
    {
        if (buf.Count == 0) return;
        Native.SendInput((uint)buf.Count, buf.ToArray(), Marshal.SizeOf(typeof(Native.INPUT)));
        buf.Clear();
    }
}

// Finestrella sempre in primo piano e trasparente ai click che disegna un cursore finto.
class Ghost : Form
{
    public Point Pos, Offset;
    readonly float scale;

    public Ghost(float scale)
    {
        this.scale = scale;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        Size = new Size((int)(13 * scale) + 2, (int)(20 * scale) + 2);
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            // LAYERED | TRANSPARENT | TOOLWINDOW | NOACTIVATE | TOPMOST
            cp.ExStyle |= 0x80000 | 0x20 | 0x80 | 0x08000000 | 0x8;
            return cp;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        int[] xy = { 0, 0, 0, 16, 4, 12, 7, 19, 9, 18, 6, 11, 11, 11 };
        Point[] arrow = new Point[xy.Length / 2];
        for (int i = 0; i < arrow.Length; i++)
            arrow[i] = new Point((int)(xy[2 * i] * scale), (int)(xy[2 * i + 1] * scale));
        using (Brush b = new SolidBrush(App.Active ? App.ActiveColor : Color.Gray))
            e.Graphics.FillPolygon(b, arrow);
        e.Graphics.DrawPolygon(Pens.Black, arrow);
    }

    // Sposta tutti i cursori finti in un'unica operazione, ciascuno a c + Offset.
    public static void MoveAll(List<Ghost> ghosts, Point c)
    {
        const uint flags = Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE;
        IntPtr h = Native.BeginDeferWindowPos(ghosts.Count);
        foreach (Ghost g in ghosts)
        {
            Point p = g.Offset;
            p.Offset(c);
            g.Pos = p;
            if (h != IntPtr.Zero) h = Native.DeferWindowPos(h, g.Handle, IntPtr.Zero, p.X, p.Y, 0, 0, flags);
        }
        if (h != IntPtr.Zero) Native.EndDeferWindowPos(h);
    }
}

// Finestra nascosta: riceve le scorciatoie e ospita l'hook del mouse.
class App : Form
{
    public static bool Active;
    public static Color ActiveColor = Color.OrangeRed;

    const string DefaultIni =
        "; MultiCursor - impostazioni (riavviare il programma dopo le modifiche)\r\n" +
        "; Scorciatoie: Ctrl, Alt, Shift, Win + un tasto (A, F1, D1, NumPad1, Space, ...)\r\n" +
        "; Add    = aggiunge un cursore dove si trova il mouse (mette in pausa)\r\n" +
        "; Toggle = attiva/disattiva: da attivo i cursori seguono il mouse e replicano i click\r\n" +
        "; Clear  = rimuove tutti i cursori\r\n" +
        "; Quit   = chiude il programma\r\n" +
        "; Save   = salva la combinazione corrente (distanze dei cursori dal mouse)\r\n" +
        "; Load   = ricrea la combinazione salvata attorno al mouse e la attiva\r\n" +
        "; Remove = elimina il cursore piu' vicino al mouse\r\n" +
        "Add=Ctrl+Shift+F1\r\n" +
        "Toggle=Ctrl+Shift+F2\r\n" +
        "Clear=Ctrl+Shift+F3\r\n" +
        "Quit=Ctrl+Shift+F4\r\n" +
        "Save=Ctrl+Shift+F5\r\n" +
        "Load=Ctrl+Shift+F6\r\n" +
        "Remove=Ctrl+Shift+F7\r\n" +
        "; Cosa replicare sui cursori aggiuntivi: Left, Right, Middle, X1, X2, Wheel\r\n" +
        "Replicate=Left,Right,Middle,X1,X2,Wheel\r\n" +
        "; HoldMs = durata della pressione su ogni punto, GapMs = pausa tra un punto e l'altro.\r\n" +
        "; 0 = massima velocita'. Per i giochi che non vedono i click provare HoldMs=20.\r\n" +
        "HoldMs=0\r\n" +
        "GapMs=0\r\n" +
        "; Colore dei cursori quando attivi (nome colore .NET: OrangeRed, Lime, Cyan, ...)\r\n" +
        "Color=OrangeRed\r\n";

    static readonly string[] Actions = { "Add", "Toggle", "Clear", "Quit", "Save", "Load", "Remove" };
    static readonly string[] Descriptions = {
        "aggiunge un cursore dove si trova il mouse (mette in pausa)",
        "attiva / mette in pausa: da attivo i cursori seguono il mouse e replicano click e rotella",
        "rimuove tutti i cursori",
        "chiude il programma",
        "salva la combinazione corrente",
        "ricrea la combinazione salvata attorno al mouse e la attiva",
        "elimina il cursore piu' vicino al mouse" };
    // Ordine in cui i comandi compaiono nell'aiuto.
    static readonly int[] HelpOrder = { 0, 6, 2, 1, 4, 5, 3 };
    readonly string[] specs = new string[Actions.Length];
    string replicateText;
    bool helpOpen;
    const int BitLeft = 1, BitRight = 2, BitMiddle = 4, BitX1 = 8, BitX2 = 16, BitWheel = 32;

    readonly List<Ghost> ghosts = new List<Ghost>();
    readonly Native.HookProc hookProc;
    readonly IntPtr hook;
    readonly NotifyIcon tray;
    readonly MethodInvoker updater;
    readonly float scale;
    int replicate;      // maschera dei tasti da replicare
    int swallowed;      // tasti la cui pressione e' stata trattenuta: va trattenuto anche il rilascio
    Point lastReal;     // ultima posizione reale del mouse
    bool updatePending;

    public App()
    {
        Dictionary<string, string> cfg = LoadIni();
        Clicker.HoldMs = ReadInt(cfg, "HoldMs");
        Clicker.GapMs = ReadInt(cfg, "GapMs");
        // Senza questo le pause brevi durerebbero circa 15 ms l'una.
        Native.timeBeginPeriod(1);
        Color c = Color.FromName(cfg["Color"]);
        if (c.IsKnownColor && c.ToArgb() != Color.Magenta.ToArgb()) ActiveColor = c;
        replicateText = cfg["Replicate"];
        foreach (string raw in cfg["Replicate"].Split(','))
        {
            switch (raw.Trim().ToLowerInvariant())
            {
                case "left": replicate |= BitLeft; break;
                case "right": replicate |= BitRight; break;
                case "middle": replicate |= BitMiddle; break;
                case "x1": replicate |= BitX1; break;
                case "x2": replicate |= BitX2; break;
                case "wheel": replicate |= BitWheel; break;
            }
        }

        using (Graphics g = CreateGraphics()) scale = g.DpiX / 96f * 1.3f;

        string failed = "";
        for (int i = 0; i < Actions.Length; i++)
        {
            uint mods, vk;
            string spec = specs[i] = cfg[Actions[i]];
            if (!ParseHotkey(spec, out mods, out vk) || !Native.RegisterHotKey(Handle, i, mods | 0x4000, vk))
                failed += "\n" + Actions[i] + " = " + spec;
        }
        if (failed != "")
            MessageBox.Show("Scorciatoie non valide o gia' in uso da un altro programma:" + failed +
                "\n\nModificare " + IniPath, "MultiCursor");

        updater = UpdateGhosts;
        hookProc = HookCallback;
        hook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, hookProc, Native.GetModuleHandle(null), 0);

        tray = new NotifyIcon();
        // L'icona e' quella incorporata nell'eseguibile (risorsa 32512), nella misura adatta all'area di notifica.
        Size small = SystemInformation.SmallIconSize;
        IntPtr hIcon = Native.LoadImage(Native.GetModuleHandle(null), (IntPtr)32512, 1, small.Width, small.Height, 0);
        tray.Icon = hIcon != IntPtr.Zero ? Icon.FromHandle(hIcon) : SystemIcons.Application;
        tray.ContextMenu = new ContextMenu(new MenuItem[] {
            new MenuItem("Aiuto", delegate { ShowHelp(); }),
            new MenuItem("-"),
            new MenuItem("Esci", delegate { Quit(); }) });
        tray.DoubleClick += delegate { ShowHelp(); };
        tray.Visible = true;
        UpdateTray();
    }

    static string IniPath { get { return Path.ChangeExtension(Application.ExecutablePath, ".ini"); } }
    static string LayoutPath { get { return Path.ChangeExtension(Application.ExecutablePath, ".layout"); } }

    static Dictionary<string, string> ParseIni(string text)
    {
        Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            int eq = line.IndexOf('=');
            if (line.StartsWith(";") || eq < 1) continue;
            d[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
        }
        return d;
    }

    // Legge il file .ini; le chiavi mancanti (file di una versione precedente) vengono aggiunte in coda.
    static Dictionary<string, string> LoadIni()
    {
        Dictionary<string, string> cfg = ParseIni(DefaultIni);
        try
        {
            if (!File.Exists(IniPath)) { File.WriteAllText(IniPath, DefaultIni); return cfg; }
            Dictionary<string, string> user = ParseIni(File.ReadAllText(IniPath));
            StringBuilder missing = new StringBuilder();
            foreach (string key in new List<string>(cfg.Keys))
            {
                if (user.ContainsKey(key)) cfg[key] = user[key];
                else missing.Append(key + "=" + cfg[key] + "\r\n");
            }
            if (missing.Length > 0) File.AppendAllText(IniPath, "\r\n; Aggiunte da una versione piu' recente\r\n" + missing);
        }
        catch { }
        return cfg;
    }

    static int ReadInt(Dictionary<string, string> cfg, string key)
    {
        int v;
        return int.TryParse(cfg[key], out v) && v > 0 ? v : 0;
    }

    static bool ParseHotkey(string spec, out uint mods, out uint vk)
    {
        mods = 0; vk = 0;
        foreach (string raw in spec.Split('+'))
        {
            string p = raw.Trim().ToLowerInvariant();
            if (p == "alt") mods |= 1;
            else if (p == "ctrl" || p == "control") mods |= 2;
            else if (p == "shift") mods |= 4;
            else if (p == "win") mods |= 8;
            else
            {
                try { vk = (uint)(Keys)Enum.Parse(typeof(Keys), p, true); }
                catch { return false; }
            }
        }
        return vk != 0;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY)
        {
            switch ((int)m.WParam)
            {
                case 0: SetActive(false); AddGhost(Cursor.Position); UpdateTray(); break;
                case 1: SetActive(!Active); break;
                case 2: ClearGhosts(); break;
                case 3: Quit(); break;
                case 4: SaveLayout(); break;
                case 5: LoadLayout(); break;
                case 6: RemoveNearest(); break;
            }
        }
        base.WndProc(ref m);
    }

    void AddGhost(Point p)
    {
        Ghost g = new Ghost(scale);
        g.Location = g.Pos = p;
        g.Show();
        ghosts.Add(g);
    }

    void RemoveNearest()
    {
        if (ghosts.Count == 0) return;
        Point c = Active ? lastReal : Cursor.Position;
        int best = 0;
        long bestDist = long.MaxValue;
        for (int i = 0; i < ghosts.Count; i++)
        {
            Point p = ghosts[i].Pos;
            long dx = p.X - c.X, dy = p.Y - c.Y, dist = dx * dx + dy * dy;
            if (dist < bestDist) { bestDist = dist; best = i; }
        }
        ghosts[best].Dispose();
        ghosts.RemoveAt(best);
        if (ghosts.Count == 0) SetActive(false);
        UpdateTray();
    }

    void ShowHelp()
    {
        if (helpOpen) return;
        StringBuilder sb = new StringBuilder("Scorciatoie\n\n");
        foreach (int i in HelpOrder) sb.Append(specs[i] + "  -  " + Descriptions[i] + "\n");
        sb.Append("\nUso\n\n" +
            "1. Porta il mouse su ogni punto voluto e aggiungi un cursore.\n" +
            "2. Porta il mouse nel suo punto di partenza e attiva: i cursori diventano colorati e lo seguono.\n" +
            "3. Metti in pausa per trascinare o per riposizionare.\n\n" +
            "Replicati: " + replicateText + "\n\n" +
            "Scorciatoie, tasti replicati, tempi e colore si cambiano in:\n" + IniPath + "\n(riavviare il programma dopo le modifiche)");
        helpOpen = true;
        // 0x40000 = sempre in primo piano
        MessageBox.Show(sb.ToString(), "MultiCursor - Aiuto", MessageBoxButtons.OK, MessageBoxIcon.None,
            MessageBoxDefaultButton.Button1, (MessageBoxOptions)0x40000);
        helpOpen = false;
    }

    void ClearGhosts()
    {
        SetActive(false);
        foreach (Ghost g in ghosts) g.Dispose();
        ghosts.Clear();
        UpdateTray();
    }

    void SetActive(bool on)
    {
        if (on)
        {
            lastReal = Cursor.Position;
            foreach (Ghost g in ghosts) g.Offset = new Point(g.Pos.X - lastReal.X, g.Pos.Y - lastReal.Y);
        }
        Active = on;
        foreach (Ghost g in ghosts) g.Invalidate();
        UpdateTray();
    }

    void SaveLayout()
    {
        if (ghosts.Count == 0) { Notify("Nessun cursore da salvare"); return; }
        Point c = Active ? lastReal : Cursor.Position;
        StringBuilder sb = new StringBuilder();
        foreach (Ghost g in ghosts) sb.Append((g.Pos.X - c.X) + "," + (g.Pos.Y - c.Y) + "\r\n");
        try
        {
            File.WriteAllText(LayoutPath, sb.ToString());
            Notify("Combinazione salvata: " + ghosts.Count + " cursori");
        }
        catch (Exception e) { Notify("Salvataggio non riuscito: " + e.Message); }
    }

    void LoadLayout()
    {
        List<Point> offsets = new List<Point>();
        try
        {
            foreach (string line in File.ReadAllLines(LayoutPath))
            {
                string[] p = line.Split(',');
                int x, y;
                if (p.Length == 2 && int.TryParse(p[0], out x) && int.TryParse(p[1], out y)) offsets.Add(new Point(x, y));
            }
        }
        catch { }
        if (offsets.Count == 0) { Notify("Nessuna combinazione salvata"); return; }
        ClearGhosts();
        Point c = Cursor.Position;
        foreach (Point o in offsets) AddGhost(new Point(c.X + o.X, c.Y + o.Y));
        SetActive(true);
    }

    void Notify(string text)
    {
        tray.ShowBalloonTip(1500, "MultiCursor", text, ToolTipIcon.None);
    }

    void UpdateTray()
    {
        tray.Text = "MultiCursor - " + (Active ? "attivo" : "in pausa") + " - cursori: " + ghosts.Count;
    }

    void Quit()
    {
        Native.UnhookWindowsHookEx(hook);
        for (int i = 0; i < Actions.Length; i++) Native.UnregisterHotKey(Handle, i);
        tray.Visible = false;
        Application.Exit();
    }

    // Eseguito dal ciclo dei messaggi: tanti movimenti ravvicinati producono un solo aggiornamento.
    void UpdateGhosts()
    {
        updatePending = false;
        if (Active) Ghost.MoveAll(ghosts, lastReal);
    }

    // L'hook deve tornare subito: qualunque attesa qui dentro rallenta il cursore di sistema.
    IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code < 0 || (Marshal.ReadInt32(lParam, 12) & Native.LLMHF_INJECTED) != 0)
            return Native.CallNextHookEx(hook, code, wParam, lParam);   // gli eventi iniettati (i nostri) passano intatti

        int msg = (int)wParam;
        bool armed = Active && ghosts.Count > 0;

        if (msg == Native.WM_MOUSEMOVE)
        {
            if (armed)
            {
                // Durante una raffica il movimento fisico viene scartato, altrimenti sposterebbe i click.
                if (Clicker.Busy) return (IntPtr)1;
                lastReal = new Point(Marshal.ReadInt32(lParam, 0), Marshal.ReadInt32(lParam, 4));
                if (!updatePending) { updatePending = true; BeginInvoke(updater); }
            }
            return Native.CallNextHookEx(hook, code, wParam, lParam);
        }

        int hi = (short)(Marshal.ReadInt32(lParam, 8) >> 16);   // n. del tasto X oppure scatti della rotella
        int bit; uint down = 0, up = 0, data = 0;
        bool release = false;
        switch (msg)
        {
            case Native.WM_LBUTTONDOWN: bit = BitLeft; down = Native.LEFTDOWN; up = Native.LEFTUP; break;
            case Native.WM_LBUTTONUP: bit = BitLeft; release = true; break;
            case Native.WM_RBUTTONDOWN: bit = BitRight; down = Native.RIGHTDOWN; up = Native.RIGHTUP; break;
            case Native.WM_RBUTTONUP: bit = BitRight; release = true; break;
            case Native.WM_MBUTTONDOWN: bit = BitMiddle; down = Native.MIDDLEDOWN; up = Native.MIDDLEUP; break;
            case Native.WM_MBUTTONUP: bit = BitMiddle; release = true; break;
            case Native.WM_XBUTTONDOWN: bit = hi == 2 ? BitX2 : BitX1; down = Native.XDOWN; up = Native.XUP; data = (uint)hi; break;
            case Native.WM_XBUTTONUP: bit = hi == 2 ? BitX2 : BitX1; release = true; break;
            case Native.WM_MOUSEWHEEL: bit = BitWheel; down = Native.WHEEL; data = (uint)hi; break;
            case Native.WM_MOUSEHWHEEL: bit = BitWheel; down = Native.HWHEEL; data = (uint)hi; break;
            default: return Native.CallNextHookEx(hook, code, wParam, lParam);
        }

        if (release)
        {
            if ((swallowed & bit) == 0) return Native.CallNextHookEx(hook, code, wParam, lParam);
            swallowed &= ~bit;
            return (IntPtr)1;
        }
        if (!armed || (replicate & bit) == 0) return Native.CallNextHookEx(hook, code, wParam, lParam);

        // L'evento reale viene trattenuto e riemesso su tutti i punti in un'unica raffica.
        // Se una raffica e' in corso il cursore e' altrove: vale l'ultima posizione reale.
        if (!Clicker.Busy) lastReal = new Point(Marshal.ReadInt32(lParam, 0), Marshal.ReadInt32(lParam, 4));
        Point[] pts = new Point[ghosts.Count];
        for (int i = 0; i < pts.Length; i++)
        {
            pts[i] = ghosts[i].Offset;
            pts[i].Offset(lastReal);
        }
        Clicker.Enqueue(lastReal, pts, down, up, data);
        if (up != 0) swallowed |= bit;
        return (IntPtr)1;
    }
}

static class Program
{
    [STAThread]
    static void Main()
    {
        bool first;
        using (Mutex m = new Mutex(true, "MultiCursor_SingleInstance", out first))
        {
            if (!first) return;
            Native.SetProcessDPIAware();
            Application.EnableVisualStyles();
            new App();
            Application.Run();
        }
    }
}

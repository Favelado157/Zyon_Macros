using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ZyonMacros;

sealed class Dashboard : Form
{
    [DllImport("user32")] static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32")] static extern bool UnregisterHotKey(nint window, int id);

    static readonly Version Current = Version.Parse(Application.ProductVersion.Split('+')[0]);
    static readonly Color Ink = Color.FromArgb(225,242,255);
    static readonly Color Dark = Color.FromArgb(4,14,27);
    static readonly Color[] ActionColors = [Color.FromArgb(255,42,72), Color.Orange, Color.SpringGreen, Color.DodgerBlue];
    static readonly string[] ActionNames = ["Gravar","Pausar","Play","Parar"];
    static readonly string[] ActionIcons = ["●","Ⅱ","▶","■"];
    readonly Panel surface = new() { AutoScroll = true, Dock = DockStyle.Fill, BackColor = Color.FromArgb(2,8,17) };
    readonly System.Windows.Forms.Timer timer = new() { Interval = 150 };
    readonly Macro macro;
    readonly Button[] actionButtons = new Button[4];
    readonly Button[] keyButtons = new Button[4];
    readonly Dictionary<string, Control> sections = [];
    Settings settings;
    Image? art;
    Label status = null!, details = null!, metrics = null!, available = null!, glowValue = null!;
    Button install = null!;
    Panel path = null!;
    CancellationTokenSource? playback;
    string? file, installerUrl;
    string updateStatus = "Verificando…";
    bool checking, closing;
    Color Accent
    {
        get
        {
            try
            {
                var color = ColorTranslator.FromHtml(settings.Accent);
                return Color.FromArgb(255, color.R, color.G, color.B);
            }
            catch { return Color.DeepSkyBlue; }
        }
    }

    public Dashboard()
    {
        settings = Settings.Load();
        macro = new(settings) { IsPanelPoint = p => WindowState != FormWindowState.Minimized && Bounds.Contains(p) };
        Text = "ZyonMacros";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1260, 790);
        MinimumSize = new Size(760, 520);
        var screen = Screen.PrimaryScreen?.WorkingArea.Size ?? new Size(1280, 840);
        Size = new Size(Math.Min(Width, screen.Width), Math.Min(Height, screen.Height));
        BackColor = Dark;
        ForeColor = Ink;
        Font = new Font("Segoe UI", 10);
        var iconPath = Path.Combine(AppContext.BaseDirectory, "assets", "app.ico");
        if (File.Exists(iconPath)) Icon = new Icon(iconPath);
        Controls.Add(surface);
        LoadArt();
        Build();
        timer.Tick += (_,_) => { if (macro.Active) RefreshState(); };
        timer.Start();
        Shown += async (_,_) =>
        {
            Try(() =>
            {
                Directory.CreateDirectory(Settings.Folder);
                if (!File.Exists(Settings.Art) && File.Exists(Settings.DefaultArt))
                    File.Copy(Settings.DefaultArt, Settings.Art);
                settings.Save();
            });
            if (!RegisterKeys(settings.Keys)) Notice("Um atalho já está em uso. Edite as teclas no painel.");
            await CheckUpdates(true);
        };
    }

    void Build()
    {
        surface.SuspendLayout();
        while (surface.Controls.Count > 0) surface.Controls[0].Dispose();
        surface.AutoScrollPosition = Point.Empty;
        surface.AutoScrollMinSize = new Size(1244, 765);
        sections.Clear();

        var header = Card(surface, "", 8, 6, 1228, 84, true);
        Logo(header, 91, 5, 455, 49, 31);
        LabelAt(header, "AUTOMATIZE SEUS MOVIMENTOS", 96, 54, 418, 19, 10);
        LabelAt(header, "v" + Current, 505, 19, 86, 34, 12, true);
        LabelAt(header, "✓  MAIS PRECISÃO\n✓  MAIS CONTROLE\n✓  MAIS RESULTADOS", 933, 8, 212, 65, 11, true);
        ButtonAt(header, "⚙", 1175, 15, 38, 34, OpenConfig);

        var nav = Card(surface, "", 8, 99, 185, 625);
        string[] names = ["Início","Gravações","Configurações","Atalhos","Personalização","Atualização","Sobre"];
        string[] icons = ["⌂","▣","⚙","⌨","✦","⟳","ⓘ"];
        for (int i = 0; i < names.Length; i++)
        {
            var name = names[i];
            var button = ButtonAt(nav, icons[i] + "    " + name, 7, 9+i*41, 170, 36, () => Navigate(name), i == 0 ? Accent : null);
            button.TextAlign = ContentAlignment.MiddleLeft;
        }
        var sideArt = Card(nav, "", 7, 300, 170, 313, true, true);
        Logo(sideArt, 4, 268, 162, 35, 15);

        var hero = Card(surface, "", 203, 99, 650, 120, true);
        sections["Início"] = hero;
        Logo(hero, 137, 23, 337, 52, 29);
        LabelAt(hero, "Macro • Gravação • Automação", 145, 75, 315, 25, 13, true);
        var system = Card(hero, "", 474, 13, 166, 93);
        LabelAt(system, "▣  Status do Sistema", 9, 8, 147, 20, 10, true);
        status = LabelAt(system, "", 10, 39, 151, 25, 12, true);
        details = LabelAt(system, "", 10, 65, 151, 18, 8);

        var actions = Card(surface, "⊕  Controles Principais", 203, 227, 650, 151);
        for (int i=0; i<4; i++)
        {
            int index = i;
            actionButtons[i] = ButtonAt(actions, "", 12+i*159, 41, 150, 98, () => Command(index), ActionColors[i], true);
            actionButtons[i].Font = new Font(Font.FontFamily, 14, FontStyle.Bold);
        }
        var info = Card(surface, "ⓘ  Informações da Gravação", 203, 386, 650, 156);
        var stat = Card(info, "", 10, 35, 318, 111);
        metrics = LabelAt(stat, "", 11, 6, 299, 100, 9);
        path = Card(info, "", 336, 35, 304, 111);
        path.Paint += PaintPath;

        var options = Card(surface, "⚙  Configurações de Gravação", 203, 550, 650, 174);
        sections["Configurações"] = options;
        Toggle(options, "Gravar movimento do mouse", 13, 33, 290, settings.Move, b => settings.Move = b);
        Toggle(options, "Gravar cliques do mouse", 13, 58, 290, settings.Clicks, b => settings.Clicks = b);
        Toggle(options, "Gravar teclas do teclado", 13, 83, 290, settings.Keyboard, b => settings.Keyboard = b);
        Toggle(options, "Gravar rolagem da roda", 13, 108, 290, settings.Wheel, b => settings.Wheel = b);
        Toggle(options, "Exibir caminho do mouse", 13, 133, 290, settings.PathVisible, b => { settings.PathVisible = b; path.Invalidate(); });
        LabelAt(options, "Modo de gravação", 326, 31, 162, 20, 9);
        var mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Dark, ForeColor = Ink, Bounds = new Rectangle(488,31,146,26) };
        mode.Items.AddRange(["Normal (Completo)", "Somente movimento"]);
        mode.SelectedIndex = settings.Mode;
        mode.SelectedIndexChanged += (_,_) => { settings.Mode=mode.SelectedIndex; SaveSettings(); };
        options.Controls.Add(mode);
        LabelAt(options, "Intervalo de repetição (ms)", 326, 60, 218, 23, 9);
        Number(options, 551, 60, 83, 1, 60000, settings.Interval, v => settings.Interval = v);
        var speedLabel=LabelAt(options, $"Velocidade do play  {settings.Speed:0.00}×", 326, 87, 168, 24, 9);
        Slider(options, 493, 84, 145, 25, 300, (int)(settings.Speed*100), v => { settings.Speed=v/100d; speedLabel.Text=$"Velocidade do play  {settings.Speed:0.00}×"; });
        Toggle(options, "Repetir gravação", 326, 110, 308, settings.Repeat, b => settings.Repeat = b);
        ButtonAt(options, "⚙   Avançado", 326, 139, 137, 27, Advanced);
        ButtonAt(options, "⟳  Restaurar Padrão", 473, 139, 161, 27, RestoreOptions);

        var shortcuts = Card(surface, "⌨  Atalhos de Teclado", 864, 99, 372, 169);
        sections["Atalhos"] = shortcuts;
        ButtonAt(shortcuts, "Editar", 282, 7, 80, 24, EditAll);
        for (int i = 0; i < 4; i++)
        {
            int j = i, y = 36+i*31;
            LabelAt(shortcuts, ActionIcons[i], 15, y, 28, 25, 14, true).ForeColor = ActionColors[i];
            LabelAt(shortcuts, ActionNames[i], 47, y, 97, 25);
            keyButtons[i] = ButtonAt(shortcuts, "", 154, y, 155, 24, () => EditKey(j));
            ButtonAt(shortcuts, "✎", 319, y, 40, 24, () => EditKey(j));
        }
        var files = Card(surface, "▣  Gerenciamento de Gravações", 864, 276, 372, 124);
        sections["Gravações"] = files;
        ButtonAt(files, "▣   Salvar Gravação", 13, 33, 346, 25, SaveRecording);
        ButtonAt(files, "▰   Carregar Gravação", 13, 62, 346, 25, LoadRecording);
        ButtonAt(files, "▤   Excluir Gravação", 13, 91, 346, 25, DeleteRecording);

        var theme = Card(surface, "✦  Personalização do Painel", 864, 408, 372, 188);
        sections["Personalização"] = theme;
        LabelAt(theme, "Cor principal", 13, 31, 130, 20, 9);
        string[] colors = ["#00aaff","#6644ff","#ff47bf","#ff383e","#ffbb00","#95df27","#00e8a4","#00deff"];
        for (int i=0; i<colors.Length; i++)
        {
            string hex = colors[i];
            ButtonAt(theme, settings.Accent.Equals(hex,StringComparison.OrdinalIgnoreCase) ? "✓" : "", 14+i*44, 53, 37, 32, () => { settings.Accent=hex; SaveSettings(); Build(); }, ColorTranslator.FromHtml(hex));
        }
        LabelAt(theme, "Intensidade do brilho", 13, 91, 141, 23, 9);
        glowValue = LabelAt(theme, settings.Glow+"%", 316, 91, 49, 23, 9);
        Slider(theme, 155, 88, 153, 0, 100, settings.Glow, v => { settings.Glow=v; glowValue.Text=v+"%"; surface.Invalidate(true); });
        LabelAt(theme, "Imagem de fundo", 13, 120, 133, 23, 9);
        Card(theme, "", 145, 118, 70, 56, true, true);
        ButtonAt(theme, "▧   Trocar Imagem", 223, 118, 136, 25, ChangeArt);
        ButtonAt(theme, "⟳   Padrão", 223, 149, 136, 25, ResetArt);

        var updates = Card(surface, "⟳  Atualização", 864, 604, 372, 120);
        sections["Atualização"] = updates;
        LabelAt(updates, "Versão atual   v"+Current, 14, 35, 235, 22, 10);
        available = LabelAt(updates, updateStatus, 14, 59, 342, 21, 9);
        install = ButtonAt(updates, installerUrl is null ? "Verificar atualização" : "↓  Atualizar Agora", 181, 83, 177, 29, UpdateClick, Color.SpringGreen);
        var footer = Card(surface, "", 8, 732, 1228, 30);
        Logo(footer, 8, 0, 165, 29, 15);
        LabelAt(footer, "Suas gravações ficam neste computador", 181, 3, 315, 24, 9);
        LabelAt(footer, "⊞   Windows 11", 542, 3, 261, 24, 9);
        LabelAt(footer, "●   Sistema pronto", 1044, 3, 170, 24, 9).ForeColor = Color.SpringGreen;
        surface.ResumeLayout();
        RefreshState();
    }

    Panel Card(Control parent, string caption, int x, int y, int w, int h, bool image = false, bool crop = false)
    {
        var p = new Canvas { Bounds = new(x,y,w,h), BackColor=Dark };
        p.Paint += (_, e) =>
        {
            var g=e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(1,1,p.Width-3,p.Height-3);
            using var outline = RoundRect(rect, 9);
            using var bg = new LinearGradientBrush(rect, Color.FromArgb(9,32,53), Dark, LinearGradientMode.Vertical);
            g.FillPath(bg, outline);
            if (image && art is not null)
            {
                var state = g.Save();
                g.SetClip(outline);
                if (crop) g.DrawImage(art, rect, new Rectangle(0,0,Math.Min(art.Width, art.Height*rect.Width/rect.Height),art.Height), GraphicsUnit.Pixel);
                else g.DrawImage(art, rect);
                using var overlay = new SolidBrush(Color.FromArgb(crop ? 15 : 70, 0, 5, 15));
                g.FillRectangle(overlay, rect);
                g.Restore(state);
            }
            using var border = new Pen(Color.FromArgb(105+settings.Glow, Accent), 1);
            g.DrawPath(border, outline);
            if (caption.Length > 0)
            {
                using var titleFont = new Font(Font.FontFamily, 11, FontStyle.Bold);
                TextRenderer.DrawText(g, caption, titleFont, new Point(10,7), Ink);
                using var line = new Pen(Color.FromArgb(90, Accent));
                g.DrawLine(line,8,30,p.Width-9,30);
            }
        };
        parent.Controls.Add(p);
        return p;
    }

    static GraphicsPath RoundRect(Rectangle bounds, int r)
    {
        var path = new GraphicsPath();
        int d=2*r;
        path.AddArc(bounds.Left,bounds.Top,d,d,180,90);
        path.AddArc(bounds.Right-d,bounds.Top,d,d,270,90);
        path.AddArc(bounds.Right-d,bounds.Bottom-d,d,d,0,90);
        path.AddArc(bounds.Left,bounds.Bottom-d,d,d,90,90);
        path.CloseFigure();
        return path;
    }

    sealed class Canvas : Panel { public Canvas() { DoubleBuffered = true; ResizeRedraw = true; } }

    Label LabelAt(Control parent,string text,int x,int y,int w,int h,float size=10,bool bold=false)
    {
        var label=new Label { Text=text, Bounds=new(x,y,w,h), ForeColor=Ink, BackColor=Color.Transparent,
            Font=new Font(Font.FontFamily,size,bold ? FontStyle.Bold : FontStyle.Regular), TextAlign=ContentAlignment.MiddleLeft };
        parent.Controls.Add(label);
        return label;
    }
    void Logo(Control parent,int x,int y,int w,int h,float size)
    {
        var logo=LabelAt(parent,"ZyonMacros",x,y,w,h,size,true);
        logo.Font=new Font("Segoe UI",size,FontStyle.Bold|FontStyle.Italic);
        logo.ForeColor=Color.FromArgb(208,243,255);
    }
    Button ButtonAt(Control parent,string text,int x,int y,int w,int h,Action action,Color? tint=null,bool large=false)
    {
        var c=tint ?? Accent;
        var b=new Button { Text=text, Bounds=new(x,y,w,h), FlatStyle=FlatStyle.Flat, ForeColor=Ink,
            BackColor=Color.FromArgb(Math.Max(6,c.R*(large?3:1)/8),Math.Max(16,c.G*(large?3:1)/8),Math.Max(25,c.B*(large?3:1)/8)),
            Cursor=Cursors.Hand, UseVisualStyleBackColor=false };
        b.FlatAppearance.BorderColor=Color.FromArgb(large?255:175,c);
        b.FlatAppearance.BorderSize=large?2:1;
        b.FlatAppearance.MouseOverBackColor=Color.FromArgb(Math.Max(16,c.R/3),Math.Max(24,c.G/3),Math.Max(37,c.B/3));
        if (large) b.Paint += (_, e) =>
        {
            using var shine = new Pen(Color.FromArgb(settings.Glow, c), 2);
            e.Graphics.DrawLine(shine, 13, b.Height-6, b.Width-13, b.Height-6);
            e.Graphics.DrawLine(shine, 13, 5, b.Width-13, 5);
        };
        b.Click += (_,_) => Try(action);
        parent.Controls.Add(b);
        return b;
    }
    void Toggle(Control parent,string text,int x,int y,int w,bool value,Action<bool> change)
    {
        var label=LabelAt(parent,text,x,y,w-57,23,9);
        Button? button=null;
        void PaintToggle()
        {
            if (button is null) return;
            button.Text=value ? "━●" : "●━";
            button.AccessibleName=text+(value ? ", ligado" : ", desligado");
            button.BackColor=value ? Color.FromArgb(0,96,158) : Color.FromArgb(37,59,77);
        }
        button=ButtonAt(parent,"",x+w-52,y+1,51,21,() => { value=!value; PaintToggle(); change(value); SaveSettings(); });
        label.Click += (_,_) => button.PerformClick();
        PaintToggle();
    }
    void Number(Control parent,int x,int y,int w,int min,int max,int value,Action<int> change)
    {
        var n=new NumericUpDown { Bounds=new(x,y,w,25), Minimum=min, Maximum=max, Value=value, ForeColor=Ink, BackColor=Dark };
        n.ValueChanged += (_,_) => Try(() => { change((int)n.Value); SaveSettings(); });
        parent.Controls.Add(n);
    }
    void Slider(Control parent,int x,int y,int w,int min,int max,int value,Action<int> change)
    {
        var slider=new TrackBar { Bounds=new(x,y,w,28), AutoSize=false, TickStyle=TickStyle.None, Minimum=min, Maximum=max, Value=value, BackColor=Dark };
        slider.Scroll += (_,_) => Try(() => { change(slider.Value); SaveSettings(); });
        parent.Controls.Add(slider);
    }

    void RefreshState()
    {
        string state = playback is not null ? "Reproduzindo" : macro.Paused ? "Pausado" : macro.Active ? "Gravando" : "Pronto";
        status.Text = "●  " + state;
        status.ForeColor = macro.Paused ? Color.Orange : macro.Active ? Color.Tomato : Color.SpringGreen;
        details.Text = macro.Active ? "Captura de entrada ativa" : playback is not null ? "Reproduzindo na tela" : "Aguardando comandos…";
        var events=macro.Saved.Events;
        metrics.Text=$"Duração                        {TimeSpan.FromMilliseconds(macro.Duration):hh\\:mm\\:ss}\n" +
            $"Movimentos (Mouse)     {events.Count(e=>e.Message==512)}\n" +
            $"Eventos (Teclado)         {events.Count(e=>e.Message is >=256 and <=261)}\n" +
            $"Tamanho                       {(file is null ? "—" : File.Exists(file) ? new FileInfo(file).Length+" bytes" : "—")}\n" +
            $"Arquivo                         {(file is null ? "Nenhum" : Path.GetFileName(file))}";
        for(int i=0;i<4;i++)
        {
            string key=((Keys)settings.Keys[i]).ToString();
            actionButtons[i].Text=$"{ActionIcons[i]}\n{(i==1 && macro.Paused ? "Retomar" : ActionNames[i])}\n{key}";
            keyButtons[i].Text=key;
        }
        actionButtons[0].Enabled = playback is null;
        actionButtons[1].Enabled = macro.Active;
        actionButtons[2].Enabled = playback is null && events.Count > 0;
        actionButtons[3].Enabled = playback is not null || macro.Active;
        path.Invalidate();
    }
    void PaintPath(object? sender, PaintEventArgs e)
    {
        var bounds=new Rectangle(10,10,path.Width-20,path.Height-20);
        var screen=SystemInformation.VirtualScreen;
        var events=macro.Saved.Events;
        if (events.Count==0 || !settings.PathVisible)
        {
            string text=events.Count==0
                ? "Nenhum movimento gravado\n\nPressione Gravar para começar."
                : $"{events.Count:N0} eventos capturados";
            TextRenderer.DrawText(e.Graphics,text,Font,bounds,Ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak);
            return;
        }
        var points=new List<PointF>();
        int step=Math.Max(1,events.Count/1000);
        for(int i=0;i<events.Count;i+=step)
        {
            var item=events[i];
            if(item.Message==512) points.Add(new(bounds.X+(item.X-screen.Left)*bounds.Width/(float)screen.Width,bounds.Y+(item.Y-screen.Top)*bounds.Height/(float)screen.Height));
        }
        if(points.Count<2)
        {
            TextRenderer.DrawText(e.Graphics,$"{events.Count:N0} eventos capturados",Font,bounds,Ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
            return;
        }
        e.Graphics.SetClip(bounds);
        using var pen=new Pen(Accent,1.5f);
        e.Graphics.DrawLines(pen,points.ToArray());
    }
    void Command(int index)
    {
        if (index == 0 && playback is null)
        {
            if (macro.Active) macro.Finish();
            else
            {
                if (macro.Saved.Events.Count > 0 && file is null &&
                    MessageBox.Show(this,"Substituir a gravação atual?","Nova gravação",MessageBoxButtons.YesNo)!=DialogResult.Yes) return;
                macro.Start();
                file=null;
            }
        }
        else if (index == 1 && macro.Active) macro.Pause();
        else if (index == 2 && playback is null)
        {
            if (macro.Active) macro.Finish();
            if (macro.Saved.Events.Count > 0) _ = Play();
        }
        else if (index == 3)
        {
            if (macro.Active) macro.Finish();
            playback?.Cancel();
        }
        RefreshState();
    }
    async Task Play()
    {
        using var source=new CancellationTokenSource();
        playback=source;
        var options=new Settings { Speed=settings.Speed, Interval=settings.Interval, Repeat=settings.Repeat, Keys=(int[])settings.Keys.Clone() };
        RefreshState();
        try
        {
            await Task.Delay(400,source.Token);
            await Macro.Replay(macro.Saved,options,source.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!closing) Notice(ex.Message); }
        finally { playback=null; if (!closing) RefreshState(); }
    }
    bool RegisterKeys(int[] keys)
    {
        for(int i=0;i<4;i++) UnregisterHotKey(Handle, 101+i);
        bool success=true;
        for(int i=0;i<4;i++) success=RegisterHotKey(Handle,101+i,0x4000,(uint)keys[i]) && success;
        return success;
    }
    protected override void WndProc(ref Message m)
    {
        if(m.Msg==0x0312 && m.WParam.ToInt32() is >=101 and <=104)
        {
            int index=m.WParam.ToInt32()-101;
            Try(() => Command(index));
        }
        base.WndProc(ref m);
    }
    void EditAll()
    {
        for(int i=0;i<4;i++) if(!EditKey(i)) break;
    }
    bool EditKey(int index)
    {
        if(macro.Active || playback is not null) { Notice("Finalize a gravação ou reprodução para editar os atalhos."); return false; }
        using var dialog=new Form { Text="Atalho — "+ActionNames[index], ClientSize=new Size(420,100),
            FormBorderStyle=FormBorderStyle.FixedDialog, StartPosition=FormStartPosition.CenterParent, BackColor=Dark, KeyPreview=true,
            MinimizeBox=false, MaximizeBox=false, ShowInTaskbar=false };
        LabelAt(dialog,"Pressione a nova tecla (F2 a F12). Esc cancela.",14,21,392,45,10,true);
        int selected=0;
        for(int i=0;i<4;i++) UnregisterHotKey(Handle,101+i);
        dialog.KeyDown+=(_,e) =>
        {
            if(e.KeyCode==Keys.Escape) dialog.Close();
            else if(e.KeyCode is >= Keys.F2 and <=Keys.F12 && e.Modifiers==Keys.None)
            {
                selected=(int)e.KeyCode;
                dialog.Close();
            }
        };
        dialog.ShowDialog(this);
        var candidate=(int[])settings.Keys.Clone();
        candidate[index]=selected;
        if(selected==0 || candidate.Distinct().Count()!=4 || !RegisterKeys(candidate))
        {
            RegisterKeys(settings.Keys);
            if(selected!=0) Notice("A tecla já está em uso. Escolha outra.");
            return false;
        }
        settings.Keys=candidate;
        SaveSettings();
        RefreshState();
        return true;
    }
    void SaveRecording()
    {
        if (playback is not null) { Notice("Pare a reprodução para salvar a gravação."); return; }
        if (macro.Active) macro.Finish();
        RefreshState();
        if (macro.Saved.Events.Count==0) { Notice("Grave algum movimento antes de salvar."); return; }
        using var picker=new SaveFileDialog { Filter="Gravação ZyonMacros (*.ghost.json)|*.ghost.json", DefaultExt="ghost.json", FileName="Gravação.ghost.json" };
        if(picker.ShowDialog(this)!=DialogResult.OK) return;
        var temporary=picker.FileName+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            File.WriteAllText(temporary,JsonSerializer.Serialize(macro.Saved));
            File.Move(temporary,picker.FileName,true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        file=picker.FileName;
        RefreshState();
    }
    void LoadRecording()
    {
        if (macro.Active || playback is not null) { Notice("Finalize a gravação ou reprodução para carregar um arquivo."); return; }
        using var picker=new OpenFileDialog { Filter="Gravação ZyonMacros (*.ghost.json)|*.ghost.json" };
        if(picker.ShowDialog(this)!=DialogResult.OK) return;
        using var stream=File.OpenRead(picker.FileName);
        if(stream.Length>40*1024*1024) throw new InvalidDataException("A gravação excede o tamanho suportado.");
        var candidate=JsonSerializer.Deserialize<Recording>(stream);
        if(candidate is null || candidate.Events is not { Count: >0 and <=250000 } || candidate.Duration is <0 or >86400000)
            throw new InvalidDataException("O arquivo não é uma gravação válida.");
        long previous=0;
        foreach(var e in candidate.Events)
        {
            if(e is null || e.Time<previous || e.Time>candidate.Duration || e.Message is not (256 or 257 or 260 or 261 or 512 or 513 or 514 or 516 or 517 or 519 or 520 or 522 or 523 or 524 or 526))
                throw new InvalidDataException("A gravação contém eventos inválidos.");
            if(e.Message<512 && (e.X is <1 or >254 || e.Y is <0 or >255 || e.Data is <0 or >1))
                throw new InvalidDataException("A gravação contém teclas inválidas.");
            previous=e.Time;
        }
        if(macro.Saved.Events.Count>0 && file is null &&
            MessageBox.Show(this,"Substituir a gravação atual?","Carregar gravação",MessageBoxButtons.YesNo)!=DialogResult.Yes) return;
        macro.Saved=candidate;
        file=picker.FileName;
        RefreshState();
    }
    void DeleteRecording()
    {
        if (macro.Active || playback is not null) { Notice("Finalize a operação atual primeiro."); return; }
        if(macro.Saved.Events.Count==0) return;
        var result=MessageBox.Show(this,file is null ? "Descartar a gravação atual?" : $"Excluir definitivamente {Path.GetFileName(file)}?",
            "Excluir gravação",MessageBoxButtons.YesNo,MessageBoxIcon.Warning);
        if(result!=DialogResult.Yes) return;
        if(file is not null) File.Delete(file);
        file=null;
        macro.Saved=new(0,[]);
        RefreshState();
    }
    void Navigate(string tab)
    {
        if(tab=="Sobre") { Notice($"ZyonMacros v{Current}\nGravador e reprodutor de movimentos, cliques, rolagem e teclas.\n\nConfigurações: {Settings.Folder}"); return; }
        surface.AutoScrollPosition=Point.Empty;
        surface.ScrollControlIntoView(sections[tab]);
        sections[tab].Focus();
        if(tab=="Atualização") _=CheckUpdates(false);
    }
    void SaveSettings() => Try(settings.Save);
    void Notice(string text) => MessageBox.Show(this,text,"ZyonMacros");
    void Try(Action action)
    {
        try { action(); }
        catch(Exception ex) { Notice(ex.Message); }
    }
    void RestoreOptions()
    {
        var defaults=new Settings();
        settings.Move=defaults.Move;
        settings.Clicks=defaults.Clicks;
        settings.Keyboard=defaults.Keyboard;
        settings.Wheel=defaults.Wheel;
        settings.PathVisible=defaults.PathVisible;
        settings.Mode=defaults.Mode;
        settings.Speed=defaults.Speed;
        settings.Sample=defaults.Sample;
        settings.Repeat=defaults.Repeat;
        settings.Interval=defaults.Interval;
        SaveSettings();
        Build();
    }
    void Advanced()
    {
        using var dialog=new Form { Text="Configurações avançadas", ClientSize=new Size(420,135), BackColor=Dark,
            FormBorderStyle=FormBorderStyle.FixedDialog, StartPosition=FormStartPosition.CenterParent, MaximizeBox=false, MinimizeBox=false, ShowInTaskbar=false };
        LabelAt(dialog,"Intervalo de amostragem do mouse (ms)",15,16,303,26);
        Number(dialog,326,16,78,1,1000,settings.Sample,n=>settings.Sample=n);
        ButtonAt(dialog,"Abrir pasta config",15,67,185,33,OpenConfig);
        ButtonAt(dialog,"Concluir",254,67,150,33,dialog.Close);
        dialog.ShowDialog(this);
    }
    void OpenConfig()
    {
        Directory.CreateDirectory(Settings.Folder);
        Open(Settings.Folder);
    }
    static void Open(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute=true });
    void LoadArt()
    {
        var source=File.Exists(Settings.Art) ? Settings.Art : Settings.DefaultArt;
        Image? decoded=null;
        try { using var loaded=Image.FromFile(source); decoded=new Bitmap(loaded); }
        catch(ArgumentException) { }
        catch(IOException) { }
        catch(OutOfMemoryException) { }
        art?.Dispose();
        art=decoded;
        surface.Invalidate(true);
    }
    void ChangeArt()
    {
        using var picker=new OpenFileDialog { Filter="Imagens|*.png;*.jpg;*.jpeg;*.bmp" };
        if(picker.ShowDialog(this)!=DialogResult.OK) return;
        Directory.CreateDirectory(Settings.Folder);
        using (var image=Image.FromFile(picker.FileName))
        using (var copy=new Bitmap(image))
            copy.Save(Settings.Art+".tmp",System.Drawing.Imaging.ImageFormat.Png);
        File.Move(Settings.Art+".tmp",Settings.Art,true);
        LoadArt();
    }
    void ResetArt()
    {
        Directory.CreateDirectory(Settings.Folder);
        File.Copy(Settings.DefaultArt,Settings.Art,true);
        LoadArt();
    }
    void UpdateClick()
    {
        if(installerUrl is null) _=CheckUpdates(false);
        else Open(installerUrl);
    }
    async Task CheckUpdates(bool prompt)
    {
        if(checking || closing) return;
        checking=true;
        installerUrl=null;
        updateStatus="Verificando…";
        ShowUpdate();
        try
        {
            string repo=settings.UpdateRepository ?? "";
            if(!Regex.IsMatch(repo,@"\A[A-Za-z0-9-]+/[A-Za-z0-9_.-]+\z")) throw new InvalidOperationException("Repositório de atualizações inválido.");
            using var client=new HttpClient { Timeout=TimeSpan.FromSeconds(8) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ZyonMacros/"+Current);
            using var response=await client.GetAsync($"https://api.github.com/repos/{repo}/releases/latest");
            if(response.StatusCode==System.Net.HttpStatusCode.NotFound)
            {
                updateStatus="Nenhuma versão publicada no repositório.";
                return;
            }
            response.EnsureSuccessStatusCode();
            using var release=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if(!Version.TryParse(release.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v','V'),out var newer) || newer<=Current)
            {
                updateStatus="Você já tem a versão mais recente.";
                return;
            }
            foreach(var asset in release.RootElement.GetProperty("assets").EnumerateArray())
            {
                string? url=asset.GetProperty("browser_download_url").GetString();
                if (asset.GetProperty("name").GetString()=="ZyonMacros-Setup-x64.exe" &&
                    Uri.TryCreate(url,UriKind.Absolute,out var uri) && uri.Scheme=="https" && uri.Host=="github.com" &&
                    uri.AbsolutePath.StartsWith($"/{repo}/releases/download/",StringComparison.OrdinalIgnoreCase))
                {
                    installerUrl=url;
                    break;
                }
            }
            updateStatus=installerUrl is null ? $"v{newer} disponível, aguardando instalador." : $"Nova versão   v{newer} disponível!";
            ShowUpdate();
            if(!closing && prompt && installerUrl is not null && MessageBox.Show(this,$"A versão v{newer} está disponível. Baixar agora?",
                "Atualização do ZyonMacros",MessageBoxButtons.YesNo,MessageBoxIcon.Information)==DialogResult.Yes)
                Open(installerUrl);
        }
        catch(Exception) { updateStatus="Falha ao consultar o GitHub. Tente novamente."; }
        finally { checking=false; if(!closing) ShowUpdate(); }
    }
    void ShowUpdate()
    {
        if(closing) return;
        available.Text=updateStatus;
        available.ForeColor=installerUrl is null ? Ink : Color.SpringGreen;
        install.Text=installerUrl is null ? "Verificar atualização" : "↓  Atualizar Agora";
        install.Enabled=!checking || installerUrl is not null;
    }
    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        if(playback is not null && !closing)
        {
            e.Cancel=true;
            playback.Cancel();
            while(playback is not null) await Task.Delay(10);
            Close();
            return;
        }
        closing=true;
        for(int i=0;i<4;i++) UnregisterHotKey(Handle,101+i);
        timer.Dispose();
        macro.Dispose();
        art?.Dispose();
        base.OnFormClosing(e);
    }
}

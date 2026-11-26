namespace GhosttzzKBot;
static class Program
{
    [STAThread]
    static void Main()
    {
        using var instance = new Mutex(true, "GhosttzzKBot-Desktop", out bool first);
        if (!first) { MessageBox.Show("O GhosttzzKBot já está aberto."); return; }
        ApplicationConfiguration.Initialize();
        Application.Run(new Dashboard());
    }
}

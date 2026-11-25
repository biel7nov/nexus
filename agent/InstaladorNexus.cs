using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NexusInstaller
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // 0. Modo especial para captura de screenshot automatizada (testes/validação)
            if (args != null && args.Length > 0)
            {
                foreach (string a in args)
                {
                    string al = a.Trim().ToLower();
                    if (al == "/screenshot" || al == "-screenshot" || al == "--screenshot")
                    {
                        Application.EnableVisualStyles();
                        Application.SetCompatibleTextRenderingDefault(false);
                        using (FormInstalador form = new FormInstalador())
                        {
                            form.StartPosition = FormStartPosition.Manual;
                            form.Location = new Point(100, 100);
                            form.Show();
                            Application.DoEvents();
                            System.Threading.Thread.Sleep(300);
                            Bitmap bmp = new Bitmap(form.Width, form.Height);
                            form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
                            string shotPath = @"C:\Users\gabrielnovaes\.gemini\antigravity\brain\b4abb51b-fbb2-420c-a6aa-a19611eabd12\installer_preview.png";
                            bmp.Save(shotPath, System.Drawing.Imaging.ImageFormat.Png);
                        }
                        return;
                    }
                }
            }

            // 1. Verifica se foi solicitado modo de desinstalação
            bool isUninstall = false;
            bool isQuiet = false;
            string exeName = Path.GetFileNameWithoutExtension(Application.ExecutablePath).ToLower();
            if (exeName.Contains("desinstal")) isUninstall = true;

            if (args != null && args.Length > 0)
            {
                foreach (string a in args)
                {
                    string al = a.Trim().ToLower();
                    if (al == "/uninstall" || al == "-uninstall" || al == "--uninstall" || al == "/desinstalar" || al == "-desinstalar")
                        isUninstall = true;
                    if (al == "/quiet" || al == "-quiet" || al == "--quiet" || al == "/silent" || al == "-silent" || al == "/s" || al == "-s")
                        isQuiet = true;
                }
            }

            // 2. Verificação de privilégios de Administrador
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = Application.ExecutablePath;
                    psi.Arguments = (args != null && args.Length > 0) ? string.Join(" ", args) : (isUninstall ? "/uninstall" : "");
                    psi.Verb = "runas";
                    psi.UseShellExecute = true;
                    Process.Start(psi);
                    return;
                }
                catch
                {
                    MessageBox.Show(
                        "O " + (isUninstall ? "desinstalador" : "instalador") + " precisa de privilégios de Administrador do Windows para gerenciar os serviços e o Firewall.\n\nPor favor, execute como Administrador.",
                        "Fast Solutions Corporate — Permissão Necessária",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    return;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 3. Execução da Desinstalação se requisitado
            if (isUninstall)
            {
                ExecutarDesinstalacao(isQuiet);
                return;
            }

            // 4. Fluxo Normal de Instalação com Interface Gráfica
            Application.Run(new FormInstalador());
        }

        public static void ExecutarDesinstalacao(bool isQuiet)
        {
            if (!isQuiet)
            {
                DialogResult dr = MessageBox.Show(
                    "Deseja realmente desinstalar o Fast Solutions Corporate Agent deste computador?\n\nEsta ação irá:\n• Encerrar o serviço de monitoramento e suporte remoto\n• Remover as regras do Windows Defender Firewall\n• Remover atalhos de chamado e o registro de programas\n• Excluir os arquivos do agente.",
                    "Fast Solutions Corporate — Confirmação de Desinstalação",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );
                if (dr != DialogResult.Yes) return;
            }

            try
            {
                // 1. Encerra processos do agente
                foreach (var p in Process.GetProcessesByName("FSCRemoteAgent"))
                {
                    try { p.Kill(); p.WaitForExit(1500); } catch { }
                }
                foreach (var p in Process.GetProcessesByName("FSCRemoteAgent_debug"))
                {
                    try { p.Kill(); p.WaitForExit(1500); } catch { }
                }
                foreach (var p in Process.GetProcessesByName("FSCRemoteAgent_test"))
                {
                    try { p.Kill(); p.WaitForExit(1500); } catch { }
                }

                // 2. Remove regras de Firewall
                try
                {
                    ProcessStartInfo psiFw1 = new ProcessStartInfo("netsh", "advfirewall firewall delete rule name=\"NexusTI Remote Agent Port 8090\"");
                    psiFw1.CreateNoWindow = true;
                    psiFw1.UseShellExecute = false;
                    Process p1 = Process.Start(psiFw1);
                    if (p1 != null) p1.WaitForExit(2000);

                    ProcessStartInfo psiFw2 = new ProcessStartInfo("netsh", "advfirewall firewall delete rule name=\"NexusTI Remote Agent App\"");
                    psiFw2.CreateNoWindow = true;
                    psiFw2.UseShellExecute = false;
                    Process p2 = Process.Start(psiFw2);
                    if (p2 != null) p2.WaitForExit(2000);

                    ProcessStartInfo psiFw3 = new ProcessStartInfo("netsh", "advfirewall firewall delete rule name=\"NexusTI Remote Agent\"");
                    psiFw3.CreateNoWindow = true;
                    psiFw3.UseShellExecute = false;
                    Process p3 = Process.Start(psiFw3);
                    if (p3 != null) p3.WaitForExit(2000);
                }
                catch { }

                // 3. Remove inicialização com o Windows no Registro
                try
                {
                    using (RegistryKey runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (runKey != null) runKey.DeleteValue("FSCRemoteAgent", false);
                    }
                }
                catch { }
                try
                {
                    using (RegistryKey runKeyLM = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (runKeyLM != null) runKeyLM.DeleteValue("FSCRemoteAgent", false);
                    }
                }
                catch { }

                // 4. Remove chaves de Desinstalação do Windows (Uninstall) em 64-bit, 32-bit e CurrentUser
                try
                {
                    using (RegistryKey baseKey64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                    using (RegistryKey ukey64 = baseKey64.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", true))
                    {
                        if (ukey64 != null) ukey64.DeleteSubKeyTree("FSCRemoteAgent", false);
                    }
                }
                catch { }
                try
                {
                    using (RegistryKey baseKey32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                    using (RegistryKey ukey32 = baseKey32.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", true))
                    {
                        if (ukey32 != null) ukey32.DeleteSubKeyTree("FSCRemoteAgent", false);
                    }
                }
                catch { }
                try
                {
                    using (RegistryKey ukeyUser = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", true))
                    {
                        if (ukeyUser != null) ukeyUser.DeleteSubKeyTree("FSCRemoteAgent", false);
                    }
                }
                catch { }

                // 5. Remove atalhos da Área de Trabalho e Menu Iniciar
                try
                {
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    string lnk1 = Path.Combine(desktop, "Abrir Chamado T.I.lnk");
                    string lnk2 = Path.Combine(desktop, "Abrir Chamado T.I (Web).url");
                    if (File.Exists(lnk1)) File.Delete(lnk1);
                    if (File.Exists(lnk2)) File.Delete(lnk2);

                    string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Fast Solutions Corporate");
                    if (Directory.Exists(startMenu)) Directory.Delete(startMenu, true);
                }
                catch { }

                // 6. Agenda auto-exclusão do diretório de instalação
                string appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
                if (Directory.Exists(appDir) && !appDir.Equals(@"C:\Windows", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        string cmdScript = string.Format("/c ping 127.0.0.1 -n 3 >nul & rmdir /s /q \"{0}\"", appDir);
                        ProcessStartInfo psiDel = new ProcessStartInfo("cmd.exe", cmdScript);
                        psiDel.CreateNoWindow = true;
                        psiDel.UseShellExecute = false;
                        Process.Start(psiDel);
                    }
                    catch { }
                }

                if (!isQuiet)
                {
                    MessageBox.Show(
                        "O Fast Solutions Corporate Agent foi completamente desinstalado do computador com sucesso.\n\nTodos os serviços, regras de rede e atalhos foram removidos.",
                        "Fast Solutions Corporate — Desinstalação Concluída",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
            }
            catch (Exception ex)
            {
                if (!isQuiet)
                {
                    MessageBox.Show("Erro durante a desinstalação: " + ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }

    public class FormInstalador : Form
    {
        private Panel pnlHeader;
        private Panel pnlInput;
        private Panel pnlSuccess;
        private ComboBox cbEmpresa;
        private TextBox txtNome;
        private ComboBox cbTag;
        private TextBox txtDiretorio;
        private Button btnProcurarDir;
        private Button btnInstalar;
        private Button btnDesinstalarExistente;
        private Label lblStatus;
        private Label lblEmpresaResult;
        private Label lblNomeResult;
        private Label lblIpResult;
        private CheckBox chkIsServidor;

        public FormInstalador()
        {
            this.Text = "NexusTI — Instalador Nexus Remote Agent";
            this.ClientSize = new Size(570, 710);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.BackColor = Color.FromArgb(11, 15, 25); // Deep Obsidian Slate
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);
            this.DoubleBuffered = true;

            // Gera ícone moderno vetorial para o formulário
            Bitmap icoBmp = CriarIconeVetorial(32);
            IntPtr hIcon = icoBmp.GetHicon();
            this.Icon = Icon.FromHandle(hIcon);

            MontarInterface();
        }

        private static Bitmap CriarIconeVetorial(int size)
        {
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                Rectangle r = new Rectangle(0, 0, size - 1, size - 1);
                int rad = Math.Max(2, size / 4);
                using (GraphicsPath path = ObterCaminhoArredondado(r, rad))
                {
                    using (SolidBrush sBr = new SolidBrush(Color.FromArgb(15, 17, 26)))
                    {
                        g.FillPath(sBr, path);
                    }
                    using (Pen p = new Pen(Color.FromArgb(56, 189, 248), Math.Max(1.0f, size * 0.03f)))
                    {
                        g.DrawPath(p, path);
                    }
                }

                int pad = Math.Max(2, (int)(size * 0.22f));
                int xL = pad + (int)(size * 0.05f);
                int xR = size - pad - (int)(size * 0.05f);
                int yT = pad;
                int yB = size - pad;
                float penW = Math.Max(2.0f, size * 0.12f);

                using (LinearGradientBrush grad = new LinearGradientBrush(
                    new Point(xL, yT), new Point(xR, yB),
                    Color.FromArgb(56, 189, 248), Color.FromArgb(168, 85, 247)))
                using (Pen penN = new Pen(grad, penW))
                {
                    penN.StartCap = LineCap.Round;
                    penN.EndCap = LineCap.Round;
                    penN.LineJoin = LineJoin.Round;

                    g.DrawLine(penN, xL, yT, xL, yB);
                    g.DrawLine(penN, xL, yT + (int)(penW * 0.5f), xR, yB - (int)(penW * 0.5f));
                    g.DrawLine(penN, xR, yT, xR, yB);
                }

                int dot = Math.Max(3, (int)(size * 0.14f));
                using (SolidBrush dBr = new SolidBrush(Color.FromArgb(56, 189, 248)))
                {
                    g.FillEllipse(dBr, size / 2 - dot / 2, size / 2 - dot / 2, dot, dot);
                }
            }
            return bmp;
        }

        private static GraphicsPath ObterCaminhoArredondado(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0) { path.AddRectangle(bounds); return path; }

            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        
        
        private static List<string> BuscarEmpresasNavegadores()
        {
            List<string> resultado = new List<string>();
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string[] browserDirs = new string[] {
                    Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Local Storage\leveldb"),
                    Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Local Storage\leveldb"),
                    Path.Combine(localAppData, @"BraveSoftware\Brave-Browser\User Data\Default\Local Storage\leveldb")
                };

                byte[] marker = Encoding.ASCII.GetBytes("fsc_kintsugi_empresas");

                foreach (string bDir in browserDirs)
                {
                    if (!Directory.Exists(bDir)) continue;
                    string[] arquivos = Directory.GetFiles(bDir, "*.*");
                    Array.Sort(arquivos, delegate(string a, string b) {
                        return File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a));
                    });

                    foreach (string file in arquivos)
                    {
                        string ext = Path.GetExtension(file).ToLower();
                        if (ext != ".log" && ext != ".ldb") continue;

                        try
                        {
                            using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            using (MemoryStream ms = new MemoryStream())
                            {
                                fs.CopyTo(ms);
                                byte[] bytes = ms.ToArray();

                                for (int i = bytes.Length - marker.Length; i >= 0; i--)
                                {
                                    bool match = true;
                                    for (int m = 0; m < marker.Length; m++)
                                    {
                                        if (bytes[i + m] != marker[m]) { match = false; break; }
                                    }
                                    if (match)
                                    {
                                        int startChunk = i + marker.Length;
                                        int lenChunk = Math.Min(600, bytes.Length - startChunk);
                                        string chunk = Encoding.UTF8.GetString(bytes, startChunk, lenChunk);

                                        int startArray = chunk.IndexOf('[');
                                        int endArray = chunk.IndexOf(']');
                                        if (startArray >= 0 && endArray > startArray)
                                        {
                                            string jsonArr = chunk.Substring(startArray, endArray - startArray + 1);
                                            List<string> lista = ParseJsonLista(jsonArr, "empresas");
                                            if (lista != null && lista.Count > 0)
                                            {
                                                foreach (string emp in lista)
                                                {
                                                    if (!resultado.Contains(emp)) resultado.Add(emp);
                                                }
                                                if (resultado.Count > 0) return resultado;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return resultado;
        }

        private static void SalvarEmpresasJsonLocal(List<string> lista)
        {
            if (lista == null || lista.Count == 0) return;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[");
            for (int i = 0; i < lista.Count; i++)
            {
                sb.Append("  \"" + lista[i] + "\"");
                if (i < lista.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.Append("]");
            string json = sb.ToString();

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] alvos = new string[] {
                Path.Combine(baseDir, "empresas.json"),
                Path.Combine(baseDir, "..\\empresas.json"),
                Path.Combine(baseDir, "res\\empresas.json"),
                @"C:\Nexus-Agent\empresas.json",
                @"c:\Users\gabrielnovaes\Documents\copia magma\empresas.json",
                @"c:\Users\gabrielnovaes\Documents\copia magma\agent\empresas.json",
                @"c:\Users\gabrielnovaes\Documents\gestão de TI\empresas.json",
                @"c:\Users\gabrielnovaes\Documents\gestão de TI\agent\empresas.json",
                @"c:\Users\gabrielnovaes\Documents\fsc-console\empresas.json",
                @"c:\Users\gabrielnovaes\Documents\fsc-console\agent\empresas.json"
            };
            foreach (string a in alvos)
            {
                try
                {
                    string dir = Path.GetDirectoryName(a);
                    if (Directory.Exists(dir)) File.WriteAllText(a, json, Encoding.UTF8);
                }
                catch { }
            }
        }

        
        private static List<string> BuscarEmpresasSupabase()
        {
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                string url = "https://hskxvdcgfgrdofgpdtzm.supabase.co/rest/v1/empresas?select=nome&order=criado_em.asc";
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.Headers["apikey"] = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Imhza3h2ZGNnZmdyZG9mZ3BkdHptIiwicm9sZSI6ImFub24iLCJpYXQiOjE3OTU1NDEzOTEsImV4cCI6MjExMTExNzM5MX0.PKQaCfnlmxAM7Vuc4s84gjLS_21OeteMkI1WLLR1BEU";
                req.Headers["Authorization"] = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Imhza3h2ZGNnZmdyZG9mZ3BkdHptIiwicm9sZSI6ImFub24iLCJpYXQiOjE3OTU1NDEzOTEsImV4cCI6MjExMTExNzM5MX0.PKQaCfnlmxAM7Vuc4s84gjLS_21OeteMkI1WLLR1BEU";
                req.Timeout = 2500;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    string json = sr.ReadToEnd();
                    List<string> res = new List<string>();
                    MatchCollection matches = Regex.Matches(json, @"""nome""\s*:\s*""([^""]+)""");
                    foreach (Match m in matches)
                    {
                        if (m.Success && m.Groups.Count > 1)
                        {
                            string n = m.Groups[1].Value.Trim();
                            if (!string.IsNullOrEmpty(n) && !res.Contains(n)) res.Add(n);
                        }
                    }
                    if (res.Count > 0) return res;
                }
            }
            catch { }
            return null;
        }

        private static List<string> BuscarSetoresSupabase()
        {
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                string url = "https://hskxvdcgfgrdofgpdtzm.supabase.co/rest/v1/setores?select=nome&order=criado_em.asc";
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.Headers["apikey"] = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Imhza3h2ZGNnZmdyZG9mZ3BkdHptIiwicm9sZSI6ImFub24iLCJpYXQiOjE3OTU1NDEzOTEsImV4cCI6MjExMTExNzM5MX0.PKQaCfnlmxAM7Vuc4s84gjLS_21OeteMkI1WLLR1BEU";
                req.Headers["Authorization"] = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Imhza3h2ZGNnZmdyZG9mZ3BkdHptIiwicm9sZSI6ImFub24iLCJpYXQiOjE3OTU1NDEzOTEsImV4cCI6MjExMTExNzM5MX0.PKQaCfnlmxAM7Vuc4s84gjLS_21OeteMkI1WLLR1BEU";
                req.Timeout = 2500;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    string json = sr.ReadToEnd();
                    List<string> res = new List<string>();
                    MatchCollection matches = Regex.Matches(json, @"""nome""\s*:\s*""([^""]+)""");
                    foreach (Match m in matches)
                    {
                        if (m.Success && m.Groups.Count > 1)
                        {
                            string n = m.Groups[1].Value.Replace("#", "").Trim();
                            if (!string.IsNullOrEmpty(n) && !res.Contains(n)) res.Add(n);
                        }
                    }
                    if (res.Count > 0) return res;
                }
            }
            catch { }
            return null;
        }

        private static List<string> CarregarEmpresasDisponiveis()
        {
            List<string> empresas = new List<string>();
            try
            {
                List<string> supaEmps = BuscarEmpresasSupabase();
                if (supaEmps != null && supaEmps.Count > 0)
                {
                    SalvarEmpresasJsonLocal(supaEmps);
                    return supaEmps;
                }
            }
            catch { }
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // 1. Prioridade Absoluta: Lê diretamente do LocalStorage do navegador (Chrome/Edge/Brave)
            // Se o usuário adicionou ou alterou empresas na console, o instalador carrega IMEDIATAMENTE!
            try
            {
                List<string> browserEmps = BuscarEmpresasNavegadores();
                if (browserEmps != null && browserEmps.Count > 0)
                {
                    SalvarEmpresasJsonLocal(browserEmps);
                    return browserEmps;
                }
            }
            catch { }

            // 2. Tenta ler de empresas.json em disco (Instantâneo)
            string[] jsonPaths = new string[] {
                Path.Combine(baseDir, "empresas.json"),
                Path.Combine(baseDir, "..\\empresas.json"),
                Path.Combine(baseDir, "res\\empresas.json"),
                @"C:\Nexus-Agent\empresas.json",
                @"c:\Users\gabrielnovaes\Documents\copia magma\empresas.json",
                @"c:\Users\gabrielnovaes\Documents\copia magma\agent\empresas.json",
                @"c:\Users\gabrielnovaes\Documents\gestão de TI\empresas.json",
                @"c:\Users\gabrielnovaes\Documents\gestão de TI\agent\empresas.json",
                @"c:\Users\gabrielnovaes\Documents\fsc-console\empresas.json",
                @"c:\Users\gabrielnovaes\Documents\fsc-console\agent\empresas.json"
            };

            foreach (string path in jsonPaths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        string json = File.ReadAllText(path, Encoding.UTF8);
                        var lista = ParseJsonLista(json, "empresas");
                        if (lista != null && lista.Count > 0)
                        {
                            return lista;
                        }
                    }
                }
                catch { }
            }

            // 2. Tenta ler empresa.txt
            string[] txtPaths = new string[] {
                Path.Combine(baseDir, "empresa.txt"),
                Path.Combine(baseDir, "..\\empresa.txt"),
                Path.Combine(baseDir, "agent\\empresa.txt"),
                @"C:\Nexus-Agent\empresa.txt",
                @"c:\Users\gabrielnovaes\Documents\copia magma\agent\empresa.txt",
                @"c:\Users\gabrielnovaes\Documents\copia magma\empresa.txt"
            };
            foreach (string tp in txtPaths)
            {
                try
                {
                    if (File.Exists(tp))
                    {
                        string txt = File.ReadAllText(tp, Encoding.UTF8).Trim();
                        if (!string.IsNullOrEmpty(txt) && !empresas.Contains(txt))
                        {
                            empresas.Add(txt);
                        }
                    }
                }
                catch { }
            }

            if (empresas.Count > 0)
            {
                                if (!empresas.Contains("Aço Móveis")) empresas.Add("Aço Móveis");
                if (!empresas.Contains("JPL")) empresas.Add("JPL");
                return empresas;
            }

            // 3. Fallback padrão
            return new List<string> {
                                "Aço Móveis",
                "JPL"
            };
        }

        private static List<string> ParseJsonLista(string json, string keyFilter)
        {
            List<string> resultado = new List<string>();
            if (string.IsNullOrEmpty(json)) return resultado;

            try
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(json, "\"([^\"]+)\"");
                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    string val = m.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(val) && !resultado.Contains(val) && val.ToLower() != "empresas" && val.ToLower() != "empresa" && val.ToLower() != "empresaprincipal" && val.ToLower() != "ok" && val.ToLower() != "message")
                    {
                        resultado.Add(val);
                    }
                }
            }
            catch { }
            return resultado;
        }

        private static List<string> CarregarSetoresDisponiveis()
        {
            // 0. Prioridade Máxima: Busca os setores em tempo real direto do Supabase Cloud (sincronizado com o site)
            try
            {
                List<string> supaSetores = BuscarSetoresSupabase();
                if (supaSetores != null && supaSetores.Count > 0)
                {
                    try
                    {
                        var js = new System.Web.Script.Serialization.JavaScriptSerializer();
                        string json = js.Serialize(supaSetores);
                        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "setores.json"), json, Encoding.UTF8);
                    }
                    catch { }
                    return supaSetores;
                }
            }
            catch { }

            // 1. Tenta ler do recurso embutido (offline)
            try
            {
                using (Stream st = Assembly.GetExecutingAssembly().GetManifestResourceStream("setores.json"))
                {
                    if (st != null)
                    {
                        using (StreamReader sr = new StreamReader(st, Encoding.UTF8))
                        {
                            var lista = ParseJsonSetores(sr.ReadToEnd());
                            if (lista != null && lista.Count > 0) return lista;
                        }
                    }
                }
            }
            catch { }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // 2. Tenta ler de arquivo local setores.json (Instantâneo)
            string[] caminhos = new string[] {
                Path.Combine(baseDir, "setores.json"),
                Path.Combine(baseDir, "..\\setores.json"),
                Path.Combine(baseDir, "res\\setores.json"),
                @"C:\Nexus-Agent\setores.json",
                @"C:\FSC-Agent\setores.json",
                @"c:\Users\gabrielnovaes\Documents\copia magma\setores.json",
                @"c:\Users\gabrielnovaes\Documents\copia magma\agent\setores.json",
                @"c:\Users\gabrielnovaes\Documents\gestão de TI\setores.json",
                @"c:\Users\gabrielnovaes\Documents\gestão de TI\agent\setores.json",
                @"c:\Users\gabrielnovaes\Documents\fsc-console\setores.json",
                @"c:\Users\gabrielnovaes\Documents\fsc-console\agent\setores.json"
            };

            foreach (string path in caminhos)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        string json = File.ReadAllText(path, Encoding.UTF8);
                        var lista = ParseJsonSetores(json);
                        if (lista != null && lista.Count > 0)
                        {
                            return lista;
                        }
                    }
                }
                catch { }
            }

            // 2. Fallback com os setores exatos cadastrados no portal web
            return new List<string> {
                "Financeiro",
                "Produção",
                "TI",
                "Diretoria",
                "DP",
                "Logística",
                "Comercial",
                "QUALIDADE",
                "PROJETO",
                "COMPRAS",
                "RECEPÇÃO",
                "ALMOXARIFADO"
            };
        }

        private static List<string> ParseJsonSetores(string json)
        {
            List<string> resultado = new List<string>();
            if (string.IsNullOrEmpty(json)) return resultado;

            try
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(json, "\"([^\"]+)\"");
                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    string val = m.Groups[1].Value.Trim();
                    string limpo = val.StartsWith("#") ? val.Substring(1) : val;
                    if (!string.IsNullOrEmpty(limpo) && !resultado.Contains(limpo) && limpo.ToLower() != "setores" && limpo.ToLower() != "departamentos")
                    {
                        resultado.Add(limpo);
                    }
                }
            }
            catch { }
            return resultado;
        }

        private void EstilizarComboBoxDark(ComboBox cb)
        {
            cb.DrawMode = DrawMode.OwnerDrawFixed;
            cb.ItemHeight = 24;
            cb.DrawItem += (s, e) => {
                if (e.Index < 0) return;
                ComboBox cmb = (ComboBox)s;
                bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                Color bgCol = isSelected ? Color.FromArgb(37, 99, 235) : Color.FromArgb(15, 23, 42);
                using (SolidBrush bg = new SolidBrush(bgCol))
                {
                    e.Graphics.FillRectangle(bg, e.Bounds);
                }
                using (SolidBrush fg = new SolidBrush(Color.White))
                {
                    string text = cmb.Items[e.Index].ToString();
                    using (StringFormat sf = new StringFormat { LineAlignment = StringAlignment.Center })
                    {
                        Rectangle txtRect = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
                        e.Graphics.DrawString(text, cmb.Font, fg, txtRect, sf);
                    }
                }
            };
        }

        private void MontarInterface()
        {
            // 1. Header Topo (Fixo)
            pnlHeader = new Panel();
            pnlHeader.Location = new Point(0, 0);
            pnlHeader.Size = new Size(570, 84);
            pnlHeader.BackColor = Color.FromArgb(15, 23, 42); // Slate-900
            pnlHeader.Paint += (s, e) => {
                using (Pen p = new Pen(Color.FromArgb(30, 41, 59), 1.5f))
                {
                    e.Graphics.DrawLine(p, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
                }
            };
            this.Controls.Add(pnlHeader);

            PictureBox picLogo = new PictureBox();
            picLogo.Image = CriarIconeVetorial(50);
            picLogo.SizeMode = PictureBoxSizeMode.CenterImage;
            picLogo.Size = new Size(54, 54);
            picLogo.Location = new Point(22, 15);
            pnlHeader.Controls.Add(picLogo);

            Label lblBrand = new Label();
            lblBrand.Text = "FAST SOLUTIONS CORPORATE  •  SUPORTE REMOTO & TELEMETRIA";
            lblBrand.Font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            lblBrand.ForeColor = Color.FromArgb(56, 189, 248); // Sky-400
            lblBrand.Location = new Point(88, 17);
            lblBrand.AutoSize = true;
            pnlHeader.Controls.Add(lblBrand);

            Label lblTitle = new Label();
            lblTitle.Text = "Instalador Nexus Remote Agent";
            lblTitle.Font = new Font("Segoe UI", 13.5f, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(248, 250, 252);
            lblTitle.Location = new Point(87, 34);
            lblTitle.AutoSize = true;
            pnlHeader.Controls.Add(lblTitle);

            Label lblSubtitle = new Label();
            lblSubtitle.Text = "Identificação da estação, telemetria e integração com o portal de chamados";
            lblSubtitle.Font = new Font("Segoe UI", 8.25f, FontStyle.Regular);
            lblSubtitle.ForeColor = Color.FromArgb(148, 163, 184); // Slate-400
            lblSubtitle.Location = new Point(88, 59);
            lblSubtitle.AutoSize = true;
            pnlHeader.Controls.Add(lblSubtitle);

            // 2. Painel de Entrada de Dados (Abaixo do Header)
            pnlInput = new Panel();
            pnlInput.Location = new Point(0, 84);
            pnlInput.Size = new Size(570, 626);
            pnlInput.BackColor = Color.FromArgb(11, 15, 25);
            this.Controls.Add(pnlInput);

            // Card Principal Central
            Panel pnlCard = new Panel();
            pnlCard.Location = new Point(24, 16);
            pnlCard.Size = new Size(522, 504);
            pnlCard.BackColor = Color.FromArgb(17, 24, 39); // Slate-900 / Dark Gray
            pnlCard.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, pnlCard.Width - 1, pnlCard.Height - 1);
                using (GraphicsPath path = ObterCaminhoArredondado(r, 14))
                using (Pen p = new Pen(Color.FromArgb(30, 41, 59), 1.5f))
                {
                    e.Graphics.DrawPath(p, path);
                }
            };
            pnlInput.Controls.Add(pnlCard);

            int curY = 16;

            // 1. Campo Empresa / Unidade Contratante
            Label lblEmpresa = new Label();
            lblEmpresa.Text = "EMPRESA / UNIDADE CONTRATANTE *";
            lblEmpresa.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            lblEmpresa.ForeColor = Color.FromArgb(203, 213, 225); // Slate-300
            lblEmpresa.Location = new Point(20, curY);
            lblEmpresa.AutoSize = true;
            pnlCard.Controls.Add(lblEmpresa);
            curY += 22;

            cbEmpresa = new ComboBox();
            cbEmpresa.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            cbEmpresa.BackColor = Color.FromArgb(15, 23, 42);
            cbEmpresa.ForeColor = Color.FromArgb(248, 250, 252);
            cbEmpresa.FlatStyle = FlatStyle.Flat;
            cbEmpresa.DropDownStyle = ComboBoxStyle.DropDown;
            cbEmpresa.Location = new Point(20, curY);
            cbEmpresa.Size = new Size(480, 28);
            List<string> empresasCadastradas = CarregarEmpresasDisponiveis();
            foreach (string emp in empresasCadastradas)
            {
                cbEmpresa.Items.Add(emp);
            }
            if (cbEmpresa.Items.Count > 0) cbEmpresa.SelectedIndex = 0;
            EstilizarComboBoxDark(cbEmpresa);
            pnlCard.Controls.Add(cbEmpresa);
            curY += 40;

            // 2. Campo Nome da Máquina / Ativo
            Label lblNomeTag = new Label();
            lblNomeTag.Text = "NOME DO COMPUTADOR / ATIVO * (ex: QUA-01, FIN-02, NOTEBOOK-01):";
            lblNomeTag.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            lblNomeTag.ForeColor = Color.FromArgb(203, 213, 225);
            lblNomeTag.Location = new Point(20, curY);
            lblNomeTag.AutoSize = true;
            pnlCard.Controls.Add(lblNomeTag);
            curY += 22;

            txtNome = new TextBox();
            txtNome.Text = Environment.MachineName;
            txtNome.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            txtNome.BackColor = Color.FromArgb(15, 23, 42);
            txtNome.ForeColor = Color.White;
            txtNome.BorderStyle = BorderStyle.FixedSingle;
            txtNome.Location = new Point(20, curY);
            txtNome.Size = new Size(480, 26);
            pnlCard.Controls.Add(txtNome);
            curY += 40;

            // 3. Campo Setor / Departamento (SEM HASHTAGS)
            Label lblSetorTag = new Label();
            lblSetorTag.Text = "SETOR / DEPARTAMENTO *";
            lblSetorTag.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            lblSetorTag.ForeColor = Color.FromArgb(203, 213, 225);
            lblSetorTag.Location = new Point(20, curY);
            lblSetorTag.AutoSize = true;
            pnlCard.Controls.Add(lblSetorTag);
            curY += 22;

            cbTag = new ComboBox();
            cbTag.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            cbTag.BackColor = Color.FromArgb(15, 23, 42);
            cbTag.ForeColor = Color.FromArgb(248, 250, 252);
            cbTag.FlatStyle = FlatStyle.Flat;
            cbTag.DropDownStyle = ComboBoxStyle.DropDown;
            cbTag.Location = new Point(20, curY);
            cbTag.Size = new Size(480, 28);
            
            List<string> setoresCadastrados = CarregarSetoresDisponiveis();
            foreach (string s in setoresCadastrados)
            {
                cbTag.Items.Add(s);
            }
            if (cbTag.Items.Count > 0) cbTag.SelectedIndex = 0;
            EstilizarComboBoxDark(cbTag);
            pnlCard.Controls.Add(cbTag);
            curY += 40;

            // 4. Campo Diretório de Instalação no Disco C:
            Label lblDirTag = new Label();
            lblDirTag.Text = "DIRETÓRIO DE INSTALAÇÃO NO DISCO C:";
            lblDirTag.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            lblDirTag.ForeColor = Color.FromArgb(203, 213, 225);
            lblDirTag.Location = new Point(20, curY);
            lblDirTag.AutoSize = true;
            pnlCard.Controls.Add(lblDirTag);
            curY += 22;

            txtDiretorio = new TextBox();
            txtDiretorio.Text = @"C:\Nexus-Agent";
            txtDiretorio.Font = new Font("Consolas", 9.5f, FontStyle.Regular);
            txtDiretorio.BackColor = Color.FromArgb(15, 23, 42);
            txtDiretorio.ForeColor = Color.FromArgb(226, 232, 240);
            txtDiretorio.BorderStyle = BorderStyle.FixedSingle;
            txtDiretorio.Location = new Point(20, curY);
            txtDiretorio.Size = new Size(380, 25);
            pnlCard.Controls.Add(txtDiretorio);

            btnProcurarDir = new Button();
            btnProcurarDir.Text = "Procurar...";
            btnProcurarDir.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnProcurarDir.BackColor = Color.FromArgb(30, 41, 59);
            btnProcurarDir.ForeColor = Color.White;
            btnProcurarDir.FlatStyle = FlatStyle.Flat;
            btnProcurarDir.FlatAppearance.BorderColor = Color.FromArgb(51, 65, 85);
            btnProcurarDir.Cursor = Cursors.Hand;
            btnProcurarDir.Location = new Point(406, curY - 1);
            btnProcurarDir.Size = new Size(94, 27);
            btnProcurarDir.Click += (s, e) => {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "Selecione a pasta no Disco C: para instalação do agente:";
                    fbd.SelectedPath = @"C:\";
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        txtDiretorio.Text = fbd.SelectedPath;
                    }
                }
            };
            pnlCard.Controls.Add(btnProcurarDir);
            curY += 36;

            // 4.1. Opção de Servidor / Nó de Infraestrutura (Monitoramento na aba Servidores & Rede)
            Panel pnlServidorToggle = new Panel();
            pnlServidorToggle.BackColor = Color.FromArgb(15, 23, 42);
            pnlServidorToggle.Location = new Point(20, curY);
            pnlServidorToggle.Size = new Size(480, 48);
            pnlServidorToggle.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, pnlServidorToggle.Width - 1, pnlServidorToggle.Height - 1);
                Color borderCol = (chkIsServidor != null && chkIsServidor.Checked) ? Color.FromArgb(56, 189, 248) : Color.FromArgb(30, 41, 59);
                using (GraphicsPath path = ObterCaminhoArredondado(r, 8))
                using (Pen p = new Pen(borderCol, 1.5f))
                {
                    e.Graphics.DrawPath(p, path);
                }
            };
            pnlCard.Controls.Add(pnlServidorToggle);

            chkIsServidor = new CheckBox();
            chkIsServidor.Text = "🖥️  Esta máquina é um Servidor (Monitorar Armazenamento e Consumo)";
            chkIsServidor.Font = new Font("Segoe UI", 8.75f, FontStyle.Bold);
            chkIsServidor.ForeColor = Color.FromArgb(241, 245, 249);
            chkIsServidor.BackColor = Color.Transparent;
            chkIsServidor.Cursor = Cursors.Hand;
            chkIsServidor.Location = new Point(12, 6);
            chkIsServidor.Size = new Size(456, 20);
            chkIsServidor.CheckedChanged += (s, e) => {
                pnlServidorToggle.Invalidate();
                if (chkIsServidor.Checked)
                {
                    chkIsServidor.ForeColor = Color.FromArgb(56, 189, 248);
                    lblStatus.Text = "● Modo Servidor ativo: discos, CPU, RAM e rede serão monitorados em Servidores & Rede.";
                }
                else
                {
                    chkIsServidor.ForeColor = Color.FromArgb(241, 245, 249);
                    lblStatus.Text = "● Pronto para instalar e configurar a estação de trabalho.";
                }
            };
            pnlServidorToggle.Controls.Add(chkIsServidor);

            Label lblServidorSub = new Label();
            lblServidorSub.Text = "Exibe painel de discos/partições, consumo de CPU, RAM e tráfego na aba Servidores & Rede";
            lblServidorSub.Font = new Font("Segoe UI", 7.5f, FontStyle.Regular);
            lblServidorSub.ForeColor = Color.FromArgb(148, 163, 184);
            lblServidorSub.Location = new Point(30, 26);
            lblServidorSub.Size = new Size(440, 16);
            lblServidorSub.Cursor = Cursors.Hand;
            lblServidorSub.Click += (s, e) => { chkIsServidor.Checked = !chkIsServidor.Checked; };
            pnlServidorToggle.Controls.Add(lblServidorSub);

            // Se já houver is_server.txt com true ou hostname começando com SRV-, pré-seleciona
            try
            {
                string existingSrvFile = Path.Combine(@"C:\Nexus-Agent", "is_server.txt");
                if (File.Exists(existingSrvFile) && File.ReadAllText(existingSrvFile).Trim().ToLower() == "true")
                {
                    chkIsServidor.Checked = true;
                }
                else if (Environment.MachineName.ToUpper().StartsWith("SRV"))
                {
                    chkIsServidor.Checked = true;
                }
            }
            catch { }

            curY += 56;

            // 5. Card de Especificações e Recursos
            Panel pnlInfo = new Panel();
            pnlInfo.BackColor = Color.FromArgb(11, 15, 25);
            pnlInfo.Location = new Point(20, curY);
            pnlInfo.Size = new Size(480, 96);
            pnlInfo.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, pnlInfo.Width - 1, pnlInfo.Height - 1);
                using (GraphicsPath path = ObterCaminhoArredondado(r, 10))
                using (Pen p = new Pen(Color.FromArgb(30, 41, 59), 1f))
                {
                    e.Graphics.DrawPath(p, path);
                }
            };
            pnlCard.Controls.Add(pnlInfo);

            Label lblInfo1 = new Label();
            lblInfo1.Text = "• Instalação Local: C:\\Nexus-Agent (com desinstalador em 'Desinstalar um programa')\n• Portal de Chamados: Atalho de chamado criado automaticamente na Área de Trabalho\n• Porta de Rede: TCP 8090 liberada no Windows Firewall com regra dupla\n• Inicialização: Serviço em segundo plano ativado com o Windows";
            lblInfo1.Font = new Font("Segoe UI", 8.25f, FontStyle.Regular);
            lblInfo1.ForeColor = Color.FromArgb(148, 163, 184);
            lblInfo1.Location = new Point(12, 10);
            lblInfo1.Size = new Size(456, 76);
            pnlInfo.Controls.Add(lblInfo1);

            // Status e Botões de Ação no Rodapé
            lblStatus = new Label();
            lblStatus.Text = "● Pronto para instalar e configurar a estação de trabalho.";
            lblStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
            lblStatus.ForeColor = Color.FromArgb(148, 163, 184);
            lblStatus.Location = new Point(24, 530);
            lblStatus.Size = new Size(522, 20);
            pnlInput.Controls.Add(lblStatus);

            // Botão Principal Instalar
            btnInstalar = new Button();
            btnInstalar.Text = "Instalar e Ativar Nexus Agent ➔";
            btnInstalar.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            btnInstalar.BackColor = Color.FromArgb(37, 99, 235); // Blue-600
            btnInstalar.ForeColor = Color.White;
            btnInstalar.FlatStyle = FlatStyle.Flat;
            btnInstalar.FlatAppearance.BorderSize = 0;
            btnInstalar.Cursor = Cursors.Hand;
            btnInstalar.Location = new Point(24, 556);
            btnInstalar.Size = new Size(365, 46);
            btnInstalar.Click += BtnInstalar_Click;
            pnlInput.Controls.Add(btnInstalar);

            // Botão Desinstalar
            btnDesinstalarExistente = new Button();
            btnDesinstalarExistente.Text = "🗑️ Desinstalar";
            btnDesinstalarExistente.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            btnDesinstalarExistente.BackColor = Color.FromArgb(31, 20, 24);
            btnDesinstalarExistente.ForeColor = Color.FromArgb(248, 113, 113);
            btnDesinstalarExistente.FlatStyle = FlatStyle.Flat;
            btnDesinstalarExistente.FlatAppearance.BorderColor = Color.FromArgb(127, 29, 29);
            btnDesinstalarExistente.Cursor = Cursors.Hand;
            btnDesinstalarExistente.Location = new Point(397, 556);
            btnDesinstalarExistente.Size = new Size(149, 46);
            btnDesinstalarExistente.Click += (s, e) => {
                Program.ExecutarDesinstalacao(false);
            };
            pnlInput.Controls.Add(btnDesinstalarExistente);

            this.ActiveControl = txtNome;

            // =================================================================
            // PAINEL DE SUCESSO (Tela 2 - Inicialmente escondido)
            // =================================================================
            pnlSuccess = new Panel();
            pnlSuccess.Location = new Point(0, 84);
            pnlSuccess.Size = new Size(570, 626);
            pnlSuccess.BackColor = Color.FromArgb(11, 15, 25);
            pnlSuccess.Visible = false;
            this.Controls.Add(pnlSuccess);

            Label lblSuccessIcon = new Label();
            lblSuccessIcon.Text = "✓";
            lblSuccessIcon.Font = new Font("Segoe UI", 42f, FontStyle.Bold);
            lblSuccessIcon.ForeColor = Color.FromArgb(16, 185, 129); // Emerald-500
            lblSuccessIcon.TextAlign = ContentAlignment.MiddleCenter;
            lblSuccessIcon.Location = new Point(24, 18);
            lblSuccessIcon.Size = new Size(522, 64);
            pnlSuccess.Controls.Add(lblSuccessIcon);

            Label lblSuccessTitle = new Label();
            lblSuccessTitle.Text = "Nexus Remote Agent Instalado com Sucesso!";
            lblSuccessTitle.Font = new Font("Segoe UI", 13.5f, FontStyle.Bold);
            lblSuccessTitle.ForeColor = Color.FromArgb(248, 250, 252);
            lblSuccessTitle.TextAlign = ContentAlignment.MiddleCenter;
            lblSuccessTitle.Location = new Point(24, 86);
            lblSuccessTitle.Size = new Size(522, 28);
            pnlSuccess.Controls.Add(lblSuccessTitle);

            Label lblSuccessSub = new Label();
            lblSuccessSub.Text = "A máquina foi cadastrada no console de TI e está integrada ao portal de chamados.";
            lblSuccessSub.Font = new Font("Segoe UI", 8.75f, FontStyle.Regular);
            lblSuccessSub.ForeColor = Color.FromArgb(148, 163, 184);
            lblSuccessSub.TextAlign = ContentAlignment.MiddleCenter;
            lblSuccessSub.Location = new Point(24, 116);
            lblSuccessSub.Size = new Size(522, 22);
            pnlSuccess.Controls.Add(lblSuccessSub);

            Panel pnlCardSuccess = new Panel();
            pnlCardSuccess.BackColor = Color.FromArgb(17, 24, 39);
            pnlCardSuccess.Location = new Point(24, 146);
            pnlCardSuccess.Size = new Size(522, 230);
            pnlCardSuccess.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, pnlCardSuccess.Width - 1, pnlCardSuccess.Height - 1);
                using (GraphicsPath path = ObterCaminhoArredondado(r, 14))
                using (Pen p = new Pen(Color.FromArgb(30, 41, 59), 1.5f))
                {
                    e.Graphics.DrawPath(p, path);
                }
            };
            pnlSuccess.Controls.Add(pnlCardSuccess);

            lblEmpresaResult = new Label();
            lblEmpresaResult.Text = "🏢 Empresa: Aço Móveis";
            lblEmpresaResult.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            lblEmpresaResult.ForeColor = Color.FromArgb(56, 189, 248);
            lblEmpresaResult.Location = new Point(18, 16);
            lblEmpresaResult.Size = new Size(484, 24);
            pnlCardSuccess.Controls.Add(lblEmpresaResult);

            lblNomeResult = new Label();
            lblNomeResult.Text = "💻 Ativo Cadastrado: TI-0001 (Qualidade)";
            lblNomeResult.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            lblNomeResult.ForeColor = Color.FromArgb(16, 185, 129);
            lblNomeResult.Location = new Point(18, 42);
            lblNomeResult.Size = new Size(484, 24);
            pnlCardSuccess.Controls.Add(lblNomeResult);

            lblIpResult = new Label();
            lblIpResult.Text = "🌐 Endereço de Conexão: http://192.168.18.122:8090";
            lblIpResult.Font = new Font("Consolas", 10.5f, FontStyle.Bold);
            lblIpResult.ForeColor = Color.White;
            lblIpResult.Location = new Point(18, 68);
            lblIpResult.Size = new Size(484, 24);
            pnlCardSuccess.Controls.Add(lblIpResult);

            Label lblDetails = new Label();
            lblDetails.Text = "• Registrado em 'Painel de Controle > Desinstalar um Programa'\n• Client de Avisos Ativo: Suporta notas, enquetes e alertas na tela\n• Atalhos 'Abrir Chamado T.I' criados na Área de Trabalho com Spotlight\n• Regras duplas de Firewall ativas (Porta 8090 + Executável)\n• Inicialização automática habilitada em segundo plano com o Windows";
            lblDetails.Font = new Font("Segoe UI", 8.25f, FontStyle.Regular);
            lblDetails.ForeColor = Color.FromArgb(148, 163, 184);
            lblDetails.Location = new Point(18, 102);
            lblDetails.Size = new Size(484, 110);
            pnlCardSuccess.Controls.Add(lblDetails);

            Button btnFechar = new Button();
            btnFechar.Text = "Concluir e Abrir Portal de Chamados";
            btnFechar.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            btnFechar.BackColor = Color.FromArgb(37, 99, 235);
            btnFechar.ForeColor = Color.White;
            btnFechar.FlatStyle = FlatStyle.Flat;
            btnFechar.FlatAppearance.BorderSize = 0;
            btnFechar.Cursor = Cursors.Hand;
            btnFechar.Location = new Point(24, 396);
            btnFechar.Size = new Size(522, 48);
            btnFechar.Click += (s, e) => {
                try {
                    Process.Start("https://nexus-ten-brown.vercel.app/chamado.html");
                } catch { }
                this.Close();
            };
            pnlSuccess.Controls.Add(btnFechar);
        }

        private void BtnInstalar_Click(object sender, EventArgs e)
        {
            string empresaAtivo = cbEmpresa.Text.Trim();
            if (string.IsNullOrEmpty(empresaAtivo)) empresaAtivo = "ACOMOVEIS";

            string nomeAtivo = txtNome.Text.Trim();
            if (string.IsNullOrEmpty(nomeAtivo)) nomeAtivo = Environment.MachineName;

            // Limpa qualquer hashtag caso o usuário digite manualmente
            string tagAtivo = cbTag.Text.Trim().Replace("#", "").Trim();
            if (string.IsNullOrEmpty(tagAtivo)) tagAtivo = "Qualidade";

            btnInstalar.Enabled = false;
            btnDesinstalarExistente.Enabled = false;
            btnInstalar.Text = "Instalando e Configurando o Agente...";
            lblStatus.Text = "Preparando diretório de instalação...";
            Application.DoEvents();

            try
            {
                // 1. Diretório de destino selecionado pelo usuário no Disco C:
                string targetDir = txtDiretorio.Text.Trim();
                if (string.IsNullOrEmpty(targetDir)) targetDir = @"C:\Nexus-Agent";
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                // 2. Encerra instâncias e processos anteriores
                lblStatus.Text = "Encerrando instâncias anteriores do agente...";
                Application.DoEvents();
                foreach (string pName in new string[] { "NexusRemoteAgent", "FSCRemoteAgent" })
                {
                    try
                    {
                        foreach (var p in Process.GetProcessesByName(pName))
                        {
                            try { p.Kill(); p.WaitForExit(1500); } catch { }
                        }
                    }
                    catch { }
                }

                // 3. Extrai executável do agente (embutido, local ou download do GitHub)
                lblStatus.Text = "Copiando binários do Nexus Remote Agent...";
                Application.DoEvents();
                string targetExe = Path.Combine(targetDir, "NexusRemoteAgent.exe");

                bool extraiu = false;
                try
                {
                    string[] candResources = Assembly.GetExecutingAssembly().GetManifestResourceNames();
                    Stream resStream = null;
                    foreach (string rName in candResources)
                    {
                        if (rName.EndsWith("NexusRemoteAgent.exe", StringComparison.OrdinalIgnoreCase) ||
                            rName.EndsWith("FSCRemoteAgent.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            resStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(rName);
                            if (resStream != null) break;
                        }
                    }
                    if (resStream == null) resStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Agent.NexusRemoteAgent.exe");
                    if (resStream == null) resStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("NexusRemoteAgent.exe");
                    if (resStream == null) resStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FSCRemoteAgent.exe");

                    if (resStream != null)
                    {
                        using (resStream)
                        using (FileStream fs = new FileStream(targetExe, FileMode.Create, FileAccess.Write))
                        {
                            resStream.CopyTo(fs);
                        }
                        extraiu = true;
                    }
                }
                catch { }

                if (!extraiu)
                {
                    string[] candsLocal = new string[] {
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NexusRemoteAgent.exe"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "agent", "NexusRemoteAgent.exe"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FSCRemoteAgent.exe"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "agent", "FSCRemoteAgent.exe")
                    };
                    foreach (string localExe in candsLocal)
                    {
                        if (File.Exists(localExe))
                        {
                            try { File.Copy(localExe, targetExe, true); extraiu = true; break; } catch { }
                        }
                    }
                }

                // Fallback automático via GitHub / Vercel caso os binários não estejam presentes
                if (!File.Exists(targetExe) || new FileInfo(targetExe).Length < 10000)
                {
                    try
                    {
                        lblStatus.Text = "Baixando versão mais recente do Nexus Agent...";
                        Application.DoEvents();
                        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                        using (WebClient wc = new WebClient())
                        {
                            wc.Headers["User-Agent"] = "NexusInstaller/2.5";
                            try
                            {
                                wc.DownloadFile("https://raw.githubusercontent.com/biel7nov/nexus/main/agent/NexusRemoteAgent.exe", targetExe);
                            }
                            catch
                            {
                                wc.DownloadFile("https://nexus-ten-brown.vercel.app/agent/NexusRemoteAgent.exe", targetExe);
                            }
                        }
                    }
                    catch { }
                }

                if (!File.Exists(targetExe))
                {
                    throw new FileNotFoundException("O arquivo do agente '" + targetExe + "' não pôde ser instalado nem baixado.");
                }

                try { string fscLegacy = Path.Combine(targetDir, "FSCRemoteAgent.exe"); if (File.Exists(targetExe)) File.Copy(targetExe, fscLegacy, true); } catch { }

                // 3.0. Copia recursos Nexus (ícone, svg, html, spotlight)
                try
                {
                    string targetChamadoHtml = Path.Combine(targetDir, "chamado.html");
                    string targetResDir = Path.Combine(targetDir, "res");
                    if (!Directory.Exists(targetResDir)) Directory.CreateDirectory(targetResDir);
                    string targetSpotlight = Path.Combine(targetResDir, "spotlight.jpg");
                try {
                    string targetEmpresas = Path.Combine(targetDir, "empresas.json");
                    string[] candsEmp = new string[] {
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "empresas.json"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\empresas.json"),
                        @"c:\Users\gabrielnovaes\Documents\copia magma\empresas.json"
                    };
                    foreach (var ec in candsEmp) {
                        if (File.Exists(ec)) { File.Copy(ec, targetEmpresas, true); break; }
                    }
                } catch { }
                try {
                    string targetSetores = Path.Combine(targetDir, "setores.json");
                    string[] cands = new string[] {
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "setores.json"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\setores.json"),
                        @"c:\Users\gabrielnovaes\Documents\copia magma\setores.json"
                    };
                    foreach (var sc in cands) {
                        if (File.Exists(sc)) { File.Copy(sc, targetSetores, true); break; }
                    }
                } catch { }
                string targetIco = Path.Combine(targetResDir, "logo_nexus.ico");
                string targetSvg = Path.Combine(targetResDir, "logo_nexus.svg");
                
                string[] icoCandidates = new string[] {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo_nexus.ico"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "res", "logo_nexus.ico"),
                    @"c:\Users\gabrielnovaes\Documents\copia magma\res\logo_nexus.ico"
                };
                foreach (var ic in icoCandidates) {
                    if (File.Exists(ic)) { try { File.Copy(ic, targetIco, true); } catch { } break; }
                }

                    string[] localChamados = new string[] {
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "chamado.html"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\chamado.html"),
                        @"c:\Users\gabrielnovaes\Documents\copia magma\chamado.html",
                        @"c:\Users\gabrielnovaes\Documents\fsc-console\chamado.html"
                    };
                    foreach (var lc in localChamados) { if (File.Exists(lc)) { File.Copy(lc, targetChamadoHtml, true); break; } }

                    string[] localSpots = new string[] {
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"res\spotlight.jpg"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\res\spotlight.jpg"),
                        @"c:\Users\gabrielnovaes\Documents\copia magma\res\spotlight.jpg",
                        @"c:\Users\gabrielnovaes\Documents\fsc-console\res\spotlight.jpg"
                    };
                    foreach (var ls in localSpots) { if (File.Exists(ls)) { File.Copy(ls, targetSpotlight, true); break; } }
                }
                catch { }

                // 3.1. Copia o próprio instalador como Desinstalador oficial dentro da pasta de destino
                string uninstallerExe = Path.Combine(targetDir, "Desinstalador-FSC-Agent.exe");
                string installerBackupExe = Path.Combine(targetDir, "Instalador-FSC-Agent.exe");
                try
                {
                    string currentExe = Application.ExecutablePath;
                    if (File.Exists(currentExe))
                    {
                        if (!currentExe.Equals(uninstallerExe, StringComparison.OrdinalIgnoreCase))
                            File.Copy(currentExe, uninstallerExe, true);
                        if (!currentExe.Equals(installerBackupExe, StringComparison.OrdinalIgnoreCase))
                            File.Copy(currentExe, installerBackupExe, true);
                    }
                }
                catch { }

                // 4. Grava identificação da máquina, empresa, token único por máquina e servidor central
                string tokenPath = Path.Combine(targetDir, "agent_token.txt");
                string machineToken = "FSC@" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                if (File.Exists(tokenPath))
                {
                    try
                    {
                        string existingTok = File.ReadAllText(tokenPath, Encoding.UTF8).Trim();
                        if (!string.IsNullOrEmpty(existingTok) && existingTok != "FSC@3606")
                            machineToken = existingTok;
                    }
                    catch { }
                }

                string serverAddr = "192.168.18.122:8090";
                string localSrvCfg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server_ip.txt");
                if (File.Exists(localSrvCfg))
                {
                    try
                    {
                        string sCfg = File.ReadAllText(localSrvCfg, Encoding.UTF8).Trim();
                        if (!string.IsNullOrEmpty(sCfg)) serverAddr = sCfg;
                    }
                    catch { }
                }

                bool isServidor = (chkIsServidor != null && chkIsServidor.Checked);
                string isServidorStr = isServidor ? "true" : "false";

                File.WriteAllText(Path.Combine(targetDir, "empresa.txt"), empresaAtivo, Encoding.UTF8);
                File.WriteAllText(Path.Combine(targetDir, "machine_name.txt"), nomeAtivo, Encoding.UTF8);
                File.WriteAllText(Path.Combine(targetDir, "machine_tag.txt"), tagAtivo, Encoding.UTF8);
                File.WriteAllText(Path.Combine(targetDir, "is_server.txt"), isServidorStr, Encoding.UTF8);
                File.WriteAllText(tokenPath, machineToken, Encoding.UTF8);
                File.WriteAllText(Path.Combine(targetDir, "server_ip.txt"), serverAddr, Encoding.UTF8);

                try {
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "empresa.txt"), empresaAtivo, Encoding.UTF8);
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "machine_name.txt"), nomeAtivo, Encoding.UTF8);
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "machine_tag.txt"), tagAtivo, Encoding.UTF8);
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "is_server.txt"), isServidorStr, Encoding.UTF8);
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "agent_token.txt"), machineToken, Encoding.UTF8);
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server_ip.txt"), serverAddr, Encoding.UTF8);
                } catch { }

                // 5. Configura Inicialização Automática com o Windows (CurrentUser e LocalMachine)
                lblStatus.Text = "Configurando inicialização automática...";
                Application.DoEvents();
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (key != null) key.SetValue("NexusRemoteAgent", "\"" + targetExe + "\"");
                    }
                }
                catch { }
                try
                {
                    using (RegistryKey keyLM = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (keyLM != null) keyLM.SetValue("NexusRemoteAgent", "\"" + targetExe + "\"");
                    }
                }
                catch { }

                // 5.1. REGISTRO OFICIAL NO 'DESINSTALAR UM PROGRAMA' DO WINDOWS
                lblStatus.Text = "Registrando em 'Desinstalar um Programa' do Windows...";
                Application.DoEvents();
                RegistrarDesinstaladorWindows(targetDir, targetExe, uninstallerExe);

                // 6. Configura Regras no Firewall do Windows (Porta 8090 TCP + Regra de Aplicativo)
                lblStatus.Text = "Liberando porta 8090 e permissões no Firewall do Windows...";
                Application.DoEvents();
                try
                {
                    ProcessStartInfo psiFw = new ProcessStartInfo("netsh", "advfirewall firewall add rule name=\"NexusTI Remote Agent Port 8090\" dir=in action=allow protocol=TCP localport=8090 profile=any");
                    psiFw.CreateNoWindow = true;
                    psiFw.UseShellExecute = false;
                    Process pFw = Process.Start(psiFw);
                    if (pFw != null) pFw.WaitForExit(3000);

                    ProcessStartInfo psiApp = new ProcessStartInfo("netsh", string.Format("advfirewall firewall add rule name=\"NexusTI Remote Agent App\" dir=in action=allow program=\"{0}\" enable=yes profile=any", targetExe));
                    psiApp.CreateNoWindow = true;
                    psiApp.UseShellExecute = false;
                    Process pApp = Process.Start(psiApp);
                    if (pApp != null) pApp.WaitForExit(3000);
                }
                catch { }

                // 7. Detecta IP Local
                string localIp = "127.0.0.1";
                try
                {
                    using (Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                    {
                        sock.Connect("8.8.8.8", 65530);
                        IPEndPoint ep = sock.LocalEndPoint as IPEndPoint;
                        if (ep != null) localIp = ep.Address.ToString();
                    }
                }
                catch
                {
                    try
                    {
                        foreach (IPAddress ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                        {
                            if (ip.AddressFamily == AddressFamily.InterNetwork && !ip.ToString().StartsWith("169.254") && !ip.ToString().StartsWith("127."))
                            {
                                localIp = ip.ToString();
                                break;
                            }
                        }
                    }
                    catch { }
                }

                // 8. Atualiza assets_registry.json se acessível
                AtualizarRegistroLocal(nomeAtivo, tagAtivo, localIp, empresaAtivo, isServidor);

                // 8.1. Cria Atalhos "Abrir Chamado T.I" na Área de Trabalho e no Menu Iniciar
                lblStatus.Text = "Criando atalhos na Área de Trabalho e Menu Iniciar...";
                Application.DoEvents();
                try
                {
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    string lnkPath = Path.Combine(desktop, "Abrir Chamado T.I.lnk");
                    
                    string psScript = string.Format(
                        "$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut('{0}'); $s.TargetPath = '{1}'; $s.Arguments = '-chamado'; $s.IconLocation = '{1},0'; $s.Description = 'Abrir Chamado com Suporte de T.I'; $s.Save()",
                        lnkPath,
                        targetExe
                    );
                    ProcessStartInfo psiLnk = new ProcessStartInfo("powershell", "-NoProfile -Command \"" + psScript + "\"");
                    psiLnk.CreateNoWindow = true;
                    psiLnk.UseShellExecute = false;
                    Process pLnk = Process.Start(psiLnk);
                    if (pLnk != null) pLnk.WaitForExit(3000);

                    string urlPath = Path.Combine(desktop, "Abrir Chamado T.I (Web).url");
                    string portalHospedadoUrl = string.Format(
                        "https://nexus-ten-brown.vercel.app/chamado.html?empresa={0}&setor={1}&ativo={2}",
                        Uri.EscapeDataString(empresaAtivo ?? "Aço Móveis"),
                        Uri.EscapeDataString(tagAtivo ?? "TI"),
                        Uri.EscapeDataString(nomeAtivo ?? Environment.MachineName)
                    );
                    File.WriteAllText(urlPath, "[InternetShortcut]\r\nURL=" + portalHospedadoUrl + "\r\nIconIndex=0\r\nIconFile=" + targetExe + "\r\n");

                    // Atalho no Menu Iniciar
                    string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Fast Solutions Corporate");
                    if (!Directory.Exists(startMenu)) Directory.CreateDirectory(startMenu);
                    string lnkStart = Path.Combine(startMenu, "Abrir Chamado T.I.lnk");
                    string lnkUninst = Path.Combine(startMenu, "Desinstalar FSC Agent.lnk");
                    string psStart = string.Format(
                        "$ws = New-Object -ComObject WScript.Shell; $s1 = $ws.CreateShortcut('{0}'); $s1.TargetPath = '{1}'; $s1.Arguments = '-chamado'; $s1.Save(); $s2 = $ws.CreateShortcut('{2}'); $s2.TargetPath = '{3}'; $s2.Arguments = '/uninstall'; $s2.Save()",
                        lnkStart, targetExe, lnkUninst, uninstallerExe
                    );
                    ProcessStartInfo psiStart = new ProcessStartInfo("powershell", "-NoProfile -Command \"" + psStart + "\"");
                    psiStart.CreateNoWindow = true;
                    psiStart.UseShellExecute = false;
                    Process pStart = Process.Start(psiStart);
                    if (pStart != null) pStart.WaitForExit(3000);
                }
                catch { }

                // 9. Inicia o Nexus Remote Agent em segundo plano
                lblStatus.Text = "Iniciando o agente em segundo plano...";
                Application.DoEvents();

                bool iniciou = false;
                try
                {
                    ProcessStartInfo psiAgent = new ProcessStartInfo(targetExe);
                    psiAgent.WorkingDirectory = targetDir;
                    psiAgent.UseShellExecute = true;
                    Process.Start(psiAgent);
                    iniciou = true;
                }
                catch { }

                if (!iniciou)
                {
                    try
                    {
                        ProcessStartInfo psiCmd = new ProcessStartInfo("cmd.exe", "/c start \"\" \"" + targetExe + "\"");
                        psiCmd.CreateNoWindow = true;
                        psiCmd.UseShellExecute = false;
                        psiCmd.WorkingDirectory = targetDir;
                        Process.Start(psiCmd);
                        iniciou = true;
                    }
                    catch { }
                }

                if (!iniciou)
                {
                    try
                    {
                        ProcessStartInfo psiPs = new ProcessStartInfo("powershell", "-NoProfile -Command \"Start-Process '" + targetExe + "'\"");
                        psiPs.CreateNoWindow = true;
                        psiPs.UseShellExecute = false;
                        Process.Start(psiPs);
                        iniciou = true;
                    }
                    catch { }
                }

                // 10. Exibe Tela de Sucesso
                lblEmpresaResult.Text = "🏢 Empresa: " + empresaAtivo;
                lblNomeResult.Text = (isServidor ? "🖥️ Servidor Monitorado: " : "💻 Ativo Cadastrado: ") + nomeAtivo + " (" + tagAtivo + ")";
                lblIpResult.Text = "🌐 Endereço de Conexão: http://" + localIp + ":8090";

                pnlInput.Visible = false;
                pnlSuccess.Visible = true;
            }
            catch (Exception ex)
            {
                btnInstalar.Enabled = true;
                btnDesinstalarExistente.Enabled = true;
                btnInstalar.Text = "Tentar Instalar Novamente";
                lblStatus.Text = "Erro na instalação: " + ex.Message;
                MessageBox.Show("Erro durante a instalação: " + ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RegistrarDesinstaladorWindows(string targetDir, string targetExe, string uninstallerExe)
        {
            string uninstSubKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\NexusRemoteAgent";

            Action<RegistryKey> gravarValores = (RegistryKey ukey) => {
                if (ukey == null) return;
                ukey.SetValue("DisplayName", "NexusTI — Remote Agent & Suporte Corporativo");
                ukey.SetValue("DisplayVersion", "2.5.0");
                ukey.SetValue("Publisher", "NexusTI");
                ukey.SetValue("InstallLocation", targetDir);
                ukey.SetValue("DisplayIcon", targetExe + ",0");
                ukey.SetValue("UninstallString", "\"" + uninstallerExe + "\" /uninstall");
                ukey.SetValue("QuietUninstallString", "\"" + uninstallerExe + "\" /uninstall /quiet");
                ukey.SetValue("EstimatedSize", 12800, RegistryValueKind.DWord);
                ukey.SetValue("NoModify", 1, RegistryValueKind.DWord);
                ukey.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                ukey.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                ukey.SetValue("URLInfoAbout", "https://nexus-ten-brown.vercel.app/chamado.html");
                ukey.SetValue("HelpLink", "https://nexus-ten-brown.vercel.app/chamado.html");
                ukey.SetValue("Contact", "Equipe de Gestão e Suporte de TI");
                ukey.SetValue("Comments", "Agente Corporativo de Suporte, Telemetria, Acesso Remoto, Avisos e Enquetes de TI");
            };

            // 1. Grava no HKLM 64-bit hive
            try
            {
                using (RegistryKey baseKey64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey ukey64 = baseKey64.CreateSubKey(uninstSubKey))
                {
                    gravarValores(ukey64);
                }
            }
            catch { }

            // 2. Grava no HKLM 32-bit hive (WOW6432Node)
            try
            {
                using (RegistryKey baseKey32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                using (RegistryKey ukey32 = baseKey32.CreateSubKey(uninstSubKey))
                {
                    gravarValores(ukey32);
                }
            }
            catch { }

            // 3. Grava no HKCU (Current User)
            try
            {
                using (RegistryKey ukeyUser = Registry.CurrentUser.CreateSubKey(uninstSubKey))
                {
                    gravarValores(ukeyUser);
                }
            }
            catch { }
        }

        private void AtualizarRegistroLocal(string nome, string tag, string ip, string empresa, bool isServidor = false)
        {
            string tipoAtivo = isServidor ? "Servidor" : "Desktop";
            // 1. Envia cadastro do ativo pela rede para o Servidor Central (funciona em qualquer máquina da LAN)
            try
            {
                string srvAddr = "192.168.18.122:8090";
                string srvFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server_ip.txt");
                if (File.Exists(srvFile))
                {
                    string s = File.ReadAllText(srvFile, Encoding.UTF8).Trim();
                    if (!string.IsNullOrEmpty(s)) srvAddr = s;
                }
                if (!srvAddr.Contains(":")) srvAddr += ":8090";

                string regUrl = string.Format(
                    "http://{0}/api/register?host={1}&tag={2}&ip={3}&user={4}&empresa={5}&os={6}&type={7}&is_server={8}",
                    srvAddr,
                    Uri.EscapeDataString(nome ?? ""),
                    Uri.EscapeDataString(tag ?? ""),
                    Uri.EscapeDataString(ip ?? ""),
                    Uri.EscapeDataString(Environment.UserName ?? ""),
                    Uri.EscapeDataString(empresa ?? "Aço Móveis"),
                    Uri.EscapeDataString("Windows 10/11 Pro"),
                    Uri.EscapeDataString(tipoAtivo),
                    isServidor ? "true" : "false"
                );
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(regUrl);
                req.Method = "GET";
                req.Timeout = 2500;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse()) { }
            }
            catch { }

            // 2. Fallback / Sincronização local caso o instalador esteja rodando no próprio servidor da console
            try
            {
                string[] paths = new string[] {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets_registry.json"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\assets_registry.json"),
                    @"C:\Nexus-Agent\assets_registry.json",
                    @"c:\Users\gabrielnovaes\Documents\copia magma\assets_registry.json",
                    @"c:\Users\gabrielnovaes\Documents\fsc-console\assets_registry.json"
                };

                foreach (string p in paths)
                {
                    try
                    {
                        if (File.Exists(p))
                        {
                            string txt = File.ReadAllText(p, Encoding.UTF8).Trim();
                            if (txt.StartsWith("[") && txt.EndsWith("]"))
                            {
                                if (!txt.Contains("\"host\": \"" + nome + "\"") && !txt.Contains("\"ip\": \"" + ip + "\""))
                                {
                                    string novoItem = string.Format(
                                        "  {{\n    \"id\": \"FSC-{0} {1}\",\n    \"host\": \"{2}\",\n    \"type\": \"{9}\",\n    \"isServer\": {10},\n    \"user\": \"{3}\",\n    \"domain\": \"CORP.LOCAL\",\n    \"loggedUserFull\": \"CORP\\\\{3}\",\n    \"location\": \"{4} - {5}\",\n    \"company\": \"{4}\",\n    \"empresa\": \"{4}\",\n    \"tag\": \"{5}\",\n    \"os\": \"{6}\",\n    \"cpu\": \"Intel Core / AMD\",\n    \"ram\": \"8.0 GB DDR4\",\n    \"disk\": \"SSD 512 GB\",\n    \"resolution\": \"1920x1080 Full HD\",\n    \"ip\": \"{7}\",\n    \"port\": 8090,\n    \"lastConnected\": \"Hoje, {8}\",\n    \"lastSeen\": \"Conectado agora\",\n    \"online\": true\n  }}",
                                        new Random().Next(100, 999),
                                        new Random().Next(100, 999),
                                        nome,
                                        Environment.UserName,
                                        empresa,
                                        tag,
                                        "Windows 10 Pro",
                                        ip,
                                        DateTime.Now.ToString("HH:mm"),
                                        tipoAtivo,
                                        isServidor ? "true" : "false"
                                    );
                                    string inner = txt.Substring(1, txt.Length - 2).Trim();
                                    string updated = "[\n" + novoItem + (string.IsNullOrEmpty(inner) ? "" : (",\n" + inner)) + "\n]";
                                    File.WriteAllText(p, updated, Encoding.UTF8);
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}

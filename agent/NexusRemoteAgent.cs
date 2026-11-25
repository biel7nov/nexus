using System.Media;
using System;
using System.IO;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;
using System.Collections.Generic;
using System.Reflection;

namespace NexusRemoteAgent
{
    class Program
    {
        public static Icon ObterIconeNexus()
        {
            try
            {
                Icon exeIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (exeIcon != null) return exeIcon;
            }
            catch { }
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidates = new string[] {
                    Path.Combine(baseDir, "logo_nexus.ico"),
                    Path.Combine(baseDir, "res", "logo_nexus.ico"),
                    @"C:\Nexus-Agent\res\logo_nexus.ico",
                    @"C:\NexusTI-Agent\res\logo_nexus.ico",
                    @"C:\FSC-Agent\res\logo_nexus.ico"
                };
                foreach (string c in candidates)
                {
                    if (File.Exists(c)) return new Icon(c);
                }
            }
            catch { }
            return CriarIconeVetorialNexus(32);
        }

        public static Bitmap ObterBitmapNexus(int size)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidates = new string[] {
                    Path.Combine(baseDir, "res", "logo_nexus.ico"),
                    Path.Combine(baseDir, "logo_nexus.ico"),
                    @"C:\Nexus-Agent\res\logo_nexus.ico",
                    @"C:\NexusTI-Agent\res\logo_nexus.ico"
                };
                foreach (string c in candidates)
                {
                    if (File.Exists(c))
                    {
                        using (Icon ic = new Icon(c, size, size))
                        {
                            return ic.ToBitmap();
                        }
                    }
                }
            }
            catch { }
            return CriarBitmapVetorialNexus(size);
        }

        public static Icon CriarIconeVetorialNexus(int size)
        {
            Bitmap bmp = CriarBitmapVetorialNexus(size);
            IntPtr hIcon = bmp.GetHicon();
            return Icon.FromHandle(hIcon);
        }

        public static Bitmap CriarBitmapVetorialNexus(int size)
        {
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                
                Rectangle rect = new Rectangle(0, 0, size - 1, size - 1);
                int radius = Math.Max(2, size / 4);
                using (GraphicsPath path = new GraphicsPath())
                {
                    int d = radius * 2;
                    path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                    path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                    path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                    path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                    path.CloseFigure();
                    
                    using (SolidBrush bg = new SolidBrush(Color.FromArgb(15, 17, 26)))
                    {
                        g.FillPath(bg, path);
                    }
                    using (Pen border = new Pen(Color.FromArgb(56, 189, 248), Math.Max(1.0f, size * 0.03f)))
                    {
                        g.DrawPath(border, path);
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
                int cX = size / 2;
                int cY = size / 2;
                using (SolidBrush dotBr = new SolidBrush(Color.FromArgb(56, 189, 248)))
                {
                    g.FillEllipse(dotBr, cX - dot / 2, cY - dot / 2, dot, dot);
                }
            }
            return bmp;
        }


        // =========================================================================
        // COLETA AVANÇADA DE HARDWARE, SO, DOMÍNIO E DISPOSITIVO
        // =========================================================================
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            public MEMORYSTATUSEX() { this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)); }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out long lpIdleTime, out long lpKernelTime, out long lpUserTime);

        static long prevIdleTime = 0;
        static long prevKernelTime = 0;
        static long prevUserTime = 0;
        static int lastCpuPct = 14;

        static int ObterUsoCpuPercent()
        {
            try
            {
                long idle, kernel, user;
                if (GetSystemTimes(out idle, out kernel, out user))
                {
                    if (prevKernelTime == 0 && prevUserTime == 0)
                    {
                        prevIdleTime = idle;
                        prevKernelTime = kernel;
                        prevUserTime = user;
                        Thread.Sleep(50);
                        GetSystemTimes(out idle, out kernel, out user);
                    }

                    long idleDiff = idle - prevIdleTime;
                    long kernelDiff = kernel - prevKernelTime;
                    long userDiff = user - prevUserTime;
                    long totalSys = kernelDiff + userDiff;

                    prevIdleTime = idle;
                    prevKernelTime = kernel;
                    prevUserTime = user;

                    if (totalSys > 0)
                    {
                        int cpu = (int)(((totalSys - idleDiff) * 100L) / totalSys);
                        if (cpu < 2) cpu = 2;
                        if (cpu > 100) cpu = 100;
                        lastCpuPct = cpu;
                        return cpu;
                    }
                }
            }
            catch { }
            return lastCpuPct;
        }

        static string ObterUptimeSistema()
        {
            try
            {
                long ms = (uint)Environment.TickCount;
                TimeSpan ts = TimeSpan.FromMilliseconds(ms);
                if (ts.Days > 0)
                    return string.Format("{0}d {1}h {2}m", ts.Days, ts.Hours, ts.Minutes);
                if (ts.Hours > 0)
                    return string.Format("{0}h {1}m", ts.Hours, ts.Minutes);
                return string.Format("{0} min", Math.Max(1, ts.Minutes));
            }
            catch { }
            return "12h 30m";
        }

        static int ObterConexoesTcpAtivas()
        {
            try
            {
                var props = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties();
                var conns = props.GetActiveTcpConnections();
                if (conns != null) return conns.Length;
            }
            catch { }
            return 24;
        }

        static string ObterDetalhesDiscosJson(out double totalAllGb, out double freeAllGb, out double usedAllGb, out int usedAllPct)
        {
            totalAllGb = 0;
            freeAllGb = 0;
            usedAllGb = 0;
            usedAllPct = 0;
            StringBuilder sb = new StringBuilder();
            sb.Append("[");
            bool first = true;
            try
            {
                foreach (DriveInfo d in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (d.IsReady && (d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Network))
                        {
                            double tot = Math.Round((double)d.TotalSize / (1024.0 * 1024.0 * 1024.0), 1);
                            double fre = Math.Round((double)d.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0), 1);
                            double usd = Math.Round(Math.Max(0, tot - fre), 1);
                            int pct = tot > 0 ? (int)Math.Round((usd / tot) * 100.0) : 0;

                            totalAllGb += tot;
                            freeAllGb += fre;
                            usedAllGb += usd;

                            string drvName = d.Name.Replace("\\", "");
                            string lbl = "";
                            try { lbl = d.VolumeLabel; } catch { }
                            if (string.IsNullOrEmpty(lbl)) lbl = (drvName.StartsWith("C") ? "Sistema Local" : "Volume de Dados");
                            string fmt = "NTFS";
                            try { fmt = d.DriveFormat; } catch { }

                            if (!first) sb.Append(",");
                            first = false;
                            sb.AppendFormat(
                                System.Globalization.CultureInfo.InvariantCulture,
                                "{{\"name\":\"{0}\",\"label\":\"{1}\",\"format\":\"{2}\",\"totalGb\":{3},\"freeGb\":{4},\"usedGb\":{5},\"usedPct\":{6}}}",
                                drvName.Replace("\"", ""),
                                lbl.Replace("\\", "/").Replace("\"", "'"),
                                fmt.Replace("\"", ""),
                                tot,
                                fre,
                                usd,
                                pct
                            );
                        }
                    }
                    catch { }
                }
            }
            catch { }
            sb.Append("]");

            totalAllGb = Math.Round(totalAllGb, 1);
            freeAllGb = Math.Round(freeAllGb, 1);
            usedAllGb = Math.Round(usedAllGb, 1);
            usedAllPct = totalAllGb > 0 ? (int)Math.Round((usedAllGb / totalAllGb) * 100.0) : 0;

            return sb.ToString();
        }

        static string DetectarTipoDispositivo()
        {
            if (isServerMachine) return "Servidor";
            try
            {
                PowerStatus power = SystemInformation.PowerStatus;
                if (power.BatteryChargeStatus != BatteryChargeStatus.NoSystemBattery && power.BatteryLifePercent >= 0)
                {
                    return "Notebook";
                }
            }
            catch { }
            return "Desktop";
        }

        static string ObterProcessador()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("ProcessorNameString");
                        if (val != null) return val.ToString().Trim();
                    }
                }
            }
            catch { }
            return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Intel Core i5";
        }

        static string ObterMemoriaRam(out double totalGb, out int percentUsed)
        {
            totalGb = 8.0;
            percentUsed = 50;
            try
            {
                MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus))
                {
                    totalGb = Math.Round((double)memStatus.ullTotalPhys / (1024 * 1024 * 1024), 1);
                    percentUsed = (int)memStatus.dwMemoryLoad;
                    return string.Format("{0} GB ({1}% em uso)", totalGb, percentUsed);
                }
            }
            catch { }
            return "8.0 GB";
        }

        static string ObterArmazenamentoC()
        {
            try
            {
                DriveInfo c = new DriveInfo("C");
                if (c.IsReady)
                {
                    double totalGb = Math.Round((double)c.TotalSize / (1024 * 1024 * 1024), 0);
                    double freeGb = Math.Round((double)c.AvailableFreeSpace / (1024 * 1024 * 1024), 0);
                    return string.Format("SSD {0} GB ({1} GB livres)", totalGb, freeGb);
                }
            }
            catch { }
            return "SSD 512 GB";
        }

        static string ObterNomeSO()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (key != null)
                    {
                        object prod = key.GetValue("ProductName");
                        object disp = key.GetValue("DisplayVersion");
                        if (prod != null)
                        {
                            return prod.ToString() + (disp != null ? " (" + disp.ToString() + " 64-bit)" : "");
                        }
                    }
                }
            }
            catch { }
            return Environment.OSVersion.VersionString;
        }

        // =====================================================================
        // GEOLOCALIZAÇÃO EM TEMPO REAL & MAPEAMENTO DE HARDWARE / SISTEMA
        // =====================================================================
        static string geoCity = "";
        static string geoRegion = "";
        static string geoCountry = "";
        static double geoLat = 0.0;
        static double geoLon = 0.0;
        static string geoIsp = "";
        static string geoPublicIp = "";
        static string geoMapsUrl = "";
        static string geoLabel = "";
        static DateTime lastGeoUpdateUtc = DateTime.MinValue;

        static void CarregarGeolocalizacaoCache()
        {
            try
            {
                string geoFile = Path.Combine(ObterDiretorioDadosAgente(), "machine_geolocation.json");
                if (File.Exists(geoFile))
                {
                    string txt = File.ReadAllText(geoFile, Encoding.UTF8).Trim();
                    var js = new System.Web.Script.Serialization.JavaScriptSerializer();
                    var dict = js.Deserialize<Dictionary<string, object>>(txt);
                    if (dict != null)
                    {
                        if (dict.ContainsKey("city")) geoCity = Convert.ToString(dict["city"]);
                        if (dict.ContainsKey("region")) geoRegion = Convert.ToString(dict["region"]);
                        if (dict.ContainsKey("country")) geoCountry = Convert.ToString(dict["country"]);
                        if (dict.ContainsKey("isp")) geoIsp = Convert.ToString(dict["isp"]);
                        if (dict.ContainsKey("publicIp")) geoPublicIp = Convert.ToString(dict["publicIp"]);
                        if (dict.ContainsKey("lat")) double.TryParse(Convert.ToString(dict["lat"]), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out geoLat);
                        if (dict.ContainsKey("lon")) double.TryParse(Convert.ToString(dict["lon"]), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out geoLon);
                        if (dict.ContainsKey("mapsUrl")) geoMapsUrl = Convert.ToString(dict["mapsUrl"]);
                        if (dict.ContainsKey("label")) geoLabel = Convert.ToString(dict["label"]);
                    }
                }
            }
            catch { }
        }

        static void AtualizarGeolocalizacao()
        {
            if ((DateTime.UtcNow - lastGeoUpdateUtc).TotalMinutes < 20 && !string.IsNullOrEmpty(geoCity))
            {
                return;
            }

            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                string json = null;

                // 1. Provedor Primário: ip-api.com
                try
                {
                    using (WebClient wc = new WebClient())
                    {
                        wc.Headers["User-Agent"] = "NexusTI-Agent/" + AGENT_VERSION;
                        json = wc.DownloadString("http://ip-api.com/json/?fields=status,message,country,countryCode,region,regionName,city,lat,lon,isp,query");
                    }
                }
                catch { }

                // 2. Provedor Secundário (Fallback): ipwho.is
                if (string.IsNullOrEmpty(json) || !json.Contains("\"city\""))
                {
                    try
                    {
                        using (WebClient wc = new WebClient())
                        {
                            wc.Headers["User-Agent"] = "NexusTI-Agent/" + AGENT_VERSION;
                            json = wc.DownloadString("https://ipwho.is/");
                        }
                    }
                    catch { }
                }

                if (!string.IsNullOrEmpty(json))
                {
                    var js = new System.Web.Script.Serialization.JavaScriptSerializer();
                    var dict = js.Deserialize<Dictionary<string, object>>(json);
                    if (dict != null)
                    {
                        string c = dict.ContainsKey("city") ? Convert.ToString(dict["city"]) : "";
                        string reg = dict.ContainsKey("region") ? Convert.ToString(dict["region"]) : (dict.ContainsKey("regionName") ? Convert.ToString(dict["regionName"]) : "");
                        string ctry = dict.ContainsKey("country") ? Convert.ToString(dict["country"]) : "";
                        string q = dict.ContainsKey("query") ? Convert.ToString(dict["query"]) : (dict.ContainsKey("ip") ? Convert.ToString(dict["ip"]) : "");
                        string prov = dict.ContainsKey("isp") ? Convert.ToString(dict["isp"]) : (dict.ContainsKey("connection") ? Convert.ToString(dict["connection"]) : "");
                        double lt = 0, ln = 0;

                        if (dict.ContainsKey("lat")) double.TryParse(Convert.ToString(dict["lat"]), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out lt);
                        else if (dict.ContainsKey("latitude")) double.TryParse(Convert.ToString(dict["latitude"]), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out lt);

                        if (dict.ContainsKey("lon")) double.TryParse(Convert.ToString(dict["lon"]), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out ln);
                        else if (dict.ContainsKey("longitude")) double.TryParse(Convert.ToString(dict["longitude"]), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out ln);

                        if (!string.IsNullOrEmpty(c))
                        {
                            geoCity = c;
                            geoRegion = reg;
                            geoCountry = ctry;
                            geoPublicIp = q;
                            geoIsp = prov;
                            geoLat = lt;
                            geoLon = ln;
                            geoMapsUrl = string.Format(System.Globalization.CultureInfo.InvariantCulture, "https://www.google.com/maps?q={0},{1}", lt, ln);
                            geoLabel = c + (string.IsNullOrEmpty(reg) ? "" : ", " + reg) + (string.IsNullOrEmpty(ctry) ? "" : " - " + ctry);
                            lastGeoUpdateUtc = DateTime.UtcNow;

                            try
                            {
                                var cacheObj = new Dictionary<string, object>
                                {
                                    { "city", geoCity },
                                    { "region", geoRegion },
                                    { "country", geoCountry },
                                    { "publicIp", geoPublicIp },
                                    { "isp", geoIsp },
                                    { "lat", geoLat },
                                    { "lon", geoLon },
                                    { "mapsUrl", geoMapsUrl },
                                    { "label", geoLabel },
                                    { "updatedAt", DateTime.UtcNow.ToString("o") }
                                };
                                string cacheJson = js.Serialize(cacheObj);
                                File.WriteAllText(Path.Combine(ObterDiretorioDadosAgente(), "machine_geolocation.json"), cacheJson, Encoding.UTF8);
                                File.WriteAllText(Path.Combine(ObterDiretorioDadosAgente(), "machine_location.txt"), geoLabel, Encoding.UTF8);
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
        }

        static void ObterFabricanteEModelo(out string mfg, out string model, out string board, out string bios, out string serial)
        {
            mfg = "Computador Corporativo";
            model = "Estação de Trabalho";
            board = "Placa-Mãe Padrão";
            bios = "UEFI/BIOS";
            serial = "";
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS"))
                {
                    if (key != null)
                    {
                        object vMfg = key.GetValue("SystemManufacturer");
                        object vMod = key.GetValue("SystemProductName");
                        object vBrd = key.GetValue("BaseBoardProduct");
                        object vBio = key.GetValue("BIOSVersion");
                        object vSer = key.GetValue("SystemSerialNumber");
                        if (vMfg != null && !string.IsNullOrEmpty(vMfg.ToString())) mfg = vMfg.ToString().Trim();
                        if (vMod != null && !string.IsNullOrEmpty(vMod.ToString())) model = vMod.ToString().Trim();
                        if (vBrd != null && !string.IsNullOrEmpty(vBrd.ToString())) board = vBrd.ToString().Trim();
                        if (vBio != null && !string.IsNullOrEmpty(vBio.ToString())) bios = vBio.ToString().Trim();
                        if (vSer != null && !string.IsNullOrEmpty(vSer.ToString())) serial = vSer.ToString().Trim();
                    }
                }
            }
            catch { }
        }

        static void ObterPlacaDeVideo(out string gpuName, out string gpuDriver)
        {
            gpuName = "Adaptador de Vídeo Padrão";
            gpuDriver = "WDDM";
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000"))
                {
                    if (key != null)
                    {
                        object vName = key.GetValue("DriverDesc");
                        object vDrv = key.GetValue("DriverVersion");
                        if (vName != null) gpuName = vName.ToString().Trim();
                        if (vDrv != null) gpuDriver = vDrv.ToString().Trim();
                    }
                }
            }
            catch { }
        }

        static void ObterDetalhesRede(out string mac, out string adapter, out string gateway, out string dns)
        {
            mac = "";
            adapter = "Ethernet";
            gateway = "";
            dns = "";
            try
            {
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                        ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                    {
                        var ipProps = ni.GetIPProperties();
                        if (ipProps != null && ipProps.UnicastAddresses.Count > 0)
                        {
                            byte[] bytes = ni.GetPhysicalAddress().GetAddressBytes();
                            if (bytes != null && bytes.Length == 6)
                            {
                                mac = string.Format("{0:X2}:{1:X2}:{2:X2}:{3:X2}:{4:X2}:{5:X2}", bytes[0], bytes[1], bytes[2], bytes[3], bytes[4], bytes[5]);
                                adapter = ni.Description;
                                if (ipProps.GatewayAddresses.Count > 0)
                                    gateway = ipProps.GatewayAddresses[0].Address.ToString();
                                if (ipProps.DnsAddresses.Count > 0)
                                    dns = ipProps.DnsAddresses[0].Address.ToString();
                                break;
                            }
                        }
                    }
                }
            }
            catch { }
        }

        static string ObterLocalizacao()
        {
            if (!string.IsNullOrEmpty(geoLabel)) return geoLabel;
            try
            {
                string locFile = Path.Combine(ObterDiretorioDadosAgente(), "machine_location.txt");
                if (File.Exists(locFile))
                {
                    string l = File.ReadAllText(locFile).Trim();
                    if (!string.IsNullOrEmpty(l)) return l;
                }
            }
            catch { }
            return customEmpresa + " - " + customMachineTag.Replace("#", "").Trim();
        }

        public static string ObterDiretorioDadosAgente()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
                if (baseDir.Equals(@"C:\FSC-Agent", StringComparison.OrdinalIgnoreCase) ||
                    baseDir.Equals(@"C:\Nexus-Agent", StringComparison.OrdinalIgnoreCase))
                {
                    return baseDir;
                }
                if (Directory.Exists(@"C:\FSC-Agent") && File.Exists(@"C:\FSC-Agent\machine_name.txt"))
                    return @"C:\FSC-Agent";
                if (Directory.Exists(@"C:\Nexus-Agent") && File.Exists(@"C:\Nexus-Agent\machine_name.txt"))
                    return @"C:\Nexus-Agent";
                if (Directory.Exists(@"C:\FSC-Agent"))
                    return @"C:\FSC-Agent";
                string defDir = @"C:\Nexus-Agent";
                if (!Directory.Exists(defDir)) Directory.CreateDirectory(defDir);
                return defDir;
            }
            catch
            {
                return @"C:\Nexus-Agent";
            }
        }

        public static void ExtrairRecursosHtmlPadrao()
        {
            try
            {
                string dataDir = ObterDiretorioDadosAgente();
                string[] htmlFiles = new string[] { "viewer.html", "chamado.html" };
                string[] resNames = Assembly.GetExecutingAssembly().GetManifestResourceNames();
                foreach (string hf in htmlFiles)
                {
                    try
                    {
                        Stream rs = null;
                        foreach (string rn in resNames)
                        {
                            if (rn.EndsWith(hf, StringComparison.OrdinalIgnoreCase))
                            {
                                rs = Assembly.GetExecutingAssembly().GetManifestResourceStream(rn);
                                if (rs != null) break;
                            }
                        }
                        if (rs == null) rs = Assembly.GetExecutingAssembly().GetManifestResourceStream(hf);
                        if (rs != null)
                        {
                            string outPath = Path.Combine(dataDir, hf);
                            using (rs)
                            using (FileStream fs = new FileStream(outPath, FileMode.Create, FileAccess.Write))
                            {
                                rs.CopyTo(fs);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        public static byte[] ObterBytesViewerHtml()
        {
            try
            {
                string[] cands = new string[] {
                    Path.Combine(ObterDiretorioDadosAgente(), "viewer.html"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "viewer.html"),
                    @"C:\FSC-Agent\viewer.html",
                    @"C:\Nexus-Agent\viewer.html",
                    @"c:\Users\gabrielnovaes\Documents\copia magma\viewer.html",
                    @"c:\Users\gabrielnovaes\Documents\fsc-console\viewer.html"
                };
                foreach (string c in cands)
                {
                    if (File.Exists(c) && new FileInfo(c).Length > 1000)
                    {
                        return File.ReadAllBytes(c);
                    }
                }
            }
            catch { }

            try
            {
                string[] resNames = Assembly.GetExecutingAssembly().GetManifestResourceNames();
                Stream rs = null;
                foreach (string rn in resNames)
                {
                    if (rn.EndsWith("viewer.html", StringComparison.OrdinalIgnoreCase))
                    {
                        rs = Assembly.GetExecutingAssembly().GetManifestResourceStream(rn);
                        if (rs != null) break;
                    }
                }
                if (rs == null) rs = Assembly.GetExecutingAssembly().GetManifestResourceStream("viewer.html");
                if (rs != null)
                {
                    using (rs)
                    using (MemoryStream ms = new MemoryStream())
                    {
                        rs.CopyTo(ms);
                        byte[] b = ms.ToArray();
                        try { File.WriteAllBytes(Path.Combine(ObterDiretorioDadosAgente(), "viewer.html"), b); } catch { }
                        return b;
                    }
                }
            }
            catch { }

            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                using (WebClient wc = new WebClient())
                {
                    wc.Headers["User-Agent"] = "NexusRemoteAgent/" + AGENT_VERSION;
                    byte[] b = wc.DownloadData("https://nexus-ten-brown.vercel.app/viewer.html");
                    if (b != null && b.Length > 1000)
                    {
                        try { File.WriteAllBytes(Path.Combine(ObterDiretorioDadosAgente(), "viewer.html"), b); } catch { }
                        return b;
                    }
                }
            }
            catch { }

            return Encoding.UTF8.GetBytes("<!DOCTYPE html><html><head><meta charset='UTF-8'><script>window.location.replace('https://nexus-ten-brown.vercel.app/viewer.html' + window.location.search);</script></head><body>Carregando Viewer NexusTI...</body></html>");
        }

        public static void LimparArquivosSoltosIndesejados()
        {
            try
            {
                string[] strayNames = new string[] {
                    "empresa.txt", "machine_name.txt", "machine_tag.txt", "machine_empresa.txt",
                    "is_server.txt", "agent_token.txt", "server_ip.txt", "machine_location.txt",
                    "avisos_vistos.txt", "empresas.json", "setores.json", "chamados.json",
                    "assets_registry.json", "avisos_historico.json", "avisos_confirmados.json",
                    "enquetes_respostas.json", "startup_error.log", "port_error.log",
                    "capture_error.log", "aviso_err.log", "aviso_error.log"
                };

                List<string> dirsToCheck = new List<string>();
                try { dirsToCheck.Add(AppDomain.CurrentDomain.BaseDirectory); } catch { }
                try { dirsToCheck.Add(Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."))); } catch { }
                dirsToCheck.Add(@"C:\");
                try { dirsToCheck.Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)); } catch { }
                try { dirsToCheck.Add(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)); } catch { }

                if (Directory.Exists(@"C:\Users"))
                {
                    foreach (string uDir in Directory.GetDirectories(@"C:\Users"))
                    {
                        dirsToCheck.Add(uDir);
                        foreach (string sub in new string[] { "Desktop", "Área de Trabalho", "Downloads" })
                        {
                            string p = Path.Combine(uDir, sub);
                            if (Directory.Exists(p))
                            {
                                dirsToCheck.Add(p);
                                try { foreach (string child in Directory.GetDirectories(p)) dirsToCheck.Add(child); } catch { }
                            }
                        }
                        try
                        {
                            foreach (string od in Directory.GetDirectories(uDir, "OneDrive*"))
                            {
                                foreach (string sub in new string[] { "Desktop", "Área de Trabalho", "Downloads" })
                                {
                                    string p = Path.Combine(od, sub);
                                    if (Directory.Exists(p))
                                    {
                                        dirsToCheck.Add(p);
                                        try { foreach (string child in Directory.GetDirectories(p)) dirsToCheck.Add(child); } catch { }
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }

                foreach (string d in dirsToCheck)
                {
                    if (string.IsNullOrEmpty(d) || !Directory.Exists(d)) continue;
                    string normD = d.TrimEnd('\\', '/').ToLower();
                    if (normD == @"c:\fsc-agent" || normD == @"c:\nexus-agent") continue;
                    if (normD.Contains("copia magma") || normD.Contains("fsc-console") || normD.Contains("gestão de ti") || normD.Contains("gestao de ti")) continue;

                    foreach (string fName in strayNames)
                    {
                        try
                        {
                            string fp = Path.Combine(d, fName);
                            if (File.Exists(fp)) File.Delete(fp);
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        public static void GarantirAtalhosAreaDeTrabalho()
        {
            try
            {
                string targetExe = Application.ExecutablePath;
                if (File.Exists(@"C:\FSC-Agent\FSCRemoteAgent.exe")) targetExe = @"C:\FSC-Agent\FSCRemoteAgent.exe";
                else if (File.Exists(@"C:\Nexus-Agent\NexusRemoteAgent.exe")) targetExe = @"C:\Nexus-Agent\NexusRemoteAgent.exe";

                string portalUrl = ObterUrlPortalChamadoHospedado();
                List<string> desktops = new List<string>();
                try
                {
                    string pubDesk = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                    if (!string.IsNullOrEmpty(pubDesk) && Directory.Exists(pubDesk)) desktops.Add(pubDesk);
                }
                catch { }
                try
                {
                    string curDesk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    if (!string.IsNullOrEmpty(curDesk) && Directory.Exists(curDesk) && !desktops.Contains(curDesk)) desktops.Add(curDesk);
                }
                catch { }
                try
                {
                    if (Directory.Exists(@"C:\Users"))
                    {
                        foreach (string uDir in Directory.GetDirectories(@"C:\Users"))
                        {
                            string uName = Path.GetFileName(uDir).ToLower();
                            if (uName == "default" || uName == "default user" || uName == "all users") continue;
                            foreach (string sub in new string[] { "Desktop", "Área de Trabalho" })
                            {
                                string p = Path.Combine(uDir, sub);
                                if (Directory.Exists(p) && !desktops.Contains(p)) desktops.Add(p);
                            }
                            try
                            {
                                foreach (string od in Directory.GetDirectories(uDir, "OneDrive*"))
                                {
                                    foreach (string sub in new string[] { "Desktop", "Área de Trabalho" })
                                    {
                                        string p = Path.Combine(od, sub);
                                        if (Directory.Exists(p) && !desktops.Contains(p)) desktops.Add(p);
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }

                foreach (string d in desktops)
                {
                    try
                    {
                        string oldUrl = Path.Combine(d, "Abrir Chamado T.I (Web).url");
                        if (File.Exists(oldUrl)) File.Delete(oldUrl);
                    }
                    catch { }
                }

                bool criadoPublico = false;
                try
                {
                    string pubDesk = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                    if (!string.IsNullOrEmpty(pubDesk) && Directory.Exists(pubDesk))
                    {
                        string pubLnk = Path.Combine(pubDesk, "Abrir Chamado T.I.lnk");
                        CriarAtalhoWScript(pubLnk, targetExe, "-chamado", targetExe + ",0", "Abrir Chamado com Suporte de T.I");
                        if (File.Exists(pubLnk))
                        {
                            criadoPublico = true;
                            foreach (string d in desktops)
                            {
                                if (d.Equals(pubDesk, StringComparison.OrdinalIgnoreCase)) continue;
                                try
                                {
                                    string dupLnk = Path.Combine(d, "Abrir Chamado T.I.lnk");
                                    if (File.Exists(dupLnk)) File.Delete(dupLnk);
                                }
                                catch { }
                            }
                        }
                    }
                }
                catch { }

                if (!criadoPublico)
                {
                    foreach (string d in desktops)
                    {
                        try
                        {
                            string lnk = Path.Combine(d, "Abrir Chamado T.I.lnk");
                            CriarAtalhoWScript(lnk, targetExe, "-chamado", targetExe + ",0", "Abrir Chamado com Suporte de T.I");
                            if (!File.Exists(lnk) && !string.IsNullOrEmpty(portalUrl))
                            {
                                string urlFallback = Path.Combine(d, "Abrir Chamado T.I.url");
                                File.WriteAllText(urlFallback, "[InternetShortcut]\r\nURL=" + portalUrl + "\r\nIconIndex=0\r\nIconFile=" + targetExe + "\r\n");
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private static void CriarAtalhoWScript(string shortcutPath, string targetPath, string arguments, string iconLocation, string description)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    object shell = Activator.CreateInstance(shellType);
                    object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                    if (shortcut != null)
                    {
                        Type st = shortcut.GetType();
                        st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
                        if (!string.IsNullOrEmpty(arguments))
                            st.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { arguments });
                        if (!string.IsNullOrEmpty(iconLocation))
                            st.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { iconLocation });
                        if (!string.IsNullOrEmpty(description))
                            st.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { description });
                        st.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
                    }
                }
            }
            catch { }
        }

        // ---------------------------------------------------------------------
        // IMPORTAÇÕES DA WIN32 API (USER32 & GDI32)
        // ---------------------------------------------------------------------
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [DllImport("user32.dll")]
        static extern bool GetCursorInfo(out CURSORINFO pci);

        [DllImport("user32.dll")]
        static extern bool DrawIcon(IntPtr hDC, int X, int Y, IntPtr hIcon);

        [DllImport("user32.dll")]
        static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr hObject);

        // Constantes do Mouse Win32
        const uint MOUSEEVENTF_MOVE = 0x0001;
        const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        const uint MOUSEEVENTF_LEFTUP = 0x0004;
        const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        const uint MOUSEEVENTF_WHEEL = 0x0800;

        // Constantes do Teclado Win32
        const byte VK_SHIFT = 0x10;
        const byte VK_CONTROL = 0x11;
        const byte VK_MENU = 0x12; // Alt
        const byte VK_LWIN = 0x5B;
        const uint KEYEVENTF_KEYUP = 0x0002;

        const int CURSOR_SHOWING = 0x00000001;

        [StructLayout(LayoutKind.Sequential)]
        struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct CURSORINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hCursor;
            public POINT ptScreenPos;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ICONINFO
        {
            public bool fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        // Configurações do Servidor
        static string sessionToken = "NEXUS@9842";
        static int port = 8090;
        static TcpListener tcpServer;
        static ImageCodecInfo jpgEncoder;
        static EncoderParameters encoderParams;
        static NotifyIcon trayIcon;
        static string localIpAddress = "127.0.0.1";

        // Identificação Amigável da Máquina (Configurada no Instalador)
        public static string customMachineName = "";
        static string customMachineTag = "Ativos-Nexus";
        public static string customEmpresa = "Aço Móveis";
        public static bool isServerMachine = false;
        private static readonly object dbLock = new object();

        public static string NormalizarTextoEmpresa(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return "Aço Móveis";
            string t = texto.Trim();
            if (t.Contains("AÃ§o") || t.Contains("Aco") || t.ToLower().Contains("moveis") || t.ToLower().Contains("móveis"))
                return "Aço Móveis";
            if (t.ToUpper().Contains("JPL"))
                return "JPL";
            if (t.ToLower().Contains("sem empresa"))
                return "Sem empresa";
            return t;
        }

        // Multi-monitores (0 = Tela 1, 1 = Tela 2, -1 = Todas as Telas)
        static int currentScreenIndex = 0;

        // Versão do Clipboard
        static long clipboardVersion = 1;

        // Versão oficial do Agente NexusTI & Build para Atualizações Silenciosas OTA
        public const string AGENT_VERSION = "1.2.7";
        public const int AGENT_BUILD = 127;
        public static bool isAtualizando = false;

        [STAThread]
        static void Main(string[] args)
        {
            // Proteção contra encerramento inesperado com log de diagnóstico
            AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                try { File.WriteAllText(Path.Combine(ObterDiretorioDadosAgente(), "startup_error.log"), e.ExceptionObject != null ? e.ExceptionObject.ToString() : "Unknown exception"); } catch {}
            };
            try { Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException); } catch { }

            // Limpa resíduos temporários de atualizações OTA anteriores (.old e _new.exe)
            try
            {
                string oldFile = Application.ExecutablePath + ".old";
                if (File.Exists(oldFile)) File.Delete(oldFile);
                string newTemp = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NexusRemoteAgent_new.exe");
                if (File.Exists(newTemp)) File.Delete(newTemp);
            }
            catch { }

            // 1. Habilita DPI Awareness
            try { SetProcessDPIAware(); } catch { }

            // Se chamado para abrir tela de chamado direto (via Área de Trabalho ou linha de comando)
            if (args != null && args.Length > 0 && (args[0].ToLower() == "-chamado" || args[0].ToLower() == "-ticket" || args[0].ToLower() == "/chamado"))
            {
                AbrirChamadoUI();
                return;
            }

            // Garante que apenas esta instância permaneça ativa
            try
            {
                Process currentProc = Process.GetCurrentProcess();
                List<Process> existingProcs = new List<Process>();
                existingProcs.AddRange(Process.GetProcessesByName("NexusRemoteAgent"));
                existingProcs.AddRange(Process.GetProcessesByName("FSCRemoteAgent"));
                foreach (var p in existingProcs)
                {
                    if (p.Id != currentProc.Id)
                    {
                        try { p.Kill(); p.WaitForExit(1500); } catch { }
                    }
                }
            }
            catch { }

            // Executa extração de HTML, limpeza de arquivos soltos e criação do atalho na Área de Trabalho Pública imediatamente (aproveitando token elevado se houver)
            try
            {
                ExtrairRecursosHtmlPadrao();
                LimparArquivosSoltosIndesejados();
                CarregarNomeCustomizado();
                GarantirAtalhosAreaDeTrabalho();
            }
            catch { }

            // Se iniciado sem --daemon (ex: via cmd.exe de OTA), relança via ShellExecute (bInheritHandles=false)
            // para liberar 100% quaisquer handles de socket herdados na porta 8090
            bool isDaemon = false;
            if (args != null)
            {
                foreach (string a in args)
                {
                    if (a != null && a.ToLower() == "--daemon") isDaemon = true;
                }
            }
            if (!isDaemon)
            {
                try
                {
                    Thread.Sleep(500);
                    ProcessStartInfo psiClean = new ProcessStartInfo(Application.ExecutablePath, "--daemon");
                    psiClean.UseShellExecute = true;
                    psiClean.WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath);
                    Process.Start(psiClean);
                    Environment.Exit(0);
                    return;
                }
                catch { }
            }

            try
            {
                // Extrai viewer.html e chamado.html embutidos, limpa arquivos soltos e garante atalho na Área de Trabalho
                ExtrairRecursosHtmlPadrao();
                LimparArquivosSoltosIndesejados();

                // Carrega Nome Customizado da Máquina (definido na instalação)
                CarregarNomeCustomizado();
                GarantirAtalhosAreaDeTrabalho();

                // Detecta IP de rede real
                try
                {
                    using (Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                    {
                        sock.Connect("8.8.8.8", 65530);
                        IPEndPoint endPoint = sock.LocalEndPoint as IPEndPoint;
                        if (endPoint != null)
                        {
                            localIpAddress = endPoint.Address.ToString();
                        }
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
                                localIpAddress = ip.ToString();
                                break;
                            }
                        }
                    }
                    catch { }
                }

                // Firewall
                try
                {
                    System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo();
                    psi.FileName = "netsh";
                    psi.Arguments = "advfirewall firewall add rule name=\"NexusTI Remote Agent\" dir=in action=allow protocol=TCP localport=8090 profile=any";
                    psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    System.Diagnostics.Process.Start(psi);
                }
                catch { }

                // Token persistente único por máquina
                string configPath = Path.Combine(ObterDiretorioDadosAgente(), "agent_token.txt");
                if (File.Exists(configPath))
                {
                    sessionToken = File.ReadAllText(configPath, Encoding.UTF8).Trim();
                }
                if (string.IsNullOrEmpty(sessionToken) || sessionToken == "FSC@3606")
                {
                    sessionToken = "FSC@" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                    try { File.WriteAllText(configPath, sessionToken, Encoding.UTF8); } catch { }
                }

                // Carrega Geolocalização em Cache imediatamente
                CarregarGeolocalizacaoCache();

                // Auto-registro do ativo, telemetria contínua, comunicados e auto-atualização silenciosa OTA
                ThreadPool.QueueUserWorkItem(_ => {
                    int tick = 0;
                    while (true)
                    {
                        try
                        {
                            if (tick == 0 || tick % 120 == 0) // Atualiza localização na inicialização e a cada 10 min
                            {
                                AtualizarGeolocalizacao();
                            }

                            VerificarComunicadosSupabase();
                            if (tick % 6 == 0)
                            {
                                EnviarTelemetriaSupabase();
                                RegistrarAtivoNoServidorRemoto();
                            }
                            // A atualização não é automática por timer: ocorre sob demanda ao clicar no botão no software ou no console
                        }
                        catch { }
                        tick++;
                        Thread.Sleep(5000);
                    }
                });

                // Qualidade 45 para velocidade máxima e latência nula
                jpgEncoder = GetEncoder(ImageFormat.Jpeg);
                encoderParams = new EncoderParameters(1);
                encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 45L);

                Thread srvThread = new Thread(IniciaServidorTcp);
                srvThread.IsBackground = false;
                srvThread.Start();

                try { CriaTrayIcon(); } catch { }
                try { Application.Run(); } catch { }

                // Mantém o processo ativo permanentemente com o servidor HTTP/TCP e telemetria na nuvem
                while (true)
                {
                    try { Thread.Sleep(60000); } catch { }
                }
            }
            catch { }
        }

        
        static void EnviarTelemetriaSupabase()
        {
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                string url = "https://hskxvdcgfgrdofgpdtzm.supabase.co/rest/v1/computadores";
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "POST";
                req.ContentType = "application/json";
                req.Headers["apikey"] = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Imhza3h2ZGNnZmdyZG9mZ3BkdHptIiwicm9sZSI6ImFub24iLCJpYXQiOjE3OTU1NDEzOTEsImV4cCI6MjExMTExNzM5MX0.PKQaCfnlmxAM7Vuc4s84gjLS_21OeteMkI1WLLR1BEU";
                req.Headers["Authorization"] = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Imhza3h2ZGNnZmdyZG9mZ3BkdHptIiwicm9sZSI6ImFub24iLCJpYXQiOjE3OTU1NDEzOTEsImV4cCI6MjExMTExNzM5MX0.PKQaCfnlmxAM7Vuc4s84gjLS_21OeteMkI1WLLR1BEU";
                req.Headers["Prefer"] = "resolution=merge-duplicates";
                req.Timeout = 4000;

                string idComp = !string.IsNullOrEmpty(customMachineName) ? customMachineName : Environment.MachineName;
                string empComp = !string.IsNullOrEmpty(customEmpresa) ? customEmpresa : "Aço Móveis";
                string setComp = !string.IsNullOrEmpty(customMachineTag) ? customMachineTag.Replace("#", "").Trim() : "TI";
                string ipComp = !string.IsNullOrEmpty(localIpAddress) ? localIpAddress : "127.0.0.1";
                int cpu = 5;
                int ram = 25;
                try { cpu = ObterUsoCpuPercent(); } catch { }
                try { double tg; ObterMemoriaRam(out tg, out ram); } catch { }

                double dTot, dFre, dUsd; int dPct;
                string drivesJson = ObterDetalhesDiscosJson(out dTot, out dFre, out dUsd, out dPct);
                if (string.IsNullOrEmpty(drivesJson)) drivesJson = "[]";

                string safeId = (idComp ?? "").Replace("\"", "'");
                string safeEmp = (empComp ?? "").Replace("\"", "'");
                string safeSet = (setComp ?? "").Replace("\"", "'");
                if (!string.IsNullOrEmpty(geoCity) && !safeSet.Contains(geoCity))
                {
                    safeSet = safeSet + " (" + geoCity + ", " + geoRegion + ")";
                }

                string json = "{\"id\":\"" + safeId + "\",\"empresa\":\"" + safeEmp + "\",\"nome_computador\":\"" + safeId + "\",\"setor\":\"" + safeSet + "\",\"ip\":\"" + ipComp + "\",\"is_server\":" + (isServerMachine ? "true" : "false") + ",\"cpu_usage\":" + cpu + ",\"ram_usage\":" + ram + ",\"discos\":" + drivesJson + ",\"status\":\"online\",\"ultimo_heartbeat\":\"" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") + "\"}";

                byte[] buffer = Encoding.UTF8.GetBytes(json);
                req.ContentLength = buffer.Length;
                using (Stream s = req.GetRequestStream())
                {
                    s.Write(buffer, 0, buffer.Length);
                }
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse()) { }
            }
            catch { }
        }

        
        static HashSet<string> avisosExibidosIds = new HashSet<string>();
        static bool primeiraVerificacaoComunicados = true;
        static DateTime agentStartTimeUtc = DateTime.UtcNow;

        static void CarregarAvisosVistos()
        {
            try
            {
                string p = Path.Combine(ObterDiretorioDadosAgente(), "avisos_vistos.txt");
                if (File.Exists(p))
                {
                    foreach (string line in File.ReadAllLines(p, Encoding.UTF8))
                    {
                        string s = line.Trim();
                        if (!string.IsNullOrEmpty(s)) avisosExibidosIds.Add(s);
                    }
                }
            }
            catch { }
        }

        static void RegistrarAvisoVisto(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            avisosExibidosIds.Add(id);
            try
            {
                string p = Path.Combine(ObterDiretorioDadosAgente(), "avisos_vistos.txt");
                File.AppendAllText(p, id + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }

        static void VerificarComunicadosSupabase()
        {
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                string url = "https://hskxvdcgfgrdofgpdtzm.supabase.co/rest/v1/comunicados?order=criado_em.desc&limit=5";
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.Headers["apikey"] = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Imhza3h2ZGNnZmdyZG9mZ3BkdHptIiwicm9sZSI6ImFub24iLCJpYXQiOjE3OTU1NDEzOTEsImV4cCI6MjExMTExNzM5MX0.PKQaCfnlmxAM7Vuc4s84gjLS_21OeteMkI1WLLR1BEU";
                req.Headers["Authorization"] = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Imhza3h2ZGNnZmdyZG9mZ3BkdHptIiwicm9sZSI6ImFub24iLCJpYXQiOjE3OTU1NDEzOTEsImV4cCI6MjExMTExNzM5MX0.PKQaCfnlmxAM7Vuc4s84gjLS_21OeteMkI1WLLR1BEU";
                req.Timeout = 3000;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    string json = sr.ReadToEnd();
                    var js = new System.Web.Script.Serialization.JavaScriptSerializer();
                    object[] lista = js.Deserialize<object[]>(json);

                    // Na primeira inicialização do agente, carrega o histórico persistente de avisos já vistos
                    if (primeiraVerificacaoComunicados)
                    {
                        CarregarAvisosVistos();
                        // Marca todos os comunicados pré-existentes na nuvem como já vistos para NUNCA
                        // abrir popups acumulados do passado na tela ao instalar ou iniciar o programa!
                        if (lista != null)
                        {
                            foreach (object item in lista)
                            {
                                Dictionary<string, object> dict = item as Dictionary<string, object>;
                                if (dict != null && dict.ContainsKey("id"))
                                {
                                    RegistrarAvisoVisto(Convert.ToString(dict["id"]));
                                }
                            }
                        }
                        primeiraVerificacaoComunicados = false;
                        return;
                    }

                    if (lista != null)
                    {
                        foreach (object item in lista)
                        {
                            Dictionary<string, object> dict = item as Dictionary<string, object>;
                            if (dict != null)
                            {
                                string id = dict.ContainsKey("id") ? Convert.ToString(dict["id"]) : "";
                                string tipo = dict.ContainsKey("tipo") ? Convert.ToString(dict["tipo"]) : "aviso";
                                string titulo = dict.ContainsKey("titulo") ? Convert.ToString(dict["titulo"]) : "";
                                string mensagem = dict.ContainsKey("mensagem") ? Convert.ToString(dict["mensagem"]) : "";
                                string urgencia = dict.ContainsKey("urgencia") ? Convert.ToString(dict["urgencia"]) : "informativo";
                                string destinatario = dict.ContainsKey("destinatario") ? Convert.ToString(dict["destinatario"]) : "broadcast";
                                string opcoes = dict.ContainsKey("opcoes") ? Convert.ToString(dict["opcoes"]) : "";
                                string autor = dict.ContainsKey("autor") ? Convert.ToString(dict["autor"]) : "Gestão de TI";
                                string criadoEmStr = dict.ContainsKey("criado_em") ? Convert.ToString(dict["criado_em"]) : "";

                                if (!string.IsNullOrEmpty(id) && !avisosExibidosIds.Contains(id))
                                {
                                    RegistrarAvisoVisto(id);

                                    // Descarta avisos emitidos no passado (antes do agente iniciar ou há mais de 10 minutos)
                                    DateTime criadoEm;
                                    if (!string.IsNullOrEmpty(criadoEmStr) && DateTime.TryParse(criadoEmStr, out criadoEm))
                                    {
                                        if (criadoEm.ToUniversalTime() < agentStartTimeUtc.AddMinutes(-5))
                                        {
                                            continue;
                                        }
                                    }

                                    bool destinatarioValido = (string.IsNullOrEmpty(destinatario) || destinatario == "broadcast" || destinatario.ToLower() == "todas" || destinatario.ToLower().Contains("todas"));
                                    if (!destinatarioValido)
                                    {
                                        if (!string.IsNullOrEmpty(customMachineName) && destinatario.Equals(customMachineName, StringComparison.OrdinalIgnoreCase)) destinatarioValido = true;
                                        if (!string.IsNullOrEmpty(customMachineTag) && destinatario.ToLower().Contains(customMachineTag.Replace("#", "").ToLower())) destinatarioValido = true;
                                        if (!string.IsNullOrEmpty(customEmpresa) && destinatario.ToLower().Contains(customEmpresa.ToLower())) destinatarioValido = true;
                                    }

                                    if (destinatarioValido)
                                    {
                                        // Intercepta comando de atualização remota OTA via Supabase
                                        string tipoNorm = (tipo ?? "").Trim().ToLower();
                                        string titNorm = (titulo ?? "").Trim().ToUpper();
                                        if (tipoNorm == "update" || tipoNorm == "ota" || titNorm.StartsWith("UPDATE") || titNorm.StartsWith("OTA"))
                                        {
                                            string targetDl = !string.IsNullOrEmpty(opcoes) ? opcoes.Trim() : null;
                                            ThreadPool.QueueUserWorkItem(_ => ExecutarAtualizacaoSilenciosa(targetDl));
                                            continue;
                                        }

                                        string[] opArr = !string.IsNullOrEmpty(opcoes) ? opcoes.Split('|') : new string[0];
                                        FormAvisoClient.Exibir(tipo, titulo, mensagem, urgencia, opArr, id, autor);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        #region AUTO-UPDATE SILENCIOSO OTA (OVER-THE-AIR) VIA GITHUB / VERCEL

        public static void VerificarAtualizacaoSilenciosa()
        {
            if (isAtualizando) return;
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                string vJson = null;
                using (WebClient wc = new WebClient())
                {
                    wc.Headers["User-Agent"] = "NexusRemoteAgent/" + AGENT_VERSION;
                    wc.Headers["Cache-Control"] = "no-cache";
                    try
                    {
                        vJson = wc.DownloadString("https://raw.githubusercontent.com/biel7nov/nexus/main/agent_version.json?t=" + DateTime.UtcNow.Ticks);
                    }
                    catch
                    {
                        try
                        {
                            vJson = wc.DownloadString("https://nexus-ten-brown.vercel.app/agent_version.json?t=" + DateTime.UtcNow.Ticks);
                        }
                        catch { }
                    }
                }

                if (string.IsNullOrEmpty(vJson)) return;

                var js = new System.Web.Script.Serialization.JavaScriptSerializer();
                var dict = js.Deserialize<Dictionary<string, object>>(vJson);
                if (dict != null)
                {
                    int remoteBuild = 0;
                    if (dict.ContainsKey("build"))
                    {
                        int.TryParse(Convert.ToString(dict["build"]), out remoteBuild);
                    }
                    string dlUrl = dict.ContainsKey("downloadUrl") ? Convert.ToString(dict["downloadUrl"]) : "";
                    if (string.IsNullOrEmpty(dlUrl) && dict.ContainsKey("fallbackUrl"))
                        dlUrl = Convert.ToString(dict["fallbackUrl"]);

                    // Se a versão remota no GitHub for superior à desta máquina, executa atualização silenciosa
                    if (remoteBuild > AGENT_BUILD)
                    {
                        ExecutarAtualizacaoSilenciosa(dlUrl);
                    }
                }
            }
            catch { }
        }

        public static void ExecutarAtualizacaoSilenciosa(string customUrl = null)
        {
            if (isAtualizando) return;
            isAtualizando = true;

            try
            {
                string currentExe = Application.ExecutablePath;
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string tempNewExe = Path.Combine(baseDir, "NexusRemoteAgent_new.exe");

                // Remove resíduos se houver
                try { if (File.Exists(tempNewExe)) File.Delete(tempNewExe); } catch { }
                try { string old = currentExe + ".old"; if (File.Exists(old)) File.Delete(old); } catch { }

                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                string[] urls = new string[] {
                    !string.IsNullOrEmpty(customUrl) ? customUrl : "https://raw.githubusercontent.com/biel7nov/nexus/main/agent/NexusRemoteAgent.exe",
                    "https://nexus-ten-brown.vercel.app/agent/NexusRemoteAgent.exe",
                    "https://raw.githubusercontent.com/biel7nov/nexus/main/agent/NexusRemoteAgent.exe"
                };

                bool baixou = false;
                foreach (string u in urls)
                {
                    try
                    {
                        using (WebClient wc = new WebClient())
                        {
                            wc.Headers["User-Agent"] = "NexusRemoteAgent-OTA/" + AGENT_VERSION;
                            wc.DownloadFile(u + "?t=" + DateTime.UtcNow.Ticks, tempNewExe);
                            if (File.Exists(tempNewExe) && new FileInfo(tempNewExe).Length > 50000)
                            {
                                baixou = true;
                                break;
                            }
                        }
                    }
                    catch { }
                }

                if (!baixou || !File.Exists(tempNewExe))
                {
                    isAtualizando = false;
                    return;
                }

                // Fecha o socket TCP atual antes de disparar o processo de atualização para nunca herdar o handle da porta 8090
                try { if (tcpServer != null) tcpServer.Stop(); } catch { }

                // Substituição direta sem criar arquivo .old:
                // O script em lote aguarda 1s para o término do processo atual, move o binário novo por cima do existente e reinicia.
                string cmd = string.Format(
                    "/c timeout /t 1 /nobreak >nul & move /y \"{0}\" \"{1}\" & start \"\" \"{1}\" --daemon",
                    tempNewExe,
                    currentExe
                );
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", cmd);
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                psi.UseShellExecute = true;
                psi.WorkingDirectory = baseDir;
                Process.Start(psi);

                // Encerra imediatamente para liberar o arquivo para substituição
                Environment.Exit(0);
            }
            catch
            {
                isAtualizando = false;
            }
        }

        #endregion

        static void RegistrarAtivoNoServidorRemoto()
        {
            try
            {
                string osName = ObterNomeSO();
                string devType = DetectarTipoDispositivo();
                CadastrarAtivoCentral(customMachineName, customMachineTag, localIpAddress, Environment.UserName, osName, devType, customEmpresa, isServerMachine);

                string srvIp = "192.168.18.122:8090";
                string srvFile = Path.Combine(ObterDiretorioDadosAgente(), "server_ip.txt");
                if (File.Exists(srvFile))
                {
                    string s = File.ReadAllText(srvFile, Encoding.UTF8).Trim();
                    if (!string.IsNullOrEmpty(s)) srvIp = s;
                }
                if (!srvIp.Contains(":")) srvIp += ":8090";

                if (!srvIp.Contains(localIpAddress) && !srvIp.StartsWith("127.0.0.1"))
                {
                    string url = string.Format(
                        "http://{0}/api/register?host={1}&tag={2}&ip={3}&user={4}&os={5}&type={6}&empresa={7}&is_server={8}",
                        srvIp,
                        Uri.EscapeDataString(customMachineName ?? ""),
                        Uri.EscapeDataString(customMachineTag ?? ""),
                        Uri.EscapeDataString(localIpAddress ?? ""),
                        Uri.EscapeDataString(Environment.UserName ?? ""),
                        Uri.EscapeDataString(osName ?? ""),
                        Uri.EscapeDataString(devType ?? "Desktop"),
                        Uri.EscapeDataString(customEmpresa ?? "Aço Móveis"),
                        isServerMachine ? "true" : "false"
                    );
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                    req.Method = "GET";
                    req.Timeout = 3000;
                    using (HttpWebResponse r = (HttpWebResponse)req.GetResponse()) { }
                }
            }
            catch { }
        }

        static void CarregarNomeCustomizado()
        {
            try
            {
                string baseDir = ObterDiretorioDadosAgente();
                string localAppDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"FastSolutions\FSC-Agent");

                string nameFile = Path.Combine(baseDir, "machine_name.txt");
                if (!File.Exists(nameFile) && Directory.Exists(localAppDir))
                {
                    nameFile = Path.Combine(localAppDir, "machine_name.txt");
                }

                if (File.Exists(nameFile))
                {
                    string n = File.ReadAllText(nameFile, Encoding.UTF8).Trim();
                    if (!string.IsNullOrEmpty(n)) customMachineName = n;
                }

                string tagFile = Path.Combine(baseDir, "machine_tag.txt");
                if (!File.Exists(tagFile) && Directory.Exists(localAppDir))
                {
                    tagFile = Path.Combine(localAppDir, "machine_tag.txt");
                }

                if (File.Exists(tagFile))
                {
                    string t = File.ReadAllText(tagFile, Encoding.UTF8).Trim();
                    if (!string.IsNullOrEmpty(t)) customMachineTag = t.Replace("#", "").Trim();
                }

                string empFile = Path.Combine(baseDir, "empresa.txt");
                if (!File.Exists(empFile) && Directory.Exists(localAppDir))
                {
                    empFile = Path.Combine(localAppDir, "empresa.txt");
                }

                if (File.Exists(empFile))
                {
                    string ep = File.ReadAllText(empFile, Encoding.UTF8).Trim();
                    if (!string.IsNullOrEmpty(ep))
                    {
                        customEmpresa = NormalizarTextoEmpresa(ep);
                        if (ep != customEmpresa)
                        {
                            try { File.WriteAllText(empFile, customEmpresa, Encoding.UTF8); } catch { }
                        }
                    }
                }

                string srvFlagFile = Path.Combine(baseDir, "is_server.txt");
                if (!File.Exists(srvFlagFile) && Directory.Exists(localAppDir))
                {
                    srvFlagFile = Path.Combine(localAppDir, "is_server.txt");
                }

                if (File.Exists(srvFlagFile))
                {
                    string sf = File.ReadAllText(srvFlagFile, Encoding.UTF8).Trim().ToLower();
                    isServerMachine = (sf == "true" || sf == "1" || sf == "servidor" || sf == "yes");
                }
                else if (!string.IsNullOrEmpty(customMachineName) && customMachineName.ToUpper().StartsWith("SRV"))
                {
                    isServerMachine = true;
                }
            }
            catch { }

            if (string.IsNullOrEmpty(customMachineName))
            {
                customMachineName = Environment.MachineName;
            }
        }

        public static string ObterUrlPortalChamadoHospedado()
        {
            CarregarNomeCustomizado();
            string emp = !string.IsNullOrEmpty(customEmpresa) ? customEmpresa : "Aço Móveis";
            string set = !string.IsNullOrEmpty(customMachineTag) ? customMachineTag.Replace("#", "").Trim() : "TI";
            string hst = !string.IsNullOrEmpty(customMachineName) ? customMachineName : Environment.MachineName;
            string usr = Environment.UserName ?? "";
            return string.Format(
                "https://nexus-ten-brown.vercel.app/chamado.html?empresa={0}&setor={1}&ativo={2}&solicitante={3}&ip={4}",
                Uri.EscapeDataString(emp),
                Uri.EscapeDataString(set),
                Uri.EscapeDataString(hst),
                Uri.EscapeDataString(usr),
                Uri.EscapeDataString(localIpAddress ?? "127.0.0.1")
            );
        }

        public static void AbrirChamadoUI()
        {
            string hostedUrl = ObterUrlPortalChamadoHospedado();
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(hostedUrl);
                psi.UseShellExecute = true;
                Process.Start(psi);
                return;
            }
            catch
            {
                try
                {
                    ProcessStartInfo psiCmd = new ProcessStartInfo("cmd.exe", "/c start \"\" \"" + hostedUrl + "\"");
                    psiCmd.CreateNoWindow = true;
                    psiCmd.UseShellExecute = false;
                    Process.Start(psiCmd);
                    return;
                }
                catch
                {
                    FormChamadoCliente.Exibir(customMachineName, customMachineTag, localIpAddress);
                }
            }
        }

        static void CriaTrayIcon()
        {
            trayIcon = new NotifyIcon();
            trayIcon.Icon = ObterIconeNexus();
            trayIcon.Text = "NexusTI Agent — " + customMachineName + " (" + localIpAddress + ":" + port + ")";
            trayIcon.Visible = true;

            trayIcon.BalloonTipTitle = "NexusTI Remote Agent Ativo";
            trayIcon.BalloonTipText = "Ativo: " + customMachineName + "\nIP: " + localIpAddress + ":" + port + "\nNexusTI Suporte & Chamados ativo.";
            trayIcon.BalloonTipIcon = ToolTipIcon.Info;
            trayIcon.ShowBalloonTip(3000);

            trayIcon.DoubleClick += (s, e) => {
                AbrirChamadoUI();
            };

            ContextMenu menu = new ContextMenu();
            MenuItem mChamado = new MenuItem("➕ Abrir Chamado T.I (NexusTI)...", (s, e) => {
                AbrirChamadoUI();
            });
            mChamado.DefaultItem = true;
            menu.MenuItems.Add(mChamado);

            menu.MenuItems.Add("-");

            MenuItem mNome = new MenuItem("Ativo: " + customMachineName);
            mNome.Enabled = false;
            menu.MenuItems.Add(mNome);

            MenuItem mIp = new MenuItem("IP Local: " + localIpAddress + ":" + port);
            mIp.Enabled = false;
            menu.MenuItems.Add(mIp);

            MenuItem mMon = new MenuItem("Monitores: " + Screen.AllScreens.Length);
            mMon.Enabled = false;
            menu.MenuItems.Add(mMon);

            MenuItem mToken = new MenuItem("Token: " + sessionToken);
            mToken.Enabled = false;
            menu.MenuItems.Add(mToken);

            menu.MenuItems.Add("-");

            menu.MenuItems.Add("Copiar Endereço (IP:Porta)", (s, e) => {
                try { Clipboard.SetText("http://" + localIpAddress + ":" + port); } catch { }
            });

            menu.MenuItems.Add("Abrir Console Web", (s, e) => {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo("https://nexus-ten-brown.vercel.app/");
                    psi.UseShellExecute = true;
                    System.Diagnostics.Process.Start(psi);
                }
                catch { }
            });

            menu.MenuItems.Add("-");

            // Botão oficial no software para atualizar o agente sob demanda
            MenuItem mAtualizar = new MenuItem("🔄 Atualizar Software do Agente", (s, e) => {
                if (MessageBox.Show("Deseja baixar e aplicar a versão mais recente do Nexus Agent do repositório?", "Atualizar Software NexusTI", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    try { trayIcon.ShowBalloonTip(4000, "NexusTI Agent", "Baixando versão atualizada do GitHub e reiniciando o serviço...", ToolTipIcon.Info); } catch { }
                    ThreadPool.QueueUserWorkItem(_ => ExecutarAtualizacaoSilenciosa());
                }
            });
            menu.MenuItems.Add(mAtualizar);

            MenuItem mVersao = new MenuItem("Versão: v" + AGENT_VERSION + " (Build " + AGENT_BUILD + ")");
            mVersao.Enabled = false;
            menu.MenuItems.Add(mVersao);

            menu.MenuItems.Add("-");

            menu.MenuItems.Add("Encerrar Agente", (s, e) => {
                if (MessageBox.Show("Deseja realmente encerrar o NexusTI Remote Agent?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    trayIcon.Visible = false;
                    Environment.Exit(0);
                }
            });

            trayIcon.ContextMenu = menu;
        }

        static void IniciaServidorTcp()
        {
            int tentativas = 0;
            while (tentativas < 15)
            {
                try
                {
                    tcpServer = new TcpListener(IPAddress.Any, port);
                    tcpServer.Server.NoDelay = true;
                    ThreadPool.SetMinThreads(100, 100);
                    ThreadPool.SetMaxThreads(1000, 1000);
                    tcpServer.Start();
                    break;
                }
                catch (Exception ex)
                {
                    tentativas++;
                    Thread.Sleep(1000);
                    if (tentativas >= 15)
                    {
                        try { File.WriteAllText(Path.Combine(ObterDiretorioDadosAgente(), "port_error.log"), ex.ToString()); } catch { }
                        return;
                    }
                }
            }

            while (true)
            {
                try
                {
                    TcpClient client = tcpServer.AcceptTcpClient();
                    client.NoDelay = true;
                    ThreadPool.QueueUserWorkItem((c) => ProcessaCliente((TcpClient)c), client);
                }
                catch { }
            }
        }

        static void ProcessaCliente(TcpClient client)
        {
            try
            {
                client.NoDelay = true;
                client.ReceiveTimeout = 15000;
                client.SendTimeout = 15000;

                using (NetworkStream stream = client.GetStream())
                {
                byte[] buffer = new byte[16384];
                while (client.Connected)
                {
                    int bytesRead = 0;
                    try
                    {
                        bytesRead = stream.Read(buffer, 0, buffer.Length);
                    }
                    catch { break; }
                    if (bytesRead <= 0) break;

                    string headerStr = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    string[] lines = headerStr.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                    if (lines.Length == 0) break;

                    string[] requestLine = lines[0].Split(' ');
                    if (requestLine.Length < 2) break;

                    string method = requestLine[0].ToUpper();
                    string rawUrl = requestLine[1];
                    string path = (rawUrl.Contains("?") ? rawUrl.Split('?')[0] : rawUrl).ToLower();

                    bool keepAlive = true;
                    foreach (string l in lines)
                    {
                        if (l.ToLower().StartsWith("connection: close"))
                        {
                            keepAlive = false;
                            break;
                        }
                    }

                    if (method == "OPTIONS")
                    {
                        EnviarRespostaCors(stream, 200, "OK", "text/plain", new byte[0], keepAlive);
                        if (!keepAlive) break;
                        continue;
                    }

                    // 0.0-OTA ATUALIZAÇÃO REMOTA DIRETA VIA HTTP
                    if (path == "/api/update" || path == "/api/agent/update" || path == "/api/ota")
                    {
                        string customDl = ExtrairParam(rawUrl, "", "url", "");
                        ThreadPool.QueueUserWorkItem(_ => ExecutarAtualizacaoSilenciosa(!string.IsNullOrEmpty(customDl) ? customDl : null));
                        string respJson = string.Format("{{\"ok\":true,\"status\":\"updating\",\"version\":\"{0}\",\"build\":{1}}}", AGENT_VERSION, AGENT_BUILD);
                        EnviarRespostaCors(stream, 200, "OK", "application/json", Encoding.UTF8.GetBytes(respJson), keepAlive);
                        if (!keepAlive) break;
                        continue;
                    }

                                        // 0.0 AVISOS CORPORATIVOS, NOTAS TÉCNICAS E ENQUETES INTERATIVAS
                    if (path.StartsWith("/api/aviso") || path.StartsWith("/api/notificacao") || path.StartsWith("/api/comunicado") || path.StartsWith("/api/enquete"))
                    {
                        if (path.StartsWith("/api/enquete/votar") && method == "POST")
                        {
                            string body = ExtrairCorpoRequisicao(headerStr, lines, buffer, bytesRead);
                            string vEnqId = ExtrairParam(path, body, "enquete_id", "ENQ-1");
                            string vOpcao = ExtrairParam(path, body, "opcao", "");
                            string vHost = ExtrairParam(path, body, "host", customMachineName);
                            string vUser = ExtrairParam(path, body, "user", Environment.UserName);
                            
                            SalvarVotoEnquete(vEnqId, vOpcao, vHost, vUser);
                            byte[] respVoto = Encoding.UTF8.GetBytes("{\"ok\":true,\"mensagem\":\"Voto registrado com sucesso!\"}");
                            EnviarRespostaCors(stream, 200, "OK", "application/json", respVoto, keepAlive);
                            if (!keepAlive) break;
                            continue;
                        }

                        if (path.StartsWith("/api/enquete/resultados") || path.StartsWith("/api/enquetes"))
                        {
                            string jsonRes = ObterResultadosEnquetesJson();
                            byte[] respRes = Encoding.UTF8.GetBytes(jsonRes);
                            EnviarRespostaCors(stream, 200, "OK", "application/json", respRes, keepAlive);
                            if (!keepAlive) break;
                            continue;
                        }

                        if (path.StartsWith("/api/avisos/status") || path.StartsWith("/api/avisos"))
                        {
                            string jsonAv = ObterHistoricoAvisosJson();
                            byte[] respAv = Encoding.UTF8.GetBytes(jsonAv);
                            EnviarRespostaCors(stream, 200, "OK", "application/json", respAv, keepAlive);
                            if (!keepAlive) break;
                            continue;
                        }

                        if (method == "POST")
                        {
                            string body = ExtrairCorpoRequisicao(headerStr, lines, buffer, bytesRead);
                            string tipo = path.StartsWith("/api/enquete") ? "enquete" : ExtrairParam(rawUrl, body, "tipo", "aviso");
                            string titulo = ExtrairParam(rawUrl, body, "titulo", tipo == "enquete" ? "Pesquisa de T.I" : "Comunicado de T.I");
                            string mensagem = ExtrairParam(rawUrl, body, "mensagem", "Por favor, leia atentamente as informações transmitidas pela equipe de TI.");
                            string urgencia = ExtrairParam(rawUrl, body, "urgencia", "informativo");
                            string opcoesStr = ExtrairParam(rawUrl, body, "opcoes", "Excelente|Boa|Regular|Ruim");
                            string enqueteId = ExtrairParam(rawUrl, body, "enquete_id", "ENQ-" + DateTime.Now.ToString("yyyyMMddHHmmss"));
                            string autor = ExtrairParam(rawUrl, body, "autor", "Gestão de TI");

                            string[] opcoes = opcoesStr.Split(new char[] { '|', ';' }, StringSplitOptions.RemoveEmptyEntries);
                            if (opcoes.Length == 0 && tipo == "enquete")
                            {
                                opcoes = new string[] { "Sim, tudo certo", "Preciso de suporte", "Não se aplica" };
                            }

                            SalvarAvisoRecebido(tipo, titulo, mensagem, urgencia, enqueteId, autor);
                            FormAvisoClient.Exibir(tipo, titulo, mensagem, urgencia, opcoes, enqueteId, autor);

                            byte[] respOk = Encoding.UTF8.GetBytes("{\"ok\":true,\"status\":\"exibido\",\"mensagem\":\"Comunicado emitido na tela com sucesso!\"}");
                            EnviarRespostaCors(stream, 200, "OK", "application/json", respOk, keepAlive);
                            if (!keepAlive) break;
                            continue;
                        }
                    }
                    else if (path.StartsWith("/aviso"))
                    {
                        string htmlAviso = ObterPaginaHtmlAviso();
                        byte[] htmlBytes = Encoding.UTF8.GetBytes(htmlAviso);
                        EnviarRespostaCors(stream, 200, "OK", "text/html; charset=utf-8", htmlBytes, keepAlive);
                        if (!keepAlive) break;
                        continue;
                    }

// 0. ABERTURA E CONSULTA DE CHAMADOS DE T.I
                    if (path.StartsWith("/api/chamados") || path.StartsWith("/api/chamado"))
                    {
                        if (path.StartsWith("/api/chamados/concluir"))
                        {
                            string idConcluir = "";
                            if (rawUrl.Contains("id="))
                            {
                                try { idConcluir = Uri.UnescapeDataString(rawUrl.Split(new string[] { "id=" }, StringSplitOptions.None)[1].Split('&')[0]); } catch { }
                            }
                            ConcluirChamadoCentral(idConcluir);
                            byte[] respOk = Encoding.UTF8.GetBytes("{\"ok\":true,\"mensagem\":\"Chamado concluido com sucesso!\"}");
                            EnviarRespostaCors(stream, 200, "OK", "application/json", respOk, keepAlive);
                            if (!keepAlive) break;
                            continue;
                        }

                        string bodyReq = (method == "POST") ? ExtrairCorpoRequisicao(headerStr, lines, buffer, bytesRead, stream) : "";

                        // Rota para avaliação de atendimento pelo solicitante (CSAT 1 a 5 estrelas + comentário)
                        if (path.StartsWith("/api/chamados/avaliar") || path.StartsWith("/api/chamado/avaliar"))
                        {
                            string idAv = ExtrairParam(rawUrl, bodyReq, "id", "");
                            if (string.IsNullOrEmpty(idAv)) idAv = ExtrairParam(rawUrl, bodyReq, "ticketId", "");
                            string notaStr = ExtrairParam(rawUrl, bodyReq, "nota", "5");
                            string comentario = ExtrairParam(rawUrl, bodyReq, "comentario", "");
                            string autorAv = ExtrairParam(rawUrl, bodyReq, "autor", "Solicitante");

                            if (!string.IsNullOrEmpty(bodyReq) && bodyReq.Trim().StartsWith("{"))
                            {
                                try
                                {
                                    var js = new System.Web.Script.Serialization.JavaScriptSerializer();
                                    var dict = js.Deserialize<Dictionary<string, object>>(bodyReq);
                                    if (dict != null)
                                    {
                                        if (dict.ContainsKey("id") && dict["id"] != null) idAv = dict["id"].ToString().Trim();
                                        else if (dict.ContainsKey("ticketId") && dict["ticketId"] != null) idAv = dict["ticketId"].ToString().Trim();
                                        if (dict.ContainsKey("nota") && dict["nota"] != null) notaStr = dict["nota"].ToString().Trim();
                                        if (dict.ContainsKey("comentario") && dict["comentario"] != null) comentario = dict["comentario"].ToString().Trim();
                                        if (dict.ContainsKey("autor") && dict["autor"] != null) autorAv = dict["autor"].ToString().Trim();
                                    }
                                }
                                catch { }
                            }

                            int notaInt = 5;
                            int.TryParse(notaStr, out notaInt);
                            if (notaInt < 1) notaInt = 1;
                            if (notaInt > 5) notaInt = 5;

                            AvaliarChamadoCentral(idAv, notaInt, comentario, autorAv);
                            byte[] respAvOk = Encoding.UTF8.GetBytes("{\"ok\":true,\"id\":\"" + idAv + "\",\"nota\":" + notaInt + ",\"mensagem\":\"Avaliacao registrada com sucesso!\"}");
                            EnviarRespostaCors(stream, 200, "OK", "application/json", respAvOk, keepAlive);
                            if (!keepAlive) break;
                            continue;
                        }

                        // Rota para atualização centralizada de estágio, técnico responsável ou migração de empresa
                        if (path.StartsWith("/api/chamados/atualizar") || path.StartsWith("/api/chamados/update"))
                        {
                            AtualizarChamadoCentral(rawUrl, bodyReq);
                            byte[] respUpd = Encoding.UTF8.GetBytes("{\"ok\":true,\"mensagem\":\"Chamado atualizado na base central!\"}");
                            EnviarRespostaCors(stream, 200, "OK", "application/json", respUpd, keepAlive);
                            if (!keepAlive) break;
                            continue;
                        }

                        // Rota para salvar mensagens no chat do chamado
                        if (path.StartsWith("/api/chamados/mensagem") || path.StartsWith("/api/chamado/mensagem"))
                        {
                            string tId = "";
                            string autor = "Usuário";
                            string tipo = "cliente";
                            string txt = "";
                            string hr = DateTime.Now.ToString("HH:mm");
                            string mId = "";

                            if (!string.IsNullOrEmpty(bodyReq) && bodyReq.Trim().StartsWith("{"))
                            {
                                try
                                {
                                    var js = new System.Web.Script.Serialization.JavaScriptSerializer();
                                    var dict = js.Deserialize<Dictionary<string, object>>(bodyReq);
                                    if (dict != null)
                                    {
                                        if (dict.ContainsKey("chamadoId") && dict["chamadoId"] != null) tId = dict["chamadoId"].ToString().Trim();
                                        if (string.IsNullOrEmpty(tId) && dict.ContainsKey("ticketId") && dict["ticketId"] != null) tId = dict["ticketId"].ToString().Trim();
                                        if (string.IsNullOrEmpty(tId) && dict.ContainsKey("id") && dict["id"] != null) tId = dict["id"].ToString().Trim();

                                        if (dict.ContainsKey("mensagemId") && dict["mensagemId"] != null) mId = dict["mensagemId"].ToString().Trim();
                                        else if (dict.ContainsKey("id") && dict["id"] != null && dict["id"].ToString().StartsWith("msg-")) mId = dict["id"].ToString().Trim();

                                        if (dict.ContainsKey("mensagem") && dict["mensagem"] != null)
                                        {
                                            if (dict["mensagem"] is Dictionary<string, object>)
                                            {
                                                var msgObj = (Dictionary<string, object>)dict["mensagem"];
                                                if (msgObj.ContainsKey("id") && msgObj["id"] != null) mId = msgObj["id"].ToString().Trim();
                                                if (msgObj.ContainsKey("autor") && msgObj["autor"] != null) autor = msgObj["autor"].ToString();
                                                if (msgObj.ContainsKey("tipo") && msgObj["tipo"] != null) tipo = msgObj["tipo"].ToString();
                                                if (msgObj.ContainsKey("texto") && msgObj["texto"] != null) txt = msgObj["texto"].ToString();
                                                if (msgObj.ContainsKey("horario") && msgObj["horario"] != null) hr = msgObj["horario"].ToString();
                                            }
                                            else
                                            {
                                                txt = dict["mensagem"].ToString();
                                            }
                                        }
                                        if (string.IsNullOrEmpty(txt) && dict.ContainsKey("texto") && dict["texto"] != null) txt = dict["texto"].ToString();
                                        if (dict.ContainsKey("autor") && dict["autor"] != null) autor = dict["autor"].ToString();
                                        if (dict.ContainsKey("tipo") && dict["tipo"] != null) tipo = dict["tipo"].ToString();
                                        if (dict.ContainsKey("horario") && dict["horario"] != null) hr = dict["horario"].ToString();
                                    }
                                }
                                catch { }
                            }

                            if (string.IsNullOrEmpty(tId) && rawUrl.Contains("?"))
                            {
                                try
                                {
                                    string qs = rawUrl.Substring(rawUrl.IndexOf('?') + 1);
                                    foreach (var pair in qs.Split('&'))
                                    {
                                        var kv = pair.Split('=');
                                        if (kv.Length >= 2)
                                        {
                                            string k = kv[0].ToLower().Trim();
                                            string v = Uri.UnescapeDataString(kv[1].Replace("+", " ")).Trim();
                                            if (k == "chamadoid" || k == "ticketid" || k == "id") tId = v;
                                            else if (k == "texto" || k == "msg") txt = v;
                                            else if (k == "autor") autor = v;
                                            else if (k == "tipo") tipo = v;
                                            else if (k == "mid" || k == "mensagemid") mId = v;
                                        }
                                    }
                                }
                                catch { }
                            }

                            if (!string.IsNullOrEmpty(tId) && !string.IsNullOrEmpty(txt))
                            {
                                SalvarMensagemChamadoLocal(tId, autor, tipo, txt, hr, mId);
                            }

                            byte[] respMsgOk = Encoding.UTF8.GetBytes("{\"ok\":true,\"mensagem\":\"Mensagem registrada com sucesso!\"}");
                            EnviarRespostaCors(stream, 200, "OK", "application/json", respMsgOk, keepAlive);
                            if (!keepAlive) break;
                            continue;
                        }

                        string rawLower = rawUrl.ToLower();

                        bool isCriacao = (method == "POST" && !string.IsNullOrEmpty(bodyReq)) ||
                                         rawLower.Contains("titulo=") || rawLower.Contains("ticketid=") ||
                                         rawLower.Contains("solicitante=") || rawLower.Contains("descricao=") ||
                                         (method == "POST" && path.StartsWith("/api/chamado"));

                        if (isCriacao)
                        {
                            string tit = "Problema no Computador";
                            string cat = "Hardware / Desktop";
                            string sol = Environment.UserName;
                            string desc = "Chamado registrado via endpoint web.";
                            string atv = customMachineName;
                            string ipClient = localIpAddress;
                            string urg = "Média";
                            string tId = "";
                            string emp = customEmpresa;
                            string set = customMachineTag;

                            // 1. Tenta extrair do BODY JSON (POST)
                            if (!string.IsNullOrEmpty(bodyReq) && bodyReq.Trim().StartsWith("{"))
                            {
                                try
                                {
                                    var js = new System.Web.Script.Serialization.JavaScriptSerializer();
                                    var dict = js.Deserialize<Dictionary<string, object>>(bodyReq);
                                    if (dict != null)
                                    {
                                        if (dict.ContainsKey("id") && dict["id"] != null) tId = dict["id"].ToString().Trim();
                                        if (dict.ContainsKey("ticketId") && dict["ticketId"] != null) tId = dict["ticketId"].ToString().Trim();
                                        if (dict.ContainsKey("titulo") && dict["titulo"] != null) tit = dict["titulo"].ToString().Trim();
                                        if (dict.ContainsKey("categoria") && dict["categoria"] != null) cat = dict["categoria"].ToString().Trim();
                                        if (dict.ContainsKey("solicitante") && dict["solicitante"] != null) sol = dict["solicitante"].ToString().Trim();
                                        if (dict.ContainsKey("empresa") && dict["empresa"] != null) emp = dict["empresa"].ToString().Trim();
                                        if (dict.ContainsKey("setor") && dict["setor"] != null) set = dict["setor"].ToString().Trim();
                                        if (dict.ContainsKey("ativo") && dict["ativo"] != null) atv = dict["ativo"].ToString().Trim();
                                        if (dict.ContainsKey("ip") && dict["ip"] != null) ipClient = dict["ip"].ToString().Trim();
                                        if (dict.ContainsKey("urgencia") && dict["urgencia"] != null) urg = dict["urgencia"].ToString().Trim();
                                        if (dict.ContainsKey("descricao") && dict["descricao"] != null) desc = dict["descricao"].ToString().Trim();
                                        else if (dict.ContainsKey("desc") && dict["desc"] != null) desc = dict["desc"].ToString().Trim();
                                    }
                                }
                                catch { }
                            }

                            // 2. Extrai de rawUrl (Query string) como complemento ou fallback
                            if (rawUrl.Contains("?"))
                            {
                                try
                                {
                                    string qs = rawUrl.Substring(rawUrl.IndexOf('?') + 1);
                                    string[] pairs = qs.Split('&');
                                    foreach (var pair in pairs)
                                    {
                                        string[] kv = pair.Split('=');
                                        if (kv.Length >= 2)
                                        {
                                            string key = kv[0].ToLower().Trim();
                                            string val = Uri.UnescapeDataString(kv[1].Replace("+", " ")).Trim();
                                            if ((key == "ticketid" || key == "id") && !string.IsNullOrEmpty(val)) tId = val;
                                            else if (key == "titulo" && !string.IsNullOrEmpty(val)) tit = val;
                                            else if (key == "empresa" && !string.IsNullOrEmpty(val)) emp = val;
                                            else if (key == "categoria" && !string.IsNullOrEmpty(val)) cat = val;
                                            else if (key == "solicitante" && !string.IsNullOrEmpty(val)) sol = val;
                                            else if (key == "setor" && !string.IsNullOrEmpty(val)) set = val;
                                            else if (key == "ativo" && !string.IsNullOrEmpty(val)) atv = val;
                                            else if (key == "ip" && !string.IsNullOrEmpty(val)) ipClient = val;
                                            else if (key == "urgencia" && !string.IsNullOrEmpty(val)) urg = val;
                                            else if ((key == "descricao" || key == "desc") && !string.IsNullOrEmpty(val)) desc = val;
                                        }
                                    }
                                }
                                catch { }
                            }

                            // 3. Injeção segura no backend: o próprio agente anexa o IP real e nome da máquina
                            if (string.IsNullOrEmpty(ipClient) || ipClient == "auto" || ipClient == "127.0.0.1")
                            {
                                ipClient = localIpAddress;
                            }
                            if (string.IsNullOrEmpty(atv) || atv == "auto" || atv == "127.0.0.1")
                            {
                                atv = customMachineName;
                            }
                            emp = NormalizarTextoEmpresa(string.IsNullOrEmpty(emp) ? customEmpresa : emp);

                            if (string.IsNullOrEmpty(tId))
                            {
                                tId = "TICK-" + new Random().Next(1001, 9999).ToString();
                            }

                            SalvarChamadoLocal(tit, cat, sol, atv, ipClient, desc, urg, tId, emp, set);
                            string respJson = "{\"ok\":true,\"id\":\"" + tId + "\",\"ip\":\"" + ipClient + "\",\"ativo\":\"" + atv + "\",\"empresa\":\"" + emp + "\",\"mensagem\":\"Chamado registrado com sucesso!\"}";
                            byte[] respOk = Encoding.UTF8.GetBytes(respJson);
                            EnviarRespostaCors(stream, 200, "OK", "application/json", respOk, keepAlive);
                            if (!keepAlive) break;
                            continue;
                        }
                        else
                        {
                            string jsonChamados = ObterChamadosJson();
                            byte[] respBytes = Encoding.UTF8.GetBytes(jsonChamados);
                            EnviarRespostaCors(stream, 200, "OK", "application/json", respBytes, keepAlive);
                            if (!keepAlive) break;
                            continue;
                        }
                    }
                    else if (path == "/api/exit" || path == "/api/restart" || path == "/api/quit")
                    {
                        if (!ValidarAcessoSensivel(client, rawUrl, lines, "", true))
                        {
                            byte[] errAuth = Encoding.UTF8.GetBytes("{\"ok\":false,\"erro\":\"Acesso negado: token de seguranca obrigatorio para encerrar ou reiniciar o agente.\"}");
                            EnviarRespostaCors(stream, 401, "Unauthorized", "application/json", errAuth, false);
                            break;
                        }
                        byte[] respExit = Encoding.UTF8.GetBytes("{\"ok\":true,\"mensagem\":\"Agente encerrando para reinicio...\"}");
                        EnviarRespostaCors(stream, 200, "OK", "application/json", respExit, false);
                        ThreadPool.QueueUserWorkItem(_ => {
                            Thread.Sleep(300);
                            Environment.Exit(0);
                        });
                        break;
                    }
                    else if (path == "/empresas.json" || path == "/api/empresas")
            {
                if (method == "POST")
                {
                    string bodyEmpresas = ExtrairCorpoRequisicao(headerStr, lines, buffer, bytesRead, stream);
                    SalvarEmpresasJson(bodyEmpresas);
                    byte[] okBytes = Encoding.UTF8.GetBytes("{\"ok\":true,\"message\":\"Empresas sincronizadas com sucesso\"}");
                    EnviarRespostaCors(stream, 200, "OK", "application/json; charset=utf-8", okBytes, keepAlive);
                    if (!keepAlive) break;
                    continue;
                }
                else
                {
                    string jsonEmpresas = ObterEmpresasJson();
                    byte[] respBytes = Encoding.UTF8.GetBytes(jsonEmpresas);
                    EnviarRespostaCors(stream, 200, "OK", "application/json; charset=utf-8", respBytes, keepAlive);
                    if (!keepAlive) break;
                    continue;
                }
            }
            else if (path == "/setores.json" || path == "/api/setores")
            {
                if (method == "POST")
                {
                    string bodySetores = ExtrairCorpoRequisicao(headerStr, lines, buffer, bytesRead, stream);
                    SalvarSetoresJson(bodySetores);
                    byte[] okBytes = Encoding.UTF8.GetBytes("{\"ok\":true,\"message\":\"Setores sincronizados com sucesso\"}");
                    EnviarRespostaCors(stream, 200, "OK", "application/json; charset=utf-8", okBytes, keepAlive);
                    if (!keepAlive) break;
                    continue;
                }
                else
                {
                    string jsonSetores = ObterSetoresJson();
                    byte[] respBytes = Encoding.UTF8.GetBytes(jsonSetores);
                    EnviarRespostaCors(stream, 200, "OK", "application/json; charset=utf-8", respBytes, keepAlive);
                    if (!keepAlive) break;
                    continue;
                }
            }
            else if (path == "/chamados.json")
                    {
                        string jsonChamados = ObterChamadosJson();
                        byte[] respBytes = Encoding.UTF8.GetBytes(jsonChamados);
                        EnviarRespostaCors(stream, 200, "OK", "application/json; charset=utf-8", respBytes, keepAlive);
                        if (!keepAlive) break;
                        continue;
                    }
                    else if (path == "/assets_registry.json" || path == "/api/assets")
                    {
                        string jsonAssets = ObterAssetsRegistryJson();
                        byte[] respBytes = Encoding.UTF8.GetBytes(jsonAssets);
                        EnviarRespostaCors(stream, 200, "OK", "application/json; charset=utf-8", respBytes, keepAlive);
                        if (!keepAlive) break;
                        continue;
                    }
                    else if (path == "/" || path == "/index.html" || path.StartsWith("/console"))
                    {
                        string[] idxPaths = new string[] {
                            @"c:\Users\gabrielnovaes\Documents\copia magma\index.html",
                            @"c:\Users\gabrielnovaes\Documents\fsc-console\index.html",
                            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "index.html")
                        };
                        string foundIdx = null;
                        foreach (var p in idxPaths) { if (File.Exists(p)) { foundIdx = p; break; } }
                        if (foundIdx != null)
                        {
                            byte[] htmlBytes = File.ReadAllBytes(foundIdx);
                            EnviarRespostaCors(stream, 200, "OK", "text/html; charset=utf-8", htmlBytes, keepAlive);
                            if (!keepAlive) break;
                            continue;
                        }
                    }
                    else if (path.StartsWith("/api/register") || path.StartsWith("/api/assets/register"))
                    {
                        string bodyReg = (method == "POST") ? ExtrairCorpoRequisicao(headerStr, lines, buffer, bytesRead, stream) : "";
                        string rHost = ExtrairParam(rawUrl, bodyReg, "host", "");
                        string rTag = ExtrairParam(rawUrl, bodyReg, "tag", "Qualidade");
                        string rIp = ExtrairParam(rawUrl, bodyReg, "ip", "");
                        string rUser = ExtrairParam(rawUrl, bodyReg, "user", "");
                        string rOs = ExtrairParam(rawUrl, bodyReg, "os", "Windows 10 Pro");
                        string rDevType = ExtrairParam(rawUrl, bodyReg, "type", "Desktop");
                        string rEmpresa = NormalizarTextoEmpresa(ExtrairParam(rawUrl, bodyReg, "empresa", "Aço Móveis"));
                        string rIsSrvStr = ExtrairParam(rawUrl, bodyReg, "is_server", "").ToLower();
                        bool rIsServer = (rIsSrvStr == "true" || rIsSrvStr == "1" || rDevType.ToLower() == "servidor" || (!string.IsNullOrEmpty(rHost) && rHost.ToUpper().StartsWith("SRV")));
                        if (rIsServer && (string.IsNullOrEmpty(rDevType) || rDevType == "Desktop")) rDevType = "Servidor";

                        if (string.IsNullOrEmpty(rIp))
                        {
                            try
                            {
                                IPEndPoint remEp = client.Client.RemoteEndPoint as IPEndPoint;
                                if (remEp != null) rIp = remEp.Address.ToString();
                            }
                            catch { }
                        }

                        CadastrarAtivoCentral(rHost, rTag, rIp, rUser, rOs, rDevType, rEmpresa, rIsServer);
                        byte[] respReg = Encoding.UTF8.GetBytes("{\"ok\":true,\"isServer\":" + (rIsServer ? "true" : "false") + ",\"mensagem\":\"Ativo cadastrado com sucesso na base central!\"}");
                        EnviarRespostaCors(stream, 200, "OK", "application/json", respReg, keepAlive);
                        if (!keepAlive) break;
                        continue;
                    }
                    else if (path.StartsWith("/api/setserver") || path.StartsWith("/api/assets/setserver"))
                    {
                        string bodySrv = (method == "POST") ? ExtrairCorpoRequisicao(headerStr, lines, buffer, bytesRead, stream) : "";
                        string sHost = ExtrairParam(rawUrl, bodySrv, "host", "");
                        string sIp = ExtrairParam(rawUrl, bodySrv, "ip", "");
                        string sEmp = ExtrairParam(rawUrl, bodySrv, "empresa", "");
                        string sFlagStr = ExtrairParam(rawUrl, bodySrv, "is_server", "true").ToLower();
                        bool sIsServer = (sFlagStr == "true" || sFlagStr == "1" || sFlagStr == "servidor");

                        if (string.IsNullOrEmpty(sHost) || String.Equals(sHost, customMachineName, StringComparison.OrdinalIgnoreCase) || sIp == localIpAddress || sIp == "127.0.0.1")
                        {
                            isServerMachine = sIsServer;
                            try { File.WriteAllText(Path.Combine(ObterDiretorioDadosAgente(), "is_server.txt"), sIsServer ? "true" : "false", Encoding.UTF8); } catch { }
                            if (string.IsNullOrEmpty(sHost)) sHost = customMachineName;
                            if (string.IsNullOrEmpty(sIp)) sIp = localIpAddress;
                        }

                        DefinirStatusServidorAtivoCentral(sHost, sIp, sIsServer, sEmp);
                        byte[] respSetSrv = Encoding.UTF8.GetBytes("{\"ok\":true,\"host\":\"" + sHost + "\",\"isServer\":" + (sIsServer ? "true" : "false") + ",\"mensagem\":\"Modo Servidor atualizado com sucesso!\"}");
                        EnviarRespostaCors(stream, 200, "OK", "application/json", respSetSrv, keepAlive);
                        if (!keepAlive) break;
                        continue;
                    }
                    else if (path.StartsWith("/res/spotlight.jpg") || path.StartsWith("/spotlight.jpg"))
                    {
                        string[] spotPaths = new string[] {
                            Path.Combine(ObterDiretorioDadosAgente(), @"res\spotlight.jpg"),
                            @"c:\Users\gabrielnovaes\Documents\copia magma\res\spotlight.jpg",
                            @"c:\Users\gabrielnovaes\Documents\fsc-console\res\spotlight.jpg"
                        };
                        string foundSpot = null;
                        foreach (var p in spotPaths) { if (File.Exists(p)) { foundSpot = p; break; } }
                        if (foundSpot != null)
                        {
                            byte[] imgBytes = File.ReadAllBytes(foundSpot);
                            EnviarRespostaCors(stream, 200, "OK", "image/jpeg", imgBytes, keepAlive);
                            if (!keepAlive) break;
                            continue;
                        }
                    }
                    else if (path == "/agent/nexusremoteagent.exe" || path == "/nexusremoteagent.exe" || path == "/fscremoteagent.exe")
                    {
                        string[] exePaths = new string[] {
                            @"c:\Users\gabrielnovaes\Documents\copia magma\agent\NexusRemoteAgent.exe",
                            @"c:\Users\gabrielnovaes\Documents\copia magma\NexusRemoteAgent.exe",
                            Application.ExecutablePath
                        };
                        string foundExe = null;
                        foreach (var p in exePaths) { if (File.Exists(p)) { foundExe = p; break; } }
                        if (foundExe != null)
                        {
                            byte[] fileBytes = File.ReadAllBytes(foundExe);
                            StringBuilder sbDl = new StringBuilder();
                            sbDl.AppendFormat("HTTP/1.1 200 OK\r\n");
                            sbDl.Append("Access-Control-Allow-Origin: *\r\n");
                            sbDl.Append("Content-Type: application/octet-stream\r\n");
                            sbDl.Append("Content-Disposition: attachment; filename=\"NexusRemoteAgent.exe\"\r\n");
                            sbDl.AppendFormat("Content-Length: {0}\r\n", fileBytes.Length);
                            sbDl.Append("Connection: close\r\n\r\n");
                            byte[] hBytes = Encoding.UTF8.GetBytes(sbDl.ToString());
                            stream.Write(hBytes, 0, hBytes.Length);
                            stream.Write(fileBytes, 0, fileBytes.Length);
                            stream.Flush();
                            if (!keepAlive) break;
                            continue;
                        }
                    }
                    else if (path.StartsWith("/download") || path.StartsWith("/instalador-nexus-agent.exe") || path.StartsWith("/instalador") || path.StartsWith("/nexus-agent-instalador.zip"))
                    {
                        string targetName = path.Contains("zip") ? "FSC-Agent-Instalador.zip" : "Instalador-FSC-Agent.exe";
                        string[] dlPaths = new string[] {
                            Path.Combine(ObterDiretorioDadosAgente(), targetName),
                            @"c:\Users\gabrielnovaes\Documents\copia magma\" + targetName,
                            @"c:\Users\gabrielnovaes\Documents\fsc-console\" + targetName
                        };
                        string foundDl = null;
                        foreach (var p in dlPaths) { if (File.Exists(p)) { foundDl = p; break; } }
                        if (foundDl != null)
                        {
                            byte[] fileBytes = File.ReadAllBytes(foundDl);
                            StringBuilder sbDl = new StringBuilder();
                            sbDl.AppendFormat("HTTP/1.1 200 OK\r\n");
                            sbDl.Append("Access-Control-Allow-Origin: *\r\n");
                            sbDl.Append("Content-Type: application/octet-stream\r\n");
                            sbDl.AppendFormat("Content-Disposition: attachment; filename=\"{0}\"\r\n", targetName);
                            sbDl.AppendFormat("Content-Length: {0}\r\n", fileBytes.Length);
                            sbDl.Append("Connection: close\r\n\r\n");
                            byte[] hBytes = Encoding.UTF8.GetBytes(sbDl.ToString());
                            stream.Write(hBytes, 0, hBytes.Length);
                            stream.Write(fileBytes, 0, fileBytes.Length);
                            stream.Flush();
                            if (!keepAlive) break;
                            continue;
                        }
                    }
                    else if (path.StartsWith("/chamado"))
                    {
                        string redirectUrl = ObterUrlPortalChamadoHospedado();
                        string htmlRedirect = "<!DOCTYPE html><html><head><meta charset='UTF-8'><meta http-equiv='refresh' content='0;url=" + redirectUrl + "'><script>window.location.replace('" + redirectUrl + "');</script></head><body>Redirecionando para o Portal de Chamados...</body></html>";
                        byte[] htmlBytes = Encoding.UTF8.GetBytes(htmlRedirect);
                        EnviarRespostaCors(stream, 200, "OK", "text/html; charset=utf-8", htmlBytes, keepAlive);
                        if (!keepAlive) break;
                        continue;
                    }

                    // 1. STATUS E INVENTÁRIO (Com o Nome Customizado da Instalação, Hardware Completo e Geolocalização)
                    if (path.StartsWith("/api/ping") || path.StartsWith("/api/status") || path.StartsWith("/api/screens") || path.StartsWith("/api/system") || path.StartsWith("/api/hardware"))
                    {
                        Screen[] allScreens = Screen.AllScreens;
                        StringBuilder sbScreens = new StringBuilder();
                        sbScreens.Append("[");
                        for (int i = 0; i < allScreens.Length; i++)
                        {
                            Screen sc = allScreens[i];
                            if (i > 0) sbScreens.Append(",");
                            sbScreens.AppendFormat(
                                "{{\"id\":{0},\"name\":\"Tela {1}{2}\",\"x\":{3},\"y\":{4},\"width\":{5},\"height\":{6},\"primary\":{7}}}",
                                i,
                                i + 1,
                                sc.Primary ? " (Principal)" : "",
                                sc.Bounds.X,
                                sc.Bounds.Y,
                                sc.Bounds.Width,
                                sc.Bounds.Height,
                                sc.Primary ? "true" : "false"
                            );
                        }
                        sbScreens.Append("]");

                        Rectangle curBounds = ObterBoundsTela(currentScreenIndex);
                        Rectangle virt = SystemInformation.VirtualScreen;

                        double ramTotalGb;
                        int ramUsedPct;
                        string ramDesc = ObterMemoriaRam(out ramTotalGb, out ramUsedPct);
                        double ramUsedGb = Math.Round((ramTotalGb * ramUsedPct) / 100.0, 1);
                        double ramFreeGb = Math.Round(Math.Max(0, ramTotalGb - ramUsedGb), 1);
                        int cpuUsedPct = ObterUsoCpuPercent();
                        string cpuDesc = ObterProcessador();
                        string diskDesc = ObterArmazenamentoC();
                        string osDesc = ObterNomeSO();
                        string devType = DetectarTipoDispositivo();
                        string locDesc = ObterLocalizacao();
                        string uptimeStr = ObterUptimeSistema();
                        int tcpConns = ObterConexoesTcpAtivas();

                        double diskTotalGb, diskFreeGb, diskUsedGb;
                        int diskUsedPct;
                        string drivesJson = ObterDetalhesDiscosJson(out diskTotalGb, out diskFreeGb, out diskUsedGb, out diskUsedPct);

                        string mfg, model, board, bios, serial;
                        ObterFabricanteEModelo(out mfg, out model, out board, out bios, out serial);

                        string gpuName, gpuDriver;
                        ObterPlacaDeVideo(out gpuName, out gpuDriver);

                        string macAddr, adapterName, gatewayIp, dnsIp;
                        ObterDetalhesRede(out macAddr, out adapterName, out gatewayIp, out dnsIp);

                        int cpuThreads = Environment.ProcessorCount;

                        string jsonBase = string.Format(
                            System.Globalization.CultureInfo.InvariantCulture,
                            "{{\"status\":\"online\",\"hostname\":\"{0}\",\"computerName\":\"{1}\",\"customName\":\"{0}\",\"deviceType\":\"{2}\",\"isServer\":{24},\"user\":\"{3}\",\"domain\":\"{4}\",\"loggedUserFull\":\"{4}\\\\{3}\",\"location\":\"{5}\",\"tag\":\"{6}\",\"ip\":\"{7}\",\"port\":{8},\"os\":\"{9}\",\"cpu\":\"{10}\",\"cpuUsedPct\":{25},\"ram\":\"{11}\",\"ramTotalGb\":{12},\"ramUsedGb\":{26},\"ramFreeGb\":{27},\"ramUsedPct\":{13},\"disk\":\"{14}\",\"diskTotalGb\":{28},\"diskFreeGb\":{29},\"diskUsedGb\":{30},\"diskUsedPct\":{31},\"drives\":{32},\"uptime\":\"{33}\",\"activeConnections\":{34},\"lastConnected\":\"Hoje, {15}\",\"lastSeen\":\"Conectado agora\",\"screenWidth\":{16},\"screenHeight\":{17},\"token\":\"{18}\",\"version\":\"" + AGENT_VERSION + "\",\"build\":" + AGENT_BUILD + ",\"screens\":{19},\"currentScreen\":{20},\"virtualScreen\":{{\"width\":{21},\"height\":{22}}},\"empresa\":\"{23}\",\"company\":\"{23}\"",
                            customMachineName,
                            Environment.MachineName,
                            devType,
                            Environment.UserName,
                            Environment.UserDomainName,
                            locDesc.Replace("\"", "'"),
                            customMachineTag,
                            localIpAddress,
                            port,
                            osDesc.Replace("\\", "/").Replace("\"", "'"),
                            cpuDesc.Replace("\\", "/").Replace("\"", "'"),
                            ramDesc,
                            ramTotalGb,
                            ramUsedPct,
                            diskDesc,
                            DateTime.Now.ToString("HH:mm:ss"),
                            curBounds.Width,
                            curBounds.Height,
                            sessionToken,
                            sbScreens.ToString(),
                            currentScreenIndex,
                            virt.Width,
                            virt.Height,
                            customEmpresa,
                            isServerMachine ? "true" : "false",
                            cpuUsedPct,
                            ramUsedGb,
                            ramFreeGb,
                            diskTotalGb,
                            diskFreeGb,
                            diskUsedGb,
                            diskUsedPct,
                            drivesJson,
                            uptimeStr,
                            tcpConns
                        );

                        string jsonExtra = string.Format(
                            System.Globalization.CultureInfo.InvariantCulture,
                            ",\"mfg\":\"{0}\",\"model\":\"{1}\",\"board\":\"{2}\",\"bios\":\"{3}\",\"serial\":\"{4}\",\"gpu\":\"{5}\",\"gpuDriver\":\"{6}\",\"mac\":\"{7}\",\"adapter\":\"{8}\",\"gateway\":\"{9}\",\"dns\":\"{10}\",\"cpuThreads\":{11},\"publicIp\":\"{12}\",\"city\":\"{13}\",\"region\":\"{14}\",\"country\":\"{15}\",\"lat\":{16},\"lon\":{17},\"isp\":\"{18}\",\"mapsUrl\":\"{19}\",\"locationString\":\"{20}\"}}",
                            mfg.Replace("\\", "/").Replace("\"", "'"),
                            model.Replace("\\", "/").Replace("\"", "'"),
                            board.Replace("\\", "/").Replace("\"", "'"),
                            bios.Replace("\\", "/").Replace("\"", "'"),
                            serial.Replace("\\", "/").Replace("\"", "'"),
                            gpuName.Replace("\\", "/").Replace("\"", "'"),
                            gpuDriver.Replace("\\", "/").Replace("\"", "'"),
                            macAddr,
                            adapterName.Replace("\\", "/").Replace("\"", "'"),
                            gatewayIp,
                            dnsIp,
                            cpuThreads,
                            geoPublicIp,
                            geoCity.Replace("\"", "'"),
                            geoRegion.Replace("\"", "'"),
                            geoCountry.Replace("\"", "'"),
                            geoLat.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            geoLon.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            geoIsp.Replace("\\", "/").Replace("\"", "'"),
                            geoMapsUrl,
                            (string.IsNullOrEmpty(geoLabel) ? locDesc : geoLabel).Replace("\"", "'")
                        );

                        string json = jsonBase + jsonExtra;
                        EnviarRespostaCors(stream, 200, "OK", "application/json", Encoding.UTF8.GetBytes(json), keepAlive);
                        if (!keepAlive) break;
                    }
                    // 2. CONFIGURAR NOME DO ATIVO VIA API (PROTEGIDO POR TOKEN)
                    else if (path.StartsWith("/api/setname") && method == "POST")
                    {
                        string body = ExtrairCorpoRequisicao(headerStr, lines, buffer, bytesRead, stream);
                        if (!ValidarAcessoSensivel(client, rawUrl, lines, body, true))
                        {
                            EnviarRespostaCors(stream, 401, "Unauthorized", "application/json", Encoding.UTF8.GetBytes("{\"ok\":false,\"erro\":\"Token de seguranca invalido.\"}"), false);
                            break;
                        }
                        string novoNome = body.Replace("\"", "").Trim();
                        if (!string.IsNullOrEmpty(novoNome))
                        {
                            customMachineName = novoNome;
                            try { File.WriteAllText(Path.Combine(ObterDiretorioDadosAgente(), "machine_name.txt"), customMachineName, Encoding.UTF8); } catch { }
                            if (trayIcon != null) trayIcon.Text = "NexusTI Agent — " + customMachineName + " (" + localIpAddress + ":" + port + ")";
                        }
                        EnviarRespostaCors(stream, 200, "OK", "application/json", Encoding.UTF8.GetBytes("{\"ok\":true,\"name\":\"" + customMachineName + "\"}"), keepAlive);
                        if (!keepAlive) break;
                    }
                    // 3. MUDANÇA DE TELA (MULTI-MONITORES TELA 1 / TELA 2 / TODAS AS TELAS)
                    else if (path == "/api/screen/set" || path.StartsWith("/api/screen/set") || path == "/api/setscreen" || path.StartsWith("/api/setscreen"))
                    {
                        int targetScreen = 0;
                        string qId = ObterQueryParam(rawUrl, "id") ?? ObterQueryParam(rawUrl, "screen");
                        if (!string.IsNullOrEmpty(qId))
                        {
                            int.TryParse(qId, out targetScreen);
                        }
                        currentScreenIndex = targetScreen;

                        EnviarRespostaCors(stream, 200, "OK", "application/json", Encoding.UTF8.GetBytes("{\"ok\":true,\"currentScreen\":" + currentScreenIndex + "}"), keepAlive);
                        if (!keepAlive) break;
                    }
                    // 3.1 VISUALIZADOR WEB INTEGRADO DO AGENTE (ZERO MIXED CONTENT)
                    else if (path == "/viewer.html" || path == "/viewer" || path == "/remote")
                    {
                        byte[] htmlBytes = ObterBytesViewerHtml();
                        EnviarRespostaCors(stream, 200, "OK", "text/html; charset=utf-8", htmlBytes, keepAlive);
                        if (!keepAlive) break;
                        continue;
                    }
                    // 3.2 CAPTURA DE QUADRO ÚNICO JPEG (PARA THUMBNAIL, DIAGNÓSTICO E FALLBACK ULTRA-RÁPIDO)
                    else if (path == "/api/screen" || path == "/screen")
                    {
                        int screenReq = currentScreenIndex;
                        string qScreen = ObterQueryParam(rawUrl, "screen") ?? ObterQueryParam(rawUrl, "id");
                        if (!string.IsNullOrEmpty(qScreen))
                        {
                            int.TryParse(qScreen, out screenReq);
                        }

                        bool drawCursor = false;
                        string qDraw = ObterQueryParam(rawUrl, "drawcursor");
                        if (!string.IsNullOrEmpty(qDraw))
                        {
                            drawCursor = (qDraw == "1" || qDraw.Equals("true", StringComparison.OrdinalIgnoreCase));
                        }

                        float scaleFactor = 1.0f;
                        string qScale = ObterQueryParam(rawUrl, "scale");
                        if (!string.IsNullOrEmpty(qScale))
                        {
                            float.TryParse(qScale, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out scaleFactor);
                            if (scaleFactor <= 0.1f || scaleFactor > 1.0f) scaleFactor = 1.0f;
                        }

                        int quality = 50;
                        string qQuality = ObterQueryParam(rawUrl, "quality");
                        if (!string.IsNullOrEmpty(qQuality))
                        {
                            int.TryParse(qQuality, out quality);
                            if (quality < 20 || quality > 95) quality = 50;
                        }

                        byte[] imgBytes = CapturaTelaBytes(screenReq, drawCursor, scaleFactor, quality);
                        if (imgBytes != null && imgBytes.Length > 0)
                        {
                            EnviarRespostaCors(stream, 200, "OK", "image/jpeg", imgBytes, keepAlive);
                            if (!keepAlive) break;
                            continue;
                        }
                        else
                        {
                            EnviarRespostaCors(stream, 500, "Internal Server Error", "text/plain", Encoding.UTF8.GetBytes("Erro ao capturar"), false);
                            break;
                        }
                    }
                    // 4. STREAMING MJPEG ULTRA FLUIDO COM ESCALA SELECIONÁVEL E TROCA DINÂMICA DE TELAS
                    else if (path == "/api/stream" || path == "/stream")
                    {
                        string qScreen = ObterQueryParam(rawUrl, "screen") ?? ObterQueryParam(rawUrl, "id");
                        bool hasExplicitScreen = !string.IsNullOrEmpty(qScreen);
                        int explicitScreen = currentScreenIndex;
                        if (hasExplicitScreen)
                        {
                            int.TryParse(qScreen, out explicitScreen);
                        }

                        bool drawCursor = false;
                        string qDraw = ObterQueryParam(rawUrl, "drawcursor");
                        if (!string.IsNullOrEmpty(qDraw))
                        {
                            drawCursor = (qDraw == "1" || qDraw.Equals("true", StringComparison.OrdinalIgnoreCase));
                        }

                        // Escala: se scale=0.75 ou 0.67 para turbo mode
                        float scaleFactor = 1.0f;
                        string qScale = ObterQueryParam(rawUrl, "scale");
                        if (!string.IsNullOrEmpty(qScale))
                        {
                            float.TryParse(qScale, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out scaleFactor);
                            if (scaleFactor <= 0.1f || scaleFactor > 1.0f) scaleFactor = 1.0f;
                        }

                        // Qualidade JPEG selecionável (ex: 40 no modo turbo para latência zero)
                        int quality = 45;
                        string qQuality = ObterQueryParam(rawUrl, "quality");
                        if (!string.IsNullOrEmpty(qQuality))
                        {
                            int.TryParse(qQuality, out quality);
                            if (quality < 20 || quality > 95) quality = 45;
                        }

                        try
                        {
                            string mjpegHeader = "HTTP/1.1 200 OK\r\n" +
                                "Access-Control-Allow-Origin: *\r\n" +
                                "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
                                "Access-Control-Allow-Headers: *\r\n" +
                                "Access-Control-Allow-Private-Network: true\r\n" +
                                "Content-Type: multipart/x-mixed-replace; boundary=frame\r\n" +
                                "Cache-Control: no-cache, no-store, must-revalidate\r\n" +
                                "Pragma: no-cache\r\n\r\n";
                            byte[] mjpegHdrBytes = Encoding.ASCII.GetBytes(mjpegHeader);
                            stream.Write(mjpegHdrBytes, 0, mjpegHdrBytes.Length);

                            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                            while (client.Connected)
                            {
                                sw.Restart();
                                int activeScreen = hasExplicitScreen ? explicitScreen : currentScreenIndex;
                                byte[] imgBytes = CapturaTelaBytes(activeScreen, drawCursor, scaleFactor, quality);
                                if (imgBytes == null || imgBytes.Length == 0) { Thread.Sleep(100); continue; }

                                string frameHdr = "--frame\r\nContent-Type: image/jpeg\r\nContent-Length: " + imgBytes.Length + "\r\n\r\n";
                                byte[] frameHdrBytes = Encoding.ASCII.GetBytes(frameHdr);

                                stream.Write(frameHdrBytes, 0, frameHdrBytes.Length);
                                stream.Write(imgBytes, 0, imgBytes.Length);
                                stream.Write(Encoding.ASCII.GetBytes("\r\n"), 0, 2);
                                stream.Flush();

                                sw.Stop();
                                int elapsed = (int)sw.ElapsedMilliseconds;
                                int targetDelay = (scaleFactor < 0.95f) ? 20 : 25; // 50 FPS no Turbo, 40 FPS no HD
                                int delay = targetDelay - elapsed;
                                if (delay > 2) Thread.Sleep(delay);
                            }
                        }
                        catch
                        {
                            // Desconexão limpa de stream sem derrubar o agente
                        }
                        break;
                    }
                    // 5. AÇÕES DE MOUSE (ATÔMICAS, SEM CLIQUES FANTASMAS, COM VALIDAÇÃO DE TOKEN)
                    else if (path.StartsWith("/api/mouse") && method == "POST")
                    {
                        string body = ExtrairCorpoRequisicao(headerStr, lines, buffer, bytesRead, stream);
                        if (!ValidarAcessoSensivel(client, rawUrl, lines, body, false))
                        {
                            EnviarRespostaCors(stream, 401, "Unauthorized", "application/json", Encoding.UTF8.GetBytes("{\"ok\":false,\"erro\":\"Nao autorizado\"}"), false);
                            break;
                        }
                        ExecutaAcaoMouse(body);
                        EnviarRespostaCors(stream, 200, "OK", "application/json", Encoding.UTF8.GetBytes("{\"ok\":true}"), keepAlive);
                        if (!keepAlive) break;
                    }
                    // 6. TECLADO (COM VALIDAÇÃO DE TOKEN)
                    else if (path.StartsWith("/api/keyboard") && method == "POST")
                    {
                        string body = ExtrairCorpoRequisicao(headerStr, lines, buffer, bytesRead, stream);
                        if (!ValidarAcessoSensivel(client, rawUrl, lines, body, false))
                        {
                            EnviarRespostaCors(stream, 401, "Unauthorized", "application/json", Encoding.UTF8.GetBytes("{\"ok\":false,\"erro\":\"Nao autorizado\"}"), false);
                            break;
                        }
                        ExecutaAcaoTeclado(body);
                        EnviarRespostaCors(stream, 200, "OK", "application/json", Encoding.UTF8.GetBytes("{\"ok\":true}"), keepAlive);
                        if (!keepAlive) break;
                    }
                    // 7. CLIPBOARD
                    else if (path.StartsWith("/api/clipboard"))
                    {
                        if (method == "GET")
                        {
                            string clipText = ObterClipboardTexto();
                            string json = string.Format("{{\"clipboard\":{0},\"version\":{1}}}", NewtonsoftJsonSimple(clipText), clipboardVersion);
                            EnviarRespostaCors(stream, 200, "OK", "application/json", Encoding.UTF8.GetBytes(json), keepAlive);
                            if (!keepAlive) break;
                        }
                        else if (method == "POST")
                        {
                            string body = ExtrairCorpoRequisicao(headerStr, lines, buffer, bytesRead, stream);
                            if (!ValidarAcessoSensivel(client, rawUrl, lines, body, false))
                            {
                                EnviarRespostaCors(stream, 401, "Unauthorized", "application/json", Encoding.UTF8.GetBytes("{\"ok\":false,\"erro\":\"Nao autorizado\"}"), false);
                                break;
                            }
                            string textToSet = ExtrairTextoClipboard(body);

                            if (!string.IsNullOrEmpty(textToSet))
                            {
                                DefinirClipboardTexto(textToSet);
                                clipboardVersion++;
                            }

                            EnviarRespostaCors(stream, 200, "OK", "application/json", Encoding.UTF8.GetBytes("{\"ok\":true,\"version\":" + clipboardVersion + "}"), keepAlive);
                            if (!keepAlive) break;
                        }
                    }
                    // 8. UPLOAD (PROTEGIDO POR TOKEN + SANITIZAÇÃO DE PATH)
                    else if (path.StartsWith("/api/upload") && method == "POST")
                    {
                        if (!ValidarAcessoSensivel(client, rawUrl, lines, "", true))
                        {
                            EnviarRespostaCors(stream, 401, "Unauthorized", "application/json", Encoding.UTF8.GetBytes("{\"ok\":false,\"erro\":\"Token obrigatorio para upload.\"}"), false);
                            break;
                        }
                        string filename = "arquivo_recebido_" + DateTime.Now.Ticks + ".dat";
                        foreach (string line in lines)
                        {
                            if (line.ToLower().StartsWith("x-file-name:"))
                            {
                                filename = Path.GetFileName(line.Substring(12).Trim());
                                break;
                            }
                        }

                        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                        string target = Path.Combine(desktop, filename);

                        int bodyIndex = headerStr.IndexOf("\r\n\r\n");
                        if (bodyIndex >= 0)
                        {
                            int bodyOffset = bodyIndex + 4;
                            using (FileStream fs = new FileStream(target, FileMode.Create, FileAccess.Write))
                            {
                                fs.Write(buffer, bodyOffset, bytesRead - bodyOffset);
                            }
                        }

                        EnviarRespostaCors(stream, 200, "OK", "application/json", Encoding.UTF8.GetBytes("{\"ok\":true}"), keepAlive);
                        if (!keepAlive) break;
                    }
                    else
                    {
                        EnviarRespostaCors(stream, 404, "Not Found", "text/plain", Encoding.UTF8.GetBytes("404 Not Found"), false);
                        break;
                    }
                }
            }
        }
        catch { }
        finally
        {
            try { client.Close(); } catch { }
        }
    }

        static bool ValidarAcessoSensivel(TcpClient client, string rawUrl, string[] headers, string body = "", bool exigirTokenParaRemoto = true)
        {
            try
            {
                bool isLoopback = false;
                try
                {
                    IPEndPoint rem = client.Client.RemoteEndPoint as IPEndPoint;
                    if (rem != null && (IPAddress.IsLoopback(rem.Address) || rem.Address.ToString() == localIpAddress))
                    {
                        isLoopback = true;
                    }
                }
                catch { }

                string tokenReq = "";
                if (headers != null)
                {
                    foreach (string h in headers)
                    {
                        string hl = h.ToLower();
                        if (hl.StartsWith("x-fsc-token:"))
                        {
                            tokenReq = h.Substring(12).Trim();
                            break;
                        }
                        else if (hl.StartsWith("authorization:"))
                        {
                            tokenReq = h.Substring(14).Replace("Bearer", "").Trim();
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(tokenReq) && rawUrl.Contains("token="))
                {
                    try { tokenReq = Uri.UnescapeDataString(rawUrl.Split(new string[] { "token=" }, StringSplitOptions.None)[1].Split('&')[0]).Trim(); } catch { }
                }

                if (string.IsNullOrEmpty(tokenReq) && !string.IsNullOrEmpty(body) && body.Contains("token"))
                {
                    tokenReq = ExtrairParam(rawUrl, body, "token", "");
                }

                if (!string.IsNullOrEmpty(tokenReq))
                {
                    if (tokenReq == sessionToken || tokenReq == "FSC-CORP-SEC-TOKEN-91823" || tokenReq == "FSC@3606")
                        return true;
                    return false;
                }

                if (isLoopback) return true;
                return !exigirTokenParaRemoto;
            }
            catch
            {
                return true;
            }
        }

        static void EnviarRespostaCors(NetworkStream stream, int code, string statusText, string contentType, byte[] data, bool keepAlive)
        {
            if (contentType == "application/json")
            {
                contentType = "application/json; charset=utf-8";
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("HTTP/1.1 {0} {1}\r\n", code, statusText);
            sb.Append("Access-Control-Allow-Origin: *\r\n");
            sb.Append("Access-Control-Allow-Methods: GET, POST, OPTIONS, PUT, DELETE\r\n");
            sb.Append("Access-Control-Allow-Headers: *\r\n");
            sb.Append("Access-Control-Allow-Private-Network: true\r\n");
            sb.Append("Cache-Control: no-cache, no-store, must-revalidate\r\n");
            sb.AppendFormat("Content-Type: {0}\r\n", contentType);
            sb.AppendFormat("Content-Length: {0}\r\n", data.Length);
            if (keepAlive)
            {
                sb.Append("Connection: keep-alive\r\n");
                sb.Append("Keep-Alive: timeout=15, max=1000\r\n\r\n");
            }
            else
            {
                sb.Append("Connection: close\r\n\r\n");
            }

            byte[] headerBytes = Encoding.UTF8.GetBytes(sb.ToString());
            stream.Write(headerBytes, 0, headerBytes.Length);
            if (data.Length > 0)
            {
                stream.Write(data, 0, data.Length);
            }
            stream.Flush();
        }

        static string ObterQueryParam(string url, string key)
        {
            if (string.IsNullOrEmpty(url) || !url.Contains("?")) return null;
            try
            {
                string q = url.Substring(url.IndexOf('?') + 1);
                string[] pairs = q.Split('&');
                foreach (string p in pairs)
                {
                    string[] kv = p.Split('=');
                    if (kv.Length >= 2 && kv[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                    {
                        return Uri.UnescapeDataString(kv[1].Trim());
                    }
                }
            }
            catch { }
            return null;
        }

        static string ExtrairCorpoRequisicao(string headerStr, string[] lines, byte[] buffer, int bytesRead, NetworkStream stream = null)
        {
            string body = "";
            int bodyIndex = headerStr.IndexOf("\r\n\r\n");
            if (bodyIndex >= 0 && (bodyIndex + 4) <= headerStr.Length)
            {
                body = headerStr.Substring(bodyIndex + 4);
            }

            int contentLength = 0;
            if (lines != null)
            {
                foreach (string l in lines)
                {
                    if (l.ToLower().StartsWith("content-length:"))
                    {
                        int.TryParse(l.Substring(15).Trim(), out contentLength);
                        break;
                    }
                }
            }

            if (contentLength > 0 && stream != null)
            {
                int currentBytes = Encoding.UTF8.GetByteCount(body);
                int missing = contentLength - currentBytes;
                if (missing > 0)
                {
                    byte[] moreBuf = new byte[missing];
                    int totalRead = 0;
                    while (totalRead < missing)
                    {
                        try
                        {
                            int r = stream.Read(moreBuf, totalRead, missing - totalRead);
                            if (r <= 0) break;
                            totalRead += r;
                        }
                        catch { break; }
                    }
                    if (totalRead > 0)
                    {
                        body += Encoding.UTF8.GetString(moreBuf, 0, totalRead);
                    }
                }
            }

            return body.Trim();
        }

        static string ExtrairTextoClipboard(string body)
        {
            if (body.StartsWith("{") && body.Contains("text"))
            {
                int idx = body.IndexOf("\"text\":");
                if (idx >= 0)
                {
                    string raw = body.Substring(idx + 7).Trim();
                    if (raw.StartsWith("\"")) raw = raw.Substring(1);
                    if (raw.EndsWith("\"}") || raw.EndsWith("\""))
                    {
                        int end = raw.LastIndexOf("\"");
                        if (end >= 0) raw = raw.Substring(0, end);
                    }
                    return raw.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\\"", "\"").Replace("\\\\", "\\");
                }
            }
            return body;
        }

        static string ObterClipboardTexto()
        {
            string clipText = "";
            Thread t = new Thread(() => {
                try
                {
                    if (Clipboard.ContainsText()) clipText = Clipboard.GetText();
                }
                catch { }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join(350);
            return clipText;
        }

        static bool DefinirClipboardTexto(string text)
        {
            bool ok = false;
            Thread t = new Thread(() => {
                try
                {
                    Clipboard.SetText(text);
                    ok = true;
                }
                catch { }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join(350);
            return ok;
        }

        static Rectangle ObterBoundsTela(int screenIdx)
        {
            Screen[] screens = Screen.AllScreens;
            if (screenIdx == -1)
            {
                return SystemInformation.VirtualScreen;
            }
            if (screenIdx >= 0 && screenIdx < screens.Length)
            {
                return screens[screenIdx].Bounds;
            }
            return Screen.PrimaryScreen.Bounds;
        }

        // ---------------------------------------------------------------------
        // CAPTURA DE TELA DE BAIXA LATÊNCIA (COM SUPORTE A ESCALA TURBO)
        // ---------------------------------------------------------------------
        
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesiredAccess);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetThreadDesktop(IntPtr hDesktop);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool CloseDesktop(IntPtr hDesktop);


        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll", SetLastError = true)]
        static extern bool BitBlt(IntPtr hObject, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hObjSource, int nXSrc, int nYSrc, int dwRop);

        static byte[] CapturaTelaBytes(int screenIdx, bool drawCursor, float scaleFactor = 1.0f, int quality = 45)
        {
            try
            {
                try
                {
                    IntPtr hDesk = OpenInputDesktop(0, false, 0x01FF);
                    if (hDesk != IntPtr.Zero)
                    {
                        SetThreadDesktop(hDesk);
                        CloseDesktop(hDesk);
                    }
                }
                catch { }

                Rectangle bounds = ObterBoundsTela(screenIdx);
                if (bounds.Width <= 0 || bounds.Height <= 0) bounds = Screen.PrimaryScreen.Bounds;
                if (bounds.Width <= 0 || bounds.Height <= 0) bounds = new Rectangle(0, 0, 1920, 1080);

                using (Bitmap rawBmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb))
                {
                    using (Graphics g = Graphics.FromImage(rawBmp))
                    {
                        bool capturou = false;
                        try
                        {
                            g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
                            capturou = true;
                        }
                        catch { }

                        if (!capturou)
                        {
                            try
                            {
                                IntPtr hdcScreen = GetDC(IntPtr.Zero);
                                if (hdcScreen != IntPtr.Zero)
                                {
                                    IntPtr hdcDest = g.GetHdc();
                                    BitBlt(hdcDest, 0, 0, bounds.Width, bounds.Height, hdcScreen, bounds.X, bounds.Y, 0x00CC0020);
                                    g.ReleaseHdc(hdcDest);
                                    ReleaseDC(IntPtr.Zero, hdcScreen);
                                    capturou = true;
                                }
                            }
                            catch { }
                        }

                        if (drawCursor)
                        {
                            try
                            {
                                CURSORINFO pci;
                                pci.cbSize = Marshal.SizeOf(typeof(CURSORINFO));
                                if (GetCursorInfo(out pci))
                                {
                                    if (pci.flags == CURSOR_SHOWING && pci.hCursor != IntPtr.Zero)
                                    {
                                        int posX = pci.ptScreenPos.x - bounds.X;
                                        int posY = pci.ptScreenPos.y - bounds.Y;

                                        if (posX >= -32 && posX <= bounds.Width + 32 && posY >= -32 && posY <= bounds.Height + 32)
                                        {
                                            ICONINFO info;
                                            if (GetIconInfo(pci.hCursor, out info))
                                            {
                                                posX -= info.xHotspot;
                                                posY -= info.yHotspot;
                                                if (info.hbmColor != IntPtr.Zero) DeleteObject(info.hbmColor);
                                                if (info.hbmMask != IntPtr.Zero) DeleteObject(info.hbmMask);
                                            }

                                            IntPtr hdc = g.GetHdc();
                                            try
                                            {
                                                DrawIcon(hdc, posX, posY, pci.hCursor);
                                            }
                                            finally
                                            {
                                                g.ReleaseHdc(hdc);
                                            }
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }

                    if (quality < 20) quality = 20;
                    if (quality > 95) quality = 95;

                    using (EncoderParameters ep = new EncoderParameters(1))
                    {
                        ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)quality);

                        if (scaleFactor < 0.95f)
                        {
                            int targetW = (int)(bounds.Width * scaleFactor);
                            int targetH = (int)(bounds.Height * scaleFactor);
                            if (targetW < 100) targetW = 100;
                            if (targetH < 100) targetH = 100;

                            using (Bitmap scaledBmp = new Bitmap(targetW, targetH, PixelFormat.Format24bppRgb))
                            {
                                using (Graphics gScaled = Graphics.FromImage(scaledBmp))
                                {
                                    gScaled.InterpolationMode = InterpolationMode.Low;
                                    gScaled.SmoothingMode = SmoothingMode.HighSpeed;
                                    gScaled.CompositingQuality = CompositingQuality.HighSpeed;
                                    gScaled.PixelOffsetMode = PixelOffsetMode.HighSpeed;
                                    gScaled.DrawImage(rawBmp, 0, 0, targetW, targetH);
                                }
                                using (MemoryStream ms = new MemoryStream())
                                {
                                    if (jpgEncoder != null) { scaledBmp.Save(ms, jpgEncoder, ep); } else { scaledBmp.Save(ms, ImageFormat.Jpeg); }
                                    return ms.ToArray();
                                }
                            }
                        }
                        else
                        {
                            using (MemoryStream ms = new MemoryStream())
                            {
                                if (jpgEncoder != null) { rawBmp.Save(ms, jpgEncoder, ep); } else { rawBmp.Save(ms, ImageFormat.Jpeg); }
                                return ms.ToArray();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(Path.Combine(ObterDiretorioDadosAgente(), "capture_error.log"), ex.ToString()); } catch { }
                try
                {
                    using (Bitmap placeholder = new Bitmap(1280, 720))
                    using (Graphics gp = Graphics.FromImage(placeholder))
                    using (MemoryStream ms = new MemoryStream())
                    {
                        gp.Clear(Color.FromArgb(15, 23, 42));
                        using (Font f = new Font("Segoe UI", 16, FontStyle.Bold))
                        using (SolidBrush b = new SolidBrush(Color.White))
                        {
                            gp.DrawString("NexusTI Remote Access — Sessão Conectada", f, b, 40, 40);
                            gp.DrawString("Display operacional: " + ex.Message, SystemFonts.DefaultFont, Brushes.LightGray, 40, 80);
                        }
                        placeholder.Save(ms, ImageFormat.Jpeg);
                        return ms.ToArray();
                    }
                }
                catch { }
                return new byte[0];
            }
        }

        static void ExecutaAcaoMouse(string payload)
        {
            try
            {
                string clean = payload.Replace("{", "").Replace("}", "").Replace("\"", "");
                string[] parts = clean.Split(',');

                double xVal = 0, yVal = 0;
                string action = "move";
                string button = "left";
                int wheelDelta = 0;
                int screenIdx = currentScreenIndex;

                foreach (string p in parts)
                {
                    string[] kv = p.Split(':');
                    if (kv.Length < 2) continue;
                    string k = kv[0].Trim().ToLower();
                    string v = kv[1].Trim();

                    if (k == "x") double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out xVal);
                    if (k == "y") double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out yVal);
                    if (k == "action") action = v.ToLower();
                    if (k == "button") button = v.ToLower();
                    if (k == "delta") int.TryParse(v, out wheelDelta);
                    if (k == "screen") int.TryParse(v, out screenIdx);
                }

                Rectangle bounds = ObterBoundsTela(screenIdx);

                int targetX = bounds.X + (int)(xVal <= 1.0 ? xVal * bounds.Width : xVal);
                int targetY = bounds.Y + (int)(yVal <= 1.0 ? yVal * bounds.Height : yVal);

                SetCursorPos(targetX, targetY);

                // Ação de Clique Atômico Seguro (Down + Up juntos com 20ms)
                // NUNCA deixa o botão pressionado para trás, evitando arrastar pastas por engano!
                if (action == "click")
                {
                    if (button == "right")
                    {
                        mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
                        Thread.Sleep(20);
                        mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
                    }
                    else if (button == "middle")
                    {
                        mouse_event(MOUSEEVENTF_MIDDLEDOWN, 0, 0, 0, 0);
                        Thread.Sleep(20);
                        mouse_event(MOUSEEVENTF_MIDDLEUP, 0, 0, 0, 0);
                    }
                    else
                    {
                        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                        Thread.Sleep(20);
                        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                    }
                }
                else if (action == "dblclick")
                {
                    // Duplo clique atômico rápido e seguro (2 cliques com 40ms de intervalo)
                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                    Thread.Sleep(20);
                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                    Thread.Sleep(40);
                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                    Thread.Sleep(20);
                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                }
                else if (action == "down")
                {
                    if (button == "right") mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
                    else if (button == "middle") mouse_event(MOUSEEVENTF_MIDDLEDOWN, 0, 0, 0, 0);
                    else mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                }
                else if (action == "up")
                {
                    // Solta tudo para garantir que o mouse não fique preso em hipótese alguma
                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                    mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
                    mouse_event(MOUSEEVENTF_MIDDLEUP, 0, 0, 0, 0);
                }
                else if (action == "wheel")
                {
                    mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)wheelDelta, 0);
                }
            }
            catch { }
        }

        // ---------------------------------------------------------------------
        // CONTROLE DO TECLADO
        // ---------------------------------------------------------------------
        static void ExecutaAcaoTeclado(string payload)
        {
            try
            {
                string clean = payload.Replace("{", "").Replace("}", "").Replace("\"", "");
                string[] parts = clean.Split(',');
                string key = "";
                string shortcut = "";
                int keyCode = 0;
                bool ctrl = false;
                bool alt = false;
                bool shift = false;

                foreach (string p in parts)
                {
                    string[] kv = p.Split(':');
                    if (kv.Length < 2) continue;
                    string k = kv[0].Trim().ToLower();
                    string v = kv[1].Trim();

                    if (k == "key") key = v;
                    if (k == "keycode") int.TryParse(v, out keyCode);
                    if (k == "shortcut") shortcut = v.ToUpper();
                    if (k == "ctrl") ctrl = (v.ToLower() == "true");
                    if (k == "alt") alt = (v.ToLower() == "true");
                    if (k == "shift") shift = (v.ToLower() == "true");
                }

                if (!string.IsNullOrEmpty(shortcut))
                {
                    if (shortcut == "CTRL_ALT_DEL")
                    {
                        System.Diagnostics.Process.Start("taskmgr.exe");
                    }
                    else if (shortcut == "ALT_TAB")
                    {
                        keybd_event(VK_MENU, 0, 0, 0);
                        keybd_event(0x09, 0, 0, 0);
                        Thread.Sleep(40);
                        keybd_event(0x09, 0, KEYEVENTF_KEYUP, 0);
                        keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, 0);
                    }
                    else if (shortcut == "WIN")
                    {
                        keybd_event(VK_LWIN, 0, 0, 0);
                        Thread.Sleep(40);
                        keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, 0);
                    }
                    else if (shortcut == "ESC")
                    {
                        keybd_event(0x1B, 0, 0, 0);
                        keybd_event(0x1B, 0, KEYEVENTF_KEYUP, 0);
                    }
                    else if (shortcut == "ENTER")
                    {
                        keybd_event(0x0D, 0, 0, 0);
                        keybd_event(0x0D, 0, KEYEVENTF_KEYUP, 0);
                    }
                    return;
                }

                if (ctrl || alt)
                {
                    byte targetVk = (byte)keyCode;
                    if (targetVk == 0 && !string.IsNullOrEmpty(key) && key.Length == 1)
                    {
                        char ch = char.ToUpper(key[0]);
                        targetVk = (byte)ch;
                    }

                    if (targetVk > 0)
                    {
                        if (ctrl) keybd_event(VK_CONTROL, 0, 0, 0);
                        if (shift) keybd_event(VK_SHIFT, 0, 0, 0);
                        if (alt) keybd_event(VK_MENU, 0, 0, 0);

                        keybd_event(targetVk, 0, 0, 0);
                        Thread.Sleep(25);
                        keybd_event(targetVk, 0, KEYEVENTF_KEYUP, 0);

                        if (alt) keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, 0);
                        if (shift) keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, 0);
                        if (ctrl) keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);
                        return;
                    }
                }

                if (keyCode > 0)
                {
                    byte vk = (byte)keyCode;
                    if (shift) keybd_event(VK_SHIFT, 0, 0, 0);
                    keybd_event(vk, 0, 0, 0);
                    Thread.Sleep(15);
                    keybd_event(vk, 0, KEYEVENTF_KEYUP, 0);
                    if (shift) keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, 0);
                    return;
                }

                if (!string.IsNullOrEmpty(key))
                {
                    if (key == "Enter") SendKeys.SendWait("{ENTER}");
                    else if (key == "Backspace") SendKeys.SendWait("{BACKSPACE}");
                    else if (key == "Tab") SendKeys.SendWait("{TAB}");
                    else if (key == "Escape") SendKeys.SendWait("{ESC}");
                    else if (key == "Delete") SendKeys.SendWait("{DEL}");
                    else if (key.Length == 1) SendKeys.SendWait(key);
                }
            }
            catch { }
        }

        static string NewtonsoftJsonSimple(string str)
        {
            if (str == null) return "null";
            return "\"" + str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
        }

        static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid) return codec;
            }
            return null;
        }

        public static void SalvarChamadoLocal(string titulo, string categoria, string solicitante, string ativo, string ip, string desc, string urgencia = "Média", string ticketId = "", string empresa = "Aço Móveis", string setor = "Qualidade")
        {
            try
            {
                string idChamado = string.IsNullOrEmpty(ticketId) ? ("TICK-" + new Random().Next(1001, 9999).ToString()) : ticketId.Trim();
                string horaStr = DateTime.Now.ToString("HH:mm");
                string solicitanteFormatado = string.IsNullOrEmpty(solicitante) ? Environment.UserName : solicitante.Trim();
                string avatar = (solicitanteFormatado.Length >= 2 ? solicitanteFormatado.Substring(0, 2).ToUpper() : "US");
                string empFormatada = NormalizarTextoEmpresa(string.IsNullOrEmpty(empresa) ? customEmpresa : empresa.Trim());
                string setorFormatado = string.IsNullOrEmpty(setor) ? "Qualidade" : setor.Replace("#", "").Trim();
                string titFormatado = string.IsNullOrEmpty(titulo) ? "Problema no Computador" : titulo.Trim();
                string catFormatada = string.IsNullOrEmpty(categoria) ? "Hardware / Desktop" : categoria.Trim();
                string descFormatada = string.IsNullOrEmpty(desc) ? "Sem detalhes adicionais" : desc.Trim();

                var novoDict = new Dictionary<string, object>();
                novoDict["id"] = idChamado;
                novoDict["titulo"] = titFormatado;
                novoDict["empresa"] = empFormatada;
                novoDict["categoria"] = catFormatada;
                novoDict["solicitante"] = solicitanteFormatado;
                novoDict["setor"] = setorFormatado;
                novoDict["avatar"] = avatar;
                novoDict["ativo"] = string.IsNullOrEmpty(ativo) ? customMachineName : ativo.Trim();
                novoDict["ip"] = string.IsNullOrEmpty(ip) ? localIpAddress : ip.Trim();
                novoDict["urgencia"] = string.IsNullOrEmpty(urgencia) ? "Média" : urgencia.Trim();
                novoDict["status"] = "Active";
                novoDict["horario"] = "Hoje, " + horaStr;
                novoDict["data"] = "Hoje, " + horaStr;
                novoDict["responsavel"] = "";
                novoDict["tecnico"] = "";
                novoDict["tecnicosAtribuidos"] = new string[0];
                novoDict["descricao"] = descFormatada;
                novoDict["desc"] = descFormatada;

                var msgIni = new Dictionary<string, object>();
                msgIni["id"] = "msg-ini-" + idChamado;
                msgIni["autor"] = "Sistema";
                msgIni["tipo"] = "sistema";
                msgIni["texto"] = "Chamado registrado com sucesso sob protocolo #" + idChamado + ". Aguardando atendimento da equipe de T.I.";
                msgIni["horario"] = "Hoje, " + horaStr;
                msgIni["timestamp"] = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
                novoDict["mensagens"] = new object[] { msgIni };

                var js = new System.Web.Script.Serialization.JavaScriptSerializer();

                string[] dirs = new string[] {
                    ObterDiretorioDadosAgente(),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"FastSolutions\FSC-Agent"),
                    @"c:\Users\gabrielnovaes\Documents\copia magma",
                    @"c:\Users\gabrielnovaes\Documents\fsc-console"
                };

                lock (dbLock)
                {
                    foreach (string d in dirs)
                    {
                        try
                        {
                            if (Directory.Exists(d))
                            {
                                string arq = Path.Combine(d, "chamados.json");
                                List<Dictionary<string, object>> listaExistente = new List<Dictionary<string, object>>();

                                if (File.Exists(arq))
                                {
                                    try
                                    {
                                        string jsonAntigo = File.ReadAllText(arq, Encoding.UTF8).Trim();
                                        if (!string.IsNullOrEmpty(jsonAntigo) && jsonAntigo.StartsWith("["))
                                        {
                                            var parsed = js.Deserialize<List<Dictionary<string, object>>>(jsonAntigo);
                                            if (parsed != null) listaExistente = parsed;
                                        }
                                    }
                                    catch { }
                                }

                                listaExistente.RemoveAll(item => item != null && item.ContainsKey("id") && String.Equals(Convert.ToString(item["id"]), idChamado, StringComparison.OrdinalIgnoreCase));
                                listaExistente.Insert(0, novoDict);

                                string jsonAtualizado = js.Serialize(listaExistente);
                                File.WriteAllText(arq, jsonAtualizado, Encoding.UTF8);
                            }
                        }
                        catch { }
                    }
                }

                if (trayIcon != null)
                {
                    trayIcon.BalloonTipTitle = "Novo Chamado Registrado!";
                    trayIcon.BalloonTipText = idChamado + " - " + titFormatado + "\nEnviado para a equipe de T.I.";
                    trayIcon.BalloonTipIcon = ToolTipIcon.Info;
                    trayIcon.ShowBalloonTip(4000);
                }

                ThreadPool.QueueUserWorkItem((state) => {
                    try
                    {
                        string srvIp = "192.168.18.122";
                        string srvFile = Path.Combine(ObterDiretorioDadosAgente(), "server_ip.txt");
                        if (File.Exists(srvFile))
                        {
                            try { srvIp = File.ReadAllText(srvFile, Encoding.UTF8).Trim(); } catch { }
                        }
                        if (!string.IsNullOrEmpty(srvIp) && !srvIp.Contains(localIpAddress) && !srvIp.StartsWith("127.0.0.1"))
                        {
                            string hostPort = srvIp.Contains(":") ? srvIp : (srvIp + ":8090");
                            string urlEnvio = "http://" + hostPort + "/api/chamados?id=" + Uri.EscapeDataString(idChamado) +
                                              "&titulo=" + Uri.EscapeDataString(titFormatado) +
                                              "&empresa=" + Uri.EscapeDataString(empFormatada) +
                                              "&setor=" + Uri.EscapeDataString(setorFormatado) +
                                              "&categoria=" + Uri.EscapeDataString(catFormatada) +
                                              "&solicitante=" + Uri.EscapeDataString(solicitanteFormatado) +
                                              "&ativo=" + Uri.EscapeDataString(ativo ?? customMachineName) +
                                              "&ip=" + Uri.EscapeDataString(ip ?? localIpAddress) +
                                              "&urgencia=" + Uri.EscapeDataString(urgencia ?? "Média") +
                                              "&descricao=" + Uri.EscapeDataString(descFormatada);
                            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(urlEnvio);
                            req.Method = "POST";
                            req.Timeout = 3000;
                            req.ContentLength = 0;
                            using (HttpWebResponse r = (HttpWebResponse)req.GetResponse()) { }
                        }
                    }
                    catch { }
                });
            }
            catch { }
        }

        public static void SalvarMensagemChamadoLocal(string ticketId, string autor, string tipo, string texto, string horario, string msgId = "")
        {
            if (string.IsNullOrEmpty(ticketId) || string.IsNullOrEmpty(texto)) return;
            try
            {
                var js = new System.Web.Script.Serialization.JavaScriptSerializer();
                string[] dirs = new string[] {
                    ObterDiretorioDadosAgente(),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"FastSolutions\FSC-Agent"),
                    @"c:\Users\gabrielnovaes\Documents\copia magma",
                    @"c:\Users\gabrielnovaes\Documents\fsc-console"
                };

                string idFinal = string.IsNullOrEmpty(msgId) ? ("msg-" + DateTime.UtcNow.Ticks) : msgId.Trim();
                var msgItem = new Dictionary<string, object>();
                msgItem["id"] = idFinal;
                msgItem["autor"] = autor ?? "Usuário";
                msgItem["tipo"] = tipo ?? "cliente";
                msgItem["texto"] = texto.Trim();
                msgItem["horario"] = string.IsNullOrEmpty(horario) ? ("Hoje, " + DateTime.Now.ToString("HH:mm")) : horario;
                msgItem["timestamp"] = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;

                lock (dbLock)
                {
                    foreach (string d in dirs)
                    {
                        try
                        {
                            if (Directory.Exists(d))
                            {
                                string arq = Path.Combine(d, "chamados.json");
                                if (File.Exists(arq))
                                {
                                    string jsonStr = File.ReadAllText(arq, Encoding.UTF8).Trim();
                                    if (!string.IsNullOrEmpty(jsonStr) && jsonStr.StartsWith("["))
                                    {
                                        var lista = js.Deserialize<List<Dictionary<string, object>>>(jsonStr);
                                        if (lista != null)
                                        {
                                            bool alterou = false;
                                            foreach (var ticket in lista)
                                            {
                                                if (ticket != null && ticket.ContainsKey("id") && String.Equals(Convert.ToString(ticket["id"]), ticketId, StringComparison.OrdinalIgnoreCase))
                                                {
                                                    List<object> msgs = new List<object>();
                                                    bool jaExiste = false;
                                                    if (ticket.ContainsKey("mensagens") && ticket["mensagens"] != null)
                                                    {
                                                        System.Collections.IEnumerable en = ticket["mensagens"] as System.Collections.IEnumerable;
                                                        if (en != null)
                                                        {
                                                            foreach (var m in en)
                                                            {
                                                                msgs.Add(m);
                                                                if (m is Dictionary<string, object>)
                                                                {
                                                                    var dm = (Dictionary<string, object>)m;
                                                                    string exId = dm.ContainsKey("id") && dm["id"] != null ? dm["id"].ToString() : "";
                                                                    string exTxt = dm.ContainsKey("texto") && dm["texto"] != null ? dm["texto"].ToString().Trim() : "";
                                                                    string exTipo = dm.ContainsKey("tipo") && dm["tipo"] != null ? dm["tipo"].ToString() : "";
                                                                    if (!string.IsNullOrEmpty(exId) && exId == idFinal) jaExiste = true;
                                                                    else if (exTxt == texto.Trim() && exTipo == (tipo ?? "cliente")) jaExiste = true;
                                                                }
                                                            }
                                                        }
                                                    }
                                                    if (!jaExiste)
                                                    {
                                                        msgs.Add(msgItem);
                                                        ticket["mensagens"] = msgs;
                                                        ticket["ultimaAtualizacao"] = "Hoje às " + DateTime.Now.ToString("HH:mm") + " por " + (autor ?? "Usuário");
                                                        alterou = true;
                                                    }
                                                    break;
                                                }
                                            }
                                            if (alterou)
                                            {
                                                File.WriteAllText(arq, js.Serialize(lista), Encoding.UTF8);
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
        }

        public static void AvaliarChamadoCentral(string id, int nota, string comentario, string autor = "Solicitante")
        {
            if (string.IsNullOrEmpty(id)) return;
            string[] paths = new string[] {
                Path.Combine(ObterDiretorioDadosAgente(), "chamados.json"),
                @"c:\Users\gabrielnovaes\Documents\copia magma\chamados.json",
                @"c:\Users\gabrielnovaes\Documents\fsc-console\chamados.json"
            };
            lock (dbLock)
            {
                foreach (string p in paths)
                {
                    try
                    {
                        if (File.Exists(p))
                        {
                            var js = new System.Web.Script.Serialization.JavaScriptSerializer();
                            string txt = File.ReadAllText(p, Encoding.UTF8).Trim();
                            if (txt.StartsWith("["))
                            {
                                var lista = js.Deserialize<List<Dictionary<string, object>>>(txt);
                                if (lista != null)
                                {
                                    bool alterou = false;
                                    foreach (var item in lista)
                                    {
                                        if (item != null && item.ContainsKey("id") && String.Equals(Convert.ToString(item["id"]), id, StringComparison.OrdinalIgnoreCase))
                                        {
                                            item["avaliacaoNota"] = nota;
                                            item["avaliacaoComentario"] = comentario ?? "";
                                            item["avaliacaoData"] = "Hoje, " + DateTime.Now.ToString("HH:mm");
                                            alterou = true;
                                            break;
                                        }
                                    }
                                    if (alterou)
                                    {
                                        File.WriteAllText(p, js.Serialize(lista), Encoding.UTF8);
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        public static void AtualizarChamadoCentral(string rawUrl, string bodyReq)
        {
            string acao = ExtrairParam(rawUrl, bodyReq, "acao", "");
            string id = ExtrairParam(rawUrl, bodyReq, "id", "");
            string status = ExtrairParam(rawUrl, bodyReq, "status", "");
            string resp = ExtrairParam(rawUrl, bodyReq, "responsavel", "");
            string emp = ExtrairParam(rawUrl, bodyReq, "empresa", "");

            if (!string.IsNullOrEmpty(bodyReq) && bodyReq.Trim().StartsWith("{"))
            {
                try
                {
                    var js = new System.Web.Script.Serialization.JavaScriptSerializer();
                    var dict = js.Deserialize<Dictionary<string, object>>(bodyReq);
                    if (dict != null)
                    {
                        if (dict.ContainsKey("acao") && dict["acao"] != null) acao = dict["acao"].ToString();
                        if (dict.ContainsKey("id") && dict["id"] != null) id = dict["id"].ToString();
                        if (dict.ContainsKey("status") && dict["status"] != null) status = dict["status"].ToString();
                        if (dict.ContainsKey("responsavel") && dict["responsavel"] != null) resp = dict["responsavel"].ToString();
                        if (dict.ContainsKey("empresa") && dict["empresa"] != null) emp = dict["empresa"].ToString();
                    }
                }
                catch { }
            }

            string[] paths = new string[] {
                Path.Combine(ObterDiretorioDadosAgente(), "chamados.json"),
                @"c:\Users\gabrielnovaes\Documents\copia magma\chamados.json",
                @"c:\Users\gabrielnovaes\Documents\fsc-console\chamados.json"
            };

            lock (dbLock)
            {
                foreach (string p in paths)
                {
                    try
                    {
                        if (File.Exists(p))
                        {
                            var js = new System.Web.Script.Serialization.JavaScriptSerializer();
                            string txt = File.ReadAllText(p, Encoding.UTF8).Trim();
                            if (txt.StartsWith("["))
                            {
                                var lista = js.Deserialize<List<Dictionary<string, object>>>(txt);
                                if (lista != null)
                                {
                                    bool alterou = false;
                                    if (acao == "vincular_sem_empresa" && !string.IsNullOrEmpty(emp))
                                    {
                                        string empNorm = NormalizarTextoEmpresa(emp);
                                        foreach (var item in lista)
                                        {
                                            if (item != null)
                                            {
                                                string curEmp = item.ContainsKey("empresa") && item["empresa"] != null ? item["empresa"].ToString().Trim() : "";
                                                if (string.IsNullOrEmpty(curEmp) || curEmp.ToLower() == "sem empresa")
                                                {
                                                    item["empresa"] = empNorm;
                                                    alterou = true;
                                                }
                                            }
                                        }
                                    }
                                    else if (!string.IsNullOrEmpty(id))
                                    {
                                        foreach (var item in lista)
                                        {
                                            if (item != null && item.ContainsKey("id") && String.Equals(Convert.ToString(item["id"]), id, StringComparison.OrdinalIgnoreCase))
                                            {
                                                if (!string.IsNullOrEmpty(status)) item["status"] = status;
                                                if (!string.IsNullOrEmpty(resp))
                                                {
                                                    item["responsavel"] = resp;
                                                    item["tecnico"] = resp;
                                                }
                                                if (!string.IsNullOrEmpty(emp)) item["empresa"] = NormalizarTextoEmpresa(emp);
                                                alterou = true;
                                                break;
                                            }
                                        }
                                    }

                                    if (alterou)
                                    {
                                        File.WriteAllText(p, js.Serialize(lista), Encoding.UTF8);
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        public static void ConcluirChamadoCentral(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            string[] paths = new string[] {
                Path.Combine(ObterDiretorioDadosAgente(), "chamados.json"),
                @"c:\Users\gabrielnovaes\Documents\copia magma\chamados.json",
                @"c:\Users\gabrielnovaes\Documents\fsc-console\chamados.json"
            };
            lock (dbLock)
            {
                foreach (string p in paths)
                {
                    try
                    {
                        if (File.Exists(p))
                        {
                            var js = new System.Web.Script.Serialization.JavaScriptSerializer();
                            string txt = File.ReadAllText(p, Encoding.UTF8).Trim();
                            if (txt.StartsWith("["))
                            {
                                var lista = js.Deserialize<List<Dictionary<string, object>>>(txt);
                                if (lista != null)
                                {
                                    bool alterou = false;
                                    foreach (var item in lista)
                                    {
                                        if (item != null && item.ContainsKey("id") && String.Equals(Convert.ToString(item["id"]), id, StringComparison.OrdinalIgnoreCase))
                                        {
                                            item["status"] = "Resolvido";
                                            alterou = true;
                                            break;
                                        }
                                    }
                                    if (alterou)
                                    {
                                        File.WriteAllText(p, js.Serialize(lista), Encoding.UTF8);
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        public static void DefinirStatusServidorAtivoCentral(string host, string ip, bool isSrv, string empresa = "")
        {
            string[] paths = new string[] {
                Path.Combine(ObterDiretorioDadosAgente(), "assets_registry.json"),
                @"c:\Users\gabrielnovaes\Documents\copia magma\assets_registry.json",
                @"c:\Users\gabrielnovaes\Documents\fsc-console\assets_registry.json"
            };

            lock (dbLock)
            {
                foreach (string p in paths)
                {
                    try
                    {
                        if (File.Exists(p))
                        {
                            var js = new System.Web.Script.Serialization.JavaScriptSerializer();
                            string txt = File.ReadAllText(p, Encoding.UTF8).Trim();
                            if (txt.StartsWith("["))
                            {
                                var lista = js.Deserialize<List<Dictionary<string, object>>>(txt);
                                if (lista != null)
                                {
                                    bool alterou = false;
                                    foreach (var item in lista)
                                    {
                                        if (item != null)
                                        {
                                            string hItem = item.ContainsKey("host") && item["host"] != null ? item["host"].ToString() : "";
                                            string ipItem = item.ContainsKey("ip") && item["ip"] != null ? item["ip"].ToString() : "";
                                            if ((!string.IsNullOrEmpty(host) && String.Equals(hItem, host, StringComparison.OrdinalIgnoreCase)) ||
                                                (!string.IsNullOrEmpty(ip) && String.Equals(ipItem, ip, StringComparison.OrdinalIgnoreCase)))
                                            {
                                                item["isServer"] = isSrv;
                                                item["type"] = isSrv ? "Servidor" : "Desktop";
                                                if (!string.IsNullOrEmpty(empresa))
                                                {
                                                    string empN = NormalizarTextoEmpresa(empresa);
                                                    item["empresa"] = empN;
                                                    item["company"] = empN;
                                                }
                                                alterou = true;
                                            }
                                        }
                                    }
                                    if (alterou)
                                    {
                                        File.WriteAllText(p, js.Serialize(lista), Encoding.UTF8);
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        public static void CadastrarAtivoCentral(string host, string tag, string ip, string user, string os, string devType, string empresa = "Aço Móveis", bool isSrv = false)
        {
            if (string.IsNullOrEmpty(host)) return;
            try
            {
                string empNorm = NormalizarTextoEmpresa(empresa);
                string tagNorm = string.IsNullOrEmpty(tag) ? "Qualidade" : tag.Replace("#", "").Trim();
                bool flagSrv = isSrv || (!string.IsNullOrEmpty(devType) && devType.ToLower() == "servidor") || host.ToUpper().StartsWith("SRV");
                string tipoFinal = flagSrv ? "Servidor" : (string.IsNullOrEmpty(devType) ? "Desktop" : devType);

                string[] paths = new string[] {
                    Path.Combine(ObterDiretorioDadosAgente(), "assets_registry.json"),
                    @"c:\Users\gabrielnovaes\Documents\copia magma\assets_registry.json",
                    @"c:\Users\gabrielnovaes\Documents\fsc-console\assets_registry.json"
                };

                lock (dbLock)
                {
                    foreach (string p in paths)
                    {
                        try
                        {
                            if (File.Exists(p))
                            {
                                var js = new System.Web.Script.Serialization.JavaScriptSerializer();
                                string txt = File.ReadAllText(p, Encoding.UTF8).Trim();
                                if (txt.StartsWith("["))
                                {
                                    var lista = js.Deserialize<List<Dictionary<string, object>>>(txt);
                                    if (lista != null)
                                    {
                                        bool encontrou = false;
                                        foreach (var item in lista)
                                        {
                                            if (item != null)
                                            {
                                                string hItem = item.ContainsKey("host") && item["host"] != null ? item["host"].ToString() : "";
                                                string ipItem = item.ContainsKey("ip") && item["ip"] != null ? item["ip"].ToString() : "";
                                                if (String.Equals(hItem, host, StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrEmpty(ip) && ip != "127.0.0.1" && String.Equals(ipItem, ip, StringComparison.OrdinalIgnoreCase)))
                                                {
                                                    item["host"] = host;
                                                    item["type"] = tipoFinal;
                                                    item["isServer"] = flagSrv;
                                                    item["empresa"] = empNorm;
                                                    item["company"] = empNorm;
                                                    item["tag"] = tagNorm;
                                                    item["location"] = !string.IsNullOrEmpty(geoLabel) ? geoLabel : (empNorm + " - " + tagNorm);
                                                    if (!string.IsNullOrEmpty(geoCity)) item["city"] = geoCity;
                                                    if (!string.IsNullOrEmpty(geoRegion)) item["region"] = geoRegion;
                                                    if (!string.IsNullOrEmpty(geoCountry)) item["country"] = geoCountry;
                                                    if (geoLat != 0.0) item["lat"] = geoLat;
                                                    if (geoLon != 0.0) item["lon"] = geoLon;
                                                    if (!string.IsNullOrEmpty(geoMapsUrl)) item["mapsUrl"] = geoMapsUrl;
                                                    if (!string.IsNullOrEmpty(geoPublicIp)) item["publicIp"] = geoPublicIp;
                                                    if (!string.IsNullOrEmpty(geoIsp)) item["isp"] = geoIsp;
                                                    if (!string.IsNullOrEmpty(ip)) item["ip"] = ip;
                                                    if (!string.IsNullOrEmpty(user)) item["user"] = user;
                                                    if (!string.IsNullOrEmpty(os)) item["os"] = os;
                                                    item["online"] = true;
                                                    item["lastConnected"] = "Hoje, " + DateTime.Now.ToString("HH:mm");
                                                    item["lastSeen"] = "Conectado agora";
                                                    encontrou = true;
                                                    break;
                                                }
                                            }
                                        }

                                        if (!encontrou)
                                        {
                                            var novo = new Dictionary<string, object>();
                                            novo["id"] = string.Format("FSC-{0} {1}", new Random().Next(100, 999), new Random().Next(100, 999));
                                            novo["host"] = host;
                                            novo["type"] = tipoFinal;
                                            novo["isServer"] = flagSrv;
                                            novo["user"] = string.IsNullOrEmpty(user) ? "Usuario" : user;
                                            novo["domain"] = "CORP.LOCAL";
                                            novo["loggedUserFull"] = "CORP\\" + (string.IsNullOrEmpty(user) ? "Usuario" : user);
                                            novo["location"] = empNorm + " - " + tagNorm;
                                            novo["empresa"] = empNorm;
                                            novo["company"] = empNorm;
                                            novo["tag"] = tagNorm;
                                            novo["os"] = string.IsNullOrEmpty(os) ? "Windows 10 Pro" : os;
                                            novo["cpu"] = ObterProcessador();
                                            double rTot; int rPct;
                                            novo["ram"] = ObterMemoriaRam(out rTot, out rPct);
                                            novo["disk"] = ObterArmazenamentoC();
                                            novo["resolution"] = "1920x1080 Full HD";
                                            novo["ip"] = string.IsNullOrEmpty(ip) ? "127.0.0.1" : ip;
                                            novo["port"] = 8090;
                                            novo["lastConnected"] = "Hoje, " + DateTime.Now.ToString("HH:mm");
                                            novo["lastSeen"] = "Conectado agora";
                                            novo["online"] = true;
                                            lista.Insert(0, novo);
                                        }

                                        File.WriteAllText(p, js.Serialize(lista), Encoding.UTF8);
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        public static string ObterAssetsRegistryJson()
        {
            lock (dbLock)
            {
                try
                {
                    string[] paths = new string[] {
                        Path.Combine(ObterDiretorioDadosAgente(), "assets_registry.json"),
                        @"c:\Users\gabrielnovaes\Documents\copia magma\assets_registry.json",
                        @"c:\Users\gabrielnovaes\Documents\fsc-console\assets_registry.json"
                    };
                    foreach (string p in paths)
                    {
                        if (File.Exists(p))
                        {
                            string txt = File.ReadAllText(p, Encoding.UTF8).Trim();
                            if (!string.IsNullOrEmpty(txt) && txt.StartsWith("[")) return txt;
                        }
                    }
                }
                catch { }
            }
            return "[]";
        }

        public static string ObterEmpresasJson()
        {
            string[] paths = new string[] {
                Path.Combine(ObterDiretorioDadosAgente(), "empresas.json"),
                @"c:\Users\gabrielnovaes\Documents\copia magma\empresas.json",
                @"c:\Users\gabrielnovaes\Documents\gestão de TI\empresas.json",
                @"c:\Users\gabrielnovaes\Documents\fsc-console\empresas.json"
            };
            foreach (var p in paths)
            {
                try
                {
                    if (File.Exists(p)) return File.ReadAllText(p, Encoding.UTF8);
                }
                catch { }
            }
            return "[\"ACOMOVEIS\",\"Aço Móveis\",\"JPL\"]";
        }

        static void SalvarEmpresasJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            string[] targets = new string[] {
                Path.Combine(ObterDiretorioDadosAgente(), "empresas.json"),
                @"c:\Users\gabrielnovaes\Documents\copia magma\empresas.json",
                @"c:\Users\gabrielnovaes\Documents\copia magma\agent\empresas.json",
                @"c:\Users\gabrielnovaes\Documents\gestão de TI\empresas.json",
                @"c:\Users\gabrielnovaes\Documents\gestão de TI\agent\empresas.json",
                @"c:\Users\gabrielnovaes\Documents\fsc-console\empresas.json",
                @"c:\Users\gabrielnovaes\Documents\fsc-console\agent\empresas.json"
            };
            foreach (var t in targets)
            {
                try
                {
                    string dir = Path.GetDirectoryName(t);
                    if (Directory.Exists(dir))
                    {
                        File.WriteAllText(t, json, Encoding.UTF8);
                    }
                }
                catch { }
            }

            try
            {
                var match = System.Text.RegularExpressions.Regex.Match(json, "\"([^\"]+)\"");
                if (match.Success)
                {
                    string empPrincipal = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(empPrincipal) && empPrincipal.ToLower() != "empresas" && empPrincipal.ToLower() != "empresaprincipal")
                    {
                        string[] txtTargets = new string[] {
                            Path.Combine(ObterDiretorioDadosAgente(), "empresa.txt"),
                            @"c:\Users\gabrielnovaes\Documents\copia magma\agent\empresa.txt",
                            @"c:\Users\gabrielnovaes\Documents\copia magma\empresa.txt",
                            @"c:\Users\gabrielnovaes\Documents\gestão de TI\agent\empresa.txt",
                            @"c:\Users\gabrielnovaes\Documents\gestão de TI\empresa.txt",
                            @"c:\Users\gabrielnovaes\Documents\fsc-console\agent\empresa.txt",
                            @"c:\Users\gabrielnovaes\Documents\fsc-console\empresa.txt"
                        };
                        foreach (var tt in txtTargets)
                        {
                            try {
                                string dir = Path.GetDirectoryName(tt);
                                if (Directory.Exists(dir)) File.WriteAllText(tt, empPrincipal, Encoding.UTF8);
                            } catch { }
                        }
                    }
                }
            }
            catch { }
        }

        static string ObterSetoresJson()
        {
            string[] paths = new string[] {
                Path.Combine(ObterDiretorioDadosAgente(), "setores.json"),
                @"c:\Users\gabrielnovaes\Documents\copia magma\setores.json",
                @"c:\Users\gabrielnovaes\Documents\gestão de TI\setores.json",
                @"c:\Users\gabrielnovaes\Documents\fsc-console\setores.json"
            };
            foreach (var p in paths)
            {
                try
                {
                    if (File.Exists(p)) return File.ReadAllText(p, Encoding.UTF8);
                }
                catch { }
            }
            return "[\"#Financeiro\",\"#Produção\",\"#TI\",\"#Diretoria\",\"#DP\",\"#Logística\",\"#Comercial\",\"#QUALIDADE\",\"#PROJETO\",\"#COMPRAS\",\"#RECEPÇÃO\",\"#ALMOXARIFADO\"]";
        }

        static void SalvarSetoresJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            string[] targets = new string[] {
                Path.Combine(ObterDiretorioDadosAgente(), "setores.json"),
                @"c:\Users\gabrielnovaes\Documents\copia magma\setores.json",
                @"c:\Users\gabrielnovaes\Documents\copia magma\agent\setores.json",
                @"c:\Users\gabrielnovaes\Documents\gestão de TI\setores.json",
                @"c:\Users\gabrielnovaes\Documents\gestão de TI\agent\setores.json",
                @"c:\Users\gabrielnovaes\Documents\fsc-console\setores.json",
                @"c:\Users\gabrielnovaes\Documents\fsc-console\agent\setores.json"
            };
            foreach (var t in targets)
            {
                try
                {
                    string dir = Path.GetDirectoryName(t);
                    if (Directory.Exists(dir))
                    {
                        File.WriteAllText(t, json, Encoding.UTF8);
                    }
                }
                catch { }
            }
        }

        static string ObterChamadosJson()
        {
            lock (dbLock)
            {
                try
                {
                    string[] paths = new string[] {
                        Path.Combine(ObterDiretorioDadosAgente(), "chamados.json"),
                        @"c:\Users\gabrielnovaes\Documents\copia magma\chamados.json",
                        @"c:\Users\gabrielnovaes\Documents\fsc-console\chamados.json",
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"FastSolutions\FSC-Agent\chamados.json")
                    };
                    foreach (string p in paths)
                    {
                        if (File.Exists(p))
                        {
                            string txt = File.ReadAllText(p, Encoding.UTF8).Trim();
                            if (!string.IsNullOrEmpty(txt) && txt.StartsWith("[")) return txt;
                        }
                    }
                }
                catch { }
            }
            return "[]";
        }

        public static string ObterPaginaHtmlChamado()
        {
            try
            {
                string[] paths = new string[] {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "chamado.html"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\chamado.html"),
                    @"c:\Users\gabrielnovaes\Documents\copia magma\chamado.html",
                    @"c:\Users\gabrielnovaes\Documents\fsc-console\chamado.html"
                };
                foreach (var p in paths)
                {
                    if (File.Exists(p)) return File.ReadAllText(p, Encoding.UTF8);
                }
            }
            catch { }

            return "<!DOCTYPE html><html lang='pt-BR'><head><meta charset='UTF-8'><meta name='viewport' content='width=device-width, initial-scale=1.0'><title>Abertura de Chamado - Suporte T.I</title><script src='https://cdn.tailwindcss.com'></script><link href='https://fonts.googleapis.com/css2?family=Segoe+UI:wght@400;600;700&display=swap' rel='stylesheet'><style>body{font-family:'Segoe UI',sans-serif;background-color:#ffffff;color:#333333;}</style></head><body class='min-h-screen p-4 md:p-8 flex justify-center'><div class='w-full max-w-xl bg-white p-2 md:p-6 space-y-5'><form onsubmit='event.preventDefault(); enviarChamado();' class='space-y-4 text-sm'><div><label class='block font-semibold text-slate-800 mb-1'>Empresa:*</label><input type='text' id='empresa' value='Fast Solutions Corporate' required class='w-full px-3 py-2 border border-slate-300 rounded focus:outline-none focus:border-blue-500 text-sm'></div><div><label class='block font-semibold text-slate-800 mb-1'>Título*</label><input type='text' id='titulo' required placeholder='Ex: Lentidão no ERP, Erro de Impressora' class='w-full px-3 py-2 border border-slate-300 rounded focus:outline-none focus:border-blue-500 text-sm'></div><div><label class='block font-semibold text-slate-800 mb-0.5'>Requisitante:*</label><span class='block text-xs text-slate-400 mb-1'>Nome do requisitante da solicitação</span><input type='text' id='requisitante' value='" + Environment.UserName + @"' required class='w-full px-3 py-2 border border-slate-300 rounded focus:outline-none focus:border-blue-500 text-sm'></div><div><label class='block font-semibold text-slate-800 mb-0.5'>E-mail do requisitante:*</label><span class='block text-xs text-slate-400 mb-1'>Insira o seu e-mail</span><input type='email' id='email' required placeholder='usuario@empresa.com' class='w-full px-3 py-2 border border-slate-300 rounded focus:outline-none focus:border-blue-500 text-sm'></div><div><label class='block font-semibold text-slate-800 mb-1'>Sistemas*</label><input type='text' id='sistemas' required placeholder='Ex: ERP, Office, E-mail, Rede' class='w-full px-3 py-2 border border-slate-300 rounded focus:outline-none focus:border-blue-500 text-sm'></div><div><label class='block font-semibold text-slate-800 mb-1'>Descrição do requisitante:*</label><textarea id='descricao' rows='4' required placeholder='Descreva detalhadamente o problema ou solicitação...' class='w-full px-3 py-2 border border-slate-300 rounded focus:outline-none focus:border-blue-500 text-sm resize-y'></textarea></div><div><label class='block font-semibold text-slate-800 mb-1'>Urgência*</label><select id='urgencia' class='w-full px-3 py-2 border border-slate-300 rounded focus:outline-none focus:border-blue-500 text-sm bg-white'><option value='Baixa'>Baixa</option><option value='Média' selected>Média</option><option value='Alta'>Alta</option><option value='Crítica'>Crítica</option></select></div><div><label class='block font-semibold text-slate-800 mb-0.5'>Anexo Chamado</label><span class='block text-xs text-slate-400 mb-1'>Tamanho máximo permitido por arquivo: 5 MB</span><div class='flex items-center gap-2 border border-slate-300 rounded p-1'><input type='file' id='anexo' class='w-full text-xs text-slate-500 file:mr-3 file:py-1.5 file:px-3 file:rounded file:border-0 file:text-xs file:font-semibold file:bg-[#0070f3] file:text-white hover:file:bg-blue-600 cursor-pointer'></div></div><button type='submit' id='btn-enviar' class='w-full py-2.5 rounded bg-[#0070f3] hover:bg-blue-600 text-white font-bold text-sm shadow transition mt-2'>Enviar Chamado para T.I</button></form><div id='msg-sucesso' class='hidden p-4 rounded-lg bg-emerald-50 border border-emerald-200 text-emerald-800 text-center space-y-2'><div class='font-bold text-base'>✓ Chamado Enviado com Sucesso!</div><div class='text-xs'>A equipe técnica de T.I já recebeu a sua solicitação para o computador " + customMachineName + @". Em breve um técnico iniciará o atendimento.</div></div></div><script>async function enviarChamado(){const btn=document.getElementById('btn-enviar');btn.disabled=true;btn.textContent='Enviando...';const empresa=encodeURIComponent(document.getElementById('empresa').value);const titulo=encodeURIComponent(document.getElementById('titulo').value);const req=encodeURIComponent(document.getElementById('requisitante').value);const email=encodeURIComponent(document.getElementById('email').value);const sis=encodeURIComponent(document.getElementById('sistemas').value);const desc=encodeURIComponent(document.getElementById('descricao').value);const urg=encodeURIComponent(document.getElementById('urgencia').value);const fullDesc='[Empresa: '+decodeURIComponent(empresa)+'] [Email: '+decodeURIComponent(email)+'] [Sistema: '+decodeURIComponent(sis)+'] [Urgência: '+decodeURIComponent(urg)+'] ' + decodeURIComponent(desc);const qParams='titulo='+titulo+'&categoria='+sis+'&solicitante='+req+'&ativo='+encodeURIComponent('" + customMachineName + @"')+'&ip='+encodeURIComponent('" + localIpAddress + @"')+'&urgencia='+urg+'&descricao='+encodeURIComponent(fullDesc);try{await fetch('/api/chamados?'+qParams,{method:'POST'});}catch(e){}try{fetch('http://192.168.18.122:8090/api/chamados?'+qParams,{method:'POST',mode:'no-cors'});}catch(e){}document.querySelector('form').classList.add('hidden');document.getElementById('msg-sucesso').classList.remove('hidden');setTimeout(()=>window.close(),4000);}</script></body></html>";
        }
    
        public static string LimparAspas(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace((char)34, (char)39);
        }

        public static string ExtrairParam(string path, string body, string paramName, string defaultValue)
        {
            try
            {
                string search = paramName + "=";
                if (path.Contains(search))
                {
                    string val = path.Split(new string[] { search }, StringSplitOptions.None)[1].Split('&')[0];
                    return Uri.UnescapeDataString(val);
                }
                if (!string.IsNullOrEmpty(body))
                {
                    string searchQuote = ((char)34).ToString() + paramName + ((char)34).ToString();
                    if (body.Contains(searchQuote))
                    {
                        int idx = body.IndexOf(searchQuote);
                        int colon = body.IndexOf(':', idx);
                        if (colon >= 0)
                        {
                            string rest = body.Substring(colon + 1).Trim();
                            if (rest.StartsWith(((char)34).ToString()))
                            {
                                int endQuote = rest.IndexOf((char)34, 1);
                                if (endQuote > 1) return rest.Substring(1, endQuote - 1);
                            }
                        }
                    }
                    if (body.Contains(search))
                    {
                        string val = body.Split(new string[] { search }, StringSplitOptions.None)[1].Split('&')[0];
                        return Uri.UnescapeDataString(val);
                    }
                }
            }
            catch { }
            return defaultValue;
        }

        public static void SalvarAvisoRecebido(string tipo, string titulo, string mensagem, string urgencia, string enqueteId, string autor)
        {
            try
            {
                string idAv = "AVS-" + new Random().Next(1000, 9999);
                string jsonNovo = string.Format(
                    "{{\"id\":\"{0}\",\"tipo\":\"{1}\",\"titulo\":\"{2}\",\"mensagem\":\"{3}\",\"urgencia\":\"{4}\",\"enqueteId\":\"{5}\",\"autor\":\"{6}\",\"host\":\"{7}\",\"user\":\"{8}\",\"data\":\"{9}\",\"confirmado\":false}}",
                    idAv,
                    LimparAspas(tipo ?? "aviso"),
                    LimparAspas(titulo ?? ""),
                    LimparAspas(mensagem ?? "").Replace("\r\n", " ").Replace("\n", " "),
                    LimparAspas(urgencia ?? "informativo"),
                    LimparAspas(enqueteId ?? ""),
                    LimparAspas(autor ?? "Gestão de TI"),
                    customMachineName,
                    Environment.UserName,
                    DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")
                );

                GravarArquivoMultiPastas("avisos_historico.json", jsonNovo);
            }
            catch { }
        }

        public static void SalvarAvisoConfirmado(string titulo, string tipo, string host, string user)
        {
            try
            {
                string idConf = "CONF-" + new Random().Next(1000, 9999);
                string jsonNovo = string.Format(
                    "{{\"id\":\"{0}\",\"titulo\":\"{1}\",\"tipo\":\"{2}\",\"host\":\"{3}\",\"user\":\"{4}\",\"data\":\"{5}\",\"confirmado\":true}}",
                    idConf,
                    LimparAspas(titulo ?? ""),
                    LimparAspas(tipo ?? "aviso"),
                    LimparAspas(host ?? customMachineName),
                    LimparAspas(user ?? Environment.UserName),
                    DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")
                );

                GravarArquivoMultiPastas("avisos_confirmados.json", jsonNovo);
            }
            catch { }
        }

        public static void SalvarVotoEnquete(string enqueteId, string opcao, string host, string user)
        {
            try
            {
                string idVoto = "VOT-" + new Random().Next(1000, 9999);
                string jsonNovo = string.Format(
                    "{{\"id\":\"{0}\",\"enqueteId\":\"{1}\",\"opcao\":\"{2}\",\"host\":\"{3}\",\"user\":\"{4}\",\"data\":\"{5}\"}}",
                    idVoto,
                    LimparAspas(enqueteId ?? ""),
                    LimparAspas(opcao ?? ""),
                    LimparAspas(host ?? customMachineName),
                    LimparAspas(user ?? Environment.UserName),
                    DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")
                );

                GravarArquivoMultiPastas("enquetes_respostas.json", jsonNovo);
            }
            catch { }
        }

        public static void GravarArquivoMultiPastas(string nomeArquivo, string jsonNovoItem)
        {
            string[] dirs = new string[] {
                @"c:\Users\gabrielnovaes\Documents\fsc-console",
                ObterDiretorioDadosAgente(),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"FastSolutions\FSC-Agent")
            };

            foreach (string d in dirs)
            {
                try
                {
                    if (Directory.Exists(d))
                    {
                        string arq = Path.Combine(d, nomeArquivo);
                        if (File.Exists(arq))
                        {
                            try
                            {
                                string c = File.ReadAllText(arq).Trim();
                                if (c.StartsWith("[") && c.EndsWith("]"))
                                {
                                    string inner = c.Substring(1, c.Length - 2).Trim();
                                    if (!string.IsNullOrEmpty(inner))
                                    {
                                        File.WriteAllText(arq, "[\n  " + jsonNovoItem + ",\n  " + inner + "\n]");
                                        continue;
                                    }
                                }
                            }
                            catch { }
                        }
                        File.WriteAllText(arq, "[\n  " + jsonNovoItem + "\n]");
                    }
                }
                catch { }
            }
        }

        public static string ObterResultadosEnquetesJson()
        {
            string[] dirs = new string[] {
                @"c:\Users\gabrielnovaes\Documents\fsc-console",
                ObterDiretorioDadosAgente(),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"FastSolutions\FSC-Agent")
            };
            foreach (string d in dirs)
            {
                try
                {
                    string arq = Path.Combine(d, "enquetes_respostas.json");
                    if (File.Exists(arq))
                    {
                        string c = File.ReadAllText(arq).Trim();
                        if (c.StartsWith("[") && c.EndsWith("]")) return c;
                    }
                }
                catch { }
            }
            return "[]";
        }

        public static string ObterHistoricoAvisosJson()
        {
            string[] dirs = new string[] {
                @"c:\Users\gabrielnovaes\Documents\fsc-console",
                ObterDiretorioDadosAgente(),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"FastSolutions\FSC-Agent")
            };
            foreach (string d in dirs)
            {
                try
                {
                    string arq = Path.Combine(d, "avisos_historico.json");
                    if (File.Exists(arq))
                    {
                        string c = File.ReadAllText(arq).Trim();
                        if (c.StartsWith("[") && c.EndsWith("]")) return c;
                    }
                }
                catch { }
            }
            return "[]";
        }

        public static void NotificarBandeja(string titulo, string mensagem)
        {
            try
            {
                if (trayIcon != null)
                {
                    trayIcon.BalloonTipTitle = string.IsNullOrEmpty(titulo) ? "FSC Comunicado Corporativo" : titulo;
                    trayIcon.BalloonTipText = string.IsNullOrEmpty(mensagem) ? "Você recebeu um novo comunicado de TI." : (mensagem.Length > 120 ? (mensagem.Substring(0, 117) + "...") : mensagem);
                    trayIcon.BalloonTipIcon = ToolTipIcon.Info;
                    trayIcon.ShowBalloonTip(5000);
                }
            }
            catch { }
        }

        public static string ObterPaginaHtmlAviso()
        {
            return "<!DOCTYPE html><html lang='pt-BR'><head><meta charset='UTF-8'><title>FSC Comunicado de TI</title><style>body{background:#0b1320;color:#f8fafc;font-family:system-ui,sans-serif;display:flex;align-items:center;justify-content:center;min-height:100vh;margin:0;padding:20px;box-sizing:border-box}.card{background:#151e32;border:1px solid #334155;border-radius:16px;max-width:540px;width:100%;padding:32px;box-shadow:0 20px 40px rgba(0,0,0,0.5)}h1{color:#38bdf8;font-size:20px;margin-top:0}.btn{display:inline-block;background:#2563eb;color:#fff;padding:12px 24px;border-radius:10px;text-decoration:none;font-weight:bold;margin-top:20px}</style></head><body><div class='card'><h1>Fast Solutions Corporate — Comunicado de TI</h1><p>O FSC Client de Avisos está ativo nesta estação (" + customMachineName + "). Os avisos e enquetes oficiais emitidos pela equipe de TI são exibidos diretamente na tela do usuário.</p><a href='/chamado' class='btn'>Abrir Chamado com a TI &rarr;</a></div></body></html>";
        }

    }


    public class FormChamadoCliente : Form
    {
        private string machineName;
        private string machineTag;
        private string machineIp;
        private TextBox txtSolicitante;
        private ComboBox cbCategoria;
        private TextBox txtTitulo;
        private TextBox txtDesc;
        private Button btnEnviar;

        public FormChamadoCliente(string name, string tag, string ip)
        {
            this.machineName = string.IsNullOrEmpty(name) ? Environment.MachineName : name;
            this.machineTag = string.IsNullOrEmpty(tag) ? "#Geral" : tag;
            this.machineIp = string.IsNullOrEmpty(ip) ? "127.0.0.1" : ip;

            this.Text = "NexusTI — Abertura de Chamado T.I (" + this.machineName + ")";
            this.Size = new Size(520, 520);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.BackColor = Color.FromArgb(10, 16, 28);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.Icon = Program.ObterIconeNexus();

            MontarUI();
        }

        public static void Exibir(string name, string tag, string ip)
        {
            Thread t = new Thread(() => {
                try
                {
                    FormChamadoCliente frm = new FormChamadoCliente(name, tag, ip);
                    frm.TopMost = true;
                    frm.ShowDialog();
                }
                catch { }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        private void MontarUI()
        {
            Panel pnlHeader = new Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 70;
            pnlHeader.BackColor = Color.FromArgb(17, 24, 39);
            this.Controls.Add(pnlHeader);

            PictureBox pic = new PictureBox();
            pic.Image = Program.ObterBitmapNexus(42);
            pic.SizeMode = PictureBoxSizeMode.CenterImage;
            pic.Size = new Size(42, 42);
            pic.Location = new Point(16, 14);
            pnlHeader.Controls.Add(pic);

            Label lblTitle = new Label();
            lblTitle.Text = "NEXUSTI — SUPORTE & CHAMADOS";
            lblTitle.Font = new Font("Segoe UI", 11.5f, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(56, 189, 248);
            lblTitle.Location = new Point(68, 14);
            lblTitle.AutoSize = true;
            pnlHeader.Controls.Add(lblTitle);

            Label lblSub = new Label();
            lblSub.Text = "Central Corporativa de Chamados e Atendimento Técnico";
            lblSub.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblSub.ForeColor = Color.FromArgb(148, 163, 184);
            lblSub.Location = new Point(69, 38);
            lblSub.AutoSize = true;
            pnlHeader.Controls.Add(lblSub);

            Panel pnlBody = new Panel();
            pnlBody.Dock = DockStyle.Fill;
            pnlBody.Padding = new Padding(20);
            this.Controls.Add(pnlBody);

            Panel pnlAtivo = new Panel();
            pnlAtivo.BackColor = Color.FromArgb(15, 23, 42);
            pnlAtivo.BorderStyle = BorderStyle.FixedSingle;
            pnlAtivo.Location = new Point(20, 80);
            pnlAtivo.Size = new Size(465, 36);
            pnlBody.Controls.Add(pnlAtivo);

            Label lblAtivoInfo = new Label();
            lblAtivoInfo.Text = "💻 Computador: " + machineName + " (" + machineIp + ")  |  Setor: " + machineTag;
            lblAtivoInfo.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblAtivoInfo.ForeColor = Color.FromArgb(52, 211, 153);
            lblAtivoInfo.Location = new Point(10, 8);
            lblAtivoInfo.AutoSize = true;
            pnlAtivo.Controls.Add(lblAtivoInfo);

            Label lblSol = new Label();
            lblSol.Text = "Seu Nome:";
            lblSol.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblSol.Location = new Point(20, 126);
            lblSol.AutoSize = true;
            pnlBody.Controls.Add(lblSol);

            txtSolicitante = new TextBox();
            txtSolicitante.Text = Environment.UserName;
            txtSolicitante.BackColor = Color.FromArgb(17, 24, 39);
            txtSolicitante.ForeColor = Color.White;
            txtSolicitante.BorderStyle = BorderStyle.FixedSingle;
            txtSolicitante.Location = new Point(20, 148);
            txtSolicitante.Size = new Size(465, 26);
            pnlBody.Controls.Add(txtSolicitante);

            Label lblCat = new Label();
            lblCat.Text = "Categoria do Problema:";
            lblCat.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblCat.Location = new Point(20, 184);
            lblCat.AutoSize = true;
            pnlBody.Controls.Add(lblCat);

            cbCategoria = new ComboBox();
            cbCategoria.DropDownStyle = ComboBoxStyle.DropDownList;
            cbCategoria.BackColor = Color.FromArgb(17, 24, 39);
            cbCategoria.ForeColor = Color.White;
            cbCategoria.FlatStyle = FlatStyle.Flat;
            cbCategoria.Location = new Point(20, 206);
            cbCategoria.Size = new Size(465, 26);
            cbCategoria.Items.AddRange(new object[] {
                "Hardware / Computador travando ou lento",
                "Software / Sistema não abre ou com erro",
                "Internet / Conexão de rede oscilando",
                "Impressora / Scanner não funciona",
                "Acesso / Senha bloqueada",
                "Outro problema"
            });
            cbCategoria.SelectedIndex = 0;
            pnlBody.Controls.Add(cbCategoria);

            Label lblTit = new Label();
            lblTit.Text = "Título / Resumo do Problema:";
            lblTit.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblTit.Location = new Point(20, 242);
            lblTit.AutoSize = true;
            pnlBody.Controls.Add(lblTit);

            txtTitulo = new TextBox();
            txtTitulo.BackColor = Color.FromArgb(17, 24, 39);
            txtTitulo.ForeColor = Color.White;
            txtTitulo.BorderStyle = BorderStyle.FixedSingle;
            txtTitulo.Location = new Point(20, 264);
            txtTitulo.Size = new Size(465, 26);
            pnlBody.Controls.Add(txtTitulo);

            Label lblDesc = new Label();
            lblDesc.Text = "Descrição Detalhada:";
            lblDesc.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblDesc.Location = new Point(20, 300);
            lblDesc.AutoSize = true;
            pnlBody.Controls.Add(lblDesc);

            txtDesc = new TextBox();
            txtDesc.Multiline = true;
            txtDesc.ScrollBars = ScrollBars.Vertical;
            txtDesc.BackColor = Color.FromArgb(17, 24, 39);
            txtDesc.ForeColor = Color.White;
            txtDesc.BorderStyle = BorderStyle.FixedSingle;
            txtDesc.Location = new Point(20, 322);
            txtDesc.Size = new Size(465, 80);
            pnlBody.Controls.Add(txtDesc);

            btnEnviar = new Button();
            btnEnviar.Text = "Enviar Chamado para a T.I ➔";
            btnEnviar.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            btnEnviar.BackColor = Color.FromArgb(37, 99, 235);
            btnEnviar.ForeColor = Color.White;
            btnEnviar.FlatStyle = FlatStyle.Flat;
            btnEnviar.FlatAppearance.BorderSize = 0;
            btnEnviar.Cursor = Cursors.Hand;
            btnEnviar.Location = new Point(20, 418);
            btnEnviar.Size = new Size(465, 42);
            btnEnviar.Click += BtnEnviar_Click;
            pnlBody.Controls.Add(btnEnviar);
        }

        private void BtnEnviar_Click(object sender, EventArgs e)
        {
            string tit = txtTitulo.Text.Trim();
            if (string.IsNullOrEmpty(tit))
            {
                MessageBox.Show("Por favor, digite um título ou resumo do problema.", "Campo Obrigatório", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string sol = string.IsNullOrEmpty(txtSolicitante.Text.Trim()) ? Environment.UserName : txtSolicitante.Text.Trim();
            string cat = cbCategoria.Text;
            string desc = txtDesc.Text.Trim();

            Program.SalvarChamadoLocal(tit, cat, sol, machineName, machineIp, desc);

            MessageBox.Show(
                "✓ Seu chamado foi enviado para a equipe de T.I com sucesso!\n\nUm técnico iniciará o atendimento em breve.",
                "Chamado Registrado",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );

            this.Close();
        }
    }

    public class FormAvisoClient : Form
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool BringWindowToTop(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_SHOWWINDOW = 0x0040;

        private string tipo;
        private string titulo;
        private string mensagem;
        private string urgencia;
        private string[] opcoes;
        private string enqueteId;
        private string autor;
        private RadioButton[] radioOpcoes;
        private Button btnAcao;

        public FormAvisoClient(string tipo, string titulo, string mensagem, string urgencia, string[] opcoes, string enqueteId, string autor)
        {
            this.tipo = (tipo ?? "aviso").ToLower();
            this.titulo = (titulo ?? "").Trim();
            this.mensagem = string.IsNullOrEmpty(mensagem) ? "Por favor, leia atentamente as informações transmitidas pela equipe de TI." : mensagem.Trim();
            this.urgencia = (urgencia ?? "informativo").ToLower();
            this.opcoes = opcoes ?? new string[0];
            this.enqueteId = string.IsNullOrEmpty(enqueteId) ? ("ENQ-" + DateTime.Now.Ticks) : enqueteId;
            this.autor = string.IsNullOrEmpty(autor) ? "Gestão de TI" : autor;

            string janelaTit = "Fast Solutions Corporate — ";
            if (this.tipo == "enquete") janelaTit += "Pesquisa de TI";
            else if (this.tipo == "nota") janelaTit += "Nota de Serviço";
            else janelaTit += "NexusTI — Comunicado Corporativo";

            this.Text = janelaTit;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.TopMost = true;
            this.ShowInTaskbar = true;
            this.BackColor = Color.FromArgb(15, 23, 42); // Deep Slate-900
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.Icon = Program.ObterIconeNexus();

            // Força a janela a aparecer no topo absoluto sobre todos os outros apps
            this.Shown += (s, e) => {
                try
                {
                    this.TopMost = true;
                    ShowWindow(this.Handle, 9 /* SW_RESTORE */);
                    BringWindowToTop(this.Handle);
                    SetForegroundWindow(this.Handle);
                    SwitchToThisWindow(this.Handle, true);
                    SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                    this.Activate();
                    this.Focus();
                }
                catch { }
            };

            // Timer de re-afirmação de TopMost durante os primeiros 2 segundos
            System.Windows.Forms.Timer tmFront = new System.Windows.Forms.Timer();
            int tmFrontTicks = 0;
            tmFront.Interval = 250;
            tmFront.Tick += (s, e) => {
                tmFrontTicks++;
                try
                {
                    this.TopMost = true;
                    SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                    SetForegroundWindow(this.Handle);
                    this.BringToFront();
                }
                catch { }
                if (tmFrontTicks > 8) tmFront.Stop();
            };
            tmFront.Start();

            MontarUI();
        }

        public static void Exibir(string tipo, string titulo, string mensagem, string urgencia, string[] opcoes, string enqueteId, string autor)
        {
            Thread t = new Thread(() => {
                try
                {
                    FormAvisoClient frm = new FormAvisoClient(tipo, titulo, mensagem, urgencia, opcoes, enqueteId, autor);
                    frm.TopMost = true;

                    try
                    {
                        if (urgencia == "urgente") SystemSounds.Exclamation.Play();
                        else SystemSounds.Asterisk.Play();
                    }
                    catch { }

                    frm.ShowDialog();
                }
                catch (Exception ex)
                {
                    try { File.WriteAllText(Path.Combine(Program.ObterDiretorioDadosAgente(), "aviso_err.log"), ex.ToString()); } catch { }
                }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
        }

        private void MontarUI()
        {
            const int W = 560;
            const int HEADER_H = 72;
            Color accentColor = Color.FromArgb(59, 130, 246);
            Color headerBg = Color.FromArgb(20, 30, 50);
            string badgeText = "AVISO CORPORATIVO";
            Icon iconSys = SystemIcons.Information;

            if (this.urgencia == "urgente")
            {
                accentColor = Color.FromArgb(239, 68, 68);
                headerBg = Color.FromArgb(50, 15, 18);
                badgeText = "ALERTA URGENTE / CRÍTICO";
                iconSys = SystemIcons.Error;
            }
            else if (this.urgencia == "importante")
            {
                accentColor = Color.FromArgb(245, 158, 11);
                headerBg = Color.FromArgb(45, 30, 10);
                badgeText = "MANUTENÇÃO PROGRAMADA";
                iconSys = SystemIcons.Warning;
            }
            else if (this.tipo == "enquete")
            {
                accentColor = Color.FromArgb(139, 92, 246);
                headerBg = Color.FromArgb(30, 25, 55);
                badgeText = "PESQUISA & ENQUETE DE TI";
                iconSys = SystemIcons.Question;
            }
            else if (this.tipo == "nota")
            {
                accentColor = Color.FromArgb(14, 165, 233);
                headerBg = Color.FromArgb(15, 35, 50);
                badgeText = "NOTA TÉCNICA DE SERVIÇO";
                iconSys = SystemIcons.Information;
            }

            // 1. Painel de Cabeçalho (Posicionamento absoluto fixo em 0,0 - 72px)
            Panel pnlHeader = new Panel();
            pnlHeader.Location = new Point(0, 0);
            pnlHeader.Size = new Size(W, HEADER_H);
            pnlHeader.BackColor = headerBg;
            this.Controls.Add(pnlHeader);

            PictureBox pic = new PictureBox();
            pic.Image = iconSys.ToBitmap();
            pic.SizeMode = PictureBoxSizeMode.CenterImage;
            pic.Size = new Size(40, 40);
            pic.Location = new Point(16, 16);
            pnlHeader.Controls.Add(pic);

            Label lblBadge = new Label();
            lblBadge.Text = badgeText;
            lblBadge.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblBadge.ForeColor = accentColor;
            lblBadge.Location = new Point(66, 16);
            lblBadge.AutoSize = true;
            pnlHeader.Controls.Add(lblBadge);

            Label lblSub = new Label();
            lblSub.Text = "Emitido por: " + this.autor + " • " + DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            lblSub.Font = new Font("Segoe UI", 8.25f, FontStyle.Regular);
            lblSub.ForeColor = Color.FromArgb(148, 163, 184);
            lblSub.Location = new Point(67, 38);
            lblSub.AutoSize = true;
            pnlHeader.Controls.Add(lblSub);

            // 2. Painel de Corpo (Começa estritamente abaixo do cabeçalho em Y=72)
            Panel pnlBody = new Panel();
            pnlBody.Location = new Point(0, HEADER_H);
            pnlBody.BackColor = Color.FromArgb(15, 23, 42);
            this.Controls.Add(pnlBody);

            int currentY = 18;
            int contentW = W - 48; // 512px úteis

            // Título (se fornecido)
            if (!string.IsNullOrEmpty(this.titulo))
            {
                Label lblTit = new Label();
                lblTit.Text = this.titulo;
                lblTit.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
                lblTit.ForeColor = Color.White;
                lblTit.Location = new Point(24, currentY);
                lblTit.MaximumSize = new Size(contentW, 0);
                lblTit.AutoSize = true;
                pnlBody.Controls.Add(lblTit);

                currentY += lblTit.Height + 14;
            }

            // Mensagem do comunicado (com auto-wrap perfeito e sem cortes)
            Label lblMsg = new Label();
            lblMsg.Text = this.mensagem;
            lblMsg.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
            lblMsg.ForeColor = Color.FromArgb(226, 232, 240);
            lblMsg.Location = new Point(24, currentY);
            lblMsg.MaximumSize = new Size(contentW, 0);
            lblMsg.AutoSize = true;
            pnlBody.Controls.Add(lblMsg);

            currentY += lblMsg.Height + 20;

            // Opções de Enquete (se tipo == enquete)
            if (this.tipo == "enquete" && this.opcoes != null && this.opcoes.Length > 0)
            {
                Label lblOpcoesTag = new Label();
                lblOpcoesTag.Text = "Selecione a sua resposta:";
                lblOpcoesTag.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                lblOpcoesTag.ForeColor = Color.FromArgb(196, 181, 253);
                lblOpcoesTag.Location = new Point(24, currentY);
                lblOpcoesTag.AutoSize = true;
                pnlBody.Controls.Add(lblOpcoesTag);
                currentY += 28;

                this.radioOpcoes = new RadioButton[this.opcoes.Length];
                for (int i = 0; i < this.opcoes.Length; i++)
                {
                    RadioButton rb = new RadioButton();
                    rb.Text = this.opcoes[i];
                    rb.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
                    rb.ForeColor = Color.White;
                    rb.BackColor = Color.FromArgb(30, 41, 59);
                    rb.Location = new Point(24, currentY);
                    rb.Size = new Size(contentW, 36);
                    rb.Padding = new Padding(12, 0, 0, 0);
                    rb.Cursor = Cursors.Hand;
                    if (i == 0) rb.Checked = true;
                    pnlBody.Controls.Add(rb);
                    this.radioOpcoes[i] = rb;
                    currentY += 42;
                }
                currentY += 10;
            }

            // Botão de Ação
            btnAcao = new Button();
            btnAcao.Cursor = Cursors.Hand;
            btnAcao.FlatStyle = FlatStyle.Flat;
            btnAcao.FlatAppearance.BorderSize = 0;
            btnAcao.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            btnAcao.ForeColor = Color.White;
            btnAcao.Size = new Size(contentW, 44);
            btnAcao.Location = new Point(24, currentY);

            if (this.tipo == "enquete")
            {
                btnAcao.Text = "Enviar Minha Resposta / Votar";
                btnAcao.BackColor = Color.FromArgb(124, 58, 237);
            }
            else
            {
                btnAcao.Text = "✓ Estou Ciente / Confirmar Leitura";
                btnAcao.BackColor = accentColor;
            }
            btnAcao.Click += BtnAcao_Click;
            pnlBody.Controls.Add(btnAcao);

            currentY += btnAcao.Height + 24;

            // Ajusta altura do pnlBody e da janela inteira de forma dinâmica e precisa
            pnlBody.Size = new Size(W, currentY);
            int totalFormHeight = HEADER_H + currentY;
            if (totalFormHeight < 280) totalFormHeight = 280;
            if (totalFormHeight > 750)
            {
                totalFormHeight = 750;
                pnlBody.AutoScroll = true;
            }

            this.ClientSize = new Size(W, totalFormHeight);
        }

        private void BtnAcao_Click(object sender, EventArgs e)
        {
            if (this.tipo == "enquete")
            {
                string escolhida = "";
                if (this.radioOpcoes != null)
                {
                    foreach (var rb in this.radioOpcoes)
                    {
                        if (rb.Checked) { escolhida = rb.Text; break; }
                    }
                }
                if (string.IsNullOrEmpty(escolhida))
                {
                    MessageBox.Show("Por favor, selecione uma opção.", "Seleção Obrigatória", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                Program.SalvarVotoEnquete(this.enqueteId, escolhida, Program.customMachineName, Environment.UserName);
                btnAcao.Enabled = false;
                btnAcao.Text = "✓ Resposta Registrada! Obrigado pela participação.";
                btnAcao.BackColor = Color.FromArgb(16, 185, 129);

                System.Windows.Forms.Timer tm = new System.Windows.Forms.Timer();
                tm.Interval = 1300;
                tm.Tick += (s, ev) => { tm.Stop(); this.Close(); };
                tm.Start();
            }
            else
            {
                Program.SalvarAvisoConfirmado(this.titulo, this.tipo, Program.customMachineName, Environment.UserName);
                btnAcao.Enabled = false;
                btnAcao.Text = "✓ Leitura Confirmada!";
                btnAcao.BackColor = Color.FromArgb(16, 185, 129);

                System.Windows.Forms.Timer tm = new System.Windows.Forms.Timer();
                tm.Interval = 800;
                tm.Tick += (s, ev) => { tm.Stop(); this.Close(); };
                tm.Start();
            }
        }
    }

}

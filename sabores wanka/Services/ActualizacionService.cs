using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace sabores_wanka.Services
{
    // Auto-update desde GitHub Releases (patron Nachis, sin nube ni terminales):
    // al iniciar compara la version instalada con el latest del repo privado
    // (token de lectura) y aplica el ZIP al reiniciar. Solo perfil Unpackaged:
    // el paquete MSIX se actualiza por la tienda, no por este servicio.
    public sealed class ActualizacionService
    {
        private const string Marcador = ".update-pending";

        // Avisa a MainWindow para mostrar el banner (version remota nueva).
        public event EventHandler<string>? ActualizacionDisponible;

        public Version InstalledVersion { get; } =
            Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

        public string? PendingRemoteVersion { get; private set; }

        // Completa un reemplazo interrumpido (la app se cerro a mitad del swap).
        public void TryRecoverPendingUpdate()
        {
            try
            {
                string baseDir = AppContext.BaseDirectory;
                string marcador = Path.Combine(baseDir, Marcador);
                if (!File.Exists(marcador)) return;
                string origen = File.ReadAllText(marcador).Trim();
                if (Directory.Exists(origen)) AplicarSwap(origen, baseDir);
                else File.Delete(marcador);
            }
            catch { }
        }

        // Revisa el latest; si es mayor, levanta el evento para el banner.
        public async Task CheckForUpdatesAsync()
        {
            try
            {
                var cfg = GitHubUpdateConfig.Cargar();
                if (cfg == null) return;
                var (version, _) = await ObtenerLatestAsync(cfg);
                if (version == null || version <= InstalledVersion) return;
                PendingRemoteVersion = version.ToString();
                ActualizacionDisponible?.Invoke(this, PendingRemoteVersion);
            }
            catch { }
        }

        // Descarga el ZIP del release y lo deja listo; al salir hace el swap.
        public async Task<bool> DownloadAndApplyUpdateAsync(string version, IProgress<double>? progreso = null)
        {
            try
            {
                var cfg = GitHubUpdateConfig.Cargar();
                if (cfg == null) return false;
                var (remota, assetUrl) = await ObtenerLatestAsync(cfg);
                if (remota == null || assetUrl == null) return false;
                if (!string.Equals(remota.ToString(), version, StringComparison.OrdinalIgnoreCase)) return false;

                string tempDir = Path.Combine(Path.GetTempPath(), "SaboresWankaUpdates", "v" + version);
                Directory.CreateDirectory(tempDir);
                string zip = Path.Combine(tempDir, "update.zip");
                string destino = Path.Combine(tempDir, "app");

                if (!await DescargarAsync(assetUrl, cfg.Token, zip, progreso)) return false;
                if (Directory.Exists(destino)) Directory.Delete(destino, true);
                ZipFile.ExtractToDirectory(zip, destino);

                // Defensa: sin exe no se toca la instalacion actual.
                string exe = Path.Combine(destino, (Assembly.GetExecutingAssembly().GetName().Name ?? "sabores wanka") + ".exe");
                if (!File.Exists(exe))
                {
                    var cualquiera = Directory.GetFiles(destino, "*.exe", SearchOption.TopDirectoryOnly);
                    if (cualquiera.Length == 0) return false;
                    exe = cualquiera[0];
                }

                string baseDir = AppContext.BaseDirectory;
                File.WriteAllText(Path.Combine(baseDir, Marcador), destino);
                AplicarSwap(destino, baseDir);
                try { Directory.Delete(tempDir, true); } catch { }
                return true;
            }
            catch { return false; }
        }

        // Lee tag_name + primer asset .zip del latest (requiere Bearer en privado).
        private static async Task<(Version? version, string? assetUrl)> ObtenerLatestAsync(GitHubUpdateConfig cfg)
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("SaboresWanka-Updater");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cfg.Token);

            using var resp = await http.GetAsync($"https://api.github.com/repos/{cfg.Owner}/{cfg.Repo}/releases/latest");
            if (!resp.IsSuccessStatusCode) return (null, null);
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            string tag = root.TryGetProperty("tag_name", out var t) ? (t.GetString() ?? "") : "";
            tag = tag.Trim().TrimStart('v', 'V');
            if (!Version.TryParse(tag, out var version)) return (null, null);

            string? assetUrl = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in assets.EnumerateArray())
                {
                    string nombre = a.TryGetProperty("name", out var n) ? (n.GetString() ?? "") : "";
                    if (!nombre.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
                    assetUrl = a.TryGetProperty("url", out var u) ? u.GetString() : null;
                    break;
                }
            }
            return (version, assetUrl);
        }

        // Baja el asset con progreso (Accept octet-stream para binario).
        private static async Task<bool> DescargarAsync(string url, string token, string destino, IProgress<double>? progreso)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("SaboresWanka-Updater");
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                http.DefaultRequestHeaders.Accept.ParseAdd("application/octet-stream");
                using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if (!resp.IsSuccessStatusCode) return false;
                long? total = resp.Content.Headers.ContentLength;
                using var red = await resp.Content.ReadAsStreamAsync();
                using var archivo = File.Create(destino);
                var buffer = new byte[81920];
                long leido = 0;
                int n;
                while ((n = await red.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await archivo.WriteAsync(buffer, 0, n);
                    leido += n;
                    if (total.HasValue && total.Value > 0)
                        progreso?.Report((double)leido / total.Value * 100);
                }
                progreso?.Report(100);
                return true;
            }
            catch { return false; }
        }

        // Reemplaza los archivos con PowerShell y relanza (la app debe salir).
        private static void AplicarSwap(string origen, string baseDir)
        {
            string exeNombre = (Assembly.GetExecutingAssembly().GetName().Name ?? "sabores wanka") + ".exe";
            string marcador = Path.Combine(baseDir, Marcador);
            string cmd = $"Start-Sleep 2; Copy-Item '{origen}\\*' '{baseDir}' -Recurse -Force; " +
                         $"Remove-Item '{marcador}' -Force -ErrorAction SilentlyContinue; " +
                         $"Start-Process '{Path.Combine(baseDir, exeNombre)}'";
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{cmd}\"",
                    UseShellExecute = true,
                });
            }
            catch { return; }
            try { Application.Current.Exit(); } catch { }
            Environment.Exit(0);
        }
    }
}

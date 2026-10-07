using System;
using System.IO;
using System.Text.Json;

namespace sabores_wanka.Services
{
    // Config del canal de actualizaciones (GitHub Releases, repo privado).
    // El token de LECTURA viaja dentro del ZIP publicado (github_update.json
    // junto al exe), nunca en git. Un archivo igual en LocalAppData lo
    // reemplaza si existe (para rotar la llave sin republicar).
    public sealed class GitHubUpdateConfig
    {
        public string Owner { get; set; } = "";
        public string Repo { get; set; } = "";
        public string Token { get; set; } = "";
        public bool Enabled { get; set; } = true;

        public bool Valida => Enabled && Owner.Length > 0 && Repo.Length > 0 && Token.Length > 0;

        public static GitHubUpdateConfig? Cargar()
        {
            foreach (string ruta in RutasCandidatas())
            {
                try
                {
                    if (!File.Exists(ruta)) continue;
                    var cfg = JsonSerializer.Deserialize<GitHubUpdateConfig>(File.ReadAllText(ruta));
                    if (cfg != null && cfg.Valida) return cfg;
                }
                catch { }
            }
            return null;
        }

        private static string[] RutasCandidatas()
        {
            string local = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SaboresWanka", "Data", "github_update.json");
            string juntoAlExe = Path.Combine(AppContext.BaseDirectory, "github_update.json");
            // LocalAppData primero: permite rotar la llave sin republicar.
            return new[] { local, juntoAlExe };
        }
    }
}

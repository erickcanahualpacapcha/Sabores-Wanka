using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace sabores_wanka.Views.PuntoDeVenta
{
    // Logger de diagnostico de la pantalla de mesas (R4: evidencia de runtime).
    // Escribe a Debug (salida del depurador) y a %LOCALAPPDATA%\SaboresWanka\mesas-diag.log.
    // Objetivo: medir la latencia de tap y de cambio de mesa. NO es un logger de produccion.
    internal static class MesasDiag
    {
        private static readonly object Gate = new();
        private static readonly string LogPath;
        private static long _pointerStamp;
        private static string _pointerMesa = "";

        static MesasDiag()
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SaboresWanka");
                Directory.CreateDirectory(dir);
                LogPath = Path.Combine(dir, "mesas-diag.log");
                lock (Gate)
                {
                    File.AppendAllText(LogPath, "\r\n===== sesion " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " =====\r\n", Encoding.UTF8);
                }
            }
            catch
            {
                LogPath = "";
            }
        }

        // Marca el instante del PointerPressed para medir el retardo hasta el Tapped.
        public static void MarkPointerPressed(string mesa)
        {
            _pointerStamp = Stopwatch.GetTimestamp();
            _pointerMesa = mesa;
        }

        public static double PointerDelayMs =>
            _pointerStamp == 0 ? 0 : (Stopwatch.GetTimestamp() - _pointerStamp) * 1000.0 / Stopwatch.Frequency;

        public static string PointerMesa => _pointerMesa;

        public static long Stamp() => Stopwatch.GetTimestamp();

        public static double Since(long stamp) =>
            stamp == 0 ? 0 : (Stopwatch.GetTimestamp() - stamp) * 1000.0 / Stopwatch.Frequency;

        public static void Log(string msg)
        {
            string line = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] [MESAS][DIAG] " + msg;
            Debug.WriteLine(line);
            if (string.IsNullOrEmpty(LogPath)) return;
            try
            {
                lock (Gate) File.AppendAllText(LogPath, line + "\r\n", Encoding.UTF8);
            }
            catch { }
        }
    }
}

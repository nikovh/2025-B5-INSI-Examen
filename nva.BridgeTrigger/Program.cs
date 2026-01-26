using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace nva.BridgeTrigger
{
    class Program
    {
        // Rutas locales Windows Server
        private const string JarPath = @"C:\bridge\bridge-publisher.jar";
        private const string JavaExePath = @"C:\Java\jdk-21\bin\java.exe";
        private const string LogPath = @"C:\bridge\bridge-trigger.log";

        // Artemis (Linux Host-Only)
        private const string ArtemisHost = "192.168.56.101";
        private const string ArtemisPort = "61616";
        private const string ArtemisUser = "nicolas";
        private const string ArtemisPass = "hola";
        private const string topicName = "nva_amq_estado_cuenta";

        static int Main(string[] args)
        {
            try
            {
                EnsureLogDirectory();

                Log("==== START ====");
                Log("ArgsLen=" + (args?.Length ?? 0));

                if (args == null || args.Length < 1)
                {
                    Log("ERROR: No se recibieron argumentos. Se esperaba <messageBodyAsString>.");
                    Console.Error.WriteLine("Uso: nva.BridgeTrigger.exe <messageBodyAsString>");
                    return 2;
                }

                // MSMQ Trigger puede pasar el body separado en múltiples argumentos
                string body = string.Join(" ", args).Trim();
                Log("BODY_LEN=" + body.Length);
                Log("BODY_HEAD=" + SafeHead(body, 300));

                // Validaciones básicas de entorno
                if (!File.Exists(JavaExePath))
                {
                    Log("ERROR: java.exe no existe en: " + JavaExePath);
                    Console.Error.WriteLine("No se encontró java.exe en: " + JavaExePath);
                    return 10;
                }

                if (!File.Exists(JarPath))
                {
                    Log("ERROR: JAR no existe en: " + JarPath);
                    Console.Error.WriteLine("No se encontró el JAR en: " + JarPath);
                    return 11;
                }

                // Convertir a Base64 para que sea un solo token en la línea de comandos
                string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(body));

                string arguments =
                    $"-jar \"{JarPath}\" {ArtemisHost} {ArtemisPort} {ArtemisUser} {ArtemisPass} {topicName} {b64}";

                Log("CMD=" + JavaExePath + " " + Truncate(arguments, 500));

                var psi = new ProcessStartInfo
                {
                    FileName = JavaExePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = @"C:\bridge"
                };

                using (var p = Process.Start(psi))
                {
                    string stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();
                    p.WaitForExit();

                    Log("EXIT_CODE=" + p.ExitCode);

                    if (!string.IsNullOrWhiteSpace(stdout))
                        Log("STDOUT=" + Truncate(stdout.Trim(), 2000));

                    if (!string.IsNullOrWhiteSpace(stderr))
                        Log("STDERR=" + Truncate(stderr.Trim(), 4000));

                    if (p.ExitCode != 0)
                    {
                        Log("==== END (ERROR) ====");
                        return p.ExitCode;
                    }
                }

                Log("==== END (OK) ====");
                return 0;
            }
            catch (Exception ex)
            {
                try
                {
                    EnsureLogDirectory();
                    Log("FATAL_EXCEPTION=" + ex);
                    Log("==== END (FATAL) ====");
                }
                catch { /* no-op */ }

                Console.Error.WriteLine("BridgeTrigger Exception: " + ex);
                return 1;
            }
        }

        private static void EnsureLogDirectory()
        {
            // Asegura que exista C:\bridge
            string dir = Path.GetDirectoryName(LogPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        private static void Log(string line)
        {
            // Esto CREA el archivo automáticamente si no existe
            File.AppendAllText(
                LogPath,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + line + Environment.NewLine,
                Encoding.UTF8
            );
        }

        private static string SafeHead(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\r", "\\r").Replace("\n", "\\n");
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }
    }
}
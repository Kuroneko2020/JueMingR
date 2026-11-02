using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace JueMingR.Setup
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new SetupForm()); return 0;
            }
            AttachConsole(unchecked((uint)-1));
            SetupResult result;
            try
            {
                if (args.Length == 2 && args.Contains("--payload-info") && args.Contains("--json"))
                {
                    SetupPayload payload = SetupPayload.Read();
                    Console.WriteLine(new JavaScriptSerializer().Serialize(new { schema = "JueMingR.Setup.Payload.1", dll = new { name = "JueMingR.dll", length = payload.Dll.Length, sha256 = payload.DllHash }, config = new { name = "Terraria.exe.config", length = payload.Config.Length, sha256 = payload.ConfigHash } }));
                    return 0;
                }
                string operation = null, directory = null; bool json = false;
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--json" && !json) json = true;
                    else if ((args[i] == "--install" || args[i] == "--uninstall") && operation == null) operation = args[i].Substring(2);
                    else if (args[i] == "--game-dir" && directory == null && ++i < args.Length) directory = args[i];
                    else throw new ArgumentException("参数无效。用法：--install 或 --uninstall --game-dir <游戏目录> [--json]");
                }
                if (operation == null || directory == null) throw new ArgumentException("请指定 --install 或 --uninstall，以及 --game-dir <游戏目录>。");
                result = new InstallService().Execute(operation, directory);
                Console.WriteLine(json ? new JavaScriptSerializer().Serialize(result) : result.message);
                return result.exitCode;
            }
            catch (Exception error)
            {
                result = new SetupResult { status = "failure", exitCode = 2, message = error.Message, failedStep = "arguments-or-payload" };
                Console.WriteLine(args.Contains("--json") ? new JavaScriptSerializer().Serialize(result) : result.message); return 2;
            }
        }
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool AttachConsole(uint processId);
    }
}

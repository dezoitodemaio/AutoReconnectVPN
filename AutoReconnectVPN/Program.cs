using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace AutoReconnectVPN
{
    public enum State
    {
        Login,
        CheckConnection,
        CheckLogin,
        Credentials
    }

    class AutoReconnectVPN
    {
        private readonly string VpnPath = @"C:\Program Files\SonicWall\Global VPN Client\SWGVC.exe";
        private readonly string LogFilePath = "c:\\log.txt";
        private bool IsRunning = true;

        public string Login;
        public string Password;
        private Process Process;

        public AutoReconnectVPN()
        {
            AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) => QuitVPN();

            // fecha a vpn caso ja esteja executando
            QuitVPN();
        }

        private void QuitVPN()
        {
            CallVPNProcess("/Q");
        }

        public void Start(LogReader logReader)
        {
            var state = State.Credentials;

            while (IsRunning)
            {
                switch (state)
                {
                    case State.Credentials:

                        Login = "cairo.martins";
                        Password = "A5t8y9g1";

                        //Login = Ask("usuario: ");
                        //Password = Ask("senha: ");

                        state = State.Login;

                        break;

                    case State.Login:

                        Console.WriteLine("Conectando...");

                        logReader.Clear();

                        CallVPNProcess($"/E \"GroupVPN_C0EAE469CBE2\" /U \"{Login}\" /P \"{Password}\" /A \"{LogFilePath}\"");

                        state = State.CheckLogin;

                        break;

                    case State.CheckConnection:

                        // TODO: as veses o login demora e nao conecta, tem que desabilitar e habilitar denovo

                        Delay(1000);

                        var isDisabled = logReader.HasContent("has been disabled");

                        if (isDisabled)
                            ShutDown();

                        var isOff = logReader.HasContent("phase 2 SA has been deleted");

                        if (isOff)
                        {
                            Console.WriteLine("VPN caiu.");
                            logReader.Clear();
                            state = State.Login;
                        }

                        break;

                    case State.CheckLogin:

                        Delay(500);

                        if (logReader.HasContent("User authentication has failed"))
                        {
                            Console.WriteLine("Usuario ou senha invalidos.");
                            state = State.Credentials;                            
                        }
                        else if (logReader.HasContent("NetGetDCName returned: logon server:"))
                        {
                            // sucesso
                            Console.WriteLine("VPN Conectada!");

                            state = State.CheckConnection;
                            logReader.Clear();
                        }                        

                        break;
                }

            }

            logReader.Finish();

            //cleanup todo
        }


        private Process CallVPNProcess(string @args)
        {
            var process = Process.Start(VpnPath, @args);
            return process;
        }

        private void ShutDown()
        {
            CallVPNProcess("/Q");
            IsRunning = false;
        }

        private void Delay(int milleseconds)
        {
            Task.Delay(milleseconds).Wait();
        }

        private string Ask(string question)
        {
            Console.Write(question);
            return Console.ReadLine();
        }
    }

    class LogReader
    {
        StreamReader sr;

        string StringBlock = string.Empty;

        string FileName;

        public LogReader(string fileName)
        {
            FileName = fileName;
        }

        public void Clear()
        {
            StringBlock = String.Empty;
        }

        public void Read()
        {
            if(sr == null)
            {
                var f = File.Open(FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                sr = new StreamReader(f, System.Text.UnicodeEncoding.Unicode);
            }            

            StringBlock += sr.ReadToEnd();

        }

        public bool HasContent(string what) => StringBlock.Contains(what);

        internal void Finish() => sr.Dispose();
    }

    class Program
    {
        static LogReader logReader = new LogReader("C:\\log.txt");

        static void Main(string[] args)
        {
            

            using (FileSystemWatcher watch = new FileSystemWatcher())
            {
                watch.Path = "C:\\";
                watch.Filter = "*log.txt*";
                watch.IncludeSubdirectories = false;

                watch.Changed += Watch_Changed;
                watch.EnableRaisingEvents = true;

                watch.NotifyFilter = NotifyFilters.Attributes | NotifyFilters.LastWrite;

                var a = new AutoReconnectVPN();
                a.Start(logReader);
            }

        }

        private static void Watch_Changed(object sender, FileSystemEventArgs e)
        {
            logReader.Read();
        }
    }
}

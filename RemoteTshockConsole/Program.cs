using System;
using System.IO;
using System.Reflection;
using System.Threading;
using ClientServerLib;
using System.Security;
using System.Linq;

namespace RemoteTshockConsole
{
    class Program
    {
        static Version Version => Assembly.GetExecutingAssembly().GetName().Version;
        static Config config;
        static AutoResetEvent resetEvent = new AutoResetEvent(false);

        static Program()
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
        }

        static void Main(string[] args)
        {
            if (!File.Exists("config.json"))
                MakeConfig();
            else
                config = Config.Load();

            Client client = new Client();
            try
            {               
                client.PacketReceived += Client_PacketReceived;
                client.ClientDisconnected += Client_ClientDisconnected;
                Console.WriteLine("Connecting...");
                client.Connect(config.ServerIp, config.Port);
                Console.Clear();
                client.Send(new Packet((short)PacketType.Authenticate, (byte)0, Version.Major, Version.Minor, config.Username, config.Password));
            }
            catch
            {
                Console.WriteLine("Error connecting. Attempting to reconnect in 5 seconds...");
                resetEvent.Set();         
            }           

            if (client.Connected)
            {
                new Thread(() =>
                {
                    for (;;)
                    {
                        string input = Console.ReadLine();
                        if (!client.tcpClient.Connected)
                            break;
                        client.Send(new Packet((short)PacketType.Input, input));
                    }
                }).Start();
            }

            resetEvent.WaitOne();                       
            Thread.Sleep(5000);
            Console.Clear();
            Main(args);
            return;
        }

        private static  Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            string dllName = args.Name.Contains(",") ? args.Name.Substring(0, args.Name.IndexOf(',')) : args.Name.Replace(".dll", "");

            dllName = dllName.Replace(".", "_");

            if (dllName.EndsWith("_resources")) return null;

            System.Resources.ResourceManager rm = new System.Resources.ResourceManager(Assembly.GetExecutingAssembly().GetName().Name + ".Properties.Resources", Assembly.GetExecutingAssembly());

            byte[] bytes = (byte[])rm.GetObject(dllName);

            return Assembly.Load(bytes);
        }

        private static void Client_ClientDisconnected(Client e)
        {
            Console.WriteLine("Connection lost. Attempting to reconnect in 5 seconds...");
            resetEvent.Set();
        }

        static void MakeConfig()
        {
            config = new Config();
            config.ServerIp = GetInput("Server address");
            string port = GetInput("Port (press enter for 8787)");
            while (!int.TryParse(port, out config.Port) && port != string.Empty) ;
            if (config.Port == 0)
                config.Port = 8787;
            config.Username = GetInput("Username");
            config.Password = GetMaskedInput("Password");
            config.Save();
            Console.Clear();
        }

        static string GetInput(string message)
        {
            Console.Write(message + ": ");
            return Console.ReadLine();
        }


        static string GetMaskedInput(string message)
        {
            Console.Write(message + ": ");
            int pos = Console.CursorLeft;
            string pwd = "";
            
            while (true)
            {
                ConsoleKeyInfo i = Console.ReadKey(true);
                if (i.Key == ConsoleKey.Enter)
                {
                    break;
                }
                else if (i.Key == ConsoleKey.Backspace)
                {
                    if (Console.CursorLeft > pos)
                    {
                        pwd.Remove(pwd.Length - 1);
                        Console.Write("\b \b");
                    }
                }
                else
                {
                    pwd += i.KeyChar;
                    Console.Write("*");
                }
            }
            return pwd;
        }
    

        private static void Client_PacketReceived(Client sender, Client.PacketReceivedEventArgs e)
        {
            PacketType packetType = (PacketType)e.Reader.ReadInt16();
            switch (packetType)
            {
                case PacketType.Disconnect:
                    {
                        string msg = e.Reader.ReadString();
                        Console.WriteLine("Disconnected:" + msg);
                    }
                    break;
                case PacketType.Message:
                    {
                        ConsoleColor oldColor = Console.ForegroundColor;
                        ConsoleColor consoleColor = (ConsoleColor)e.Reader.ReadByte();
                        string msg = e.Reader.ReadString();
                        Console.ForegroundColor = consoleColor;

                        if (msg.EndsWith(": "))
                            Console.Write(msg);
                        else
                        Console.WriteLine(msg);

                        Console.ForegroundColor = oldColor;
                    }
                    break;
                case PacketType.MessageBuffer:
                    {
                        short length = e.Reader.ReadInt16();
                        ConsoleColor oldColor = Console.ForegroundColor;
                        for (int i = 0; i < length; i++)
                        {
                            Console.ForegroundColor = (ConsoleColor)e.Reader.ReadByte();
                            string msg = e.Reader.ReadString();
                            if (msg.EndsWith(": "))
                                Console.Write(msg);
                            else
                                Console.WriteLine(msg);
                        }
                        Console.ForegroundColor = oldColor;
                    }
                    break;
            }
        }
    }
}

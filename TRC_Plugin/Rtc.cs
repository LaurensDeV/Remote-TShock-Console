using ClientServerLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Terraria;
using TerrariaApi.Server;

namespace RTC_Plugin
{
    [ApiVersion(1, 22)]
    public class Rtc : TerrariaPlugin
    {
        public static Version buildVersion => Assembly.GetExecutingAssembly().GetName().Version;
        static Listener listener;
        static Config config;
        public static TaskReader ConsoleInput;
        static List<ConsoleClient> clients;

        public static string[] MessagesBuffer;
        public static byte[] ColorBuffer;
             
        public override string Author => "Ancientgods";
        public override string Description => "Allows for remote control of the tshock server";
        public override string Name => "Tshock Remote Console";
        public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;


        public Rtc(Main game) : base(game)
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            Order = -1;
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            string dllName = args.Name.Contains(",") ? args.Name.Substring(0, args.Name.IndexOf(',')) : args.Name.Replace(".dll", "");

            dllName = dllName.Replace(".", "_");

            if (dllName.EndsWith("_resources")) return null;

            System.Resources.ResourceManager rm = new System.Resources.ResourceManager(GetType().Namespace + ".Properties.Resources", Assembly.GetExecutingAssembly());

            byte[] bytes = (byte[])rm.GetObject(dllName);

            return Assembly.Load(bytes);           
        }

        public override void Initialize()
        {          
            if (!File.Exists(Config.ConfigPath))
            {
                Directory.CreateDirectory(Config.ConfigPath.Replace("Rtc_config.json", ""));
                config = new Config();
                config.Save();
            }
            config = Config.Load();

            MessagesBuffer = new string[config.MessageBufferLength];
            ColorBuffer = new byte[config.MessageBufferLength];
            
            clients = new List<ConsoleClient>();     
            listener = new Listener(config.ListenPort);
            listener.ClientAccepted += Listener_ClientAccepted;
            listener.Start();

            ConsoleInput = new TaskReader(Console.In);
            Console.SetIn(ConsoleInput);
            Console.SetOut(new TaskWriter(Console.Out, SendInputToClients));
        }

        public static void AddToMessageBuffer(string message, byte color)
        {
            for (int i = 1; i < MessagesBuffer.Length; i++)
                MessagesBuffer[i - 1] = MessagesBuffer[i];
        
            for (int i = 1; i < ColorBuffer.Length; i++)
                ColorBuffer[i - 1] = ColorBuffer[i];

            MessagesBuffer[MessagesBuffer.Length -1] = message;
            ColorBuffer[ColorBuffer.Length - 1] = color;
        }

        public Action<string> SendInputToClients = (s) =>
        {
            byte[] packetBytes;

            if (s.Length > 0)
            {
                if (s == ": ")
                    return;

                AddToMessageBuffer(s, (byte)Console.ForegroundColor);

                packetBytes = new Packet((short)PacketType.Message, (byte)Console.ForegroundColor, s).GetBytes();

                foreach (ConsoleClient clnt in clients)
                    clnt?.RemoteClient?.Send(packetBytes);             
            }
        };
        
        private void Listener_ClientAccepted(Client client)
        {
            if (clients.Count >= config.MaxConnections)
            {
                Packet dcPacket = new Packet((short)PacketType.Disconnect, "Maximum connection limited has been reached!");
                client.Send(dcPacket);
                client.Close();
            }

            client.ClientDisconnected += Client_ClientDisconnected;
            clients.Add(new ConsoleClient(client));
        }

        private void Client_ClientDisconnected(Client e)
        {
            clients.RemoveAll(c => c.RemoteClient == e);
        }      

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }
}

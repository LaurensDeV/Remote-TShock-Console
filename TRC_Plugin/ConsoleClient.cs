using ClientServerLib;
using TShockAPI;
using TShockAPI.DB;

namespace RTC_Plugin
{
    public class ConsoleClient
    {
        public Client RemoteClient;
        public bool Authenticated;
        User TSUser;
        InterfaceType InterfaceType;

        public ConsoleClient(Client remoteClient)
        {
            RemoteClient = remoteClient;
            RemoteClient.PacketReceived += RemoteClient_PacketReceived;
        }

        private void RemoteClient_PacketReceived(Client sender, Client.PacketReceivedEventArgs e)
        {
            PacketType packetType = (PacketType)e.Reader.ReadInt16();

            //Disconnect the user if he attempts to do anything else before authenticating.
            if (packetType != PacketType.Authenticate && !Authenticated)
            {
                Disconnect("Your attempt at sending packets before authenticating has been ignored!");
                return;
            }
            switch (packetType)
            {
                case PacketType.Authenticate:
                    InterfaceType = (InterfaceType)e.Reader.ReadByte();
                    int major = e.Reader.ReadInt32();
                    int minor = e.Reader.ReadInt32();
                    if (Rtc.buildVersion.Major != major || Rtc.buildVersion.Minor != minor)
                    {
                        Disconnect($"Your version ({major}.{minor}) is incompatible with the server's version ({Rtc.buildVersion.Major}.{Rtc.buildVersion.Minor}).");
                        return;
                    }                    
                    string Username = e.Reader.ReadString();
                    string Password = e.Reader.ReadString();
                    TSUser = TShock.Users.GetUserByName(Username);

                    if (TSUser == null || !TSUser.VerifyPassword(Password))
                    {
                        Disconnect("Invalid username/password or insufficient privileges.");
                        return;
                    }
                    Group g = TShock.Groups.GetGroupByName(TSUser.Group);
                
                    if (!g.HasPermission("*"))
                    {
                        Disconnect("Invalid username/password or insufficient privileges.");
                        return;
                    }
                    Authenticated = true;
                    Packet pck = new Packet((short)PacketType.MessageBuffer, (short)Rtc.MessagesBuffer.Length);
                    for (int i = 0; i < Rtc.MessagesBuffer.Length; i++)
                    {                  
                        if (!string.IsNullOrEmpty(Rtc.MessagesBuffer[i]))
                           pck.Write(Rtc.ColorBuffer[i], Rtc.MessagesBuffer[i]);                 
                        
                    }
                    sender.Send(pck);
                    break;
                case PacketType.Input:
                    string text = e.Reader.ReadString();
                    Rtc.ConsoleInput.SendText(text);
                    break;
            }
        }

        public void Disconnect(string message)
        {
            Packet pck = new Packet((short)PacketType.Disconnect, message);
            RemoteClient.Send(pck);
            RemoteClient.Close();
        }
    }

    //To be used for when I decide to add a graphical version of the remote console that uses additional packets.
    enum InterfaceType
    {
        CLI,
        GUI
    }
}

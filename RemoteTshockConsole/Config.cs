using Newtonsoft.Json;
using System.IO;

namespace RemoteTshockConsole
{
    public class Config
    {
        public string ServerIp;
        public int Port;
        public string Username;
        public string Password;

        public Config()
        {
            Port = 8787;
        }

        public void Save()
        {
            using (StreamWriter sw = new StreamWriter(File.Open("Config.json", FileMode.Create)))
            {
                sw.Write(JsonConvert.SerializeObject(this, Formatting.Indented));
            }
        }

        public static Config Load()
        {
            using (StreamReader sr = new StreamReader(File.Open("Config.json", FileMode.Open)))
            {
                return JsonConvert.DeserializeObject<Config>(sr.ReadToEnd());
            }
        }
    }
}

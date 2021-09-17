using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace CrackLiveXAML
{
    public static class RuntimeUpdateSender
    {
        private static Dictionary<int, TcpClient> _activeClients = new Dictionary<int, TcpClient>();
        private static byte[] _buffer = new byte[204800];
        public const int SendPort = 53030;
        public const int SendPort20 = 53032;
        private static bool _replyReceived;

        public static void Send(string filePath, string[] propertiesToReset)
        {
            RuntimeUpdateSender.SendGeneric(filePath, propertiesToReset, false);
            RuntimeUpdateSender.SendGeneric(filePath, propertiesToReset, true);
        }

        private static void SendGeneric(string filePath, string[] propertiesToReset, bool is20Buffer)
        {
            try
            {
                byte[] buffer = RuntimeUpdateSender.GetBuffer(filePath, propertiesToReset, is20Buffer);
                bool flag = false;
                try
                {
                    flag = RuntimeUpdateSender.TrySendTcp(buffer, is20Buffer ? 53032 : 53030, false);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Could not send to TCP server on 127.0.0.1: " + ex.Message);
                    throw;
                }
                Logger.WriteLine("Buffer sent");
            }
            catch (MalformedXamlException ex)
            {
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Sending runtime update failed: " + (object)ex);
                int num = (int)MessageBox.Show("Sending LiveXAML update failed: " + Environment.NewLine + (object)ex);
                throw;
            }
        }

        private static bool TrySendTcp(byte[] buffer, int port, bool isReconnecting = false)
        {
            Logger.WriteLine("Sending buffer (" + (object)buffer.Length + ")");
            TcpClient tcpClient = (TcpClient)null;
            RuntimeUpdateSender._activeClients.TryGetValue(port, out tcpClient);
            if (isReconnecting || tcpClient == null || !tcpClient.Connected)
            {
                RuntimeUpdateSender._activeClients[port] = tcpClient = new TcpClient();
                if (!tcpClient.ConnectAsync("127.0.0.1", port).Wait(50))
                    return false;
                NetworkStream stream = tcpClient.GetStream();
                stream.BeginRead(RuntimeUpdateSender._buffer, 0, RuntimeUpdateSender._buffer.Length, new AsyncCallback(RuntimeUpdateSender.ReplyReceived), (object)stream);
            }
            try
            {
                tcpClient.Client.Send(buffer, 0, buffer.Length, SocketFlags.None);
            }
            catch (SocketException ex)
            {
                if (!isReconnecting)
                    RuntimeUpdateSender.TrySendTcp(buffer, port, true);
                else
                    throw;
            }
            return true;
        }

        private static void ReplyReceived(IAsyncResult ar)
        {
            try
            {
                NetworkStream asyncState = (NetworkStream)ar.AsyncState;
                if (asyncState.EndRead(ar) == 0)
                    return;
                RuntimeUpdateSender._replyReceived = true;
                asyncState.BeginRead(RuntimeUpdateSender._buffer, 0, RuntimeUpdateSender._buffer.Length, new AsyncCallback(RuntimeUpdateSender.ReplyReceived), (object)asyncState);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        private static byte[] GetBuffer(string filePath, string[] propertiesToReset, bool is20Buffer)
        {
            byte[] numArray1 = new byte[2]
            {
        (byte) 190,
        (byte) 239
            };
            byte[] numArray2 = new byte[1] { byte.MaxValue };
            List<byte> list = ((IEnumerable<byte>)numArray1).ToList<byte>();
            list.Add((byte)1);
            string extension = Path.GetExtension(filePath);
            byte[] numArray3 = extension == null || !extension.Equals(".xaml", StringComparison.InvariantCultureIgnoreCase) ? File.ReadAllBytes(filePath) : Encoding.Unicode.GetBytes(File.ReadAllText(filePath));
            if (is20Buffer)
            {
                try
                {
                    XDocument doc = XDocument.Parse(Encoding.Unicode.GetString(numArray3));
                    string targetId = doc.Root.Attributes().Where<XAttribute>((Func<XAttribute, bool>)(a =>
                    {
                        if (a.Name.LocalName == "Class")
                            return a.Name.Namespace == doc.Root.GetNamespaceOfPrefix("x");
                        return false;
                    })).Select<XAttribute, string>((Func<XAttribute, string>)(a => a.Value)).FirstOrDefault<string>();
                    list.AddRange((IEnumerable<byte>)RuntimeUpdateSender.CreateMarkupBuffer(numArray3, targetId));
                }
                catch (Exception ex)
                {
                    throw new MalformedXamlException();
                }
            }
            else
                list.AddRange((IEnumerable<byte>)RuntimeUpdateSender.CreateMarkupBuffer(numArray3, filePath));
            byte[] propertiesBuffer = RuntimeUpdateSender.CreatePropertiesBuffer(RuntimeUpdateSender.GetPropertiesToReset(propertiesToReset));
            list.AddRange((IEnumerable<byte>)propertiesBuffer);
            list.AddRange((IEnumerable<byte>)numArray2);
            return list.ToArray();
        }

        private static byte[] CreatePropertiesBuffer(List<string> properties)
        {
            byte[] bytes = Encoding.Unicode.GetBytes(string.Join(",", (IEnumerable<string>)properties));
            return ((IEnumerable<byte>)BitConverter.GetBytes((ushort)bytes.Length)).Concat<byte>((IEnumerable<byte>)bytes).ToArray<byte>();
        }

        private static byte[] CreateMarkupBuffer(byte[] buffer, string targetId)
        {
            byte[] bytes1 = Encoding.Unicode.GetBytes(targetId);
            byte[] bytes2 = BitConverter.GetBytes(bytes1.Length);
            byte[] bytes3 = BitConverter.GetBytes(buffer.Length);
            byte[] bytes4 = BitConverter.GetBytes(RuntimeUpdateSender.Fletcher16(buffer));
            return ((IEnumerable<byte>)bytes2).Concat<byte>((IEnumerable<byte>)bytes1).Concat<byte>((IEnumerable<byte>)bytes3).Concat<byte>((IEnumerable<byte>)buffer).Concat<byte>((IEnumerable<byte>)bytes4).ToArray<byte>();
        }

        private static ushort Fletcher16(byte[] data)
        {
            ushort num1 = 0;
            ushort num2 = 0;
            for (int index = 0; index < data.Length; ++index)
            {
                num1 = (ushort)(((int)num1 + (int)data[index]) % (int)byte.MaxValue);
                num2 = (ushort)(((int)num2 + (int)num1) % (int)byte.MaxValue);
            }
            return (ushort)((uint)num2 << 8 | (uint)num1);
        }

        private static List<string> GetPropertiesToReset(string[] propertiesToReset)
        {
            if (propertiesToReset == null)
                return new List<string>();
            return ((IEnumerable<string>)propertiesToReset).Distinct<string>().ToList<string>();
        }
    }
}

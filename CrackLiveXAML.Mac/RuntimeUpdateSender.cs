using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml.Linq;

namespace LiveXAML
{
  public static class RuntimeUpdateSender
  {
    private static byte[] _buffer = new byte[204800];
    private static TcpClient _activeClient;
    public const int SendPort = 53030;

    public static void Send(string filepath, string[] propertiesToReset)
    {
      RuntimeUpdateSender.SendGeneric(filepath, propertiesToReset, false);
      RuntimeUpdateSender.SendGeneric(filepath, propertiesToReset, true);
    }

    private static void SendGeneric(string filePath, string[] propertiesToReset, bool is20Buffer)
    {
      try
      {
        byte[] buffer = RuntimeUpdateSender.GetBuffer(filePath, propertiesToReset, is20Buffer);
        try
        {
          Server.Send(buffer, is20Buffer);
        }
        catch (Exception ex)
        {
          //Log.LogError("Could not send to TCP server on 127.0.0.1: " + ex.Message);
          throw;
        }
        //Log.LogInfo("Buffer sent");
      }
      catch (Exception ex)
      {
        //Log.LogError("Sending runtime update failed: " + (object) ex);
        throw;
      }
    }

    private static bool TrySendTcp(byte[] buffer, bool isReconnecting = false)
    {
      //Log.LogError("Sending buffer (" + (object) buffer.Length + ")");
      if (isReconnecting || RuntimeUpdateSender._activeClient == null || !RuntimeUpdateSender._activeClient.Connected)
      {
        RuntimeUpdateSender._activeClient = new TcpClient();
        if (!RuntimeUpdateSender._activeClient.ConnectAsync("127.0.0.1", 53030).Wait(50))
          return false;
        NetworkStream stream = RuntimeUpdateSender._activeClient.GetStream();
        stream.BeginRead(RuntimeUpdateSender._buffer, 0, RuntimeUpdateSender._buffer.Length, new AsyncCallback(RuntimeUpdateSender.ReplyReceived), (object) stream);
      }
      try
      {
        new UdpClient(new IPEndPoint(IPAddress.Broadcast, 53050)).Send(buffer, buffer.Length);
        RuntimeUpdateSender._activeClient.Client.Send(buffer, 0, buffer.Length, SocketFlags.None);
      }
      catch (SocketException ex)
      {
        if (!isReconnecting)
          RuntimeUpdateSender.TrySendTcp(buffer, true);
        else
          throw;
      }
      return true;
    }

    private static void ReplyReceived(IAsyncResult ar)
    {
      try
      {
        NetworkStream asyncState = (NetworkStream) ar.AsyncState;
        if (asyncState.EndRead(ar) == 0)
          return;
        asyncState.BeginRead(RuntimeUpdateSender._buffer, 0, RuntimeUpdateSender._buffer.Length, new AsyncCallback(RuntimeUpdateSender.ReplyReceived), (object) asyncState);
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
      byte[] numArray2 = new byte[1]{ byte.MaxValue };
      List<byte> list = ((IEnumerable<byte>) numArray1).ToList<byte>();
      list.Add((byte) 1);
      string extension = Path.GetExtension(filePath);
      byte[] numArray3 = extension == null || !extension.Equals(".xaml", StringComparison.InvariantCultureIgnoreCase) ? System.IO.File.ReadAllBytes(filePath) : Encoding.Unicode.GetBytes(System.IO.File.ReadAllText(filePath));
      if (is20Buffer)
      {
        XDocument doc = XDocument.Parse(Encoding.Unicode.GetString(numArray3));
        string targetId = doc.Root.Attributes().Where<XAttribute>((Func<XAttribute, bool>) (a =>
        {
          if (a.Name.LocalName == "Class")
            return a.Name.Namespace == doc.Root.GetNamespaceOfPrefix("x");
          return false;
        })).Select<XAttribute, string>((Func<XAttribute, string>) (a => a.Value)).FirstOrDefault<string>();
        list.AddRange((IEnumerable<byte>) RuntimeUpdateSender.CreateMarkupBuffer(numArray3, targetId));
      }
      else
        list.AddRange((IEnumerable<byte>) RuntimeUpdateSender.CreateMarkupBuffer(numArray3, filePath));
      byte[] propertiesBuffer = RuntimeUpdateSender.CreatePropertiesBuffer(RuntimeUpdateSender.GetPropertiesToReset(propertiesToReset));
      list.AddRange((IEnumerable<byte>) propertiesBuffer);
      list.AddRange((IEnumerable<byte>) numArray2);
      return list.ToArray();
    }

    private static byte[] CreatePropertiesBuffer(List<string> properties)
    {
      byte[] bytes = Encoding.Unicode.GetBytes(string.Join(",", (IEnumerable<string>) properties));
      return ((IEnumerable<byte>) BitConverter.GetBytes((ushort) bytes.Length)).Concat<byte>((IEnumerable<byte>) bytes).ToArray<byte>();
    }

    private static byte[] CreateMarkupBuffer(byte[] buffer, string targetId)
    {
      byte[] bytes1 = Encoding.Unicode.GetBytes(targetId);
      byte[] bytes2 = BitConverter.GetBytes(bytes1.Length);
      byte[] bytes3 = BitConverter.GetBytes(buffer.Length);
      byte[] bytes4 = BitConverter.GetBytes(RuntimeUpdateSender.Fletcher16(buffer));
      return ((IEnumerable<byte>) bytes2).Concat<byte>((IEnumerable<byte>) bytes1).Concat<byte>((IEnumerable<byte>) bytes3).Concat<byte>((IEnumerable<byte>) buffer).Concat<byte>((IEnumerable<byte>) bytes4).ToArray<byte>();
    }

    private static ushort Fletcher16(byte[] data)
    {
      ushort num1 = 0;
      ushort num2 = 0;
      for (int index = 0; index < data.Length; ++index)
      {
        num1 = (ushort) (((int) num1 + (int) data[index]) % (int) byte.MaxValue);
        num2 = (ushort) (((int) num2 + (int) num1) % (int) byte.MaxValue);
      }
      return (ushort) ((uint) num2 << 8 | (uint) num1);
    }

    private static List<string> GetPropertiesToReset(string[] propertiesToReset)
    {
      if (propertiesToReset == null)
        return new List<string>();
      return ((IEnumerable<string>) propertiesToReset).Distinct<string>().ToList<string>();
    }
  }
}

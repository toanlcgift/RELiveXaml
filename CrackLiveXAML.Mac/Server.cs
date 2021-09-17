using Gtk;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace LiveXAML
{
  public class Server
  {
    private static ConcurrentDictionary<TcpListener, byte[]> _buffers = new ConcurrentDictionary<TcpListener, byte[]>();
    private static HashSet<System.Net.Sockets.Socket> _clients = new HashSet<System.Net.Sockets.Socket>();
    private static HashSet<System.Net.Sockets.Socket> _clients20 = new HashSet<System.Net.Sockets.Socket>();
    private static object _sync = new object();
    private static TcpListener _tcpListener;
    private static TcpListener _tcpListener20;
    private static TcpListener _handshakeListener;

    public static void Start()
    {
      try
      {
        Server._tcpListener = new TcpListener(IPAddress.Any, 53030);
        Server._tcpListener.Start();
        Server._buffers[Server._tcpListener] = new byte[102400];
        Server._tcpListener20 = new TcpListener(IPAddress.Any, 53032);
        Server._tcpListener20.Start();
        Server._buffers[Server._tcpListener20] = new byte[102400];
        Server._handshakeListener = new TcpListener(IPAddress.Any, 53031);
        Server._handshakeListener.Start();
        Server._tcpListener.BeginAcceptSocket(new AsyncCallback(Server.AcceptClient), (object) Server._tcpListener);
        Server._tcpListener20.BeginAcceptSocket(new AsyncCallback(Server.AcceptClient), (object) Server._tcpListener20);
        Server._handshakeListener.BeginAcceptSocket(new AsyncCallback(Server.AcceptHandshake), (object) null);
        Console.ReadKey();
      }
      catch (Exception ex)
      {
        Application.Invoke((EventHandler) ((param0, param1) => new MessageDialog((Window) null, DialogFlags.DestroyWithParent, MessageType.Error, ButtonsType.Close, "Unable to start server" + Environment.NewLine + ex.Message, new object[1]
        {
          (object) "Error"
        }).Show()));
      }
    }

    public static void Send(byte[] buffer, bool is20Buffer)
    {
      HashSet<System.Net.Sockets.Socket> socketSet = is20Buffer ? Server._clients20 : Server._clients;
      List<System.Net.Sockets.Socket> socketList = new List<System.Net.Sockets.Socket>();
      foreach (System.Net.Sockets.Socket socket in socketSet)
      {
        try
        {
          //Log.LogError("Sending to " + (object) socket.RemoteEndPoint);
          socket.Send(buffer, 0, buffer.Length, SocketFlags.None);
        }
        catch
        {
          socketList.Add(socket);
          //Log.LogError("Socket failed to send");
        }
      }
      foreach (System.Net.Sockets.Socket socket in socketList)
        socketSet.Remove(socket);
    }

    private static void AcceptHandshake(IAsyncResult ar)
    {
      try
      {
        //Log.LogInfo("Handshake received");
        Server._handshakeListener.BeginAcceptSocket(new AsyncCallback(Server.AcceptHandshake), (object) null);
        Server.SendHandshakeResponses(Server._handshakeListener.EndAcceptSocket(ar)).ContinueWith((System.Action<Task>) (t =>
        {
          if (t.Exception == null)
            return;
          Console.WriteLine("Handshake failed");
          Console.WriteLine((object) t.Exception);
        }));
      }
      catch (Exception ex)
      {
        //Log.LogError("AcceptHandshake failed: " + (object) ex);
        Application.Invoke((EventHandler) ((param0, param1) => new MessageDialog((Window) null, DialogFlags.DestroyWithParent, MessageType.Error, ButtonsType.Close, "AcceptHandshake failed: " + Environment.NewLine + ex.Message, new object[1]
        {
          (object) "Error"
        }).Show()));
      }
    }

    private static async Task SendHandshakeResponses(System.Net.Sockets.Socket socket)
    {
      for (int i = 0; i < 10; ++i)
      {
        await Task.Delay(50);
        socket.Send(new byte[1]{ (byte) 170 });
        Console.Write(".");
      }
      Console.WriteLine();
    }

    private static void AcceptClient(IAsyncResult ar)
    {
      try
      {
       //Log.LogInfo("Client connected");
        TcpListener asyncState = ar.AsyncState as TcpListener;
        asyncState.BeginAcceptSocket(new AsyncCallback(Server.AcceptClient), (object) asyncState);
        System.Net.Sockets.Socket socket = asyncState.EndAcceptSocket(ar);
        if (asyncState == Server._tcpListener)
          Server._clients.Add(socket);
        else if (asyncState == Server._tcpListener20)
          Server._clients20.Add(socket);
        Server.StartReceiving(socket, asyncState);
      }
      catch (Exception ex)
      {
        //Log.LogError("AcceptClient failed: " + (object) ex);
      }
    }

    private static void StartReceiving(System.Net.Sockets.Socket socket, TcpListener tcpListener)
    {
      try
      {
        byte[] buffer = Server._buffers[tcpListener];
        socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(Server.EndReceive), (object) new Server.ConnectionInfo(socket, tcpListener));
      }
      catch (Exception ex1)
      {
        //Log.LogError("BeginReceive failed: " + (object) ex1);
        try
        {
          Server._tcpListener.BeginAcceptSocket(new AsyncCallback(Server.AcceptClient), (object) null);
        }
        catch (Exception ex2)
        {
          //Log.LogError("BeginAcceptSocket failed: " + (object) ex2);
        }
      }
    }

    private static void EndReceive(IAsyncResult ar)
    {
      try
      {
        Server.ConnectionInfo asyncState = (Server.ConnectionInfo) ar.AsyncState;
        System.Net.Sockets.Socket socket1 = asyncState.Socket;
        TcpListener tcpListener = asyncState.TcpListener;
        int num = socket1.EndReceive(ar);
        HashSet<System.Net.Sockets.Socket> socketSet = tcpListener == Server._tcpListener ? Server._clients : Server._clients20;
        byte[] buffer = Server._buffers[tcpListener];
        //Log.LogInfo("Message received (" + (object) num + "), number of clients: " + (object) Server._clients.Count);
        if (num == 0)
        {
          socketSet.Remove(socket1);
        }
        else
        {
          List<System.Net.Sockets.Socket> socketList = new List<System.Net.Sockets.Socket>();
          foreach (System.Net.Sockets.Socket socket2 in socketSet)
          {
            try
            {
              //Log.LogInfo("Sending to " + (object) socket2.RemoteEndPoint);
              socket2.Send(buffer, 0, num, SocketFlags.None);
            }
            catch
            {
              socketList.Add(socket2);
              //Log.LogError("Socket failed to send");
            }
          }
          foreach (System.Net.Sockets.Socket socket2 in socketList)
            socketSet.Remove(socket2);
          Server.SendBroadcast(buffer, num);
          Server.StartReceiving(socket1, tcpListener);
        }
      }
      catch (Exception ex)
      {
        //Log.LogError("EndReceive failed: " + ex.Message);
      }
    }

    private static void SendBroadcast(byte[] buffer, int bytesToRead)
    {
      try
      {
        foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
          try
          {
            if (networkInterface.OperationalStatus == OperationalStatus.Up && networkInterface.SupportsMulticast && networkInterface.GetIPProperties().GetIPv4Properties() != null)
            {
              if (NetworkInterface.LoopbackInterfaceIndex != networkInterface.GetIPProperties().GetIPv4Properties().Index)
              {
                foreach (UnicastIPAddressInformation unicastAddress in networkInterface.GetIPProperties().UnicastAddresses)
                {
                  if (unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork)
                  {
                    UdpClient udpClient = new UdpClient(new IPEndPoint(unicastAddress.Address.Address, 0));
                    udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
                    udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontRoute, 1);
                    IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Broadcast, 53050);
                    int offset = 0;
                    while (offset < bytesToRead)
                    {
                      int size = Math.Min(bytesToRead - offset, 5000);
                      udpClient.Client.SendTo(buffer, offset, size, SocketFlags.None, (EndPoint) ipEndPoint);
                      offset += size;
                    }
                  }
                }
              }
            }
          }
          catch (Exception ex)
          {
            //Log.LogError("SendBroadcast failed for " + networkInterface.Description);
            //Log.LogError(ex.ToString());
          }
        }
      }
      catch (Exception ex)
      {
        //Log.LogError("SendBroadcast failed");
        //Log.LogError(ex.ToString());
      }
    }

    private struct ConnectionInfo
    {
      public TcpListener TcpListener;
      public System.Net.Sockets.Socket Socket;

      public ConnectionInfo(System.Net.Sockets.Socket socket, TcpListener tcpListener)
      {
        this.Socket = socket;
        this.TcpListener = tcpListener;
      }
    }
  }
}

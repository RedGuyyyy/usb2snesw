using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;
using System.Net.WebSockets;

using usb2snes.core;

namespace usb2snes.bridge
{
    public enum OpcodeType
    {
        // Special
        DeviceList,
        Boot,
        Menu,
        Reset,
        Stream,

        GetAddress,
        PutAddress,

        GetFile,
        PutFile,
        List,
        Remove,
        Rename,
        MakeDir,
    }

    public enum SpaceType
    {
        File,
        SNES,
        MSU,
    }

    // Message format:
    // {
    //   Device: COMN,
    //   Opcode: Boot,
    //   Operands: [ [ "fileName.sfc", 0 ] ]
    //   Space: File
    // }
    public class Message
    {
        public string Device { get; set; }
        public OpcodeType Opcode { get; set; }
        public List<Tuple<string, int>> Operands { get; set; }
        public SpaceType Space { get; set; }
    }

    public class Response
    {
        public List<string> Results { get; set; }
    }

    public class Server
    {
        public Server()
        {
            _q = new CommunicationQueue<Tuple<WebSocket,Message>>();
            _stop = false;
            _h = new ServerHandler(_q);
            _s = new Scheduler(_q);

            _sT = new Thread(_s.Run);
        }

        public void Stop()
        {
            _q.Stop();
            _s.Stop();
            _h.Stop();
        }

        private CommunicationQueue<Tuple<WebSocket, Message>> _q;

        bool _stop;
        ServerHandler _h;
        Scheduler _s;
        private Thread _sT;

        private class Scheduler
        {
            public Scheduler(CommunicationQueue<Tuple<WebSocket, Message>> q)
            {
                _q = q;
            }

            async public void Run()
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();

                while (!_stop)
                {
                    var t = _q.Dequeue();

                    if (t.Item1)
                    {
                        var s = t.Item2.Item1;
                        var m = t.Item2.Item2;

                        // perform snes operation
                        switch (m.Opcode)
                        {
                            case OpcodeType.Boot:
                                break;
                            case OpcodeType.DeviceList:
                                Response r = new Response();
                                var d = core.core.GetDeviceList();
                                foreach (var c in d) r.Results.Add(c.Name);
                                await s.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(r))), WebSocketMessageType.Text, true, CancellationToken.None);
                                break;
                            case OpcodeType.Menu:
                                break;
                            case OpcodeType.Reset:
                                break;
                            case OpcodeType.Stream:
                                break;

                            case OpcodeType.GetAddress:
                                break;
                            case OpcodeType.PutAddress:
                                break;

                            case OpcodeType.GetFile:
                                break;
                            case OpcodeType.PutFile:
                                break;
                            case OpcodeType.List:
                                break;
                            case OpcodeType.Remove:
                                break;
                            case OpcodeType.Rename:
                                break;
                            case OpcodeType.MakeDir:
                                break;
                        }
                    }
                }
            }

            public void Stop()
            {
                _stop = true;
            }

            private CommunicationQueue<Tuple<WebSocket, Message>> _q;
            private bool _stop = false;
        }

        private class ServerHandler : IHttpHandler
        {
            public ServerHandler(CommunicationQueue<Tuple<WebSocket, Message>> q)
            {
                _q = q;
            }

            public void ProcessRequest(HttpContext context)
            {
                if (context.IsWebSocketRequest)
                    context.AcceptWebSocketRequest(HandleWebSocket);
                else
                    context.Response.StatusCode = 400;
            }

            private async Task HandleWebSocket(WebSocketContext wsContext)
            {
                const int maxMessageSize = 1024;
                byte[] receiveBuffer = new byte[maxMessageSize];
                WebSocket s = wsContext.WebSocket;
                JavaScriptSerializer serializer = new JavaScriptSerializer();

                _sockets.Add(s);

                while (s.State == System.Net.WebSockets.WebSocketState.Open)
                {
                    WebSocketReceiveResult result = await s.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await s.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        // FIXME: we should handle data as binary
                        await s.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Cannot accept binary message", CancellationToken.None);
                    }
                    else
                    {
                        int count = result.Count;

                        while (result.EndOfMessage == false)
                        {
                            if (count >= maxMessageSize)
                            {
                                string closeMessage = string.Format("Maximum message size: {0} bytes.", maxMessageSize);
                                await s.CloseAsync(WebSocketCloseStatus.MessageTooBig, closeMessage, CancellationToken.None);
                                return;
                            }

                            result = await s.ReceiveAsync(new ArraySegment<Byte>(receiveBuffer, count, maxMessageSize - count), CancellationToken.None);
                            count += result.Count;
                        }

                        var messageString = Encoding.UTF8.GetString(receiveBuffer, 0, count);
                        var m = serializer.Deserialize<Message>(messageString);

                        _q.Enqueue(Tuple.Create(s, m));
                    }
                }
            }

            public bool IsReusable
            {
                get { return true; }
            }
            public void Stop()
            {
                foreach (var s in _sockets) s.Abort();
            }

            private List<WebSocket> _sockets = new List<WebSocket>();

            private CommunicationQueue<Tuple<WebSocket, Message>> _q;
        }

        // thread-aware queue class
        private class CommunicationQueue<T>
        {
            private readonly Queue<T> queue = new Queue<T>();

            public void Enqueue(T item)
            {
                lock (queue)
                {
                    queue.Enqueue(item);
                    if (queue.Count == 1) Monitor.PulseAll(queue);
                }
            }

            public Tuple<bool, T> Dequeue()
            {
                lock (queue)
                {
                    if (queue.Count == 0) Monitor.Wait(queue);
                    bool f = false;
                    T t = default(T);
                    if (queue.Count != 0)
                    {
                        f = true;
                        t = queue.Dequeue();
                    }
                    return Tuple.Create(f, t);
                }
            }

            public int Count()
            {
                lock (queue)
                {
                    return queue.Count;
                }
            }

            public void Stop()
            {
                lock (queue)
                {
                    Monitor.PulseAll(queue);
                }
            }
        }
    }

}

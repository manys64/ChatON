using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net.Sockets;
using System.IO;
using System.Threading;

using System.Net;
using Rssdp;

namespace Server
{
    class Server
    {

        static Socket listenerSocket;
        //Tu tez sockety priv chatow
        static List<ClientData> _clients;

        /// <summary>
        /// start server
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            Console.WriteLine("Starting Server...");
            listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _clients = new List<ClientData>();

            IPEndPoint ip = new IPEndPoint(IPAddress.Parse(Packet.GetIP4Adress()), 4242);
            listenerSocket.Bind(ip);

            Thread listenThread = new Thread(ListenThread);
            listenThread.Start();
            Console.WriteLine("Success... Listening IP: " + Packet.GetIP4Adress() + ":4242");
        }


        /// <summary>
        /// listen for new connections
        /// on wen connection create new ClientData and add to list of clients
        /// </summary>
        static void ListenThread()
        {
            while (true)
            {
                listenerSocket.Listen(0);
                _clients.Add(new ClientData(listenerSocket.Accept()));
            }
        }


        /// <summary>
        /// receive data from socket
        /// then data received call to data manager
        /// </summary>
        public static void Data_IN(object cSocket)
        {
            Socket clientSocket = (Socket)cSocket;

            byte[] Buffer;
            int readBytes;

            while (clientSocket.Connected)
            {
                try
                {
                    Buffer = new byte[clientSocket.SendBufferSize];
                    readBytes = clientSocket.Receive(Buffer);

                    if (readBytes > 0)
                    {
                        Packet p = new Packet(Buffer);
                        DataManager(p);
                    }
                }
                catch (ObjectDisposedException ex)
                {
                    Console.WriteLine("Client Disconnected.");
                    Console.WriteLine("IOException source: {0}", ex.Source);
                }
            }
        }


        /// <summary>
        /// manage all received packets by PacketType
        /// </summary>
        /// <param name="p"></param>
        public static void DataManager(Packet p)
        {
            switch (p.packetType)
            {
                case PacketType.Chat:
                    SendMessageToCLients(p);
                    break;


                case PacketType.CloseConnection:
                    CancellationTokenSource cts = new CancellationTokenSource();
                    var exitClient = GetClientByID(p);

                    CloseClientConnection(exitClient);
                    RemoveClientFromList(exitClient);
                    SendMessageToCLients(p);
                    AbortClientThread(cts, exitClient);
                    break;
            }
        }

        /// <summary>
        /// send message for each client in clietn list
        /// </summary>
        /// <param name="p"></param>
        public static void SendMessageToCLients(Packet p)
        {
            foreach (ClientData c in _clients)
            {
                c.clientSocket.Send(p.ToBytes());
            }
        }

        /// <summary>
        /// get specific client by id
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        private static ClientData GetClientByID(Packet p)
        {
            return (from client in _clients
                    where client.id == p.senderID
                    select client)
                    .FirstOrDefault();

        }

        private static void CloseClientConnection(ClientData c)
        {
            c.clientSocket.Close();
        }
        private static void RemoveClientFromList(ClientData c)
        {
            _clients.Remove(c);
        }
      
        private static void AbortClientThread(CancellationTokenSource token, ClientData client)
        {

            token.Cancel();
            if (token.IsCancellationRequested)
            {
                Console.WriteLine("Abbording client " + client.id);
            }
        }
    }
}

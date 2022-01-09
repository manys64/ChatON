using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Server
{
    class Server
    {

        static Socket listenerSocket;
        //Tu tez sockety priv chatow
        static List<ClientData> _clients;
        static List<string> clientNames = new List<string>();
        static List<Chat> chats = new List<Chat>();

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

            chats.Add(new Chat());
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

        public static bool nickVal(Packet p)
        {
            string enteredNick = p.data[0];

            if (clientNames.Contains(enteredNick))
            {
                return true;

            }
            else
            {
                return false;
            }


        }


        /// <summary>
        /// manage all received packets by PacketType
        /// </summary>
        /// <param name="p"></param>
        /// <param name="p"></param>
        public static void DataManager(Packet p)
        {
            switch (p.packetType)
            {
                case PacketType.Chat:
                    chatsReload(p);
                    break;

                case PacketType.CloseConnection:
                    CancellationTokenSource cts = new CancellationTokenSource();
                    var exitClient = GetClientByID(p);
                    clientNames.Remove(p.data[0]);
                    p.clientNames = clientNames;
                    var clientChatList = Chat.getChatsOfUser(chats, p.data[0]);
                    foreach (Chat chat in clientChatList)
                    {
                        if (chat != chats[0])
                            chats.Remove(chat);
                    }
                    CloseClientConnection(exitClient);
                    RemoveClientFromList(exitClient);
                    SendMessageToCLients(p);
                    AbortClientThread(cts, exitClient);
                    break;
            }
        }

        public static void chatsReload(Packet p)
        {
            string clientName = p.data[0];
            string name = clientNames.Find(x => x == clientName);
            if (String.IsNullOrWhiteSpace(name))
            {
                clientNames.Add(clientName);
                var client = GetClientByID(p);
                client.name = clientName;
                foreach (string cName in clientNames)
                {
                    if (cName != clientName)
                    {
                        Chat chat = new Chat();
                        chat.Users.Add(cName);
                        chat.Users.Add(clientName);
                        chats.Add(chat);
                    }
                }
            }
            p.clientNames = clientNames;

            SendMessageToCLients(p);
        }



        /// <summary>
        /// send message for each client in clietn list
        /// </summary>
        /// <param name="p"></param>
        public static void SendMessageToCLients(Packet p)
        {
            var mainChat = chats[0];
            var chatId = p.data[2];
            Chat chat;
            if (string.IsNullOrWhiteSpace(chatId))
            {
                chatId = mainChat.Id;
                chat = mainChat;
            }
            else
            {
                chat = chats.Find(x => x.Id == chatId) ?? mainChat;
            }
            chat.Messages.Add(new Message(p.data[0], DateTime.Now, p.data[1]));
            
            foreach (ClientData c in _clients)
            {
                List<Chat> clientChatList = Chat.getChatsOfUser(chats, c.name);
                p.chats = clientChatList;
                p.chats.Add(mainChat);
                var isPermited = p.chats.Find(x => x.Id == chatId) != null;
                p.data[2] = isPermited ? chatId : null;
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

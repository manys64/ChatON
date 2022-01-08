using Server;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
/// <summary>
/// Client Part of the code: UI + Net Socket code
/// </summary>
namespace ChatON
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static Socket socket;
        public static IPAddress ipAdress;
        public static string ID;
        public static string login;
        public static Thread thread;
        public static bool isConnected = false;
        private static string chatId;
        private static string mainChatId;
        public static string mainChatName = "Czat ogólny";

        public MainWindow()
        {
            InitializeComponent();
            serverIP.Text = Packet.GetIP4Adress();
        }

        /// <summary>
        /// on window close event update server for close connection
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainWindow_Closed(object sender, EventArgs e)
        {
            if (isConnected)
            {
                Packet p = new Packet(PacketType.CloseConnection, ID);

                p.data.Add(login);
                p.data.Add(login + " opuścił(a) czat.");
                p.data.Add(chatId);

                socket.Send(p.ToBytes());

                socket.Close();
                isConnected = false;
                thread.Abort();
            }
        }


        /// <summary>
        /// connect Button click event
        /// validate login and ip adress
        /// connect if valid and start new thread for receiving data from server
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConnectBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Login.Text) || Login.Text.Length > 17)//login validation
            {
                //  LoginRequireShowMsg(); //TODO TEKST POKAZANY ZA DLUGI LOGIN
            }
            //else if (!IPAddress.TryParse(serverIP.Text, out ipAdress))//ip validation
            //{
            //  //  ValidIPRequireShowMsg();
            //}
            //else//connection to server
            //{
            string ip = serverIP.Text;
            IPAddress.TryParse(ip, out ipAdress);
            //AddMsgToBoard(ip, "System");

            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint ipEndPoint = new IPEndPoint(ipAdress, 4242);

            try
            {
                socket.Connect(ipEndPoint);
                login = Login.Text;

                isConnected = true;
                ConnectBtn.IsEnabled = false;
                SendBtn.IsEnabled = true;

                thread = new Thread(Data_IN);
                thread.Start();

                MasterChat(new Chat());
            }
            catch (SocketException)
            {
                var errorChat = new Chat();
                errorChat.Users = new List<string> { login, "System" };
                errorChat.Messages = new List<Message> { new Message("System", DateTime.Now, "Error during connecting to server") };
                AddMsgToBoard(errorChat);
            }
            //}
        }


        /// <summary>
        /// send message to server
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SendBtn_Click(object sender, RoutedEventArgs e)
        {
            string msg = Msg.Text;
            Msg.Text = string.Empty;

            Packet p = new Packet(PacketType.Chat, ID);
            p.data.Add(login);
            p.data.Add(msg);
            p.data.Add(chatId);
            socket.Send(p.ToBytes());
        }

        //dodane wlasnorecznie zeby tez na enter sie wysylalo. nie moze byc okno widoczne poki nie jestes na czacie bo wywali
        private void OnKeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                //TODO ZE JAK MSG JEST PUSTE ZEBY NIE MOZNA BYLO WYSLAC
                string msg = Msg.Text;
                Msg.Text = string.Empty;

                Packet p = new Packet(PacketType.Chat, ID);
                p.data.Add(login);
                p.data.Add(msg);
                p.data.Add(chatId);
                socket.Send(p.ToBytes());
            }
        }




        /// <summary>
        /// receive data from socket
        /// then data received call to data manager
        /// </summary>
        private void Data_IN()
        {
            byte[] buffer;
            int readBytes;

            while (true)
            {
                try
                {
                    buffer = new byte[socket.SendBufferSize];
                    readBytes = socket.Receive(buffer);

                    if (readBytes > 0)
                    {
                        DataManager(new Packet(buffer));
                    }
                }
                catch (SocketException)
                {
                    ConnectionToServerLost();
                }
            }
        }


        /// <summary>
        /// manage all received packages by PacketType
        /// </summary>
        /// <param name="p"></param>
        private void DataManager(Packet p)
        {
            switch (p.packetType)
            {
                case PacketType.Registration:
                    ID = p.data[0];
                    Packet packet = new Packet(PacketType.Chat, ID);
                    packet.data.Add(login);
                    packet.data.Add(login + " dołączył(a) do czatu.");
                    packet.data.Add(chatId);
                    socket.Send(packet.ToBytes());
                    break;
                case PacketType.Chat:
                case PacketType.CloseConnection:
                    AddPrivateChat(p.clientNames, p.chats);
                    if (p.chats.Count > 0)
                    {
                        mainChatId ??= p.data[2];
                        chatId = p.data[2] ?? chatId;
                        var mainChat = p.chats.Find(x => x.Id == chatId);
                        if (mainChat != null)
                        {
                            AddMsgToBoard(mainChat);
                        }

                    }
                    break;
            }
        }


        /// <summary>
        /// Server connection lost
        /// add message to user, close socket and thread
        /// enable to user try to connect to server
        /// </summary>
        private void ConnectionToServerLost()
        {
            var errorChat = new Chat();
            errorChat.Users = new List<string> { login, "System" };
            errorChat.Messages = new List<Message> { new Message("Server", DateTime.Now, "Server disconnected") };
            AddMsgToBoard(errorChat);

            this.Dispatcher.Invoke(new Action(() =>
            {
                ConnectBtn.IsEnabled = true;
                SendBtn.IsEnabled = false;
            }));

            socket.Close();
            thread.Abort();
        }

        /// <summary>
        /// add new message to chat board
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="user"></param>
        private void AddMsgToBoard(Chat chat)
        { 
            this.Dispatcher.Invoke(new Action(() =>
            {
                if (chat.Id == mainChatId)
                {
                    chatName.Text = mainChatName;
                }
                else
                {
                    chatName.Text = chat.Users.Find(x => x != login);
                }
                MsgBoard.Children.Clear();
                foreach (Message msg in chat.Messages)
                {
                    TextBox messageHeader = new TextBox();
                    messageHeader.FontSize = 19;//nie dziala tak
                    messageHeader.Foreground = Brushes.Black;
                    messageHeader.FontWeight = FontWeights.Bold;
                    // cel w innym kolorze i w innych fontsajzie. solution: moze dwa razy invoke action? Nope
                    messageHeader.Text += Environment.NewLine + msg.UserName + "     " +
                                     msg.Date.ToString("dd/MM/yyyy H:mm");
                    messageHeader.BorderBrush = Brushes.Transparent;

                    TextBox message = new TextBox();
                    message.FontSize = 15;
                    message.Foreground = Brushes.Purple;
                    int messLines = msg.MessageString.Length / 40;
                    message.Text += Environment.NewLine + msg.MessageString;
                    //TODO MSG MA ROBIC NEWLINE zaleznie od wartosci
                    message.BorderBrush = Brushes.BlanchedAlmond;
                    MsgBoard.Children.Add(messageHeader);
                    MsgBoard.Children.Add(message);
                }
                MsgBoardScroll.ScrollToEnd();
                //  MsgBoard.UpdateLayout();
            }));
        }

        private void AddPrivateChat(List<string> clients, List<Chat> chats)
        {
            this.Dispatcher.Invoke(new Action(() =>
            {
                sp.Children.Clear();
                MasterChat(chats.Find(x => x.Id == mainChatId));
            }));
            foreach (string client in clients)
            {
                this.Dispatcher.Invoke(new Action(() =>
                {
                    if (client != login)
                    {
                        Button privateChat = new Button();

                        privateChat.Content = client;
                        privateChat.Name = "newButton" + client;
                        privateChat.Foreground = Brushes.White;
                        privateChat.Background = Brushes.MediumPurple;
                        privateChat.Height = 45;
                        privateChat.Width = 140;
                        Chat chat = Chat.getChatsOfUser(chats, client)[0];
                        privateChat.DataContext = chat;
                        privateChat.Click += new RoutedEventHandler(ChangeChat);

                        sp.Children.Add(privateChat);
                    }

                }));
            }
        }

        private void ChangeChat(Object sender, RoutedEventArgs eventArgs)
        {
            Button button = (Button)sender;
            Chat chat = (Chat)button.DataContext;
            chatId = chat.Id;
            AddMsgToBoard(chat);
        }

        private void MasterChat(Chat chat)
        {
            Button czatOgolny = new Button();

            czatOgolny.Content = mainChatName;
            czatOgolny.Name = "CzatOgolny";
            czatOgolny.Foreground = Brushes.White;
            czatOgolny.Background = Brushes.MediumPurple;
            czatOgolny.Height = 45;
            czatOgolny.Width = 140;
            czatOgolny.DataContext = chat;
            czatOgolny.Click += new RoutedEventHandler(ChangeChat);

            sp.Children.Add(czatOgolny);
        }

    }
}

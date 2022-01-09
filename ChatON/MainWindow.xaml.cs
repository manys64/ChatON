using Server;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
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
                //  thread.Abort();
                CancellationTokenSource cts = new CancellationTokenSource();
                AbortClientThread(cts);
            }
        }

        private static void AbortClientThread(CancellationTokenSource token)
        {

            token.Cancel();

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
          
            bool loginHasSpace = Login.Text.Contains(" ");
            if (string.IsNullOrWhiteSpace(Login.Text) || loginHasSpace || invalidIp(serverIP.Text))//login validation
            {
                if (string.IsNullOrWhiteSpace(Login.Text) || loginHasSpace)
                {
                    LoginRequireShowMsg();

                }
                else if (invalidIp(serverIP.Text))
                {
                    IPRequireShowMsg();
                }
           
                else
                {
                    LoginRequireShowMsg();
                    IPRequireShowMsg();
                }
              
            }
            else//connection to server
            {
                ClearRequireMsg();
                IPClearRequireMsg();
                showAllUIAfterConnection();
                string ip = serverIP.Text;
                IPAddress.TryParse(ip, out ipAdress);

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
            }

        }

        /// <summary>
        /// send message to server
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SendBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(Msg.Text))
            {
                string msg = Msg.Text;
                Msg.Text = string.Empty;

                Packet p = new Packet(PacketType.Chat, ID);
                p.data.Add(login);
                p.data.Add(msg);
                p.data.Add(chatId);
                socket.Send(p.ToBytes());
            }

        }

     

        private void OnKeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(Msg.Text))
            {
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
                    login = login +"#"+ ID.Substring(0, 4);
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
            errorChat.Messages = new List<Message> { new Message("Server", DateTime.Now, "Server nie jest aktywny.") };
            AddMsgToBoard(errorChat);

            this.Dispatcher.Invoke(new Action(() =>
            {
                ConnectBtn.IsEnabled = true;
                SendBtn.IsEnabled = false;
            }));

            socket.Close();
            //thread.Abort();
            CancellationTokenSource cts = new CancellationTokenSource();
            AbortClientThread(cts);
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
                    messageHeader.FontSize = 19;
                    messageHeader.Foreground = Brushes.Black;
                    messageHeader.FontWeight = FontWeights.Bold;
                    messageHeader.Text += Environment.NewLine + msg.UserName + "     " +
                                     msg.Date.ToString("dd/MM/yyyy H:mm");
                    messageHeader.BorderBrush = Brushes.Transparent;

                    TextBox message = new TextBox();
                    message.FontSize = 15;
                    message.Foreground = Brushes.Purple;
                    int chunkSize = 39;
                int stringLength = msg.MessageString.Length;

                for (int i = 0; i < stringLength; i += chunkSize)
                {
                    if (i + chunkSize > stringLength) chunkSize = stringLength - i;
                    string lastString;
                    if (msg.MessageString[i + chunkSize - 1].ToString() != " " && i + chunkSize < stringLength && msg.MessageString[i + chunkSize].ToString() != " ")
                    {
                        lastString = "-";
                    }
                    else
                    {
                        lastString = " ";
                    }
                    message.Text += Environment.NewLine + msg.MessageString.Substring(i, chunkSize) + lastString;
                }
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
                        string clientBtnName = Reverse(client);
                        clientBtnName = clientBtnName.Remove(4, 1);
                        clientBtnName = Reverse(clientBtnName);
                        privateChat.Name = "newButton" + clientBtnName;
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

        public static string Reverse(string s)
        {
            char[] charArray = s.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
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

        private void LoginRequireShowMsg()
        {
            LoginRequire.Visibility = Visibility.Visible;
        }


        private void ClearRequireMsg()
        {
            LoginRequire.Visibility = Visibility.Hidden;
        }

        private void IPRequireShowMsg()
        {
            IPRequire.Visibility = Visibility.Visible;
        }


        private void IPClearRequireMsg()
        {
            IPRequire.Visibility = Visibility.Hidden;
        }

        private void Msg_GotFocus(object sender, RoutedEventArgs e)
        {
            string txt = Msg.Text;
            string initialString = "Wpisz wiadomość.";
            if (txt == initialString)
            {
                Msg.Text = "";
            }
        }

        private bool invalidIp(string ip)
        {

            Match match = Regex.Match(ip, @"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}");
            if (!match.Success)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private void showAllUIAfterConnection()
        {
            LeftGrid.Visibility = Visibility.Visible;
            chatName.Visibility = Visibility.Visible;
            MainGrid.Visibility = Visibility.Visible;

            InitialGrid.Visibility = Visibility.Hidden;
        }


    }
}



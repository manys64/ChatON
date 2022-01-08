using System;
using System.Collections.Generic;
using System.Text;

namespace Server
{
    [Serializable]
    public class Chat
    {
        List<string> users;
        List<Message> messages;
        string id;

        public Chat()
        {
            id = Guid.NewGuid().ToString();
            users = new List<string>();
            messages = new List<Message>();
        }

        public List<string> Users { get => users; set => users = value; }
        public List<Message> Messages { get => messages; set => messages = value; }
        public string Id { get => id; set => id = value; }

        public static List<Chat> getChatsOfUser(List<Chat> chats, string user)
        {
            var findChats = new List<Chat>();
            foreach (Chat chat in chats)
            {
                var findChat = chat.users.Find(x => x == user);
                if (!String.IsNullOrWhiteSpace(findChat))
                {
                    findChats.Add(chat);
                }
            }
            return findChats;
        }
    }

    [Serializable]
    public class Message
    {
        private string userName;
        private DateTime date;
        private string message;

        public Message(string userName, DateTime date, string message)
        {
            this.userName = userName;
            this.date = date;
            this.message = message;
        }

        public string MessageString { get => message; set => message = value; }
        public DateTime Date { get => date; set => date = value; }
        public string UserName { get => userName; set => userName = value; }

    }
}

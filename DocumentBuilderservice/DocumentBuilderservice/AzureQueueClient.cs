using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentBuilderservice
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;

    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Messaging;

    public class AzureQueueClient
    {
        private SubscriptionClient _subscibtionClient;

        private int _sequence;

        public AzureQueueClient(string clientUniqueName)
        {
            NamespaceManager namespaceManager = NamespaceManager.Create();
            var topicName = "SettingsTopic";
            try
            {
                if (namespaceManager.TopicExists(topicName))
                {
                    if (namespaceManager.SubscriptionExists(topicName, clientUniqueName))
                    {
                        namespaceManager.DeleteSubscription(topicName, clientUniqueName);
                    }

                    namespaceManager.CreateSubscription(topicName, clientUniqueName);
                }
            }
            catch (Exception e)
            {
                // ignored
            }
            _subscibtionClient = SubscriptionClient.Create(topicName, clientUniqueName, ReceiveMode.ReceiveAndDelete);
        }

        public bool SendFile(string filePath)
        {
            return SendFile("FileQueue", filePath, 256000);
        }

        public bool SendFile(string queueName, string filePath, int chunkSize)
        {
            try
            {
                var client = QueueClient.Create(queueName);
                byte[] bytes = File.ReadAllBytes(filePath);
                var f = CutFile(bytes, chunkSize);
                foreach (var message in f)
                {
                    client.Send(message);
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        public bool SendStatusMessage(string message)
        {
            return SendStatusMessage("StatusQueue", message);
        }

        public bool SendStatusMessage(string statusQueueName, string message)
        {
            try
            {
                var client = QueueClient.Create(statusQueueName);
                client.Send(new BrokeredMessage(message));
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        public Dictionary<string, int> ReceiveNewSettings()
        {
            var settinsDict = new Dictionary<string, int>();
            var message = _subscibtionClient.Receive(new TimeSpan(1));
            if (message != null)
            {
                var body = message.GetBody<string>();
                if (body == "New Configuration")
                {
                    var timeoutSetting = (int)message.Properties["Timeout"];
                    settinsDict.Add("Timeout", timeoutSetting);
                }
                if (body == "Update Status")
                {
                    settinsDict.Add("StatusUpdate", 1);
                }
            }

            return settinsDict;
        }

        private List<BrokeredMessage> CutFile(byte[] infile, int chunkSize)
        {
            var outList = new List<BrokeredMessage>();
            var t = infile.Length / chunkSize;
            for (int i = 0; i <= infile.Length / chunkSize; i++)
            {
                var arr = infile.Skip(chunkSize * i).Take(chunkSize).ToArray();
                var partCount = new KeyValuePair<string, object>("PartCount", infile.Length / chunkSize);
                var part = new KeyValuePair<string, object>("Part", i);
                var sequenceNumber = new KeyValuePair<string, object>("Sequence", _sequence);
                outList.Add(
                    new BrokeredMessage(arr)
                    {
                        ContentType = "application/octet-stream",
                        Properties = { partCount, part, sequenceNumber }
                    });
            }

            _sequence++;
            return outList;
        }
    }
}

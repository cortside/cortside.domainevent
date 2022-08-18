using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Amqp;
using Newtonsoft.Json;

namespace Cortside.DomainEvent {
    public class DomainEventMessage {
        public static DomainEventMessage CreateGenericInstance(Type dataType, Message message) {
            string body = GetBody(message);

            List<string> errors = new List<string>();
            var data = JsonConvert.DeserializeObject(body, dataType,
                new JsonSerializerSettings {
                    Error = (object sender, Newtonsoft.Json.Serialization.ErrorEventArgs args) => {
                        errors.Add(args.ErrorContext.Error.Message);
                        args.ErrorContext.Handled = true;
                    }
                });
            if (errors.Count > 0) {
                throw new JsonSerializationException($"Message {message.Properties.MessageId} rejected because of errors deserializing messsage body: {string.Join(", ", errors)}");
            }

            var eventType = typeof(DomainEventMessage<>).MakeGenericType(dataType);
            dynamic domainEvent = Activator.CreateInstance(eventType);
            ((DomainEventMessage)domainEvent).Data = (dynamic)data;
            domainEvent.Message = message;

            return domainEvent;
        }

        public static string GetBody(Message message) {
            string body = null;
            // Get the body
            if (message.Body is string) {
                body = message.Body as string;
            } else if (message.Body is byte[]) {
                using (var reader = XmlDictionaryReader.CreateBinaryReader(
                    new MemoryStream(message.Body as byte[]),
                    null,
                    XmlDictionaryReaderQuotas.Max)) {
                    var doc = new XmlDocument();
                    doc.Load(reader);
                    body = doc.InnerText;
                }
            } else {
                throw new ArgumentException($"Message {message.Properties.MessageId} has body with an invalid type {message.Body.GetType()}");
            }

            return body;
        }

        internal Message Message { get; set; }
        public string MessageId => Message?.Properties?.MessageId;
        public string CorrelationId => Message?.Properties?.CorrelationId;
        [Obsolete("Use EventType instead.")]
        public string MessageTypeName => EventType;
        public string EventType => Message?.ApplicationProperties[Constants.MESSAGE_TYPE_KEY] as string ?? Message?.ApplicationProperties[Constants.MESSAGE_TYPE_KEY_OLD] as string;
        public int DeliveryCount => Convert.ToInt32(Message?.Header?.DeliveryCount);
        public object Data { get; set; }


        // header
        //private bool durable;

        //private byte priority;

        //private uint ttl;

        //private bool firstAcquirer;

        //private uint deliveryCount;


        // properties
        //private object messageId;

        //private byte[] userId;

        //private string to;

        //private string subject;

        //private string replyTo;

        //private object correlationId;

        //private Symbol contentType;

        //private Symbol contentEncoding;

        //private DateTime absoluteExpiryTime;

        //private DateTime creationTime;

        //private string groupId;

        //private uint groupSequence;

        //private string replyToGroupId;

    }

    public class DomainEventMessage<T> : DomainEventMessage {
        public new T Data {
            get { return (T)base.Data; }
            set { base.Data = value; }
        }
    }
}

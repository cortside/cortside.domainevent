using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
            if (message.Body is string s) {
                body = s;
            } else if (message.Body is byte[] bytes) {
                if (bytes[0] == 64) {
                    using (var reader = XmlDictionaryReader.CreateBinaryReader(new MemoryStream(bytes), null, XmlDictionaryReaderQuotas.Max)) {
                        var doc = new XmlDocument();
                        doc.Load(reader);
                        body = doc.InnerText;
                    }
                } else {
                    body = Encoding.UTF8.GetString(bytes);
                }
            } else {
                throw new ArgumentException($"Message {message.Properties.MessageId} has body with an invalid type {message.Body.GetType()}");
            }

            return body;
        }

        internal Message Message { get; set; }
        public string MessageId => Message?.Properties?.MessageId;
        public string CorrelationId => Message?.Properties?.CorrelationId;
        public string MessageTypeName => Message?.ApplicationProperties[Constants.MESSAGE_TYPE_KEY] as string;
        public int DeliveryCount => Convert.ToInt32(Message?.Header?.DeliveryCount);
        public object Data { get; set; }
    }

    public class DomainEventMessage<T> : DomainEventMessage {
        public new T Data {
            get { return (T)base.Data; }
            set { base.Data = value; }
        }
    }
}

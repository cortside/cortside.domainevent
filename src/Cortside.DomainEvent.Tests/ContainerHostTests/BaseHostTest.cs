using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using Amqp;
using Amqp.Listener;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cortside.DomainEvent.Tests.ContainerHostTests {
    [CollectionDefinition("dbcontexttests", DisableParallelization = true)]
    public class BaseHostTest : IDisposable {
        protected TimeSpan Timeout = TimeSpan.FromMilliseconds(5000);
        protected ContainerHost host;
        protected ILinkProcessor linkProcessor;
        protected readonly DomainEventReceiverSettings receiverSettings;
        protected readonly DomainEventPublisherSettings publisterSettings;
        protected readonly Random random;
        protected readonly ServiceProvider provider;
        protected readonly Dictionary<string, Type> eventTypes;

        protected Address Address { get; set; }

        public BaseHostTest() {
            this.random = new Random();
            var start = random.Next(10000, Int16.MaxValue);
            var port = GetAvailablePort(start);

            this.receiverSettings = new DomainEventReceiverSettings() {
                Protocol = "amqp",
                PolicyName = "guest",
                Key = "guest",
                Namespace = $"localhost:{port}",
                Queue = "queue",
                AppName = "unittest" + port.ToString()
            };

            this.publisterSettings = new DomainEventPublisherSettings() {
                Protocol = "amqp",
                PolicyName = "guest",
                Key = "guest",
                Namespace = $"localhost:{port}",
                Topic = "/exchange/test/",
                AppName = "unittest" + port.ToString()
            };
            this.Address = new Address(publisterSettings.ConnectionString);

            this.host = new ContainerHost(this.Address);
            this.host.Listeners[0].SASL.EnableExternalMechanism = true;
            this.host.Listeners[0].SASL.EnableAnonymousMechanism = true;
            this.host.Open();

            eventTypes = new Dictionary<string, Type> {
                { typeof(TestEvent).FullName, typeof(TestEvent) }
            };

            var services = new ServiceCollection();
            provider = services.BuildServiceProvider();
        }

        public void Dispose() {
            if (this.host != null) {
                this.host.Close();
            }
        }

        /// <summary>
        /// checks for used ports and retrieves the first free port
        /// </summary>
        /// <returns>the free port or 0 if it did not find a free port</returns>
        private int GetAvailablePort(int startingPort) {
            IPEndPoint[] endPoints;
            List<int> portArray = new List<int>();

            IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();

            //getting active connections
            TcpConnectionInformation[] connections = properties.GetActiveTcpConnections();
            portArray.AddRange(from n in connections
                               where n.LocalEndPoint.Port >= startingPort
                               select n.LocalEndPoint.Port);

            //getting active tcp listners - WCF service listening in tcp
            endPoints = properties.GetActiveTcpListeners();
            portArray.AddRange(from n in endPoints
                               where n.Port >= startingPort
                               select n.Port);

            //getting active udp listeners
            endPoints = properties.GetActiveUdpListeners();
            portArray.AddRange(from n in endPoints
                               where n.Port >= startingPort
                               select n.Port);

            portArray.Sort();

            for (int i = startingPort; i < UInt16.MaxValue; i++)
                if (!portArray.Contains(i))
                    return i;

            return 0;
        }
    }
}

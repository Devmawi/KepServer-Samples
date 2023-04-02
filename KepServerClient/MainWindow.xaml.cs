using MQTTnet.Client;
using MQTTnet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Text.Json;
using System.Text.Unicode;
using Opc.Ua.Client;
using Opc.Ua;

namespace KepServerClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // http://127.0.0.1:39320/ (short documentation)
        // http://127.0.0.1:39320/iotgateway/browse
        // http://127.0.0.1:39320/iotgateway/read
        private readonly HttpClient _httpClient = new HttpClient() { BaseAddress = new Uri("http://127.0.0.1:39320/iotgateway/") };
        private readonly IMqttClient _mqttClient = new MqttFactory().CreateMqttClient();
        private Session session;

        public MainWindow()
        {
            InitializeComponent();
            SubscribeByMqtt();
            SubcripeToMonitoredItem();
        }

        public async void SubscribeByMqtt()
        {

            var mqttClientOptions = new MqttClientOptionsBuilder().WithTcpServer("localhost").Build();

            // Setup message handling before connecting so that queued messages
            // are also handled properly. When there is no event handler attached all
            // received messages get lost.
            _mqttClient.ApplicationMessageReceivedAsync += e =>
            {
                var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                var res = JsonSerializer.Deserialize<IotGatewayMqttMessage>(payload);

                Dispatcher.Invoke(() =>
                {
                    if (cbEnableMqttSubscription.IsChecked == true)
                        Value.Text = res?.Values.Last().Value.ToString();
                });

                return Task.CompletedTask;
            };

            await _mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);

            var mqttSubscribeOptions = new MqttFactory().CreateSubscribeOptionsBuilder()
                .WithTopicFilter(
                    f =>
                    {
                        f.WithTopic("iotgateway");
                    })
                .Build();

            await _mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);


        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var res = await _httpClient.GetFromJsonAsync<IotGatewayReadResponse>("read?ids=SimChannel.SimDevice.Counter");
            Value.Text = res?.ReadResults[0].Value.ToString();
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var iotGatewayWriteRequest = new IotGatewayWriteRequest[]
            {
                new IotGatewayWriteRequest()
                {
                    Id=TagName.Text,
                    Value=int.Parse(Value.Text)
                }
            };
            var res = await _httpClient.PostAsJsonAsync<IotGatewayWriteRequest[]>("write", iotGatewayWriteRequest);
            if (!res.IsSuccessStatusCode)
            {
                MessageBox.Show("Error :(", "Result", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                MessageBox.Show("Success :)", "Result", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void Button_Click_2(object sender, RoutedEventArgs e)
        {
            var iotGatewayWriteRequest = new IotGatewayWriteRequest[]
            {
                new IotGatewayWriteRequest()
                {
                    Id=TagName.Text,
                    Value=int.Parse(Value.Text)
                }
            };

            // more on https://github.com/dotnet/MQTTnet/blob/master/Samples/Client/Client_Publish_Samples.cs
            // https://www.kepware.com/getattachment/c5c35697-8a91-4273-8077-b28fe5d60d8c/iot-gateway-manual.pdf (p. 26)
            var mqttFactory = new MqttFactory();

            using (var mqttClient = mqttFactory.CreateMqttClient())
            {
                var mqttClientOptions = new MqttClientOptionsBuilder()
                    .WithTcpServer("localhost")
                    .Build();

                var payload = JsonSerializer.Serialize(iotGatewayWriteRequest);

                await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);

                var applicationMessage = new MqttApplicationMessageBuilder()
                    .WithTopic("iotgateway/write")
                    .WithPayload(payload)
                    .Build();

                await mqttClient.PublishAsync(applicationMessage, CancellationToken.None);

                await mqttClient.DisconnectAsync();
            }
        }

        private ApplicationConfiguration BuildOpcUAConfiguration()
        {

            var config = new ApplicationConfiguration()
            {
                ApplicationName = "Custom .NET Client",
                ApplicationUri = Utils.Format(@"urn:{0}:" + "dotnet-client", "localhost"),
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    //ApplicationCertificate = new CertificateIdentifier { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\MachineDefault", SubjectName = Utils.Format(@"CN={0}, DC={1}", MyApplicationName, ServerAddress) },
                    //TrustedIssuerCertificates = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Certificate Authorities" },
                    //TrustedPeerCertificates = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Applications" },
                    RejectedCertificateStore = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\RejectedCertificates" },
                    AutoAcceptUntrustedCertificates = true,
                    AddAppCertToTrustedStore = true,

                },

                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 },
                TraceConfiguration = new TraceConfiguration()
            };
            // config.Validate(ApplicationType.Client).GetAwaiter().GetResult();
            if (config.SecurityConfiguration.AutoAcceptUntrustedCertificates)
            {
                config.CertificateValidator.CertificateValidation += (s, e) => { e.Accept = (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted); };
            }
            return config;
        }

        public async void SubcripeToMonitoredItem() {

            var config = BuildOpcUAConfiguration();
            string serverAddress = "opc.tcp://127.0.0.1:49320";
            var selectedEndpoint = CoreClientUtils.SelectEndpoint(serverAddress, useSecurity: false, 15000);

            session = await Session.Create(config, new ConfiguredEndpoint(null, selectedEndpoint, EndpointConfiguration.Create(config)), false, "", 60000, null, null);
            //session.Open("DOTNET", new UserIdentity() );

            var subscription = new Subscription(session.DefaultSubscription) { PublishingInterval = 1000 };

            //Console.WriteLine("Step 5 - Add a list of items you wish to monitor to the subscription.");
            var list = new List<MonitoredItem> { };
            list.Add(new MonitoredItem(subscription.DefaultItem) { DisplayName = "SimChannel.SimDevice.Counter", StartNodeId = "ns=2;s=SimChannel.SimDevice.Counter", });

            list.ForEach(i => i.Notification += OnValueChanged);
            subscription.AddItems(list);

            //Console.WriteLine("Step 6 - Add the subscription to the session.");
            session.AddSubscription(subscription);
            subscription.Create();

           
        }


        // https://github.com/OPCFoundation/UA-.NETStandard/blob/e34eb9b512723e474320300596e92cdb2579f0dc/Applications/ClientControls.Net4/Common/Client/WriteRequestListViewCtrl.cs
        // https://github.com/OPCFoundation/UA-.NETStandard/blob/4325cd72237bf57ec96e3b97a411a8d18f520612/Applications/ConsoleReferenceClient/ClientSamples.cs
        private void btnOpcUaWrite_Click(object sender, RoutedEventArgs e)
        {

            //RequestHeader requestHeader = new RequestHeader()
            //{
                
            //};
            WriteValueCollection nodesToWrite = new WriteValueCollection();
            WriteValue intWriteVal = new WriteValue();
            intWriteVal.NodeId = new NodeId("ns=2;s=SimChannel.SimDevice.Counter");
            intWriteVal.AttributeId = Attributes.Value;
            intWriteVal.Value = new DataValue();
            intWriteVal.Value.Value = int.Parse(Value.Text);
            nodesToWrite.Add(intWriteVal);
            //nodesToWrite.Add(new WriteValue()
            //{
            //    NodeId = "SimChannel.SimDevice.Counter",
            //    Value = new DataValue(0)
            //});

            // read the values.
            StatusCodeCollection results = null;
            DiagnosticInfoCollection diagnosticInfos = null;
            session.Write(null, nodesToWrite, out results, out diagnosticInfos);
            //await session.CloseAsync();
        }

        private void OnValueChanged(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
        {
            var value = (monitoredItem.LastValue as Opc.Ua.MonitoredItemNotification)?.Value;
            Dispatcher.Invoke(() =>
            {
                if (cbEnableOpcUAMonitoredItem.IsChecked == true)
                {
                    
                    Value.Text = value?.GetValue<int>(0).ToString();
                }
                   

            });           
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            //session.Close();
        }
    }
}

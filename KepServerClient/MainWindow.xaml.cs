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

        public MainWindow()
        {
            InitializeComponent();
            SubscribeByMqtt();
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
                        if(cbEnableMqttSubscription.IsChecked == true)
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
            if (!res.IsSuccessStatusCode) {
                MessageBox.Show("Error :(", "Result", MessageBoxButton.OK, MessageBoxImage.Error);
            } else {
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
    }
}

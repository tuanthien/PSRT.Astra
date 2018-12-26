﻿using Newtonsoft.Json;
using PSRT.Astra.Properties;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PSRT.Astra
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static new App Current => Application.Current as App;

        public Logger Logger = new Logger();

        public App()
        {
            // older version of .net (or perhaps just windows) use a different
            // security protocol by default which does not work with specific
            // web server settings used by some of the servers astra needs
            // to contact
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            Logger.Info($"PSRT.Astra {Assembly.GetExecutingAssembly().GetName().Version}");

            if (Settings.Default.UpgradeRequired)
            {
                Logger.Info("Upgrading settings file");

                Settings.Default.Upgrade();
                Settings.Default.UpgradeRequired = false;
                Settings.Default.Save();
            }

            DispatcherUnhandledException += _UnhandledException;
        }

        private void _UnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Logger.Warning("Entered dispatcher unhandled exception handler", e.Exception);

            const string messageBoxTitle = "PSRT Astra fatal exception";
            const string messageBoxContent = "PSRT Astra has encountered an unrecoverable error.\n\nIf this is a repeating issue, please click below to upload the error information and share it with a developer.\n\nWould you like to upload the error information?";
            var messageBoxResult = MessageBox.Show(messageBoxContent, messageBoxTitle, MessageBoxButton.YesNo, MessageBoxImage.Error);
            if (messageBoxResult == MessageBoxResult.No)
                return;

            Logger.Info("Uploading error information");

            var uploadResult = Task.Run(async () =>
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("X-Auth-Token", "ahDhbbK8esumH1uZgcvIwFjk2yMC4DhKZykRHoYDW");
                    var json = JsonConvert.SerializeObject(new
                    {
                        description = $"PSRT.Astra {Assembly.GetExecutingAssembly().GetName().Version} | {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")}",
                        expiration = "never",
                        sections = new[]
                        {
                            new
                            {
                                name = "Log",
                                syntax = "text",
                                contents = Logger.Content
                            },
                            new
                            {
                                name = "Exception",
                                syntax = "text",
                                contents = e.Exception.ToString()
                            }
                        }
                    });
                    return await client.PostAsync("https://api.paste.ee/v1/pastes", new StringContent(json, Encoding.UTF8, "application/json"));
                }
            }).Result;

            if (uploadResult.StatusCode != HttpStatusCode.Created)
            {
                MessageBox.Show(messageBoxTitle, "Unable to upload error information.", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            var resultJson = Task.Run(async () => await uploadResult.Content.ReadAsStringAsync()).Result;
            var resultObject = JsonConvert.DeserializeAnonymousType(resultJson, new { link = string.Empty });
            Process.Start(resultObject.link);
        }
    }
}

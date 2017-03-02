// <copyright file="MainWindow.xaml.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
//
// Microsoft Cognitive Services (formerly Project Oxford): https://www.microsoft.com/cognitive-services
//
// Microsoft Cognitive Services (formerly Project Oxford) GitHub:
// https://github.com/Microsoft/Cognitive-Speech-STT-Windows
//
// Copyright (c) Microsoft Corporation
// All rights reserved.
//
// MIT License:
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// </copyright>

namespace Microsoft.CognitiveServices.SpeechRecognition
{
    using System;
    using System.ComponentModel;
    using System.Configuration;
    using System.Diagnostics;
    using System.IO;
    using System.IO.IsolatedStorage;
    using System.Net;
    using System.Net.Sockets;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Windows;
    using System.Web;
    using System.Net.Http;
    using System.Collections.ObjectModel;
    using System.Collections.Generic;
    using System.Windows.Xps.Packaging;
    using System.Linq;
    using Spire.Doc;
    using Spire.Pdf;
    using System.Drawing;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Resources;
    using System.Windows.Controls;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private StreamWriter swSender;
        private StreamReader srReceiver;
        private TcpClient tcpServer;
        //private Thread thrMessaging;
        private IPAddress ipAddr;
        //private bool Connected;
        //Speicherort für die empfangen Dateien
        String path = Environment.CurrentDirectory + @"\Empfangen\";
        String path2 = Environment.CurrentDirectory;
        /// <summary>
        /// The isolated storage subscription key file name.
        /// </summary>
        private const string IsolatedStorageSubscriptionKeyFileName = "Subscription.txt";

        /// <summary>
        /// The default subscription key prompt message
        /// </summary>
        private const string DefaultSubscriptionKeyPromptMessage = "Paste your subscription key here to start";

        /// <summary>
        /// You can also put the primary key in app.config, instead of using UI.
        /// string subscriptionKey = ConfigurationManager.AppSettings["primaryKey"];
        /// </summary>
        private string subscriptionKey;

        /// <summary>
        /// The data recognition client
        /// </summary>
        private DataRecognitionClient dataClient;

        /// <summary>
        /// The microphone client
        /// </summary>
        private MicrophoneRecognitionClient micClient;

        // Hier die Collection von den Xps-Dateien
        ObservableCollection<String> xpsFilePaths = new ObservableCollection<String>();

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow()
        {
            this.InitializeComponent();
            this.Initialize();
        }

        #region Events

        /// <summary>
        /// Implement INotifyPropertyChanged interface
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion Events

        /// <summary>
        /// Gets or sets a value indicating whether this instance is microphone client short phrase.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is microphone client short phrase; otherwise, <c>false</c>.
        /// </value>
        public bool IsMicrophoneClientShortPhrase { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is microphone client dictation.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is microphone client dictation; otherwise, <c>false</c>.
        /// </value>
        public bool IsMicrophoneClientDictation { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is microphone client with intent.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is microphone client with intent; otherwise, <c>false</c>.
        /// </value>
        public bool IsMicrophoneClientWithIntent { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is data client short phrase.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is data client short phrase; otherwise, <c>false</c>.
        /// </value>
        public bool IsDataClientShortPhrase { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is data client with intent.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is data client with intent; otherwise, <c>false</c>.
        /// </value>
        public bool IsDataClientWithIntent { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is data client dictation.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is data client dictation; otherwise, <c>false</c>.
        /// </value>
        public bool IsDataClientDictation { get; set; }

        /// <summary>
        /// Gets or sets subscription key
        /// </summary>
        public string SubscriptionKey
        {
            get
            {
                return this.subscriptionKey;
            }

            set
            {
                this.subscriptionKey = "b943e5d1a1b5426f84720d15ad9be64e";
                //this.OnPropertyChanged<string>();
            }
        }

        /// <summary>
        /// Gets the LUIS application identifier.
        /// </summary>
        /// <value>
        /// The LUIS application identifier.
        /// </value>
        private string LuisAppId
        {
            get { return ConfigurationManager.AppSettings["luisAppID"]; }
        }

        /// <summary>
        /// Gets the LUIS subscription identifier.
        /// </summary>
        /// <value>
        /// The LUIS subscription identifier.
        /// </value>
        private string LuisSubscriptionID
        {
            get { return ConfigurationManager.AppSettings["luisSubscriptionID"]; }
        }

        /// <summary>
        /// Gets a value indicating whether or not to use the microphone.
        /// </summary>
        /// <value>
        ///   <c>true</c> if [use microphone]; otherwise, <c>false</c>.
        /// </value>
        private bool UseMicrophone
        {
            get
            {
                return this.IsMicrophoneClientWithIntent ||
                    this.IsMicrophoneClientDictation ||
                    this.IsMicrophoneClientShortPhrase;
            }
        }

        /// <summary>
        /// Gets a value indicating whether LUIS results are desired.
        /// </summary>
        /// <value>
        ///   <c>true</c> if LUIS results are to be returned otherwise, <c>false</c>.
        /// </value>
        private bool WantIntent
        {
            get
            {
                return !string.IsNullOrEmpty(this.LuisAppId) &&
                    !string.IsNullOrEmpty(this.LuisSubscriptionID) &&
                    (this.IsMicrophoneClientWithIntent || this.IsDataClientWithIntent);
            }
        }

        /// <summary>
        /// Gets the current speech recognition mode.
        /// </summary>
        /// <value>
        /// The speech recognition mode.
        /// </value>
        private SpeechRecognitionMode Mode
        {
            get
            {
                if (this.IsMicrophoneClientDictation ||
                    this.IsDataClientDictation)
                {
                    return SpeechRecognitionMode.LongDictation;
                }

                return SpeechRecognitionMode.ShortPhrase;
            }
        }

        /// <summary>
        /// Gets the default locale.
        /// </summary>
        /// <value>
        /// The default locale.
        /// </value>
        private string DefaultLocale
        {
            get { return "de-DE"; }
        }

        /// <summary>
        /// Gets the short wave file path.
        /// </summary>
        /// <value>
        /// The short wave file.
        /// </value>
        private string ShortWaveFile
        {
            get
            {
                return ConfigurationManager.AppSettings["ShortWaveFile"];
            }
        }

        /// <summary>
        /// Gets the long wave file path.
        /// </summary>
        /// <value>
        /// The long wave file.
        /// </value>
        private string LongWaveFile
        {
            get
            {
                return ConfigurationManager.AppSettings["LongWaveFile"];
            }
        }

        /// <summary>
        /// Raises the System.Windows.Window.Closed event.
        /// </summary>
        /// <param name="e">An System.EventArgs that contains the event data.</param>
        protected override void OnClosed(EventArgs e)
        {
            if (null != this.dataClient)
            {
                this.dataClient.Dispose();
            }

            if (null != this.micClient)
            {
                this.micClient.Dispose();
            }

            base.OnClosed(e);
        }

        /// <summary>
        /// Saves the subscription key to isolated storage.
        /// </summary>
        /// <param name="subscriptionKey">The subscription key.</param>
        private static void SaveSubscriptionKeyToIsolatedStorage(string subscriptionKey)
        {
            using (IsolatedStorageFile isoStore = IsolatedStorageFile.GetStore(IsolatedStorageScope.User | IsolatedStorageScope.Assembly, null, null))
            {
                using (var oStream = new IsolatedStorageFileStream(IsolatedStorageSubscriptionKeyFileName, FileMode.Create, isoStore))
                {
                    using (var writer = new StreamWriter(oStream))
                    {
                        writer.WriteLine(subscriptionKey);
                    }
                }
            }
        }

        /// <summary>
        /// Initializes a fresh audio session.
        /// </summary>
        private void Initialize()
        {

            // Background Mic Image
            string imagePath = path2 + @"\..\..\images\";
            Console.WriteLine(imagePath);
            ImageBrush imgBrush = new ImageBrush();
            imgBrush.ImageSource = new BitmapImage(new Uri(imagePath + @"mic_standard.png", UriKind.Relative));

            _startButton.Background = imgBrush;


            this.IsMicrophoneClientShortPhrase = true;
            this.IsMicrophoneClientWithIntent = false;
            this.IsMicrophoneClientDictation = false;
            // Set the default choice for the group of checkbox.
            //this._micRadioButton.IsChecked = true;

            this.SubscriptionKey = this.GetSubscriptionKeyFromIsolatedStorage();
        }

        /// <summary>
        /// Handles the Click event of the _startButton control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // Background Mic Image
            string imagePath = path2 + @"\..\..\images\";
            Console.WriteLine(imagePath);
            ImageBrush imgBrush = new ImageBrush();
            imgBrush.ImageSource = new BitmapImage(new Uri(imagePath + @"mic_record.png", UriKind.Relative));
            _startButton.Background = imgBrush;




            //this._radioGroup.IsEnabled = false;

            this.LogRecognitionStart();

            if (this.UseMicrophone)
            {
                if (this.micClient == null)
                {
                    if (this.WantIntent)
                    {
                        this.CreateMicrophoneRecoClientWithIntent();
                    }
                    else
                    {
                        this.CreateMicrophoneRecoClient();
                    }
                }

                this.micClient.StartMicAndRecognition();
            }
            else
            {
                if (null == this.dataClient)
                {
                    if (this.WantIntent)
                    {
                        this.CreateDataRecoClientWithIntent();
                    }
                    else
                    {
                        this.CreateDataRecoClient();
                    }
                }
                

                this.SendAudioHelper((this.Mode == SpeechRecognitionMode.ShortPhrase) ? this.ShortWaveFile : this.LongWaveFile);
            }
        }

        /// <summary>
        /// Logs the recognition start.
        /// </summary>
        private void LogRecognitionStart()
        {
            string recoSource;
            if (this.UseMicrophone)
            {
                recoSource = "microphone";
            }
            else if (this.Mode == SpeechRecognitionMode.ShortPhrase)
            {
                recoSource = "short wav file";
            }
            else
            {
                recoSource = "long wav file";
            }

            this.WriteLine("\n--- Start speech recognition using " + recoSource + " with " + this.Mode + " mode in " + this.DefaultLocale + " language ----\n\n");
        }

        /// <summary>
        /// Creates a new microphone reco client without LUIS intent support.
        /// </summary>
        private void CreateMicrophoneRecoClient()
        {
            this.micClient = SpeechRecognitionServiceFactory.CreateMicrophoneClient(
                this.Mode,
                this.DefaultLocale,
                this.SubscriptionKey);

            // Event handlers for speech recognition results
            this.micClient.OnMicrophoneStatus += this.OnMicrophoneStatus;
            this.micClient.OnPartialResponseReceived += this.OnPartialResponseReceivedHandler;
            if (this.Mode == SpeechRecognitionMode.ShortPhrase)
            {
                this.micClient.OnResponseReceived += this.OnMicShortPhraseResponseReceivedHandler;
            }
            else if (this.Mode == SpeechRecognitionMode.LongDictation)
            {
                this.micClient.OnResponseReceived += this.OnMicDictationResponseReceivedHandler;
            }

            this.micClient.OnConversationError += this.OnConversationErrorHandler;
        }

        /// <summary>
        /// Creates a new microphone reco client with LUIS intent support.
        /// </summary>
        private void CreateMicrophoneRecoClientWithIntent()
        {
            this.WriteLine("--- Start microphone dictation with Intent detection ----");

            this.micClient =
                SpeechRecognitionServiceFactory.CreateMicrophoneClientWithIntent(
                this.DefaultLocale,
                this.SubscriptionKey,
                this.LuisAppId,
                this.LuisSubscriptionID);
            this.micClient.OnIntent += this.OnIntentHandler;

            // Event handlers for speech recognition results
            this.micClient.OnMicrophoneStatus += this.OnMicrophoneStatus;
            this.micClient.OnPartialResponseReceived += this.OnPartialResponseReceivedHandler;
            this.micClient.OnResponseReceived += this.OnMicShortPhraseResponseReceivedHandler;
            this.micClient.OnConversationError += this.OnConversationErrorHandler;
        }

        /// <summary>
        /// Handles the Click event of the HelpButton control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://www.projectoxford.ai/doc/general/subscription-key-mgmt");
        }

        /// <summary>
        /// Creates a data client without LUIS intent support.
        /// Speech recognition with data (for example from a file or audio source).  
        /// The data is broken up into buffers and each buffer is sent to the Speech Recognition Service.
        /// No modification is done to the buffers, so the user can apply their
        /// own Silence Detection if desired.
        /// </summary>
        private void CreateDataRecoClient()
        {
            this.dataClient = SpeechRecognitionServiceFactory.CreateDataClient(
                this.Mode,
                this.DefaultLocale,
                this.SubscriptionKey);

            // Event handlers for speech recognition results
            if (this.Mode == SpeechRecognitionMode.ShortPhrase)
            {
                this.dataClient.OnResponseReceived += this.OnDataShortPhraseResponseReceivedHandler;
            }
            else
            {
                this.dataClient.OnResponseReceived += this.OnDataDictationResponseReceivedHandler;
            }

            this.dataClient.OnPartialResponseReceived += this.OnPartialResponseReceivedHandler;
            this.dataClient.OnConversationError += this.OnConversationErrorHandler;
        }

        /// <summary>
        /// Creates a data client with LUIS intent support.
        /// Speech recognition with data (for example from a file or audio source).  
        /// The data is broken up into buffers and each buffer is sent to the Speech Recognition Service.
        /// No modification is done to the buffers, so the user can apply their
        /// own Silence Detection if desired.
        /// </summary>
        private void CreateDataRecoClientWithIntent()
        {
            this.dataClient = SpeechRecognitionServiceFactory.CreateDataClientWithIntent(
                this.DefaultLocale,
                this.SubscriptionKey,
                this.LuisAppId,
                this.LuisSubscriptionID);

            // Event handlers for speech recognition results
            this.dataClient.OnResponseReceived += this.OnDataShortPhraseResponseReceivedHandler;
            this.dataClient.OnPartialResponseReceived += this.OnPartialResponseReceivedHandler;
            this.dataClient.OnConversationError += this.OnConversationErrorHandler;

            // Event handler for intent result
            this.dataClient.OnIntent += this.OnIntentHandler;
        }

        /// <summary>
        /// Sends the audio helper.
        /// </summary>
        /// <param name="wavFileName">Name of the wav file.</param>
        private void SendAudioHelper(string wavFileName)
        {
            using (FileStream fileStream = new FileStream(wavFileName, FileMode.Open, FileAccess.Read))
            {
                // Note for wave files, we can just send data from the file right to the server.
                // In the case you are not an audio file in wave format, and instead you have just
                // raw data (for example audio coming over bluetooth), then before sending up any 
                // audio data, you must first send up an SpeechAudioFormat descriptor to describe 
                // the layout and format of your raw audio data via DataRecognitionClient's sendAudioFormat() method.
                int bytesRead = 0;
                byte[] buffer = new byte[1024];

                try
                {
                    do
                    {
                        // Get more Audio data to send into byte buffer.
                        bytesRead = fileStream.Read(buffer, 0, buffer.Length);

                        // Send of audio data to service. 
                        this.dataClient.SendAudio(buffer, bytesRead);
                    }
                    while (bytesRead > 0);
                }
                finally
                {
                    // We are done sending audio.  Final recognition results will arrive in OnResponseReceived event call.
                    this.dataClient.EndAudio();
                }
            }
        }

        /// <summary>
        /// Called when a final response is received;
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="SpeechResponseEventArgs"/> instance containing the event data.</param>
        private void OnMicShortPhraseResponseReceivedHandler(object sender, SpeechResponseEventArgs e)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                this.WriteLine("--- OnMicShortPhraseResponseReceivedHandler ---");

                // we got the final result, so it we can end the mic reco.  No need to do this
                // for dataReco, since we already called endAudio() on it as soon as we were done
                // sending all the data.
                this.micClient.EndMicAndRecognition();

                this.WriteResponseResult(e);

                _startButton.IsEnabled = true;
                //_radioGroup.IsEnabled = true;
            }));
        }

        /// <summary>
        /// Called when a final response is received;
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="SpeechResponseEventArgs"/> instance containing the event data.</param>
        private void OnDataShortPhraseResponseReceivedHandler(object sender, SpeechResponseEventArgs e)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                this.WriteLine("--- OnDataShortPhraseResponseReceivedHandler ---");

                // we got the final result, so it we can end the mic reco.  No need to do this
                // for dataReco, since we already called endAudio() on it as soon as we were done
                // sending all the data.
                this.WriteResponseResult(e);

                _startButton.IsEnabled = true;
                //_radioGroup.IsEnabled = true;
            }));
        }

        /// <summary>
        /// Writes the response result.
        /// </summary>
        /// <param name="e">The <see cref="SpeechResponseEventArgs"/> instance containing the event data.</param>
        private void WriteResponseResult(SpeechResponseEventArgs e)
        {
            if (e.PhraseResponse.Results.Length == 0)
            {
                this.WriteLine("No phrase response is available.");
            }
            else
            {
                this.WriteLine("********* Final n-BEST Results *********");
                for (int i = 0; i < e.PhraseResponse.Results.Length; i++)
                {
                    this.WriteLine(
                        "[{0}] Confidence={1}, Text=\"{2}\"", 
                        i, 
                        e.PhraseResponse.Results[i].Confidence,
                        e.PhraseResponse.Results[i].DisplayText);
                }
                // HIER IST DAS ENDE
                for (int i = 0; i < e.PhraseResponse.Results.Length; i++)
                {
                    Console.WriteLine(e.PhraseResponse.Results[i].DisplayText);
                    InitializeConnection(e.PhraseResponse.Results[i].DisplayText);
                }

                // Background Mic Image
                string imagePath = path2 + @"\..\..\images\";
                Console.WriteLine(imagePath);
                ImageBrush imgBrush = new ImageBrush();
                imgBrush.ImageSource = new BitmapImage(new Uri(imagePath + @"mic_standard.png", UriKind.Relative));
                _startButton.Background = imgBrush;

                _meinText.Text = e.PhraseResponse.Results[0].DisplayText;
                this.WriteLine();
            }
        }

        /// <summary>
        /// Called when a final response is received;
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="SpeechResponseEventArgs"/> instance containing the event data.</param>
        private void OnMicDictationResponseReceivedHandler(object sender, SpeechResponseEventArgs e)
        {
            this.WriteLine("--- OnMicDictationResponseReceivedHandler ---");
            if (e.PhraseResponse.RecognitionStatus == RecognitionStatus.EndOfDictation ||
                e.PhraseResponse.RecognitionStatus == RecognitionStatus.DictationEndSilenceTimeout)
            { 
                Dispatcher.Invoke(
                    (Action)(() => 
                    {
                        // we got the final result, so it we can end the mic reco.  No need to do this
                        // for dataReco, since we already called endAudio() on it as soon as we were done
                        // sending all the data.
                        this.micClient.EndMicAndRecognition();

                        this._startButton.IsEnabled = true;
                        //this._radioGroup.IsEnabled = true;
                    }));                
            }

            this.WriteResponseResult(e);
        }

        /// <summary>
        /// Called when a final response is received;
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="SpeechResponseEventArgs"/> instance containing the event data.</param>
        private void OnDataDictationResponseReceivedHandler(object sender, SpeechResponseEventArgs e)
        {
            this.WriteLine("--- OnDataDictationResponseReceivedHandler ---");
            if (e.PhraseResponse.RecognitionStatus == RecognitionStatus.EndOfDictation ||
                e.PhraseResponse.RecognitionStatus == RecognitionStatus.DictationEndSilenceTimeout)
            {
                Dispatcher.Invoke(
                    (Action)(() => 
                    {
                        _startButton.IsEnabled = true;
                        //_radioGroup.IsEnabled = true;

                        // we got the final result, so it we can end the mic reco.  No need to do this
                        // for dataReco, since we already called endAudio() on it as soon as we were done
                        // sending all the data.
                    }));
            }

            this.WriteResponseResult(e);
        }

        /// <summary>
        /// Called when a final response is received and its intent is parsed
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="SpeechIntentEventArgs"/> instance containing the event data.</param>
        private void OnIntentHandler(object sender, SpeechIntentEventArgs e)
        {
            this.WriteLine("--- Intent received by OnIntentHandler() ---");
            this.WriteLine("{0}", e.Payload);
            this.WriteLine();
        }

        /// <summary>
        /// Called when a partial response is received.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="PartialSpeechResponseEventArgs"/> instance containing the event data.</param>
        private void OnPartialResponseReceivedHandler(object sender, PartialSpeechResponseEventArgs e)
        {
            this.WriteLine("--- Partial result received by OnPartialResponseReceivedHandler() ---");
            this.WriteLine("{0}", e.PartialResult);
            this.WriteLine();
        }

        /// <summary>
        /// Called when an error is received.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="SpeechErrorEventArgs"/> instance containing the event data.</param>
        private void OnConversationErrorHandler(object sender, SpeechErrorEventArgs e)
        {
           Dispatcher.Invoke(() =>
           {
               _startButton.IsEnabled = true;
               //_radioGroup.IsEnabled = true;
           });

            this.WriteLine("--- Error received by OnConversationErrorHandler() ---");
            this.WriteLine("Error code: {0}", e.SpeechErrorCode.ToString());
            this.WriteLine("Error text: {0}", e.SpeechErrorText);
            this.WriteLine();
        }

        /// <summary>
        /// Called when the microphone status has changed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="MicrophoneEventArgs"/> instance containing the event data.</param>
        private void OnMicrophoneStatus(object sender, MicrophoneEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                WriteLine("--- Microphone status change received by OnMicrophoneStatus() ---");
                WriteLine("********* Microphone status: {0} *********", e.Recording);
                if (e.Recording)
                {
                    WriteLine("Please start speaking.");
                }

                WriteLine();
            });
        }

        /// <summary>
        /// Writes the line.
        /// </summary>
        private void WriteLine()
        {
            this.WriteLine(string.Empty);
        }

        /// <summary>
        /// Writes the line.
        /// </summary>
        /// <param name="format">The format.</param>
        /// <param name="args">The arguments.</param>
        private void WriteLine(string format, params object[] args)
        {
            var formattedStr = string.Format(format, args);
            Trace.WriteLine(formattedStr);
            Dispatcher.Invoke(() =>
            {
                //_logText.Text += (formattedStr + "\n");
                //_logText.ScrollToEnd();
            });
            // unser code
            //_meinText = _logText.Text;

        }

        /// <summary>
        /// Gets the subscription key from isolated storage.
        /// </summary>
        /// <returns>The subscription key.</returns>
        private string GetSubscriptionKeyFromIsolatedStorage()
        {
            string subscriptionKey = null;

            using (IsolatedStorageFile isoStore = IsolatedStorageFile.GetStore(IsolatedStorageScope.User | IsolatedStorageScope.Assembly, null, null))
            {
                try
                {
                    using (var iStream = new IsolatedStorageFileStream(IsolatedStorageSubscriptionKeyFileName, FileMode.Open, isoStore))
                    {
                        using (var reader = new StreamReader(iStream))
                        {
                            subscriptionKey = reader.ReadLine();
                        }
                    }
                }
                catch (FileNotFoundException)
                {
                    subscriptionKey = null;
                }
            }

            if (string.IsNullOrEmpty(subscriptionKey))
            {
                subscriptionKey = DefaultSubscriptionKeyPromptMessage;
            }

            return subscriptionKey;
        }

        /// <summary>
        /// Handles the Click event of the subscription key save button.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void SaveKey_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveSubscriptionKeyToIsolatedStorage(this.SubscriptionKey);
                MessageBox.Show("Subscription key is saved in your disk.\nYou do not need to paste the key next time.", "Subscription Key");
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    "Fail to save subscription key. Error message: " + exception.Message,
                    "Subscription Key", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handles the Click event of the DeleteKey control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void DeleteKey_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.SubscriptionKey = DefaultSubscriptionKeyPromptMessage;
                SaveSubscriptionKeyToIsolatedStorage(string.Empty);
                MessageBox.Show("Subscription key is deleted from your disk.", "Subscription Key");
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    "Fail to delete subscription key. Error message: " + exception.Message,
                    "Subscription Key", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Helper function for INotifyPropertyChanged interface 
        /// </summary>
        /// <typeparam name="T">Property type</typeparam>
        /// <param name="caller">Property name</param>
        private void OnPropertyChanged<T>([CallerMemberName]string caller = null)
        {
            var handler = this.PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(caller));
            }
        }

        /// <summary>
        /// Handles the Click event of the RadioButton control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void RadioButton_Click(object sender, RoutedEventArgs e)
        {
            // Reset everything
            if (this.micClient != null)
            {
                this.micClient.EndMicAndRecognition();
                this.micClient.Dispose();
                this.micClient = null;
            }

            if (this.dataClient != null)
            {
                this.dataClient.Dispose();
                this.dataClient = null;
            }

            //this._logText.Text = string.Empty;
            this._startButton.IsEnabled = true;
            //this._radioGroup.IsEnabled = true;
        }

        /*       public void communication(string server, int port)
               {
                   string request = "GET / HTTP/1.1\r\nHost: " + server +
                   "\r\nConnection: Close\r\n\r\n";
                   Byte[] bytesSent = Encoding.ASCII.GetBytes(request);
                   Byte[] bytesReceived = new Byte[256];
                   string test;
                   //Socket s = new Socket(SocketType.Stream, ProtocolType.Tcp);
                   Socket s = ConnectSocket(server, port);
                   s.Send(bytesSent, bytesSent.Length, 0);

                   // Receive the server home page content.
                   int bytes = 0;
                   string page = "test";

                   // The following will block until te page is transmitted.
                   do
                   {
                       bytes = s.Receive(bytesReceived, bytesReceived.Length, 0);
                       page = page + Encoding.ASCII.GetString(bytesReceived, 0, bytes);
                   }
                   while (bytes > 0);

                   Console.WriteLine(page);
               }

               private static Socket ConnectSocket(string server, int port)
               {
                   Socket s = null;
                   IPHostEntry hostEntry = null;

                   // Get host related information.
                   hostEntry = Dns.GetHostEntry(server);

                   // Loop through the AddressList to obtain the supported AddressFamily. This is to avoid
                   // an exception that occurs when the host IP Address is not compatible with the address family
                   // (typical in the IPv6 case).
                   foreach (IPAddress address in hostEntry.AddressList)
                   {
                       IPEndPoint ipe = new IPEndPoint(address, port);
                       Socket tempSocket =
                           new Socket(ipe.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                       tempSocket.Connect(ipe);

                       if (tempSocket.Connected)
                       {
                           s = tempSocket;
                           break;
                       }
                       else
                       {
                           continue;
                       }
                   }
                   return s;
               }*/

        private void InitializeConnection(String anfrage)
        {
            
            Console.WriteLine("PATH" + path);
            // Speicherort anlegen
            
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            // Speicherort erstmal leeren
            deleteAllFiles(path);

            // Parse the IP address
            string ipAdress = "172.26.38.107";
            ipAddr = IPAddress.Parse(ipAdress);

            // Start a new TCP connections to the chat server
            tcpServer = new TcpClient();

            try
            {
                // Versuche mit Server zu verbinden
                tcpServer.Connect(ipAddr, 420);

                // Senden: Initialisiere StreamWriter und Sende Anfrage zum Server
                swSender = new StreamWriter(tcpServer.GetStream());
                SendMessage(anfrage);

                // Empfangen: Dateien empfangen
                ReceiveMessages(path);
                Console.WriteLine("Empfangen Vorbei");

                // Start the thread for receiving messages and further communication
                //thrMessaging = new Thread(new ThreadStart(ReceiveMessages));
                //thrMessaging.Start();
                //Connected = true;

            }
            catch (Exception e2)
            {
                MessageBox.Show(e2.ToString());
            }

            // Hole alle .docx Dateien aus dem Pfad und erstelle .xps Dateien
            convertDocxToXps(getDocxFiles(path));

            // Goenn dir die ganzen Xps Paths in die listbox (View)
            addXpsFilePaths(path);
        }

        private void ReceiveMessages(String path)
        {

            // Receive the response from the server
            NetworkStream networkStream = tcpServer.GetStream();
            srReceiver = new StreamReader(networkStream);

            int fileCount = int.Parse(srReceiver.ReadLine());
            if (fileCount == 0)
            {
                MessageBox.Show("Keine Ergebnisse erhalten.");
            }
            Thread.Sleep(100);
            for (int i = fileCount; i > 0; i--)
            {

                // Sende wenn bereit
                swSender.WriteLine("done");
                swSender.Flush();
                
                //erst kommt Filename
                string filename = srReceiver.ReadLine();
                Console.WriteLine("Dateiname: " + filename);

                // lese datei groesse
                int length = int.Parse(srReceiver.ReadLine());
                Console.WriteLine("Dateigroeße: {0} bytes", length);

                // lese bytes und fuege dem buffer hinzu
                byte[] buffer = new byte[length];
                int toRead = length;
                int read = 0;

                while (toRead > 0)
                {
                    int noChars = networkStream.Read(buffer, read, toRead);
                    read += noChars;
                    toRead -= noChars;
                }

                // erzeuge die datei
                BinaryWriter bWrite = new BinaryWriter(File.Open(path + filename, FileMode.Create));
                bWrite.Write(buffer);
                bWrite.Flush();
                bWrite.Close();
            }
            
        }

        /* Konvertiere die Datein zu .xps Dateien */
        private void convertDocxToXps(Dictionary<String, Object> dokumente)
        {
            foreach (var doc in dokumente)
            {
                if (doc.Key.EndsWith("docx"))
                {
                    Console.WriteLine("WILL ICH SEHEN " + doc.Key);
                    ((Document)doc.Value).SaveToFile(doc.Key + ".xps", Spire.Doc.FileFormat.XPS);
                }
                else if (doc.Key.EndsWith("pdf"))
                {
                    Console.WriteLine("WILL ICH SEHEN " + doc.Key);
                    ((PdfDocument)doc.Value).SaveToFile(doc.Key + ".xps", Spire.Pdf.FileFormat.XPS);
                }
            }
        }

        private void deleteAllFiles(String path)
        {
            System.IO.DirectoryInfo di = new DirectoryInfo(path);
            if(di.GetFiles().Count() > 0)
            {
                foreach (FileInfo file in di.GetFiles())
                {
                    file.Delete();
                }
                foreach (DirectoryInfo dir in di.GetDirectories())
                {
                    dir.Delete(true);
                }
            }
        }

        private Dictionary<String, Object> getDocxFiles(String path) {
            Dictionary<String, Object> dic = new Dictionary<String, Object>();

            List<string> filePathsDocx = Directory.GetFiles(path, "*.*").Where(file => file.ToLower().EndsWith("docx") || file.ToLower().EndsWith("pdf")).ToList();
            foreach (String fp in filePathsDocx)
            {
                if (fp.EndsWith("docx"))
                {
                    Document doc = new Document();
                    doc.LoadFromFile(fp);
                    dic.Add(fp, doc);
                }
                else if (fp.EndsWith("pdf"))
                {
                    /*
                    PdfDocument doc = new PdfDocument();
                    doc.LoadFromFile(fp);
                    dic.Add(fp, doc);
                    */
                }
                
            }
            return dic;
        }

        /* Der Benutzer soll ein SpeicherDialog angezeigt bekommen,
         * den Speicherpfad und den Dateiennamen bestimmen können.
         */
        private void rightButton(object sender, EventArgs e)
        {
            if (fileList.SelectedValue != null) { 
                // Zeigt einen Speicherdialog an
                System.Windows.Forms.SaveFileDialog saveFileDialog1 = new System.Windows.Forms.SaveFileDialog();
                saveFileDialog1.Filter = "Docx|*.docx";
                saveFileDialog1.Title = "Speicher die Datei";
                saveFileDialog1.ShowDialog();

                // Wenn der Dateiname bestimmt worden ist..
                if (saveFileDialog1.FileName != "")
                {
                    // Der aktuell ausgewählte Pfad aus der Liste (Dateiendung .xps)
                    string selected = fileList.SelectedValue.ToString();

                    // Trenne die Dateiendung .xps, somit ist die Dateiendung .docx
                    string docxPath = selected.Substring(0, selected.LastIndexOf('.'));

                    // Ziel: Vom Benutzer gewählter neuer Pfad
                    var to = saveFileDialog1.FileName;
                
                    // Kopiere die Datei zu dem gewählten Pfad
                    File.Copy(docxPath, to);
                }
            }
        }

        private void leftButton(object sender, EventArgs e)
        {
            if (fileList.SelectedValue != null) {
                string selected = fileList.SelectedValue.ToString();

                XpsDocument xpsDocument = new XpsDocument(selected, FileAccess.Read);
                dok1.Document = xpsDocument.GetFixedDocumentSequence();
            }


            
        }

        private void addXpsFilePaths(String path)
        {
            string[] filePaths = Directory.GetFiles(path, "*.xps");

            foreach (String fp in filePaths)
            {
                fileList.Items.Add(fp);
            }

        }

        private void processMessage(String p)
        {
            Console.WriteLine("der server antwortet: " +p);
        }

        private void SendMessage(String p)
        {
            if (p != "")
            {
                Console.Write("gesendet: " + p);
                // ZUM TESTEN ______________________________________________________________________________________________!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                //p = "Gib mir alle Lieferscheine mit 0815";
                //p = "Gib mir alle Recsf";

                p = p.Replace("ä","ae");
                p = p.Replace("ö", "oe");
                p = p.Replace("ü", "ue");
                p = p.Replace("ß", "ss");

                //"Gib mir alle Rechnungen vom Kunden Rheinwerk Group"
                p = HttpUtility.UrlEncode(p, System.Text.Encoding.UTF8);
                Console.WriteLine(p);
                swSender.WriteLine(p);
                swSender.Flush();
            }

        }


    }
}


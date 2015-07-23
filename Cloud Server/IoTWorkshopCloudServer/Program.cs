/*
* IoT Dimmer Control Server
*
* This program implements:
*   - An HTTP server to receive control requests from a phone 
*     or a web browser and puts them in a queue. 
*   - A text server for Arduino clients that get a copy of the queued 
*     messages. 
*     
*   This server is intended to be deployed as a cloud service.  
* 
* Created July 9th, 2015
* by Ulises Ruiz, Juan Romero and Xavier Guzman.
* 
* Copyright (c) 2015 Grupo Flextronics, S.A. de C.V.
*
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
*
* The above copyright notice and this permission notice shall be included in
* all copies or substantial portions of the Software.
*
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
* THE SOFTWARE.
* 
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Flextronics.CampusParty.Server;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Collections.Specialized;
using System.Web;

namespace Flextronics.CampusParty.Server.IoTWorkshopCloudServer
{
    class Program
    {

        private static readonly string bindAddressHttp = "tcp://0.0.0.0:8080";
        private static readonly string bindAddressArduino = "tcp://0.0.0.0:9000";

        private static readonly string HTTP_VERB_GET = "GET";
        private static readonly string HTTP_REQUEST_ADDRESS = "/iotdimmer";
        private static readonly string VALID_TOKEN = "letmein";

        private static readonly string HTTP_PARAM_INTENSITY = "intensity";
        private static readonly string HTTP_PARAM_TOKEN = "token";
        private static readonly string HTTP_PARAM_TARGET = "device";

        private static Dictionary<string, int> controlRequests = new Dictionary<string, int>();
        private static List<TcpClient> arduinoClients = new List<TcpClient>();

        private static ASCIIEncoding asciiEncoding = new ASCIIEncoding();

        static void Main(string[] args)
        {
            TcpListener httpListener = null;
            TcpListener arduinoListener = null;

            System.Console.WriteLine("Initializing...");

            httpListener = startListener(bindAddressHttp);
            arduinoListener = startListener(bindAddressArduino);

            System.Console.WriteLine("Listening for HTTP and Arduino clients. Press [Ctrl] + [Q] to quit:");
            communicationLoop(httpListener, arduinoListener);

            System.Console.WriteLine("Quit message received - Shutting down:");
            closeListener(httpListener);
            closeListener(arduinoListener);
            foreach (TcpClient client in arduinoClients)
            {
                try
                {
                    if (IsConnected(client.Client))
                    {
                        client.Close();
                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine("WARNINIG: Arduino Connection Close: " + ex.Message);
                    System.Console.WriteLine(ex.ToString());
                }
            }
            arduinoClients.Clear();

            System.Console.WriteLine("Shut down completed");
        }

        private static void closeListener(TcpListener listener)
        {

            listener.Stop();
            listener = null;
        }

        private static void communicationLoop(TcpListener httpListener, TcpListener arduinoListener)
        {
            bool quitMessageReceived = false;

            while (!quitMessageReceived)
            {
                if (httpListener.Pending())
                    try
                    {
                        TcpClient httpClient = httpListener.AcceptTcpClient();
                        NetworkStream networkStream = httpClient.GetStream();
                        System.Console.WriteLine("Accepted HTTP client: " + 
                                                    httpClient.Client.RemoteEndPoint);
                        handleHttpClient(networkStream);
                        networkStream.Flush();
                        networkStream.Close();
                        httpClient.Close();
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine("WARNINIG: HTTP Connection: " + ex.Message);
                        System.Console.WriteLine(ex.ToString());
                    }

                if (arduinoListener.Pending())
                {
                    try
                    {
                        TcpClient arduinoClient = arduinoListener.AcceptTcpClient();
                        System.Console.WriteLine("Accepted Arduino client: " + 
                                                arduinoClient.Client.RemoteEndPoint);
                        arduinoClients.Add(arduinoClient);
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine("WARNINIG: Arduino Connection: " + ex.Message);
                        System.Console.WriteLine(ex.ToString());

                    }
                }

                List<TcpClient> disconnectedClients = new List<TcpClient>();
                if (controlRequests.Count > 0)
                {
                    bool didDeliverAtLeastOneMessage = false;
                    foreach (TcpClient client in arduinoClients)
                    {
                        try
                        {
                            if (IsConnected(client.Client))
                            {
                                System.Console.WriteLine("Attempting to send control requests to " + 
                                                            client.Client.RemoteEndPoint);
                                foreach (KeyValuePair<string, int> request in controlRequests)
                                {
                                    NetworkStream networkStream = client.GetStream();
                                    if (networkStream.CanWrite)
                                    {
                                        System.Console.WriteLine("\t" + client.Client.RemoteEndPoint + ": " + 
                                                                    request.Key + " = " + request.Value);
                                        sendArduinoControlRequest(networkStream, request.Key, request.Value);
                                        networkStream.Flush();
                                        didDeliverAtLeastOneMessage = true;
                                    }
                                    else
                                    {
                                        System.Console.WriteLine("Marking " + client.Client.RemoteEndPoint + 
                                                                    " as disconnected");
                                        disconnectedClients.Add(client);
                                    }
                                }
                            }
                            else
                            {
                                System.Console.WriteLine("Marking " + client.Client.RemoteEndPoint + 
                                                            " as disconnected");
                                disconnectedClients.Add(client);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Console.WriteLine("Marking " + client.Client.RemoteEndPoint + 
                                                        " as disconnected due to exception: " + ex.Message);
                            disconnectedClients.Add(client);
                        }
                    }

                    if (didDeliverAtLeastOneMessage)
                    {
                        System.Console.WriteLine("Clearing control requests cache");
                        controlRequests.Clear();
                    }

                    foreach (TcpClient disconnectedClient in disconnectedClients)
                    {
                        arduinoClients.Remove(disconnectedClient);
                    }
                }

                if (System.Console.KeyAvailable)
                {
                    ConsoleKeyInfo cki = System.Console.ReadKey(true);
                    quitMessageReceived = cki.Key == ConsoleKey.Q && // [Ctrl] + [Q]
                                            cki.Modifiers.HasFlag(ConsoleModifiers.Control);
                }
                System.Threading.Thread.Sleep(100);
            }
        }

        private static bool IsConnected(Socket socket)
        {
            try
            {
                return !(socket.Poll(1, SelectMode.SelectRead) && socket.Available == 0);
            }
            catch (SocketException)
            {
                return (false);
            }
        }

        private static TcpListener startListener(string uriString)
        {
            TcpListener listener = null;
            Uri uri = new Uri(uriString);
            IPAddress bindAddress = IPAddress.Parse(uri.Host);
            IPEndPoint endPoint = new IPEndPoint(bindAddress, uri.Port);
            listener = new TcpListener(endPoint);
            listener.Start();
            return (listener);
        }


        private static void handleHttpClient(NetworkStream stream)
        {

            Func<NameValueCollection, bool> containsAllParamas = (requestParameters) =>
            {
                // Are there parameters in query string?
                if (requestParameters != null)
                    // Is the intensity parameter present?
                    if (requestParameters.AllKeys.Contains(HTTP_PARAM_INTENSITY))
                        // Is the access token present?
                        if (requestParameters.AllKeys.Contains(HTTP_PARAM_TOKEN))
                            // Is the target parameter present?
                            if (requestParameters.AllKeys.Contains(HTTP_PARAM_TARGET)) 
                                return (true);

                return (false);
            };

            string httpRequest = receiveHttpRequest(stream);
            if (httpRequest.Length > 0)
            {
                string[] requestLines = httpRequest.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                if (requestLines.Length > 0)
                {
                    string message = requestLines[0];
                    string[] messagePieces = message.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                    //if (messagePieces.Length > 1)
                    //{
                    string verb = messagePieces[0];
                    string requestUri = messagePieces[1]; //      /dimmer?intensity=5&token=letmein&target=sample1
                    string htppSignature = messagePieces[2];
                    string address = null;
                    NameValueCollection requestParameters = null;

                    if (requestUri.Contains("/"))
                    {
                        if (requestUri.Contains("?"))
                        {
                            address = requestUri.Substring(requestUri.IndexOf("/"), requestUri.IndexOf("?"));
                            string queryString = requestUri.Substring(requestUri.IndexOf("?") + 1);
                            requestParameters = HttpUtility.ParseQueryString(queryString);
                        }
                        else
                        {
                            address = requestUri.Substring(requestUri.IndexOf("/"));
                        }
                    }
                    if (verb == HTTP_VERB_GET)
                    {
                        // Is the resource ok? http://<server>/resource
                        if (address.Trim().ToLower() == HTTP_REQUEST_ADDRESS) 
                        {
                            if (containsAllParamas(requestParameters))
                            {
                                string intensity = requestParameters[HTTP_PARAM_INTENSITY];
                                string token = requestParameters[HTTP_PARAM_TOKEN];
                                string target = requestParameters[HTTP_PARAM_TARGET];

                                int intensityData;
                                if (Int32.TryParse(intensity, out intensityData))
                                {
                                    if (token == VALID_TOKEN) // Is the access token the righg one?
                                    {
                                        string response = "<HEAD>" + Environment.NewLine;
                                        response += "</HEAD>" + Environment.NewLine;
                                        response += "<BODY>" + Environment.NewLine;
                                        if (intensityData >= 0 && intensityData <= 7)
                                        {
                                            if (controlRequests.ContainsKey(target))
                                            {
                                                int previousValue = controlRequests[target];
                                                controlRequests[target] = intensityData;
                                                response += "Received command: " + target + " will switch intencity to " + 
                                                            intensityData.ToString() + " instead of " + previousValue.ToString();
                                            }
                                            else
                                            {
                                                controlRequests.Add(target, intensityData);
                                                response += "Received command: " + target + " will switch intencity to " + 
                                                            intensityData.ToString();
                                            }
                                        }
                                        System.Console.WriteLine(response);
                                        response += generateControlPage(token, target);
                                        response += "</BODY>" + Environment.NewLine;
                                        writeHttpSuccessHtmlResponse(stream, response);
                                        return;
                                    }
                                    else // The token is not right!
                                    {
                                        writeHttpErrorResponse(stream, 403, "Forbidden");
                                        return;
                                    }
                                }
                                else // Unable to parse intensity!
                                {
                                    writeHttpErrorResponse(stream, 416, "Requested Range Not Satisfiable");
                                    return;
                                }
                            }

                        }
                        else // Not the right resource!
                        {
                            writeHttpErrorResponse(stream, 404, "Not found");
                            return;
                        }
                    }
                    else // Not a GET verb!
                    {
                        writeHttpErrorResponse(stream, 405, "Method not allowed");
                        return;
                    }
                }
                //}
            }

            // Request was not understood!
            writeHttpErrorResponse(stream, 400, "Bad request");
        }

        private static string generateControlPage(string token, string target)
        {
            StringBuilder response = new StringBuilder();

            response.AppendLine("<br><br><b>Select intensity:</b><br>");
            response.Append("<a href = '/iotdimmer?intensity=0&token=").Append(token).Append("&device=").
                            Append(target).AppendLine("'>Off</a><br>");
            response.Append("<a href = '/iotdimmer?intensity=1&token=").Append(token).Append("&device=").
                            Append(target).AppendLine("'>Intensity 1</a><br>");
            response.Append("<a href = '/iotdimmer?intensity=2&token=").Append(token).Append("&device=").
                            Append(target).AppendLine("'>Intensity 2</a><br>");
            response.Append("<a href = '/iotdimmer?intensity=3&token=").Append(token).Append("&device=").
                            Append(target).AppendLine("'>Intensity 3</a><br>");
            response.Append("<a href = '/iotdimmer?intensity=4&token=").Append(token).Append("&device=").
                            Append(target).AppendLine("'>Intensity 4</a><br>");
            response.Append("<a href = '/iotdimmer?intensity=5&token=").Append(token).Append("&device=").
                            Append(target).AppendLine("'>Intensity 5</a><br>");
            response.Append("<a href = '/iotdimmer?intensity=6&token=").Append(token).Append("&device=").
                            Append(target).AppendLine("'>Intensity 6</a><br>");
            response.Append("<a href = '/iotdimmer?intensity=7&token=").Append(token).Append("&device=").
                            Append(target).AppendLine("'>Intensity 7</a><br>");

            return (response.ToString());
        }

        private static void writeHttpErrorResponse(NetworkStream stream, int errorCode, string errorMessage)
        {

            StreamWriter outputStream = new StreamWriter(new BufferedStream(stream));
            outputStream.WriteLine("HTTP/1.0 " + errorCode.ToString() + " " + errorMessage);
            outputStream.WriteLine("Connection: close");
            outputStream.WriteLine("");
            outputStream.Flush();
        }

        private static void writeHttpSuccessHtmlResponse(NetworkStream stream, string htmlBody)
        {

            StreamWriter outputStream = new StreamWriter(new BufferedStream(stream));
            outputStream.WriteLine("HTTP/1.0 200 OK");
            outputStream.WriteLine("Content-Type: text/html");
            outputStream.WriteLine("Connection: close");
            outputStream.WriteLine("");
            outputStream.WriteLine("<!DOCTYPE HTML>");
            outputStream.WriteLine("<HTML>");
            outputStream.Write(htmlBody);
            outputStream.WriteLine("</HTML>");
            outputStream.Flush();
        }



        private static void sendAsciiMessage(NetworkStream stream, string asciiMessage)
        {
            StreamWriter writer = new StreamWriter(stream);
            writer.WriteLine(asciiMessage);
        }

        private static string receiveHttpRequest(NetworkStream stream)
        {
            StringBuilder message = new StringBuilder();
            StreamReader reader = new StreamReader(stream);
            bool readToEnd = false;

            //string line;
            //while ((line = reader.ReadLine()) != null)
            //{
            //    message.AppendLine(line);
            //}

            string line = reader.ReadLine();
            if (line != null) message.AppendLine(line);
            while (line != null && !readToEnd)
            {
                line = reader.ReadLine();
                readToEnd = line == null || line.Length == 0;
                if (!readToEnd) message.AppendLine(line);
            }
            return (message.ToString());
        }

        private static void sendArduinoControlRequest(NetworkStream stream, string target, int intensity)
        {
            StreamWriter outputStream = new StreamWriter(new BufferedStream(stream));
            outputStream.WriteLine("{" + VALID_TOKEN + ": " + target + "=" + intensity.ToString() + "}");
            outputStream.Flush();
        }
    }
}
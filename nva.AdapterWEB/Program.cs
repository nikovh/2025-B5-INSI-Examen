using System;
using System.Messaging;
using System.Net.Http;
using System.Runtime.Remoting.Messaging;

namespace nva.AdapterWEB
{
    class Program
    {
        static void Main(string[] args)
        {
            // CONFIGURACION
            string baseUrl = "http://localhost:5000";
            string endpoint = "/api/payments/today";
            string url = baseUrl.TrimEnd('/') + endpoint;
            string queuePath = $@"FormatName:DIRECT=OS:{Environment.MachineName}\private$\nva_web_pagos";

            Console.WriteLine("Consultando API: " + url);

            try
            {
                string json = ObtenerJson(url);
                if (string.IsNullOrWhiteSpace(json))
                {
                    Console.WriteLine("La API devolvió vacío. No se envía mensaje.");
                    return;
                }

                EnviarAMsmq(queuePath, json, "Webpagos JSON");
                Console.WriteLine("Mensaje enviado a MSMQ.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: " + ex);
            }

            Console.WriteLine("Fin. Presiona tecla para salir.");
            Console.ReadKey();
        }

        static string ObtenerJson(string url)
        {
            using (var http = new HttpClient())
            {
                var resp = http.GetAsync(url).GetAwaiter().GetResult();
                resp.EnsureSuccessStatusCode();

                return resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            }
        }

        static void EnviarAMsmq(string queuePath, string json, string label)
        {
            using (var cola = new MessageQueue(queuePath))
            using (var tx = new MessageQueueTransaction())
            {
                cola.Formatter = new XmlMessageFormatter(new Type[] { typeof(string) });

                tx.Begin();

                var msg = new Message
                {
                    Body = json,
                    Formatter = cola.Formatter,
                    Label = label,
                    Recoverable = true
                };

                cola.Send(msg, tx);
                tx.Commit();
            }
        }
    }
},
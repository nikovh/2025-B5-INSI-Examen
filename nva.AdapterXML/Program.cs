using System;
using System.IO;
using System.Messaging; 
using System.Xml.Linq;

namespace nva.AdapterXML
{
    class Program
    {
        static void Main(string[] args)
        {
            // 1. Definimos dónde están los archivos y qué fecha buscamos
            string rutaCarpeta = @"C:\AukanGym\PagosXML";
            string fechaHoy = DateTime.Today.ToString("yyyyMMdd");
            // Buscamos archivos que empiecen con suc_ y terminen con la fecha de hoy
            string patronBusqueda = $"suc_*-pagos-{fechaHoy}.xml";

            // 1) Verifica la cola ANTES de procesar
            VerificarCola(@".\private$\nva_suc_pagos");

            Console.WriteLine($"Buscando archivos en {rutaCarpeta} con patrón {patronBusqueda}...");

            try
            {
                string[] archivos = Directory.GetFiles(rutaCarpeta, patronBusqueda);

                foreach (string rutaArchivo in archivos)
                {
                    Console.WriteLine($"Procesando: {Path.GetFileName(rutaArchivo)}");
                    ProcesarArchivo(rutaArchivo);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }

            Console.WriteLine("Proceso terminado. Presiona cualquier tecla para salir.");
            Console.ReadKey();

        }

        static void ProcesarArchivo(string ruta)
        {
            // 2. Cargamos el XML en memoria
            XDocument doc = XDocument.Load(ruta);

            // Sacamos el ID de sucursal del nombre del archivo (ej: suc_001 -> 001)
            string nombreArchivo = Path.GetFileName(ruta);
            string sucursalId = nombreArchivo.Split('-')[0].Replace("suc_", "");

            // 3. Por cada etiqueta <Pago> dentro del XML...
            foreach (var elementoPago in doc.Descendants("Pago"))
            {
                // Creamos un "envoltorio" para no perder el ID de sucursal
                string mensajeParaEnviar = $@"
                <PagoSucursal>
                    <SucursalId>{sucursalId}</SucursalId>
                    <Datos>{elementoPago}</Datos>
                </PagoSucursal>";

                //4.Lo enviamos a la cola MSMQ
                EnviarAMsmq(mensajeParaEnviar);

            }
        }

        static void EnviarAMsmq(string contenido)
        {
            // Ruta de la cola 
            string rutaCola = @".\private$\nva_suc_pagos";

            // Usamos una transacción 
            using (MessageQueue cola = new MessageQueue(rutaCola))
            using (MessageQueueTransaction tx = new MessageQueueTransaction())
            {
                tx.Begin();

                Message mensaje = new Message();
                mensaje.Body = contenido;
                mensaje.Formatter = new XmlMessageFormatter(new Type[] { typeof(string) });
                mensaje.Label = "Pago Sucursal XML";
                mensaje.Recoverable = true; // Para que no se borre si se reinicia el PC

                cola.Send(mensaje, tx);

                tx.Commit();
                Console.WriteLine(" -> Mensaje enviado a MSMQ.");
            }
        }

        static void VerificarCola(string queuePath)
        {
            Console.WriteLine("Verificando cola: " + queuePath);
            Console.WriteLine("Exists? " + MessageQueue.Exists(queuePath));

            using (var q = new MessageQueue(queuePath))
            {
                Console.WriteLine("FormatName: " + q.FormatName);
                Console.WriteLine("Transactional: " + q.Transactional);
            }
        }
    }
}
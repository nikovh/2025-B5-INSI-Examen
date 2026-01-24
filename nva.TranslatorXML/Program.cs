using System;
using System.Messaging;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using nva.Canonico;

namespace nva.TranslatorXML
{
    class Program
    {
        static void Main(string[] args)
        {
            string machine = Environment.MachineName;

            string qIn = $@"FormatName:DIRECT=OS:{machine}\private$\nva_suc_pagos";
            string qOut = $@"FormatName:DIRECT=OS:{machine}\private$\nva_xml_canonico";
            string qInvalid = $@"FormatName:DIRECT=OS:{machine}\private$\nva_invalid";

            Console.WriteLine("Translator XML iniciado.");
            Console.WriteLine("IN : " + qIn);
            Console.WriteLine("OUT: " + qOut);

            int totalIn = 0;
            int totalOut = 0;

            using (var inQ = new MessageQueue(qIn))
            using (var outQ = new MessageQueue(qOut))
            using (var invalidQ = new MessageQueue(qInvalid))
            {
                inQ.Formatter = new XmlMessageFormatter(new[] { typeof(string) });
                outQ.Formatter = new XmlMessageFormatter(new[] { typeof(string) });
                invalidQ.Formatter = new XmlMessageFormatter(new[] { typeof(string) });

                while (true)
                {
                    Message inMsg = null;

                    using (var rx = new MessageQueueTransaction())
                    {
                        try
                        {
                            rx.Begin();
                            inMsg = inQ.Receive(TimeSpan.FromSeconds(2), rx);
                            rx.Commit();
                        }
                        catch (MessageQueueException mqe) when (mqe.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
                        {
                            break; // cola vacía
                        }
                    }

                    totalIn++;

                    string body = (string)inMsg.Body;

                    try
                    {
                        // Espera <PagoSucursal><SucursalId>001</SucursalId><Datos><Pago>...</Pago></Datos></PagoSucursal>
                        var doc = XDocument.Parse(body);
                        var root = doc.Root;

                        string sucursalId = root.Element("SucursalId")?.Value;
                        var datos = root.Element("Datos");

                        if (string.IsNullOrWhiteSpace(sucursalId) || datos == null)
                        {
                            EnviarTx(invalidQ, body, "INVALID_XML_WRAPPER");
                            continue;
                        }

                        // Dentro de <Datos> viene el <Pago>...</Pago>
                        var pagoNode = datos.Element("Pago");
                        if (pagoNode == null)
                        {
                            EnviarTx(invalidQ, body, "INVALID_XML_NO_PAGO_NODE");
                            continue;
                        }

                        string rut = pagoNode.Element("Rut")?.Value;
                        string montoStr = pagoNode.Element("Monto")?.Value;
                        string formaPago = pagoNode.Element("FormaPago")?.Value;
                        string codAut = pagoNode.Element("CodigoAutorizacion")?.Value;
                        string tarjeta = pagoNode.Element("Tarjeta")?.Value;

                        if (string.IsNullOrWhiteSpace(rut) || string.IsNullOrWhiteSpace(montoStr) || string.IsNullOrWhiteSpace(formaPago))
                        {
                            EnviarTx(invalidQ, pagoNode.ToString(SaveOptions.DisableFormatting), "INVALID_XML_MISSING_FIELDS");
                            continue;
                        }

                        int monto = int.Parse(montoStr);

                        // Validar MedioPago
                        MedioPago? medioPago = MapFormaPago(formaPago);
                        if (!medioPago.HasValue)
                        {
                            EnviarTx(invalidQ, pagoNode.ToString(SaveOptions.DisableFormatting), "INVALID_MEDIO_PAGO");
                            continue;
                        }

                        var pago = new PagoCanonico
                        {
                            SchemaVersion = 1,
                            ClienteId = rut,
                            Monto = monto,
                            Moneda = "CLP",
                            Origen = OrigenPago.SUCURSAL,
                            // XML original trae fecha a nivel de archivo; si no la tenemos aquí, usamos hoy
                            FechaPago = DateTime.Today.ToString("yyyy-MM-dd"),
                            MedioPago = medioPago.Value,
                            CodigoAutorizacion = string.IsNullOrWhiteSpace(codAut) ? null : codAut,
                            TarjetaUltimos4 = string.IsNullOrWhiteSpace(tarjeta) ? null : tarjeta,
                            SucursalId = sucursalId
                        };

                        pago.Meta.IngestadoEn = DateTimeOffset.Now.ToString("o");
                        pago.Meta.CorrelationId = Guid.NewGuid().ToString("N");
                        pago.Meta.OrigenMensajeId = null;

                        pago.PagoId = CalcularPagoIdSucursal(pago);

                        string canonicoJson = CanonicoJson.Serialize(pago);

                        EnviarTx(outQ, canonicoJson, "PAGO_CANONICO_XML");
                        totalOut++;
                    }
                    catch (Exception ex)
                    {
                        EnviarTx(invalidQ, body, "INVALID_XML_EXCEPTION");
                        Console.WriteLine("Error parseando XML: " + ex.Message);
                    }
                }
            }

            Console.WriteLine($"FIN. Mensajes leídos: {totalIn} | Pagos publicados: {totalOut}");
        }

        static void EnviarTx(MessageQueue q, string body, string label)
        {
            using (var tx = new MessageQueueTransaction())
            {
                tx.Begin();
                var msg = new Message
                {
                    Body = body,
                    Formatter = new XmlMessageFormatter(new[] { typeof(string) }),
                    Label = label,
                    Recoverable = true
                };
                q.Send(msg, tx);
                tx.Commit();
            }
        }

        static MedioPago? MapFormaPago(string formaPago)
        {
            
            if (string.IsNullOrWhiteSpace(formaPago)) return null;

            if (formaPago.Equals("TC", StringComparison.OrdinalIgnoreCase)) return MedioPago.TC;
            if (formaPago.Equals("TD", StringComparison.OrdinalIgnoreCase)) return MedioPago.TD;
            if (formaPago.Equals("EF", StringComparison.OrdinalIgnoreCase)) return MedioPago.EF;

            return null;
        }

        static string CalcularPagoIdSucursal(PagoCanonico p)
        {
            string baseStr = $"SUCURSAL|{p.ClienteId}|{p.Monto}|{p.FechaPago}|{p.MedioPago}|{p.SucursalId}|{p.CodigoAutorizacion}|{p.TarjetaUltimos4}";
            return Sha256Hex(baseStr);
        }

        static string Sha256Hex(string s)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s ?? ""));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
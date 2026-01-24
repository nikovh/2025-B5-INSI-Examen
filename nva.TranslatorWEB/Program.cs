using System;
using System.Messaging;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using nva.Canonico;
   
namespace nva.TranslatorWEB
{
    class Program
    {
        static void Main(string[] args)
        {
            string machine = Environment.MachineName;

            string qIn      = $@"FormatName:DIRECT=OS:{machine}\private$\nva_web_pagos";
            string qOut     = $@"FormatName:DIRECT=OS:{machine}\private$\nva_web_canonico";
            string qInvalid = $@"FormatName:DIRECT=OS:{machine}\private$\nva_invalid";

            Console.WriteLine("Translator Web iniciado.");
            Console.WriteLine("IN : " + qIn);
            Console.WriteLine("OUT: " + qOut);
            Console.WriteLine("INV: " + qInvalid);

            int totalInMessages = 0;
            int totalOutPayments = 0;
            int totalInvalidItems = 0;

            using (var inQ = new MessageQueue(qIn))
            using (var outQ = new MessageQueue(qOut))
            using (var invalidQ = new MessageQueue(qInvalid))
            {
                inQ.Formatter       = new XmlMessageFormatter(new[] { typeof(string) });
                outQ.Formatter      = new XmlMessageFormatter(new[] { typeof(string) });
                invalidQ.Formatter  = new XmlMessageFormatter(new[] { typeof(string) });

                while (true)
                {
                    // 1) Receive transaccional con timeout
                    Message inMsg = ReceiveTxOrNull(inQ, TimeSpan.FromSeconds(2));
                    if (inMsg == null) break;

                    totalInMessages++;

                    string body = (string)inMsg.Body;

                    // 2) Parse JSON array
                    JArray arr;
                    try
                    {
                        arr = JArray.Parse(body);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("JSON inválido (no es array). Se envía a invalid. Error: " + ex.Message);
                        SendTx(invalidQ, body, "INVALID_WEB_JSON_ARRAY");
                        continue;
                    }

                    int publishedFromThisMessage = 0;

                    // 3) Split + Translate
                    foreach (var token in arr)
                    {
                        JObject obj = null;
                        try
                        {
                            obj = (JObject)token;

                            string identifier           = (string)obj["identifier"];
                            int? amount                 = (int?)obj["amount"];
                            string paymentType          = (string)obj["paymentType"];
                            string authorizationCode    = (string)obj["authorizationCode"];
                            string card                 = (string)obj["card"];

                            // Campos mínimos
                            if (string.IsNullOrWhiteSpace(identifier) || amount == null)
                            {
                                totalInvalidItems++;
                                SendTx(invalidQ, token.ToString(Formatting.None), "INVALID_WEB_ITEM_MISSING_FIELDS");
                                continue;
                            }

                            // Medio de pago 
                            MedioPago medio;
                            try
                            {
                                medio = MapPaymentTypeStrict(paymentType);
                            }
                            catch (Exception)
                            {
                                totalInvalidItems++;
                                SendTx(invalidQ, token.ToString(Formatting.None), "INVALID_WEB_PAYMENTTYPE");
                                continue;
                            }

                            var pago = new PagoCanonico
                            {
                                SchemaVersion = 1,
                                ClienteId = identifier,
                                Monto = amount.Value,
                                Moneda = "CLP",
                                Origen = OrigenPago.WEB,
                                FechaPago = DateTime.Today.ToString("yyyy-MM-dd"),
                                MedioPago = medio,
                                CodigoAutorizacion = string.IsNullOrWhiteSpace(authorizationCode) ? null : authorizationCode,
                                TarjetaUltimos4 = string.IsNullOrWhiteSpace(card) ? null : card,
                                SucursalId = null
                            };

                            pago.Meta.IngestadoEn = DateTimeOffset.Now.ToString("o");
                            pago.Meta.CorrelationId = Guid.NewGuid().ToString("N");
                            pago.Meta.OrigenMensajeId = null;

                            pago.PagoId = CalcularPagoIdWeb(pago);

                            string canonicoJson = CanonicoJson.Serialize(pago);

                            SendTx(outQ, canonicoJson, "PAGO_CANONICO_WEB");

                            publishedFromThisMessage++;
                            totalOutPayments++;
                        }
                        catch (Exception ex)
                        {
                            // Si algo inesperado pasa, no caemos: mandamos el ítem a invalid.
                            totalInvalidItems++;
                            string raw = obj != null ? obj.ToString(Formatting.None) : token.ToString(Formatting.None);
                            SendTx(invalidQ, raw, "INVALID_WEB_ITEM_EXCEPTION");
                            Console.WriteLine("Elemento web inválido: " + ex.Message);
                        }
                    }

                    Console.WriteLine($"Mensaje procesado: pagos publicados={publishedFromThisMessage}");
                }
            }

            Console.WriteLine($"FIN. Mensajes leídos={totalInMessages} | Pagos publicados={totalOutPayments} | Items inválidos={totalInvalidItems}");
        }

        // Receive transaccional: si timeout, devuelve null
        static Message ReceiveTxOrNull(MessageQueue q, TimeSpan timeout)
        {
            using (var tx = new MessageQueueTransaction())
            {
                try
                {
                    tx.Begin();
                    var msg = q.Receive(timeout, tx);
                    tx.Commit();
                    return msg;
                }
                catch (MessageQueueException mqe) when (mqe.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
                {
                    try { tx.Abort(); } catch { }
                    return null;
                }
            }
        }

        static void SendTx(MessageQueue q, string body, string label)
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

        static MedioPago MapPaymentTypeStrict(string paymentType)
        {
            if (string.IsNullOrWhiteSpace(paymentType))
                throw new Exception("paymentType vacío");

            if (paymentType.Equals("Credit Card", StringComparison.OrdinalIgnoreCase)) return MedioPago.TC;
            if (paymentType.Equals("Debit Card", StringComparison.OrdinalIgnoreCase)) return MedioPago.TD;

            throw new Exception("paymentType no soportado: " + paymentType);
        }

        static string CalcularPagoIdWeb(PagoCanonico p)
        {
            // Nota: sucursalId es null en WEB
            string baseStr =
                $"WEB|{p.ClienteId}|{p.Monto}|{p.FechaPago}|{p.MedioPago}|{p.CodigoAutorizacion}|{p.TarjetaUltimos4}";
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

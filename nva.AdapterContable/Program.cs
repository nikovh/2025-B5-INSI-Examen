using System;
using System.Messaging;
using Newtonsoft.Json;
using nva.Canonico;

namespace nva.AdapterContable
{
    class Program
    {
        static void Main(string[] args)
        {
            string machine = Environment.MachineName;

            string qIn = $@"FormatName:DIRECT=OS:{machine}\private$\nva_pagos";
            string qOut = $@"FormatName:DIRECT=OS:{machine}\private$\nva_estado_cuenta";
            string qInvalid = $@"FormatName:DIRECT=OS:{machine}\private$\nva_invalid";

            Console.WriteLine("Adapter Contable iniciado.");
            Console.WriteLine("IN : " + qIn);
            Console.WriteLine("OUT: " + qOut);

            int inCount = 0, okCount = 0, invalidCount = 0;

            using (var inQ = new MessageQueue(qIn))
            using (var outQ = new MessageQueue(qOut))
            using (var invalidQ = new MessageQueue(qInvalid))
            {
                inQ.Formatter = new XmlMessageFormatter(new[] { typeof(string) });
                outQ.Formatter = new XmlMessageFormatter(new[] { typeof(string) });
                invalidQ.Formatter = new XmlMessageFormatter(new[] { typeof(string) });

                var client = new ContableRef.ContabilidadServiceClient();

                while (true)
                {
                    var msg = ReceiveTxOrNull(inQ, TimeSpan.FromSeconds(2));
                    if (msg == null) break;

                    inCount++;

                    string body = (string)msg.Body;

                    PagoCanonico pago;
                    try
                    {
                        pago = CanonicoJson.Deserialize<PagoCanonico>(body);
                    }
                    catch (Exception ex)
                    {
                        invalidCount++;
                        Console.WriteLine("Pago canónico inválido: " + ex.Message);
                        SendTx(invalidQ, body, "INVALID_PAGO_CANONICO");
                        continue;
                    }

                    try
                    {
                        // 1) SOAP: Registrar Pago
                        ContableRef.EstadoCuenta resp = client.RegistrarPago(pago.ClienteId, pago.Monto);

                        if (resp == null)
                        {
                            invalidCount++;
                            SendTx(invalidQ, body, "INVALID_CONTA_NULL_RESPONSE");
                            continue;
                        }


                        // 2) Traducir respuesta SOAP a canonico
                        var estadoCan = new EstadoCuentaCanonico
                        {
                            SchemaVersion = 1,
                            ClienteId = resp.ClienteId,
                            Saldo = resp.Saldo,
                            AlDia = resp.Saldo <= 0,
                            ActualizadoEn = DateTimeOffset.Now.ToString("o"),
                            Origen = "CONTABLE"
                        };

                        string estadoJson = CanonicoJson.Serialize(estadoCan);

                        // 3) Publicar a nva_estado_cuenta
                        SendTx(outQ, estadoJson, "ESTADO_CUENTA");

                        okCount++;
                    }
                    catch (Exception ex)
                    {
                        invalidCount++;
                        Console.WriteLine("Error invocando SOAP contable: " + ex.Message);
                        SendTx(invalidQ, body, "INVALID_CONTA_SVC_CALL");
                    }
                }
            }

            Console.WriteLine($"FIN. Leídos={inCount} OK={okCount} Invalid={invalidCount}");
        }

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
    }
}
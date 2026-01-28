package cl.nva.agendamiento;

import com.google.gson.Gson;
import com.google.gson.JsonSyntaxException;
import jakarta.jms.*;


import org.apache.activemq.artemis.jms.client.ActiveMQConnectionFactory;

public class Main {

    public static void main(String[] args) throws Exception {

        String artemisUrl = env("ARTEMIS_URL");
        String artemisUser = env("ARTEMIS_USER");
        String artemisPass = env("ARTEMIS_PASS");
        String destinationName = env("ARTEMIS_DEST");
        String destType = System.getenv().getOrDefault("ARTEMIS_DEST_TYPE", "topic").toLowerCase();

        String soapEndpoint = env("AGENDA_SOAP_ENDPOINT");
        String soapNamespace = System.getenv().getOrDefault("AGENDA_SOAP_NAMESPACE", "");

        String opHabilitar = env("AGENDA_OP_HABILITAR");
        String opDeshabilitar = env("AGENDA_OP_DESHABILITAR");

        String soapActionH = System.getenv().getOrDefault("SOAP_ACTION_HABILITAR", "");
        String soapActionD = System.getenv().getOrDefault("SOAP_ACTION_DESHABILITAR", "");

        int maxRetries = Integer.parseInt(System.getenv().getOrDefault("SOAP_MAX_RETRIES", "5"));
        long baseSleepMs = Long.parseLong(System.getenv().getOrDefault("SOAP_BACKOFF_MS", "800"));

        System.out.println("== Adapter Agendamiento SOAP ==");
        System.out.println("ARTEMIS_URL=" + artemisUrl);
        System.out.println("DEST=" + destinationName + " (" + destType + ")");
        System.out.println("AGENDA_SOAP_ENDPOINT=" + soapEndpoint);
        System.out.println("AGENDA_SOAP_NAMESPACE=" + (soapNamespace.isBlank() ? "(vacío)" : soapNamespace));
        System.out.println("OP_HABILITAR=" + opHabilitar + " | OP_DESHABILITAR=" + opDeshabilitar);

        Gson gson = new Gson();
        AgendaSoapClient soap = new AgendaSoapClient(soapEndpoint, soapNamespace);

        ConnectionFactory cf = new ActiveMQConnectionFactory(artemisUrl, artemisUser, artemisPass);

        try (Connection connection = cf.createConnection()) {

            Session session = connection.createSession(false, Session.CLIENT_ACKNOWLEDGE);
            MessageConsumer consumer;

            if ("queue".equals(destType)) {
                Queue queue = session.createQueue(destinationName);
                consumer = session.createConsumer(queue);
            } else {
                Topic topic = session.createTopic(destinationName);
                consumer = session.createConsumer(topic);
            }

            connection.start();
            System.out.println("Escuchando... (CTRL+C para salir)\n");

            while (true) {
                Message msg = consumer.receive();

                if (!(msg instanceof TextMessage)) {
                    System.out.println("[WARN] Mensaje no TextMessage, ACK y continuo.");
                    msg.acknowledge();
                    continue;
                }

                String body = ((TextMessage) msg).getText();
                System.out.println("[IN] " + body);

                try {
                    EstadoCuenta ec = gson.fromJson(body, EstadoCuenta.class);

                    boolean alDia = ec.saldo <= 0;
                    System.out.println("[DECISION] " + ec + " => alDia=" + alDia);

                    boolean ok = callWithRetry(soap, ec.clienteId, alDia,
                            opHabilitar, opDeshabilitar,
                            soapActionH, soapActionD,
                            maxRetries, baseSleepMs);

                    if (ok) {
                        msg.acknowledge();
                        System.out.println("[ACK] OK\n");
                    } else {
                        System.out.println("[NO ACK] SOAP sigue fallando. Se reintentará por redelivery.\n");
                    }

                } catch (JsonSyntaxException ex) {
                    System.out.println("[ERROR] JSON inválido. ACK y continuo. " + ex.getMessage() + "\n");
                    msg.acknowledge();
                }
            }
        }
    }

    private static boolean callWithRetry(AgendaSoapClient soap, String clienteId, boolean alDia,
                                         String opHabilitar, String opDeshabilitar,
                                         String soapActionH, String soapActionD,
                                         int maxRetries, long baseSleepMs) {
        for (int i = 1; i <= maxRetries; i++) {
            try {
                if (alDia) soap.habilitar(opHabilitar, clienteId, soapActionH);
                else soap.deshabilitar(opDeshabilitar, clienteId, soapActionD);
                return true;
            } catch (Exception e) {
                System.out.println("[SOAP][ERROR] intento " + i + "/" + maxRetries + ": " + e.getMessage());
                try { Thread.sleep(baseSleepMs * i); } catch (InterruptedException ignored) {}
            }
        }
        return false;
    }

    private static String env(String key) {
        String v = System.getenv(key);
        if (v == null || v.isBlank()) throw new IllegalArgumentException("Falta variable de entorno: " + key);
        return v;
    }
}

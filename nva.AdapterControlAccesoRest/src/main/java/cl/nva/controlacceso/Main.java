package cl.nva.controlacceso;

import org.apache.activemq.artemis.jms.client.ActiveMQConnectionFactory;

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

        String controlBase = env("CONTROL_ACCESO_BASE_URL"); 

        String clientId = System.getenv().getOrDefault("JMS_CLIENT_ID", "nva-adapter-control-acceso");
        //String durableName = System.getenv().getOrDefault("JMS_DURABLE_SUB", "nva-sub-control-acceso");

        int maxRetries = Integer.parseInt(System.getenv().getOrDefault("REST_MAX_RETRIES", "5"));
        long baseSleepMs = Long.parseLong(System.getenv().getOrDefault("REST_BACKOFF_MS", "800"));

        System.out.println("== Adapter Control Acceso (Act 8) ==");
        System.out.println("ARTEMIS_URL=" + artemisUrl);
        System.out.println("DEST=" + destinationName + " (" + destType + ")");
        System.out.println("CONTROL_ACCESO_BASE_URL=" + controlBase);

        Gson gson = new Gson();
        ControlAccesoClient client = new ControlAccesoClient(controlBase);

        ConnectionFactory cf = new ActiveMQConnectionFactory(artemisUrl, artemisUser, artemisPass);
        try (Connection connection = cf.createConnection()) {

            // if (!"queue".equals(destType)) {
            //     connection.setClientID(clientId);
            // }

            Session session = connection.createSession(false, Session.CLIENT_ACKNOWLEDGE);

            // Destination dest;
            MessageConsumer consumer;

            if ("queue".equals(destType)) {
                //dest = session.createQueue(destinationName);
                Queue queue = session.createQueue(destinationName);
                consumer = session.createConsumer(queue);
            } else {
                Topic topic = session.createTopic(destinationName);
                //consumer = session.createDurableSubscriber(topic, durableName);
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
                    boolean habilitar = ec.saldo <= 0;

                    System.out.println("[DECISION] " + ec + " => habilitar=" + habilitar);

                    boolean ok = callWithRetry(client, ec.clienteId, habilitar, maxRetries, baseSleepMs);

                    if (ok) {
                        msg.acknowledge();
                        System.out.println("[ACK] OK\n");
                    } else {
                        System.out.println("[NO ACK] REST sigue fallando. Se reintentará por redelivery.\n");
                    }

                } catch (JsonSyntaxException ex) {
                    System.out.println("[ERROR] JSON inválido. ACK y continuo. " + ex.getMessage() + "\n");
                    msg.acknowledge();
                }
            }
        }
    }

    private static boolean callWithRetry(ControlAccesoClient client, String rut, boolean habilitar,
                                         int maxRetries, long baseSleepMs) {
        for (int i = 1; i <= maxRetries; i++) {
            try {
                client.setHabilitado(rut, habilitar);
                return true;
            } catch (Exception e) {
                System.out.println("[REST][ERROR] intento " + i + "/" + maxRetries + ": " + e.getMessage());
                try {
                    long sleep = baseSleepMs * i; // backoff lineal simple
                    Thread.sleep(sleep);
                } catch (InterruptedException ignored) { }
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

package cl.nva.agendamiento;

import jakarta.xml.soap.*;

import java.net.URL;

public class AgendaSoapClient {

    private final String endpoint;
    private final String namespace; // puede ir vacío pero ideal traerlo del WSDL

    public AgendaSoapClient(String endpoint, String namespace) {
        this.endpoint = endpoint;
        this.namespace = namespace == null ? "" : namespace;
    }

    public void habilitar(String opName, String clienteId, String soapAction) throws Exception {
        call(opName, clienteId, soapAction);
    }

    public void deshabilitar(String opName, String clienteId, String soapAction) throws Exception {
        call(opName, clienteId, soapAction);
    }

    private void call(String operation, String clienteId, String soapAction) throws Exception {
        MessageFactory mf = MessageFactory.newInstance();
        SOAPMessage msg = mf.createMessage();

        SOAPEnvelope env = msg.getSOAPPart().getEnvelope();
        SOAPBody body = env.getBody();

        SOAPBodyElement opEl;
        if (!namespace.isBlank()) {
            Name op = env.createName(operation, "ns1", namespace);
            opEl = body.addBodyElement(op);
        } else {
            opEl = body.addBodyElement(env.createName(operation));
        }

        opEl.addChildElement("clienteId").addTextNode(clienteId);

        if (soapAction != null && !soapAction.isBlank()) {
            msg.getMimeHeaders().addHeader("SOAPAction", soapAction);
        }

        msg.saveChanges();

        SOAPConnection conn = SOAPConnectionFactory.newInstance().createConnection();

        System.out.println("[SOAP][REQ] op=" + operation + " clienteId=" + clienteId + " -> " + endpoint);
        SOAPMessage response = conn.call(msg, new URL(endpoint));

        if (response.getSOAPBody() != null && response.getSOAPBody().hasFault()) {
            String fault = response.getSOAPBody().getFault().getFaultString();
            conn.close();
            throw new RuntimeException("SOAP Fault: " + fault);
        }

        System.out.println("[SOAP][OK] op=" + operation + " clienteId=" + clienteId);
        conn.close();
    }
}

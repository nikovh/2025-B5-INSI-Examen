package cl.nva.controlacceso;

import java.io.IOException;
import java.net.URI;
import java.net.http.*;
import java.time.Duration;


public class ControlAccesoClient {
    private final HttpClient http;
    private final String baseUrl;

    public ControlAccesoClient(String baseUrl) {
        this.baseUrl = baseUrl;
        this.http = HttpClient.newBuilder()
                .connectTimeout(Duration.ofSeconds(5))
                .build();
    }

    public void setHabilitado(String rut, boolean habilitado) throws IOException, InterruptedException {
        int code = patchHabilitado(rut, habilitado);

        if (code == 404) {
            postUsuarioPlaceholder(rut, habilitado);
            int code2 = patchHabilitado(rut, habilitado);
            if (code2 >= 400) {
                throw new IOException("PATCH falló tras crear usuario. HTTP " + code2);
            }
        } else if (code >= 400) {
            throw new IOException("PATCH falló. HTTP " + code);
        }
    }

    private int patchHabilitado(String rut, boolean habilitado) throws IOException, InterruptedException {
        String body = "{ \"habilitado\": " + habilitado + " }";

        HttpRequest req = HttpRequest.newBuilder()
                .uri(URI.create(baseUrl + "/api/users/" + rut))
                .header("Content-Type", "application/json")
                .method("PATCH", HttpRequest.BodyPublishers.ofString(body))
                .timeout(Duration.ofSeconds(8))
                .build();

        HttpResponse<String> res = http.send(req, HttpResponse.BodyHandlers.ofString());
        System.out.println("[REST][PATCH] " + req.uri() + " -> " + res.statusCode());
        return res.statusCode();
    }

    private void postUsuarioPlaceholder(String rut, boolean habilitado) throws IOException, InterruptedException {
        String body = "{"
                + "\"rut\":\"" + rut + "\","
                + "\"nombre\":\"" + rut + "\","
                + "\"email\":\"" + rut + "@example.com\","
                + "\"telefono\":\"000000000\","
                + "\"habilitado\":" + habilitado
                + "}";

        HttpRequest req = HttpRequest.newBuilder()
                .uri(URI.create(baseUrl + "/api/users"))
                .header("Content-Type", "application/json")
                .POST(HttpRequest.BodyPublishers.ofString(body))
                .timeout(Duration.ofSeconds(8))
                .build();

        HttpResponse<String> res = http.send(req, HttpResponse.BodyHandlers.ofString());
        System.out.println("[REST][POST] " + req.uri() + " -> " + res.statusCode());
    }
}
